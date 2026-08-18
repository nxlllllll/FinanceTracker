#Requires -Version 5.1
<#
.SYNOPSIS
    Группа Users: профиль, сводные цифры и смена собственных учётных данных.

.DESCRIPTION
    Всё адресуется как /users/me — пользователь берётся из токена, а не из URL. Отдельно
    проверяется, что смена пароля и email требует текущего пароля и отзывает остальные
    сессии, оставляя текущую живой.

.EXAMPLE
    ./Test-Users.ps1
#>

[CmdletBinding()]
param(
    [string] $BaseUrl   = 'http://localhost:8080',
    [string] $ApiPrefix = '/api/v1'
)

function Wait-NonZeroBalance {
    param([int] $TimeoutSeconds = 15)

    $watch = [Diagnostics.Stopwatch]::StartNew()

    while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $total = Read-Json -Response (Send-Api -Method GET -Path '/users/me/balance' -Token $token)
        if ([decimal]$total.amount -gt 0) { return $total }
        Start-Sleep -Milliseconds 300
    }

    return $null
}

$ErrorActionPreference = 'Stop'
$script:BaseUrl   = $BaseUrl
$script:ApiPrefix = $ApiPrefix

. "$PSScriptRoot\_Common.ps1"

Start-Suite -Name 'Users'

# Своя учётка на прогон, а не переиспользуемая: набор меняет пароль и email, и остаточное
# состояние сломало бы следующий запуск.
$stamp = [guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "usr-$stamp@financetracker.test"
$password = $script:DefaultPassword

$register = Send-Api -Method POST -Path '/auth/register' `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ email = $email; password = $password; baseCurrency = 'RUB' }

if (-not (Assert-Status -Response $register -Expected 201 -What "регистрация $email" -PassThru)) {
    Complete-Suite
    return
}

$userId = (Read-Json -Response $register).id
$token = ((Read-Json -Response (Send-Api -Method POST -Path '/auth/login' -Body @{ email = $email; password = $password })).accessToken)

Write-Note "учётка: $email"

# ---------------------------------------------------------------- профиль

Write-Step 'Профиль'

$me = Send-Api -Method GET -Path '/users/me' -Token $token
if (Assert-Status -Response $me -Expected 200 -What 'GET /users/me' -PassThru) {
    $profile = Read-Json -Response $me

    Assert-True -Condition ($profile.id -eq $userId) -What 'профиль принадлежит владельцу токена'
    Assert-True -Condition ($profile.email -eq $email) -What 'ответ несёт email'
    Assert-True -Condition ($profile.baseCurrency -eq 'RUB') -What 'ответ несёт базовую валюту'
}

Assert-Status -Response (Send-Api -Method GET -Path '/users/me') -Expected 401 -What 'без токена — 401'

# ---------------------------------------------------------------- сводные цифры

Write-Step 'Сводные цифры'

$balance = Send-Api -Method GET -Path '/users/me/balance' -Token $token
if (Assert-Status -Response $balance -Expected 200 -What 'GET /users/me/balance' -PassThru) {
    $total = Read-Json -Response $balance

    Assert-True -Condition ($total.amount -eq 0) -What 'без счетов общий баланс нулевой'
    Assert-True -Condition ($total.currency -eq 'RUB') -What 'баланс отдан в базовой валюте'
}

$period = (Get-Date).ToString('yyyy-MM-01')

$summary = Send-Api -Method GET -Path "/users/me/summary/$period" -Token $token
if (Assert-Status -Response $summary -Expected 200 -What 'GET /users/me/summary/{period}' -PassThru) {
    $parsed = Read-Json -Response $summary

    Assert-True -Condition ($parsed.income -eq 0) -What 'доход за месяц без операций нулевой'
    Assert-True -Condition ($parsed.expense -eq 0) -What 'расход за месяц без операций нулевой'
    Assert-True -Condition ($parsed.PSObject.Properties.Name -contains 'recalculationPending') `
        -What 'сводка несёт recalculationPending'
}

Assert-Status -Response (Send-Api -Method GET -Path "/users/me/summary/$((Get-Date).ToString('yyyy-MM-17'))" -Token $token) `
    -Expected 200 -What 'любая дата месяца принимается'

# ---------------------------------------------------------------- лента операций

Write-Step 'Лента операций'

$stampShort = $stamp.Substring(0, 6)

$accountId = (Read-Json -Response (Send-Api -Method POST -Path '/accounts' -Token $token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ name = "Основной-$stampShort"; type = 'Checking'; currency = 'RUB'; initialBalance = 20000 })).id

$secondAccountId = (Read-Json -Response (Send-Api -Method POST -Path '/accounts' -Token $token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ name = "Второй-$stampShort"; type = 'Savings'; currency = 'RUB'; initialBalance = 0 })).id

$categoryId = (Read-Json -Response (Send-Api -Method POST -Path '/categories' -Token $token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body @{ name = "Еда-$stampShort"; type = 'expense' })).id

$occurredAt = (Get-Date).ToUniversalTime().ToString('o')

Send-Api -Method POST -Path "/accounts/$accountId/transactions" -Token $token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ categoryId = $categoryId; amount = 1200; currency = 'RUB'; direction = 'Debit'; description = 'Обед'; occurredAt = $occurredAt } | Out-Null

$transfer = Send-Api -Method POST -Path "/accounts/$accountId/transfers" -Token $token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ toAccountId = $secondAccountId; amount = 5000; description = 'Накопления'; occurredAt = $occurredAt }

Assert-Status -Response $transfer -Expected 202 -What 'перевод принят как 202 — кредит идёт асинхронно'

$history = Send-Api -Method GET -Path '/users/me/operations?pageSize=20' -Token $token
if (Assert-Status -Response $history -Expected 200 -What 'GET /users/me/operations' -PassThru) {
    $items = (Read-Json -Response $history).items

    Assert-True -Condition ($items.Count -ge 2) -What 'лента содержит и транзакцию, и перевод'

    $expense = $items | Where-Object { $_.type -eq 'expense' } | Select-Object -First 1
    $movement = $items | Where-Object { $_.type -eq 'transfer' } | Select-Object -First 1

    Assert-True -Condition ($null -ne $expense.transaction) -What 'у транзакции заполнен блок transaction'
    Assert-True -Condition ($null -eq $expense.transfer) -What 'у транзакции блок transfer пуст'
    Assert-True -Condition ($null -ne $movement.transfer) -What 'у перевода заполнен блок transfer'
    Assert-True -Condition ($null -eq $movement.transaction) -What 'у перевода блок transaction пуст'
    Assert-True -Condition ($null -ne $movement.transfer.status) -What 'перевод несёт статус — иначе исход не узнать'
}

$filtered = Send-Api -Method GET -Path '/users/me/operations?type=expense' -Token $token
if (Assert-Status -Response $filtered -Expected 200 -What 'type в нижнем регистре — тот же вид, что в ответах' -PassThru) {
    $items = (Read-Json -Response $filtered).items
    Assert-True -Condition (@($items | Where-Object { $_.type -ne 'expense' }).Count -eq 0) `
        -What 'фильтр по типу действительно отсекает'
}

$badType = Send-Api -Method GET -Path '/users/me/operations?type=nonsense' -Token $token
if (Assert-Status -Response $badType -Expected 400 -What 'недопустимое значение type' -PassThru) {
    Assert-True -Condition ($null -ne (Read-Json -Response $badType).errors.type) -What 'ошибка называет параметр'
}

$page = Send-Api -Method GET -Path '/users/me/operations?pageSize=1' -Token $token
if (Assert-Status -Response $page -Expected 200 -What 'первая страница' -PassThru) {
    $first = Read-Json -Response $page

    Assert-True -Condition ($first.items.Count -eq 1) -What 'страница содержит ровно один элемент'

    if ($first.hasNextPage) {
        $cursor = @{
            pageSize         = 1
            cursorOccurredAt = ConvertTo-IsoOffset -Instant $first.nextCursorDate
            cursorId         = $first.nextCursorId
        }

        $next = Send-Api -Method GET -Path ('/users/me/operations' + (ConvertTo-Query -Parameters $cursor)) -Token $token
        if (Assert-Status -Response $next -Expected 200 -What 'вторая страница по курсору со смещением +03:00' -PassThru) {
            Assert-True -Condition ((Read-Json -Response $next).items[0].id -ne $first.items[0].id) `
                -What 'вторая страница отдаёт другую операцию'
        }
    }
}

Assert-Status -Response (Send-Api -Method GET -Path '/users/me/operations?pageSize=101' -Token $token) `
    -Expected 400 -What 'pageSize сверх потолка'

Assert-Status -Response (Send-Api -Method GET -Path ('/users/me/operations' + (ConvertTo-Query -Parameters @{
    dateFrom = ConvertTo-IsoOffset -Instant (Get-Date).AddDays(-7)
    dateTo   = ConvertTo-IsoOffset -Instant (Get-Date).AddDays(1)
})) -Token $token) -Expected 200 -What 'диапазон дат со смещением принимается'

# ---------------------------------------------------------------- баланс после операций

Write-Step 'Баланс после операций'

$after = Wait-NonZeroBalance

if ($null -eq $after) {
    Write-Note "общий баланс так и остался нулевым за 15 с"
} else {
    Write-Note "общий баланс: $($after.amount) $($after.currency)"
}

Assert-True -Condition ($null -ne $after) `
    -What 'общий баланс складывает счета: перевод между своими деньги не уничтожает'

# ---------------------------------------------------------------- базовая валюта

Write-Step 'Базовая валюта'

Assert-Status -Response (Send-Api -Method PATCH -Path '/users/me/base-currency' -Token $token `
    -Body @{ baseCurrency = 'XX' }) -Expected 400 -What 'некорректный код валюты'

Assert-Status -Response (Send-Api -Method PATCH -Path '/users/me/base-currency' -Token $token `
    -Body @{ baseCurrency = 'USD' }) -Expected 204 -What 'смена базовой валюты'

Assert-True -Condition ((Read-Json -Response (Send-Api -Method GET -Path '/users/me' -Token $token)).baseCurrency -eq 'USD') `
    -What 'новая базовая валюта видна в профиле'

Send-Api -Method PATCH -Path '/users/me/base-currency' -Token $token -Body @{ baseCurrency = 'RUB' } | Out-Null

# ---------------------------------------------------------------- пароль

Write-Step 'Смена пароля'

$newPassword = 'Pa55word!Changed4Suite'

Assert-Status -Response (Send-Api -Method PATCH -Path '/users/me/password' -Token $token `
    -Body @{ currentPassword = 'НеТотПароль1!'; newPassword = $newPassword }) `
    -Expected 401, 422 -What 'неверный текущий пароль отклонён'

Assert-Status -Response (Send-Api -Method PATCH -Path '/users/me/password' -Token $token `
    -Body @{ currentPassword = $password; newPassword = 'x' }) `
    -Expected 400 -What 'слишком слабый новый пароль отклонён'

# Вторая сессия: она должна умереть при смене пароля, а текущая — выжить.
$secondToken = ((Read-Json -Response (Send-Api -Method POST -Path '/auth/login' -Body @{ email = $email; password = $password })).accessToken)
Assert-Status -Response (Send-Api -Method GET -Path '/users/me' -Token $secondToken) -Expected 200 -What 'вторая сессия работает до смены пароля'

Assert-Status -Response (Send-Api -Method PATCH -Path '/users/me/password' -Token $token `
    -Body @{ currentPassword = $password; newPassword = $newPassword }) `
    -Expected 204 -What 'смена пароля'

Assert-Status -Response (Send-Api -Method GET -Path '/users/me' -Token $token) `
    -Expected 200 -What 'текущая сессия пережила смену пароля'

Assert-Status -Response (Send-Api -Method GET -Path '/users/me' -Token $secondToken) `
    -Expected 401 -What 'остальные сессии отозваны — смысл смены пароля после потери устройства'

Assert-Status -Response (Send-Api -Method POST -Path '/auth/login' -Body @{ email = $email; password = $password }) `
    -Expected 400, 401 -What 'старый пароль больше не подходит'

Assert-Status -Response (Send-Api -Method POST -Path '/auth/login' -Body @{ email = $email; password = $newPassword }) `
    -Expected 200 -What 'новый пароль подходит'

# ---------------------------------------------------------------- email

Write-Step 'Смена email'

$newEmail = "usr-$stamp-changed@financetracker.test"

Assert-Status -Response (Send-Api -Method PATCH -Path '/users/me/email' -Token $token `
    -Body @{ currentPassword = 'НеТотПароль1!'; newEmail = $newEmail }) `
    -Expected 401, 422 -What 'неверный текущий пароль отклонён'

Assert-Status -Response (Send-Api -Method PATCH -Path '/users/me/email' -Token $token `
    -Body @{ currentPassword = $newPassword; newEmail = 'не-email' }) `
    -Expected 400 -What 'некорректный email отклонён'

Assert-Status -Response (Send-Api -Method PATCH -Path '/users/me/email' -Token $token `
    -Body @{ currentPassword = $newPassword; newEmail = $newEmail }) `
    -Expected 204 -What 'смена email'

Assert-True -Condition ((Read-Json -Response (Send-Api -Method GET -Path '/users/me' -Token $token)).email -eq $newEmail) `
    -What 'новый email виден в профиле'

Assert-Status -Response (Send-Api -Method POST -Path '/auth/login' -Body @{ email = $newEmail; password = $newPassword }) `
    -Expected 200 -What 'вход по новому email'

Assert-Status -Response (Send-Api -Method POST -Path '/auth/login' -Body @{ email = $email; password = $newPassword }) `
    -Expected 400, 401 -What 'старый email больше не подходит'

Complete-Suite
