#Requires -Version 5.1
<#
.SYNOPSIS
    Группа RecurringTransactions: шаблоны, из которых воркер раз в месяц делает транзакции.

.DESCRIPTION
    Шаблон — не транзакция: создание не двигает баланс, а исполнение делает воркер по
    расписанию. Отдельно проверяется, что деактивированный шаблон нельзя редактировать —
    это единственная группа, где изменение зависит от состояния.

.EXAMPLE
    ./Test-RecurringTransactions.ps1
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

Start-Suite -Name 'RecurringTransactions'

$user = New-TestUser -Label 'rec'
Write-Note "учётка: $($user.Email)"

Write-Step 'Подготовка'

$stamp = [guid]::NewGuid().ToString('N').Substring(0, 6)

function New-Category {
    param([string] $Name, [string] $Type = 'expense', [string] $Token = $user.Token)

    $response = Send-Api -Method POST -Path '/categories' -Token $Token `
        -Headers @{ 'Idempotency-Key' = New-Key } -Body @{ name = $Name; type = $Type }

    if ($response.Status -ne 201) { throw "Не удалось создать категорию '$Name': $($response.Status) $(ConvertTo-Text -Raw $response.Content)" }
    return (Read-Json -Response $response).id
}

$accountResponse = Send-Api -Method POST -Path '/accounts' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ name = "Счёт-$stamp"; type = 'Checking'; currency = 'RUB'; initialBalance = 50000 }

if (-not (Assert-Status -Response $accountResponse -Expected 201 -What 'счёт создан' -PassThru)) {
    Complete-Suite
    return
}

$accountId = (Read-Json -Response $accountResponse).id

$expenseCategory = New-Category -Name "Подписки-$stamp"
$incomeCategory  = New-Category -Name "Зарплата-$stamp" -Type 'income'

function Get-Balance {
    $account = Read-Json -Response (Send-Api -Method GET -Path "/accounts/$accountId" -Token $user.Token)
    return [decimal]$account.balance.amount
}

Write-Note "счёт $accountId"

Write-Step 'Создание'

$key = New-Key
$body = @{
    accountId   = $accountId
    categoryId  = $expenseCategory
    amount      = 999
    currency    = 'RUB'
    direction   = 'Debit'
    dayOfMonth  = 15
    description = 'Подписка'
}

$balanceBefore = Get-Balance

$created = Send-Api -Method POST -Path '/recurring-transactions' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = $key } -Body $body

if (-not (Assert-Status -Response $created -Expected 201 -What 'POST /recurring-transactions' -PassThru)) {
    Complete-Suite
    return
}

$recurringId = (Read-Json -Response $created).id
Assert-True -Condition ($null -ne $created.Headers['Location']) -What 'ответ несёт Location'

Assert-True -Condition ((Get-Balance) -eq $balanceBefore) `
    -What 'создание шаблона не тронуло баланс'

$repeat = Send-Api -Method POST -Path '/recurring-transactions' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = $key } -Body $body

Assert-Status -Response $repeat -Expected 201 -What 'повтор с тем же Idempotency-Key'
Assert-True -Condition ((Read-Json -Response $repeat).id -eq $recurringId) `
    -What 'повтор вернул исходный шаблон'

Assert-Status -Response (Send-Api -Method POST -Path '/recurring-transactions' -Token $user.Token -Body $body) `
    -Expected 400 -What 'создание без Idempotency-Key'

Write-Step 'Некорректный ввод'

$badDay = $body.Clone(); $badDay.dayOfMonth = 32
Assert-Status -Response (Send-Api -Method POST -Path '/recurring-transactions' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $badDay) `
    -Expected 400 -What 'день месяца выше 31 отклонён'

$zeroDay = $body.Clone(); $zeroDay.dayOfMonth = 0
Assert-Status -Response (Send-Api -Method POST -Path '/recurring-transactions' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $zeroDay) `
    -Expected 400 -What 'нулевой день месяца отклонён'

$lastDay = $body.Clone(); $lastDay.dayOfMonth = 31
Assert-Status -Response (Send-Api -Method POST -Path '/recurring-transactions' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $lastDay) `
    -Expected 201 -What '31-е число принимается'

$negative = $body.Clone(); $negative.amount = -100
Assert-Status -Response (Send-Api -Method POST -Path '/recurring-transactions' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $negative) `
    -Expected 400 -What 'отрицательная сумма отклонена'

$badCurrency = $body.Clone(); $badCurrency.currency = 'XX'
Assert-Status -Response (Send-Api -Method POST -Path '/recurring-transactions' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $badCurrency) `
    -Expected 400 -What 'некорректный код валюты'

$wrongDirection = $body.Clone(); $wrongDirection.categoryId = $incomeCategory
Assert-Status -Response (Send-Api -Method POST -Path '/recurring-transactions' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $wrongDirection) `
    -Expected 422 -What 'расход в категорию дохода отклонён'

$missingAccount = $body.Clone(); $missingAccount.accountId = [guid]::NewGuid()
Assert-Status -Response (Send-Api -Method POST -Path '/recurring-transactions' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body $missingAccount) `
    -Expected 400, 404 -What 'несуществующий счёт отклонён'

Write-Step 'Чтение'

$single = Send-Api -Method GET -Path "/recurring-transactions/$recurringId" -Token $user.Token
if (Assert-Status -Response $single -Expected 200 -What 'GET /recurring-transactions/{id}' -PassThru) {
    $template = Read-Json -Response $single

    Assert-True -Condition ($template.amount.amount -eq 999) -What 'сумма отдана в camelCase'
    Assert-True -Condition ($template.dayOfMonth -eq 15) -What 'ответ несёт день месяца'
    Assert-True -Condition ($template.isActive -eq $true) -What 'шаблон активен при создании'
    Assert-True -Condition ($template.PSObject.Properties.Name -contains 'lastMissedAt') `
        -What 'ответ несёт lastMissedAt — без него пропущенный месяц не виден клиенту'
    Assert-True -Condition ($null -eq $template.lastExecutedAt) -What 'новый шаблон ещё не исполнялся'
}

Assert-Status -Response (Send-Api -Method GET -Path "/recurring-transactions/$([guid]::NewGuid())" -Token $user.Token) `
    -Expected 404 -What 'несуществующий шаблон'

$page = Send-Api -Method GET -Path '/recurring-transactions?pageSize=1' -Token $user.Token
if (Assert-Status -Response $page -Expected 200 -What 'первая страница' -PassThru) {
    $first = Read-Json -Response $page

    Assert-True -Condition ($first.items.Count -eq 1) -What 'страница содержит ровно один элемент'
    Assert-True -Condition ($first.hasNextPage -eq $true) -What 'страница сообщает о продолжении'

    $cursor = @{
        pageSize        = 1
        cursorCreatedAt = ConvertTo-IsoOffset -Instant $first.nextCursorDate
        cursorId        = $first.nextCursorId
    }

    $next = Send-Api -Method GET -Path ('/recurring-transactions' + (ConvertTo-Query -Parameters $cursor)) -Token $user.Token
    if (Assert-Status -Response $next -Expected 200 -What 'вторая страница по курсору со смещением +03:00' -PassThru) {
        Assert-True -Condition ((Read-Json -Response $next).items[0].id -ne $first.items[0].id) `
            -What 'вторая страница отдаёт другой шаблон'
    }
}

Assert-Status -Response (Send-Api -Method GET -Path '/recurring-transactions?pageSize=101' -Token $user.Token) `
    -Expected 400 -What 'pageSize сверх потолка'

Write-Step 'Изменение'

Assert-Status -Response (Send-Api -Method PATCH -Path "/recurring-transactions/$recurringId/amount" -Token $user.Token `
    -Body @{ amount = 1500 }) -Expected 204 -What 'изменение суммы'

$afterAmount = Read-Json -Response (Send-Api -Method GET -Path "/recurring-transactions/$recurringId" -Token $user.Token)
Assert-True -Condition ($afterAmount.amount.amount -eq 1500) -What 'новая сумма видна при чтении'

Assert-Status -Response (Send-Api -Method PATCH -Path "/recurring-transactions/$recurringId/amount" -Token $user.Token `
    -Body @{ amount = -1 }) -Expected 400 -What 'отрицательная сумма при изменении отклонена'

Assert-Status -Response (Send-Api -Method PATCH -Path "/recurring-transactions/$recurringId/day-of-month" -Token $user.Token `
    -Body @{ dayOfMonth = 20 }) -Expected 204 -What 'изменение дня месяца'

Assert-True -Condition ((Read-Json -Response (Send-Api -Method GET -Path "/recurring-transactions/$recurringId" -Token $user.Token)).dayOfMonth -eq 20) `
    -What 'новый день месяца виден при чтении'

Assert-Status -Response (Send-Api -Method PATCH -Path "/recurring-transactions/$recurringId/day-of-month" -Token $user.Token `
    -Body @{ dayOfMonth = 32 }) -Expected 400 -What 'день месяца выше 31 при изменении отклонён'

Assert-Status -Response (Send-Api -Method PATCH -Path "/recurring-transactions/$recurringId/currency" -Token $user.Token `
    -Body @{ currency = 'USD' }) -Expected 204 -What 'изменение валюты'

$afterCurrency = Read-Json -Response (Send-Api -Method GET -Path "/recurring-transactions/$recurringId" -Token $user.Token)
Assert-True -Condition ($afterCurrency.amount.currency -eq 'USD') -What 'новая валюта видна при чтении'
Assert-True -Condition ($afterCurrency.amount.amount -eq 1500) `
    -What 'сумма не пересчитана: 1500 RUB стали 1500 USD, а не эквивалентом'

Assert-Status -Response (Send-Api -Method PATCH -Path "/recurring-transactions/$recurringId/currency" -Token $user.Token `
    -Body @{ currency = 'XX' }) -Expected 400 -What 'некорректная валюта при изменении отклонена'

Assert-Status -Response (Send-Api -Method PATCH -Path "/recurring-transactions/$recurringId/currency" -Token $user.Token `
    -Body @{ currency = 'USD' }) -Expected 204 -What 'та же валюта — успех без изменений'

Write-Step 'Активность'

Assert-Status -Response (Send-Api -Method POST -Path "/recurring-transactions/$recurringId/deactivate" -Token $user.Token) `
    -Expected 204 -What 'деактивация'

Assert-True -Condition ((Read-Json -Response (Send-Api -Method GET -Path "/recurring-transactions/$recurringId" -Token $user.Token)).isActive -eq $false) `
    -What 'шаблон помечен неактивным'

Assert-Status -Response (Send-Api -Method POST -Path "/recurring-transactions/$recurringId/deactivate" -Token $user.Token) `
    -Expected 204 -What 'повторная деактивация идемпотентна'

Assert-Status -Response (Send-Api -Method PATCH -Path "/recurring-transactions/$recurringId/amount" -Token $user.Token `
    -Body @{ amount = 2000 }) -Expected 422 -What 'сумму деактивированного шаблона менять нельзя'

Assert-Status -Response (Send-Api -Method PATCH -Path "/recurring-transactions/$recurringId/currency" -Token $user.Token `
    -Body @{ currency = 'RUB' }) -Expected 422 -What 'валюту деактивированного шаблона менять нельзя'

Assert-Status -Response (Send-Api -Method PATCH -Path "/recurring-transactions/$recurringId/day-of-month" -Token $user.Token `
    -Body @{ dayOfMonth = 5 }) -Expected 422 -What 'день месяца деактивированного шаблона менять нельзя'

Assert-Status -Response (Send-Api -Method POST -Path "/recurring-transactions/$recurringId/activate" -Token $user.Token) `
    -Expected 204 -What 'активация'

Assert-True -Condition ((Read-Json -Response (Send-Api -Method GET -Path "/recurring-transactions/$recurringId" -Token $user.Token)).isActive -eq $true) `
    -What 'шаблон снова активен'

Assert-Status -Response (Send-Api -Method POST -Path "/recurring-transactions/$recurringId/activate" -Token $user.Token) `
    -Expected 204 -What 'повторная активация идемпотентна'

Assert-Status -Response (Send-Api -Method PATCH -Path "/recurring-transactions/$recurringId/amount" -Token $user.Token `
    -Body @{ amount = 2000 }) -Expected 204 -What 'после активации изменения снова проходят'

Write-Step 'Изоляция между учётками'

$other = Get-OutsiderUser

Assert-Status -Response (Send-Api -Method GET -Path "/recurring-transactions/$recurringId" -Token $other.Token) `
    -Expected 404 -What 'чужой шаблон не читается'

Assert-Status -Response (Send-Api -Method POST -Path "/recurring-transactions/$recurringId/deactivate" -Token $other.Token) `
    -Expected 404 -What 'чужой шаблон не деактивируется'

Assert-Status -Response (Send-Api -Method PATCH -Path "/recurring-transactions/$recurringId/amount" -Token $other.Token `
    -Body @{ amount = 1 }) -Expected 404 -What 'чужой шаблон не редактируется'

Assert-Status -Response (Send-Api -Method GET -Path "/recurring-transactions/$recurringId") `
    -Expected 401 -What 'без токена — 401'

Complete-Suite
