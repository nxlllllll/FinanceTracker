#Requires -Version 5.1
<#
.SYNOPSIS
    Группа Transactions: запись операций по счёту, их пагинация и участие в аналитике.

.DESCRIPTION
    Создание и список вложены под счёт, остальное адресуется по идентификатору самой
    операции. Отдельно проверяется, что исключение из аналитики не трогает баланс —
    это самое вероятное недопонимание в группе.

.EXAMPLE
    ./Test-Transactions.ps1
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

Start-Suite -Name 'Transactions'

$user = New-TestUser -Label 'tx'
Write-Note "учётка: $($user.Email)"

Write-Step 'Подготовка: счёт и категории'

$accountResponse = Send-Api -Method POST -Path '/accounts' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ name = 'Основной'; type = 'Checking'; currency = 'RUB'; initialBalance = 10000 }

if (-not (Assert-Status -Response $accountResponse -Expected 201 -What 'счёт создан' -PassThru)) {
    Complete-Suite
    return
}

$accountId = (Read-Json -Response $accountResponse).id

function New-Category {
    param([string] $Name, [string] $Type, [string] $Token = $user.Token)

    $response = Send-Api -Method POST -Path '/categories' -Token $Token `
        -Headers @{ 'Idempotency-Key' = New-Key } -Body @{ name = $Name; type = $Type }

    if ($response.Status -ne 201) { throw "Не удалось создать категорию '$Name': $($response.Status) $($response.Content)" }
    return (Read-Json -Response $response).id
}

$expenseCategory = New-Category -Name 'Продукты' -Type 'expense'
$otherExpense    = New-Category -Name 'Транспорт' -Type 'expense'
$incomeCategory  = New-Category -Name 'Зарплата' -Type 'income'

Write-Note "счёт $accountId, категорий 3"

function Get-Balance {
    $account = Read-Json -Response (Send-Api -Method GET -Path "/accounts/$accountId" -Token $user.Token)
    return [decimal]$account.balance.amount
}

<#
.SYNOPSIS
    Дожидается, пока баланс догонит ожидаемое значение.

.DESCRIPTION
    Баланс живёт в rm_account_balances и обновляется проекцией через outbox, а транзакция
    пишется синхронно. Читать баланс сразу после записи — значит спрашивать догоняющую
    копию до того, как она догнала.
#>
function Wait-Balance {
    param([Parameter(Mandatory)][decimal] $Expected, [int] $TimeoutSeconds = 10)

    $watch = [Diagnostics.Stopwatch]::StartNew()

    while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if ((Get-Balance) -eq $Expected) { return $true }
        Start-Sleep -Milliseconds 200
    }

    return $false
}

Write-Step 'Создание'

$occurredAt = (Get-Date).ToUniversalTime().ToString('o')
$key = New-Key

$body = @{
    categoryId  = $expenseCategory
    amount      = 1500
    currency    = 'RUB'
    direction   = 'Debit'
    description = 'Покупка на неделю'
    occurredAt  = $occurredAt
}

$created = Send-Api -Method POST -Path "/accounts/$accountId/transactions" -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = $key } -Body $body

if (-not (Assert-Status -Response $created -Expected 201 -What 'POST /accounts/{id}/transactions' -PassThru)) {
    Complete-Suite
    return
}

$transactionId = (Read-Json -Response $created).id
Assert-True -Condition ($null -ne $created.Headers['Location']) -What 'ответ несёт Location'

$repeat = Send-Api -Method POST -Path "/accounts/$accountId/transactions" -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = $key } -Body $body

Assert-Status -Response $repeat -Expected 201 -What 'повтор с тем же Idempotency-Key'
Assert-True -Condition ((Read-Json -Response $repeat).id -eq $transactionId) `
    -What 'повтор вернул исходную операцию, а не списал деньги дважды'

Assert-True -Condition (Wait-Balance -Expected 8500) `
    -What 'баланс уменьшился ровно один раз: 10000 - 1500'

Assert-Status -Response (Send-Api -Method POST -Path "/accounts/$accountId/transactions" -Token $user.Token -Body $body) `
    -Expected 400 -What 'создание без Idempotency-Key'

$badCurrency = $body.Clone(); $badCurrency.currency = 'XX'
Assert-Status -Response (Send-Api -Method POST -Path "/accounts/$accountId/transactions" -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $badCurrency) `
    -Expected 400 -What 'некорректный код валюты'

$wrongDirection = $body.Clone(); $wrongDirection.categoryId = $incomeCategory
Assert-Status -Response (Send-Api -Method POST -Path "/accounts/$accountId/transactions" -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $wrongDirection) `
    -Expected 422 -What 'расход в категорию дохода отклонён'

$archivedTarget = New-Category -Name 'Списанная' -Type 'expense'
Send-Api -Method POST -Path "/categories/$archivedTarget/archive" -Token $user.Token | Out-Null

$intoArchived = $body.Clone(); $intoArchived.categoryId = $archivedTarget
Assert-Status -Response (Send-Api -Method POST -Path "/accounts/$accountId/transactions" -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $intoArchived) `
    -Expected 422 -What 'создание в архивную категорию отклонено'

$lowercaseDirection = $body.Clone()
$lowercaseDirection.direction = 'debit'
$lowercaseDirection.amount = 100
Assert-Status -Response (Send-Api -Method POST -Path "/accounts/$accountId/transactions" -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $lowercaseDirection) `
    -Expected 201 -What 'direction в нижнем регистре принимается в теле запроса'

$offsetOccurred = $body.Clone()
$offsetOccurred.occurredAt = ConvertTo-IsoOffset -Instant (Get-Date)
$offsetOccurred.amount = 200
Assert-Status -Response (Send-Api -Method POST -Path "/accounts/$accountId/transactions" -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $offsetOccurred) `
    -Expected 201 -What 'occurredAt со смещением +03:00 принимается'

Write-Step 'Чтение'

$single = Send-Api -Method GET -Path "/transactions/$transactionId" -Token $user.Token
if (Assert-Status -Response $single -Expected 200 -What 'GET /transactions/{id}' -PassThru) {
    $tx = Read-Json -Response $single

    Assert-True -Condition ($tx.amount.amount -eq 1500) -What 'сумма отдана в camelCase — amount.amount читается'
    Assert-True -Condition ($tx.amount.currency -eq 'RUB') -What 'валюта суммы отдана в camelCase'
    Assert-True -Condition ($tx.PSObject.Properties.Name -contains 'rateStatus') `
        -What 'ответ несёт rateStatus — клиент должен знать, окончателен ли пересчёт в базовую валюту'
    Assert-True -Condition ($tx.accountId -eq $accountId) -What 'ответ несёт accountId'
}

Assert-Status -Response (Send-Api -Method GET -Path "/transactions/$([guid]::NewGuid())" -Token $user.Token) `
    -Expected 404 -What 'несуществующая операция'

Write-Step 'Пагинация'

$page = Send-Api -Method GET -Path "/accounts/$accountId/transactions?pageSize=1" -Token $user.Token
if (Assert-Status -Response $page -Expected 200 -What 'первая страница' -PassThru) {
    $first = Read-Json -Response $page

    Assert-True -Condition ($first.items.Count -eq 1) -What 'страница содержит ровно один элемент'
    Assert-True -Condition ($first.hasNextPage -eq $true) -What 'страница сообщает о продолжении'

    $cursor = @{
        pageSize         = 1
        cursorOccurredAt = ConvertTo-IsoUtc -Instant $first.nextCursorDate
        cursorId         = $first.nextCursorId
    }

    $next = Send-Api -Method GET -Path ("/accounts/$accountId/transactions" + (ConvertTo-Query -Parameters $cursor)) -Token $user.Token
    if (Assert-Status -Response $next -Expected 200 -What 'вторая страница по курсору' -PassThru) {
        $second = Read-Json -Response $next
        Assert-True -Condition ($second.items[0].id -ne $first.items[0].id) -What 'вторая страница отдаёт другую операцию'

        $cursor.cursorOccurredAt = ConvertTo-IsoOffset -Instant $first.nextCursorDate
        $offset = Send-Api -Method GET -Path ("/accounts/$accountId/transactions" + (ConvertTo-Query -Parameters $cursor)) -Token $user.Token

        if (Assert-Status -Response $offset -Expected 200 -What 'курсор со смещением +03:00' -PassThru) {
            Assert-True -Condition ((Read-Json -Response $offset).items[0].id -eq $second.items[0].id) `
                -What 'смещение не меняет момент — та же страница, что и по UTC'
        }
    }
}

Assert-Status -Response (Send-Api -Method GET -Path "/accounts/$accountId/transactions?pageSize=101" -Token $user.Token) `
    -Expected 400 -What 'pageSize сверх потолка'

$foreignAccount = Send-Api -Method GET -Path "/accounts/$([guid]::NewGuid())/transactions" -Token $user.Token
if (Assert-Status -Response $foreignAccount -Expected 200 -What 'несуществующий счёт — пустая страница, а не отказ' -PassThru) {
    Assert-True -Condition ((Read-Json -Response $foreignAccount).items.Count -eq 0) `
        -What 'выдача пуста — фильтр по владельцу ничего не раскрывает'
}

Write-Step 'Фильтры'

$byDirection = Send-Api -Method GET -Path "/accounts/$accountId/transactions?direction=debit" -Token $user.Token
if (Assert-Status -Response $byDirection -Expected 200 -What 'direction в нижнем регистре — тот же вид, что в ответах' -PassThru) {
    $items = (Read-Json -Response $byDirection).items
    Assert-True -Condition (@($items | Where-Object { $_.direction -ne 'debit' }).Count -eq 0) `
        -What 'фильтр по направлению действительно отсекает'
}

$badDirection = Send-Api -Method GET -Path "/accounts/$accountId/transactions?direction=sideways" -Token $user.Token
if (Assert-Status -Response $badDirection -Expected 400 -What 'недопустимое значение direction' -PassThru) {
    Assert-True -Condition ($null -ne (Read-Json -Response $badDirection).errors.direction) `
        -What 'ошибка называет параметр'
}

Assert-Status -Response (Send-Api -Method GET -Path ("/accounts/$accountId/transactions" + (ConvertTo-Query -Parameters @{
    dateFrom = ConvertTo-IsoOffset -Instant (Get-Date).AddDays(-7)
    dateTo   = ConvertTo-IsoOffset -Instant (Get-Date).AddDays(1)
})) -Token $user.Token) -Expected 200 -What 'диапазон дат со смещением принимается'

Assert-Status -Response (Send-Api -Method GET -Path "/accounts/$accountId/transactions?categoryId=$expenseCategory" -Token $user.Token) `
    -Expected 200 -What 'фильтр по категории'

Write-Step 'Изменение'

Assert-Status -Response (Send-Api -Method PATCH -Path "/transactions/$transactionId/description" -Token $user.Token `
    -Body @{ description = 'Уточнённое описание' }) -Expected 204 -What 'смена описания'

Assert-Status -Response (Send-Api -Method PATCH -Path "/transactions/$transactionId/description" -Token $user.Token `
    -Body @{ description = $null }) -Expected 204 -What 'очистка описания'

Assert-Status -Response (Send-Api -Method PATCH -Path "/transactions/$transactionId/category" -Token $user.Token `
    -Body @{ categoryId = $otherExpense }) -Expected 204 -What 'перенос в другую категорию того же направления'

Assert-Status -Response (Send-Api -Method PATCH -Path "/transactions/$transactionId/category" -Token $user.Token `
    -Body @{ categoryId = $otherExpense }) -Expected 204 -What 'перенос в ту же категорию — успех без изменений'

Assert-Status -Response (Send-Api -Method PATCH -Path "/transactions/$transactionId/category" -Token $user.Token `
    -Body @{ categoryId = $incomeCategory }) -Expected 422 -What 'перенос в категорию другого направления отклонён'

Send-Api -Method POST -Path "/categories/$expenseCategory/archive" -Token $user.Token | Out-Null

Assert-Status -Response (Send-Api -Method PATCH -Path "/transactions/$transactionId/category" -Token $user.Token `
    -Body @{ categoryId = $expenseCategory }) -Expected 422 -What 'перенос в архивную категорию отклонён'

Send-Api -Method POST -Path "/categories/$expenseCategory/unarchive" -Token $user.Token | Out-Null

Assert-Status -Response (Send-Api -Method PATCH -Path "/transactions/$transactionId/category" -Token $user.Token `
    -Body @{ categoryId = $([guid]::NewGuid()) }) -Expected 404 -What 'перенос в несуществующую категорию'

Write-Step 'Участие в аналитике'

$balanceBefore = Get-Balance

Assert-Status -Response (Send-Api -Method POST -Path "/transactions/$transactionId/exclude" -Token $user.Token) `
    -Expected 204 -What 'исключение из аналитики'

Assert-True -Condition ((Get-Balance) -eq $balanceBefore) `
    -What 'баланс не изменился — деньги двигались, из аналитики убрана только запись'

$excluded = Read-Json -Response (Send-Api -Method GET -Path "/transactions/$transactionId" -Token $user.Token)
Assert-True -Condition ($excluded.isExcluded -eq $true) -What 'операция помечена исключённой'

Assert-Status -Response (Send-Api -Method POST -Path "/transactions/$transactionId/exclude" -Token $user.Token) `
    -Expected 204 -What 'повторное исключение идемпотентно'

Assert-Status -Response (Send-Api -Method POST -Path "/transactions/$transactionId/include" -Token $user.Token) `
    -Expected 204 -What 'возврат в аналитику'

Assert-True -Condition ((Get-Balance) -eq $balanceBefore) -What 'баланс не изменился и при возврате'

Write-Step 'Изоляция между учётками'

$other = New-TestUser -Label 'tx-other'
$otherCategory = New-Category -Name 'Своя' -Type 'expense' -Token $other.Token

Assert-Status -Response (Send-Api -Method GET -Path "/transactions/$transactionId" -Token $other.Token) `
    -Expected 404 -What 'чужая операция не читается'

Assert-Status -Response (Send-Api -Method POST -Path "/transactions/$transactionId/exclude" -Token $other.Token) `
    -Expected 404 -What 'чужая операция не исключается'

$foreignBody = $body.Clone()
$foreignBody.categoryId = $otherCategory

Assert-Status -Response (Send-Api -Method POST -Path "/accounts/$accountId/transactions" -Token $other.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $foreignBody) `
    -Expected 404 -What 'на чужой счёт нельзя записать операцию'

Assert-Status -Response (Send-Api -Method GET -Path "/transactions/$transactionId") `
    -Expected 401 -What 'без токена — 401'

Complete-Suite
