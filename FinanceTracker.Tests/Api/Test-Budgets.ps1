#Requires -Version 5.1
<#
.SYNOPSIS
    Группа Budgets: лимиты трат по категории на период и прогресс по ним.

.DESCRIPTION
    Главное здесь — исключающее ограничение на пересечение периодов: только один активный
    бюджет может покрывать категорию в конкретный день. Проверка смотрит лишь на активные,
    поэтому дни деактивированного можно занять — и тогда вернуть его получится только после
    освобождения места. Этот путь проверяется целиком, вместе с тем, что отказ называет
    занявший бюджет.

    Прогресс пересчитывает воркер, а не обработчик команды, поэтому после переноса периода
    ответ 204 означает лишь то, что период изменён — цифры подтягиваются следом.

.EXAMPLE
    ./Test-Budgets.ps1
#>
[CmdletBinding()]
param(
    [string] $BaseUrl   = 'http://localhost:8080',
    [string] $ApiPrefix = '/api/v1'
)

$ErrorActionPreference = 'Stop'
$script:BaseUrl   = $BaseUrl
$script:ApiPrefix = $ApiPrefix

. "$PSScriptRoot\_Common.ps1"

Start-Suite -Name 'Budgets'

$user = New-TestUser -Label 'bud'
Write-Note "учётка: $($user.Email)"

# ---------------------------------------------------------------- подготовка

Write-Step 'Подготовка'

function New-Category {
    param([string] $Name, [string] $Type = 'expense', [string] $Token = $user.Token)

    $response = Send-Api -Method POST -Path '/categories' -Token $Token `
        -Headers @{ 'Idempotency-Key' = New-Key } -Body @{ name = $Name; type = $Type }

    if ($response.Status -ne 201) { throw "Не удалось создать категорию '$Name': $($response.Status) $(ConvertTo-Text -Raw $response.Content)" }
    return (Read-Json -Response $response).id
}

<#
.SYNOPSIS
    Дожидается, пока прогресс бюджета сойдётся с ожидаемым.

.DESCRIPTION
    Пересчёт после переноса периода и учёт новых трат делает воркер, а не обработчик команды:
    204 означает, что период изменён, но не что цифры уже подтянулись.
#>
function Wait-Spent {
    param(
        [Parameter(Mandatory)][string] $BudgetId,
        [Parameter(Mandatory)][decimal] $Expected,
        [int] $TimeoutSeconds = 15
    )

    $watch = [Diagnostics.Stopwatch]::StartNew()

    while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $progress = Read-Json -Response (Send-Api -Method GET -Path "/budgets/$BudgetId/progress" -Token $user.Token)
        if ([decimal]$progress.spent -eq $Expected) { return $true }
        Start-Sleep -Milliseconds 300
    }

    return $false
}

function Get-Progress {
    param([Parameter(Mandatory)][string] $BudgetId)

    return Read-Json -Response (Send-Api -Method GET -Path "/budgets/$BudgetId/progress" -Token $user.Token)
}

# Учётка переиспользуется между прогонами, поэтому прогоны разводятся категориями:
# бюджеты прошлых запусков висят на своих категориях и текущему не мешают.
$stamp = [guid]::NewGuid().ToString('N').Substring(0, 6)

$groceries = New-Category -Name "Продукты-$stamp"
$transport = New-Category -Name "Транспорт-$stamp"

$accountResponse = Send-Api -Method POST -Path '/accounts' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ name = "Счёт-$stamp"; type = 'Checking'; currency = 'RUB'; initialBalance = 100000 }

if (-not (Assert-Status -Response $accountResponse -Expected 201 -What 'счёт создан' -PassThru)) {
    Complete-Suite
    return
}

$accountId = (Read-Json -Response $accountResponse).id

# Периоды в текущем и следующем месяце: трату нельзя записать ни будущей датой, ни раньше
# открытия счёта, а бюджет должен покрывать день, когда она произошла.
$today        = Get-Date
$firstOfMonth = $today.AddDays(-$today.Day + 1)

$monthStart = $firstOfMonth.ToString('yyyy-MM-dd')
$monthEnd   = $firstOfMonth.AddMonths(1).AddDays(-1).ToString('yyyy-MM-dd')
$nextStart  = $firstOfMonth.AddMonths(1).ToString('yyyy-MM-dd')
$nextEnd    = $firstOfMonth.AddMonths(2).AddDays(-1).ToString('yyyy-MM-dd')
$farStart   = $firstOfMonth.AddMonths(6).ToString('yyyy-MM-dd')
$farEnd     = $firstOfMonth.AddMonths(7).AddDays(-1).ToString('yyyy-MM-dd')

Write-Note "период: $monthStart … $monthEnd"

# ---------------------------------------------------------------- создание

Write-Step 'Создание'

$key = New-Key
$body = @{
    categoryId = $groceries
    amount     = 30000
    currency   = 'RUB'
    from       = $monthStart
    to         = $monthEnd
}

$created = Send-Api -Method POST -Path '/budgets' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = $key } -Body $body

if (-not (Assert-Status -Response $created -Expected 201 -What 'POST /budgets' -PassThru)) {
    Complete-Suite
    return
}

$budgetId = (Read-Json -Response $created).id
Assert-True -Condition ($null -ne $created.Headers['Location']) -What 'ответ несёт Location'

$repeat = Send-Api -Method POST -Path '/budgets' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = $key } -Body $body

Assert-Status -Response $repeat -Expected 201 -What 'повтор с тем же Idempotency-Key'
Assert-True -Condition ((Read-Json -Response $repeat).id -eq $budgetId) `
    -What 'повтор вернул исходный бюджет, а не создал второй на те же дни'

Assert-Status -Response (Send-Api -Method POST -Path '/budgets' -Token $user.Token -Body $body) `
    -Expected 400 -What 'создание без Idempotency-Key'

# ---------------------------------------------------------------- пересечения

Write-Step 'Пересечение периодов'

$overlap = Send-Api -Method POST -Path '/budgets' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $body

if (Assert-Status -Response $overlap -Expected 422 -What 'тот же период на ту же категорию отклонён' -PassThru) {
    $refusal = Read-Json -Response $overlap
    Assert-True -Condition ($refusal.code -eq 'budget.overlapping_period') `
        -What "код budget.overlapping_period (получен '$($refusal.code)')"
    Assert-True -Condition ($refusal.conflictingBudgetId -eq $budgetId) `
        -What 'отказ называет мешающий бюджет'
}

$overlapPartial = $body.Clone()
$overlapPartial.from = $firstOfMonth.AddDays(14).ToString('yyyy-MM-dd')
$overlapPartial.to   = $nextEnd
Assert-Status -Response (Send-Api -Method POST -Path '/budgets' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $overlapPartial) `
    -Expected 422 -What 'частичное пересечение отклонено'

# Один общий день: обе границы включительные, так что это пересечение.
$sharedBoundary = $body.Clone()
$sharedBoundary.from = $monthEnd
$sharedBoundary.to   = $nextEnd
Assert-Status -Response (Send-Api -Method POST -Path '/budgets' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $sharedBoundary) `
    -Expected 422 -What 'пересечение по одному общему дню отклонено'

$adjacent = $body.Clone()
$adjacent.from = $nextStart
$adjacent.to   = $nextEnd
Assert-Status -Response (Send-Api -Method POST -Path '/budgets' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $adjacent) `
    -Expected 201 -What 'следующий месяц без общих дней принимается'

$otherCategory = $body.Clone()
$otherCategory.categoryId = $transport
Assert-Status -Response (Send-Api -Method POST -Path '/budgets' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $otherCategory) `
    -Expected 201 -What 'тот же период на другой категории принимается'

# ---------------------------------------------------------------- некорректный ввод

Write-Step 'Некорректный ввод'

$scratch = New-Category -Name "Черновик-$stamp"

$reversed = $body.Clone()
$reversed.categoryId = $scratch
$reversed.from = $monthEnd
$reversed.to   = $monthStart
Assert-Status -Response (Send-Api -Method POST -Path '/budgets' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $reversed) `
    -Expected 400 -What 'период задом наперёд отклонён'

$negative = $body.Clone()
$negative.categoryId = $scratch
$negative.amount = -100
Assert-Status -Response (Send-Api -Method POST -Path '/budgets' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $negative) `
    -Expected 400 -What 'отрицательная сумма отклонена'

$badCurrency = $body.Clone()
$badCurrency.categoryId = $scratch
$badCurrency.currency = 'XX'
Assert-Status -Response (Send-Api -Method POST -Path '/budgets' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $badCurrency) `
    -Expected 400 -What 'некорректный код валюты'

$missingCategory = $body.Clone()
$missingCategory.categoryId = [guid]::NewGuid()
Assert-Status -Response (Send-Api -Method POST -Path '/budgets' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $missingCategory) `
    -Expected 400, 404 -What 'несуществующая категория отклонена'

# ---------------------------------------------------------------- чтение

Write-Step 'Чтение'

$single = Send-Api -Method GET -Path "/budgets/$budgetId" -Token $user.Token
if (Assert-Status -Response $single -Expected 200 -What 'GET /budgets/{id}' -PassThru) {
    $budget = Read-Json -Response $single

    Assert-True -Condition ($budget.amount.amount -eq 30000) -What 'сумма отдана в camelCase'
    Assert-True -Condition ($budget.isActive -eq $true) -What 'бюджет активен при создании'
    Assert-True -Condition ($budget.categoryId -eq $groceries) -What 'ответ несёт категорию'
}

Assert-Status -Response (Send-Api -Method GET -Path "/budgets/$([guid]::NewGuid())" -Token $user.Token) `
    -Expected 404 -What 'несуществующий бюджет'

$page = Send-Api -Method GET -Path '/budgets?pageSize=1' -Token $user.Token
if (Assert-Status -Response $page -Expected 200 -What 'первая страница' -PassThru) {
    $first = Read-Json -Response $page

    Assert-True -Condition ($first.items.Count -eq 1) -What 'страница содержит ровно один элемент'
    Assert-True -Condition ($first.hasNextPage -eq $true) -What 'страница сообщает о продолжении'

    $cursor = @{
        pageSize        = 1
        cursorCreatedAt = ConvertTo-IsoOffset -Instant $first.nextCursorDate
        cursorId        = $first.nextCursorId
    }

    $next = Send-Api -Method GET -Path ('/budgets' + (ConvertTo-Query -Parameters $cursor)) -Token $user.Token
    if (Assert-Status -Response $next -Expected 200 -What 'вторая страница по курсору со смещением +03:00' -PassThru) {
        Assert-True -Condition ((Read-Json -Response $next).items[0].id -ne $first.items[0].id) `
            -What 'вторая страница отдаёт другой бюджет'
    }
}

Assert-Status -Response (Send-Api -Method GET -Path '/budgets?pageSize=101' -Token $user.Token) `
    -Expected 400 -What 'pageSize сверх потолка'

# ---------------------------------------------------------------- прогресс

Write-Step 'Прогресс'

$progress = Send-Api -Method GET -Path "/budgets/$budgetId/progress" -Token $user.Token
if (Assert-Status -Response $progress -Expected 200 -What 'GET /budgets/{id}/progress' -PassThru) {
    $p = Read-Json -Response $progress

    Assert-True -Condition ($p.spent -eq 0) -What 'потрачено ноль, пока трат нет'
    Assert-True -Condition ($p.remaining -eq 30000) -What 'остаток равен лимиту'
    Assert-True -Condition ($p.budgetId -eq $budgetId) -What 'ответ несёт идентификатор бюджета'
}

# Трата текущим моментом: будущее домен не принимает, а раньше открытия счёта — тем более.
$spendAt = $today.ToUniversalTime().ToString('o')
$spend = Send-Api -Method POST -Path "/accounts/$accountId/transactions" -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ categoryId = $groceries; amount = 5000; currency = 'RUB'; direction = 'Debit'; description = 'Внутри периода'; occurredAt = $spendAt }

if (Assert-Status -Response $spend -Expected 201 -What 'трата внутри периода записана' -PassThru) {
    Assert-True -Condition (Wait-Spent -BudgetId $budgetId -Expected 5000) -What 'прогресс учёл трату'

    $p = Get-Progress -BudgetId $budgetId
    Assert-True -Condition ($p.remaining -eq 25000) -What 'остаток уменьшился'
    Assert-True -Condition ([math]::Round($p.percentage) -eq 17) `
        -What "percentage отдан в процентах, а не долей (получено $($p.percentage))"
}

Assert-Status -Response (Send-Api -Method GET -Path "/budgets/$([guid]::NewGuid())/progress" -Token $user.Token) `
    -Expected 404 -What 'прогресс несуществующего бюджета'

# ---------------------------------------------------------------- изменение

Write-Step 'Изменение'

Assert-Status -Response (Send-Api -Method PATCH -Path "/budgets/$budgetId/amount" -Token $user.Token `
    -Body @{ amount = 40000 }) -Expected 204 -What 'изменение суммы'

$afterAmount = Read-Json -Response (Send-Api -Method GET -Path "/budgets/$budgetId" -Token $user.Token)
Assert-True -Condition ($afterAmount.amount.amount -eq 40000) -What 'новая сумма видна при чтении'
Assert-True -Condition ($afterAmount.amount.currency -eq 'RUB') -What 'валюта не изменилась вместе с суммой'

Assert-Status -Response (Send-Api -Method PATCH -Path "/budgets/$budgetId/amount" -Token $user.Token `
    -Body @{ amount = -1 }) -Expected 400 -What 'отрицательная сумма при изменении отклонена'

# Лимит ниже уже потраченного: домен этому не мешает, бюджет просто становится перерасходованным.
Assert-Status -Response (Send-Api -Method PATCH -Path "/budgets/$budgetId/amount" -Token $user.Token `
    -Body @{ amount = 1000 }) -Expected 204 -What 'сумма ниже потраченного принимается'

$overspent = Get-Progress -BudgetId $budgetId
Assert-True -Condition ($overspent.remaining -lt 0) -What 'перерасход показан отрицательным остатком, а не нулём'
Assert-True -Condition ($overspent.percentage -gt 100) `
    -What "перерасход показан процентом больше ста (получено $($overspent.percentage))"

Send-Api -Method PATCH -Path "/budgets/$budgetId/amount" -Token $user.Token -Body @{ amount = 30000 } | Out-Null

$intoOccupied = Send-Api -Method PATCH -Path "/budgets/$budgetId/period" -Token $user.Token `
    -Body @{ from = $monthStart; to = $nextEnd }
if (Assert-Status -Response $intoOccupied -Expected 422 -What 'перенос периода в пересечение отклонён' -PassThru) {
    Assert-True -Condition ($null -ne (Read-Json -Response $intoOccupied).conflictingBudgetId) `
        -What 'отказ при переносе называет мешающий бюджет'
}

Assert-Status -Response (Send-Api -Method PATCH -Path "/budgets/$budgetId/period" -Token $user.Token `
    -Body @{ from = $farStart; to = $farEnd }) `
    -Expected 204 -What 'перенос на свободный период'

Assert-True -Condition (Wait-Spent -BudgetId $budgetId -Expected 0) `
    -What 'после переноса прогресс пересчитан: трата осталась вне нового периода'

# ---------------------------------------------------------------- активность

Write-Step 'Активность'

Assert-Status -Response (Send-Api -Method POST -Path "/budgets/$budgetId/deactivate" -Token $user.Token) `
    -Expected 204 -What 'деактивация'

Assert-True -Condition ((Read-Json -Response (Send-Api -Method GET -Path "/budgets/$budgetId" -Token $user.Token)).isActive -eq $false) `
    -What 'бюджет помечен неактивным'

Assert-Status -Response (Send-Api -Method POST -Path "/budgets/$budgetId/deactivate" -Token $user.Token) `
    -Expected 204 -What 'повторная деактивация идемпотентна'

# Проверка пересечения смотрит только на активные, поэтому дни деактивированного свободны.
$claim = Send-Api -Method POST -Path '/budgets' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ categoryId = $groceries; amount = 5000; currency = 'RUB'; from = $farStart; to = $farEnd }

if (Assert-Status -Response $claim -Expected 201 -What 'дни деактивированного бюджета можно занять другим' -PassThru) {
    $blockerId = (Read-Json -Response $claim).id

    $blocked = Send-Api -Method POST -Path "/budgets/$budgetId/activate" -Token $user.Token
    if (Assert-Status -Response $blocked -Expected 422 -What 'активация поверх занятых дней отклонена' -PassThru) {
        Assert-True -Condition ((Read-Json -Response $blocked).conflictingBudgetId -eq $blockerId) `
            -What 'отказ называет бюджет, занявший период — без этого выход из тупика неочевиден'
    }

    Send-Api -Method POST -Path "/budgets/$blockerId/deactivate" -Token $user.Token | Out-Null

    Assert-Status -Response (Send-Api -Method POST -Path "/budgets/$budgetId/activate" -Token $user.Token) `
        -Expected 204 -What 'после освобождения дней активация проходит'
}

Assert-True -Condition ((Read-Json -Response (Send-Api -Method GET -Path "/budgets/$budgetId" -Token $user.Token)).isActive -eq $true) `
    -What 'бюджет снова активен'

Assert-Status -Response (Send-Api -Method POST -Path "/budgets/$budgetId/activate" -Token $user.Token) `
    -Expected 204 -What 'повторная активация идемпотентна'

# ---------------------------------------------------------------- изоляция

Write-Step 'Изоляция между учётками'

$other = Get-OutsiderUser

Assert-Status -Response (Send-Api -Method GET -Path "/budgets/$budgetId" -Token $other.Token) `
    -Expected 404 -What 'чужой бюджет не читается'

Assert-Status -Response (Send-Api -Method GET -Path "/budgets/$budgetId/progress" -Token $other.Token) `
    -Expected 404 -What 'чужой прогресс не читается'

Assert-Status -Response (Send-Api -Method POST -Path "/budgets/$budgetId/deactivate" -Token $other.Token) `
    -Expected 404 -What 'чужой бюджет не деактивируется'

Assert-Status -Response (Send-Api -Method GET -Path "/budgets/$budgetId") `
    -Expected 401 -What 'без токена — 401'

Complete-Suite
