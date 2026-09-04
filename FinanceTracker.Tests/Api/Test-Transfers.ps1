#Requires -Version 5.1
<#
.SYNOPSIS
    Группа Transfers: перевод между своими счетами, его чтение и наблюдение за исходом.

.DESCRIPTION
    Перевод отвечает 202, а не 201, потому что на момент ответа сделана только его половина:
    дебет списан синхронно, кредит применяет воркер через outbox, и до тех пор перевод может
    быть компенсирован. Поэтому набор проверяет не «создалось», а весь путь: что ответ дал
    адрес, что по адресу читается перевод, и что его статус доезжает до completed.

    Отдельно проверяется, что дата у перевода не принимается извне — в отличие от транзакции,
    он описывает не событие снаружи системы, а её собственное действие.

.EXAMPLE
    ./Test-Transfers.ps1
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

Start-Suite -Name 'Transfers'

$user = New-TestUser -Label 'transfer'
Write-Note "учётка: $($user.Email)"

Write-Step 'Подготовка: три счёта'

function New-Account {
    param([string] $Name, [string] $Currency = 'RUB', [decimal] $Balance = 0, [string] $Token = $user.Token)

    $response = Send-Api -Method POST -Path '/accounts' -Token $Token `
        -Headers @{ 'Idempotency-Key' = New-Key } `
        -Body @{ name = $Name; type = 'Checking'; currency = $Currency; initialBalance = $Balance }

    if ($response.Status -ne 201) { throw "Не удалось создать счёт '$Name': $($response.Status) $($response.Content)" }
    return (Read-Json -Response $response).id
}

$fromAccountId  = New-Account -Name 'Источник'    -Balance 50000
$toAccountId    = New-Account -Name 'Назначение'
$asideAccountId = New-Account -Name 'Непричастный'

Write-Note "источник $fromAccountId, назначение $toAccountId"

<#
.SYNOPSIS
    Дожидается, пока перевод дойдёт до ожидаемого статуса.

.DESCRIPTION
    Кредит применяет отдельный воркер, получив сообщение через outbox и RabbitMQ. Читать
    статус сразу после ответа 202 — значит спрашивать до того, как вторая половина случилась.
#>
function Wait-TransferStatus {
    param(
        [Parameter(Mandatory)][string] $TransferId,
        [Parameter(Mandatory)][string] $Expected,
        [int] $TimeoutSeconds = 20
    )

    $watch = [Diagnostics.Stopwatch]::StartNew()
    $last  = '(не прочитан)'

    while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $response = Send-Api -Method GET -Path "/transfers/$TransferId" -Token $user.Token

        if ($response.Status -eq 200) {
            $last = (Read-Json -Response $response).status
            if ($last -eq $Expected) { return $true }
        }

        Start-Sleep -Milliseconds 300
    }

    Write-Note "последний статус: $last"
    return $false
}

Write-Step 'Создание: 202 с адресом перевода'

$created = Send-Api -Method POST -Path "/accounts/$fromAccountId/transfers" -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ toAccountId = $toAccountId; amount = 1500; description = 'Первый' }

if (-not (Assert-Status -Response $created -Expected 202 -What 'перевод принят' -PassThru)) {
    Complete-Suite
    return
}

$transferId = (Read-Json -Response $created).id
$location   = [string]($created.Headers['Location'])

Assert-True -Condition ($location -like "*/transfers/$transferId") `
    -What "Location указывает на перевод ($location)"

Write-Step 'Чтение по идентификатору'

$fetched = Send-Api -Method GET -Path "/transfers/$transferId" -Token $user.Token
Assert-Status -Response $fetched -Expected 200 -What 'перевод читается'

$transfer = Read-Json -Response $fetched

Assert-True -Condition ($transfer.id -eq $transferId)                   -What 'идентификатор совпадает'
Assert-True -Condition ($transfer.fromAccountId -eq $fromAccountId)     -What 'источник совпадает'
Assert-True -Condition ($transfer.toAccountId -eq $toAccountId)         -What 'назначение совпадает'
Assert-True -Condition ([decimal]$transfer.amountFrom.amount -eq 1500)  -What 'сумма списания совпадает'
Assert-True -Condition ($transfer.status -in @('pendingCredit', 'completed')) `
    -What "статус сразу после создания — pendingCredit или completed ($($transfer.status))"

Write-Step 'Исход: кредит доезжает'

Assert-True -Condition (Wait-TransferStatus -TransferId $transferId -Expected 'completed') `
    -What 'статус дошёл до completed'

Write-Step 'Список'

$all = Send-Api -Method GET -Path '/transfers' -Token $user.Token
Assert-Status -Response $all -Expected 200 -What 'список читается'

$items = (Read-Json -Response $all).items
Assert-True -Condition (@($items | Where-Object { $_.id -eq $transferId }).Count -eq 1) `
    -What 'перевод есть в списке'

Write-Step 'Фильтры списка'

$completed = Read-Json -Response (Send-Api -Method GET -Path (
    '/transfers' + (ConvertTo-Query -Parameters @{ status = 'completed' })
) -Token $user.Token)

Assert-True -Condition (@($completed.items | Where-Object { $_.id -eq $transferId }).Count -eq 1) `
    -What 'фильтр status=completed находит перевод'

$compensated = Read-Json -Response (Send-Api -Method GET -Path (
    '/transfers' + (ConvertTo-Query -Parameters @{ status = 'compensated' })
) -Token $user.Token)

Assert-True -Condition (@($compensated.items | Where-Object { $_.id -eq $transferId }).Count -eq 0) `
    -What 'фильтр status=compensated его не находит'

$byAccount = Read-Json -Response (Send-Api -Method GET -Path (
    '/transfers' + (ConvertTo-Query -Parameters @{ accountId = $fromAccountId })
) -Token $user.Token)

Assert-True -Condition (@($byAccount.items | Where-Object { $_.id -eq $transferId }).Count -eq 1) `
    -What 'фильтр по счёту-источнику находит перевод'

$byDestination = Read-Json -Response (Send-Api -Method GET -Path (
    '/transfers' + (ConvertTo-Query -Parameters @{ accountId = $toAccountId })
) -Token $user.Token)

Assert-True -Condition (@($byDestination.items | Where-Object { $_.id -eq $transferId }).Count -eq 1) `
    -What 'фильтр по счёту-назначению находит его же — перевод принадлежит обоим'

$byAside = Read-Json -Response (Send-Api -Method GET -Path (
    '/transfers' + (ConvertTo-Query -Parameters @{ accountId = $asideAccountId })
) -Token $user.Token)

Assert-True -Condition (@($byAside.items | Where-Object { $_.id -eq $transferId }).Count -eq 0) `
    -What 'фильтр по непричастному счёту его не находит'

Write-Step 'Пагинация'

Send-Api -Method POST -Path "/accounts/$fromAccountId/transfers" -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ toAccountId = $toAccountId; amount = 250 } | Out-Null

$firstPage = Read-Json -Response (Send-Api -Method GET -Path (
    '/transfers' + (ConvertTo-Query -Parameters @{ pageSize = 1 })
) -Token $user.Token)

Assert-True -Condition (@($firstPage.items).Count -eq 1) -What 'страница из одного перевода'
Assert-True -Condition ($firstPage.hasNextPage -eq $true) -What 'есть следующая страница'

$secondPage = Read-Json -Response (Send-Api -Method GET -Path (
    '/transfers' + (ConvertTo-Query -Parameters @{
        pageSize          = 1
        cursorOccurredAt  = (ConvertTo-IsoUtc -Instant $firstPage.nextCursorDate)
        cursorId          = $firstPage.nextCursorId
    })
) -Token $user.Token)

Assert-True -Condition (@($secondPage.items).Count -eq 1) -What 'вторая страница получена по курсору'
Assert-True -Condition ($secondPage.items[0].id -ne $firstPage.items[0].id) `
    -What 'вторая страница не повторяет первую'

Write-Step 'Отказы'

Assert-Status -Response (Send-Api -Method GET -Path "/transfers/$([guid]::NewGuid())" -Token $user.Token) `
    -Expected 404 -What 'несуществующий перевод — 404'

Assert-Status -Response (Send-Api -Method GET -Path (
    '/transfers' + (ConvertTo-Query -Parameters @{ status = 'halfway' })
) -Token $user.Token) -Expected 400 -What 'нераспознанный статус — 400'

Assert-Status -Response (Send-Api -Method GET -Path (
    '/transfers' + (ConvertTo-Query -Parameters @{ pageSize = 0 })
) -Token $user.Token) -Expected 400 -What 'pageSize вне диапазона — 400'

Assert-Status -Response (Send-Api -Method GET -Path (
    '/transfers' + (ConvertTo-Query -Parameters @{ cursorId = [guid]::NewGuid() })
) -Token $user.Token) -Expected 400 -What 'курсор без даты — 400'

Assert-Status -Response (Send-Api -Method POST -Path "/accounts/$fromAccountId/transfers" -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ toAccountId = $fromAccountId; amount = 100 }
) -Expected 400 -What 'перевод самому себе — 400'

Assert-Status -Response (Send-Api -Method POST -Path "/accounts/$fromAccountId/transfers" -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ toAccountId = $toAccountId; amount = 999999999 }
) -Expected 400, 422 -What 'сумма больше остатка — отказ'

Assert-Status -Response (Send-Api -Method POST -Path "/accounts/$fromAccountId/transfers" -Token $user.Token `
    -Body @{ toAccountId = $toAccountId; amount = 100 }
) -Expected 400 -What 'без Idempotency-Key — 400'

Write-Step 'Изоляция'

$outsider = Get-OutsiderUser

Assert-Status -Response (Send-Api -Method GET -Path "/transfers/$transferId" -Token $outsider.Token) `
    -Expected 404 -What 'чужой перевод не читается — 404, как несуществующий'

$outsiderList = Read-Json -Response (Send-Api -Method GET -Path '/transfers' -Token $outsider.Token)
Assert-True -Condition (@($outsiderList.items | Where-Object { $_.id -eq $transferId }).Count -eq 0) `
    -What 'чужой перевод не попадает в список постороннего'

Write-Step 'Дата извне не принимается'

$dated = Send-Api -Method POST -Path "/accounts/$fromAccountId/transfers" -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ toAccountId = $toAccountId; amount = 100; occurredAt = (ConvertTo-IsoUtc -Instant ([datetimeoffset]::UtcNow.AddDays(-30))) }

if (Assert-Status -Response $dated -Expected 202 -What 'лишнее поле occurredAt не ломает запрос' -PassThru) {
    $datedTransfer = Read-Json -Response (Send-Api -Method GET -Path "/transfers/$((Read-Json -Response $dated).id)" -Token $user.Token)

    Assert-True -Condition (([datetimeoffset]$datedTransfer.occurredAt) -gt [datetimeoffset]::UtcNow.AddDays(-1)) `
        -What 'перевод датирован моментом создания, а не присланной датой'
}

Complete-Suite
