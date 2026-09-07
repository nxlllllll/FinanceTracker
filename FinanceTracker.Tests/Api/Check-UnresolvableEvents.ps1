#Requires -Version 5.1
<#
.SYNOPSIS
    Проверяет очередь эскалаций: чтение, фильтры и перевод события в работу и в закрытие.

.DESCRIPTION
    События в unresolvable_events пишет только система, и добиться их появления через API
    нельзя — поэтому набор сеет их напрямую в базу, а через API только читает и меняет.
    Право на них выдано лишь роли admin, а роль выдаётся через CLI, так что набор требует
    локального docker и живёт рядом с Check-RebuildProjection.ps1, а не среди Test-*.

.EXAMPLE
    ./Check-UnresolvableEvents.ps1
#>
[CmdletBinding()]
param(
    [string] $BaseUrl   = 'http://localhost:8080',
    [string] $ApiPrefix = '/api/v1',
    [string] $Database  = 'FinanceTracker',
    [string] $DbUser    = 'postgres'
)

$ErrorActionPreference = 'Stop'
$script:BaseUrl   = $BaseUrl
$script:ApiPrefix = $ApiPrefix

. "$PSScriptRoot\_Common.ps1"

Start-Suite -Name 'UnresolvableEvents'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

function Invoke-Sql {
    param([Parameter(Mandatory)][string] $Sql)

    Push-Location $repoRoot
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = docker compose exec -T postgres psql -U $DbUser -d $Database -t -A -F ',' -c $Sql 2>&1
    }
    finally {
        $ErrorActionPreference = $previous
        Pop-Location
    }

    if ($LASTEXITCODE -ne 0) { throw "psql failed: $output" }

    return @($output | Where-Object { $_ -and $_.Trim() })
}

function Invoke-Cli {
    param([Parameter(Mandatory)][string[]] $Arguments)

    Push-Location $repoRoot
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = docker compose --profile tools run --rm cli @Arguments 2>&1
    }
    finally {
        $ErrorActionPreference = $previous
        Pop-Location
    }

    Write-Note ($output -join "`n    ")

    return $LASTEXITCODE
}

<#
.SYNOPSIS
    Сеет событие эскалации напрямую в базу и возвращает его идентификатор.

.DESCRIPTION
    $PayloadSql — выражение SQL, а не строка JSON: Windows PowerShell срезает двойные кавычки
    при передаче аргументов нативной команде, и литерал вроде {"amount":100} доезжает до psql
    как {amount:100}. json_build_object обходится без них.
#>
function New-SeededEvent {
    param(
        [Parameter(Mandatory)][string] $TypeCode,
        [Parameter(Mandatory)][string] $Reason,
        [Parameter(Mandatory)][string] $PayloadSql,
        [int] $MinutesAgo = 0
    )

    $id = [guid]::NewGuid().ToString()

    Invoke-Sql -Sql @"
INSERT INTO unresolvable_events (id, type_code, reference_id, reason, payload, occurred_at)
VALUES ('$id', '$TypeCode', '$([guid]::NewGuid())', '$Reason', $PayloadSql, now() - interval '$MinutesAgo minutes');
"@ | Out-Null

    return $id
}

function Get-EventIds {
    param([Parameter(Mandatory)]$Response)

    $body = Read-Json -Response $Response
    if ($null -eq $body) { return @() }

    return @($body.items | ForEach-Object { $_.id })
}

# ------------------------------------------------------------------ обычный пользователь

Write-Step 'Без права: очередь эскалаций закрыта'

$plain = New-TestUser -Label 'unresolvable-plain'
Write-Note "учётка: $($plain.Email)"

$someId = [guid]::NewGuid().ToString()

Assert-Status -Response (Send-Api -Method GET -Path '/unresolvable-events' -Token $plain.Token) `
    -Expected 403 -What 'GET /unresolvable-events без права'

Assert-Status -Response (Send-Api -Method GET -Path "/unresolvable-events/$someId" -Token $plain.Token) `
    -Expected 403 -What 'GET /unresolvable-events/{id} без права'

Assert-Status -Response (Send-Api -Method POST -Path "/unresolvable-events/$someId/acknowledge" -Token $plain.Token) `
    -Expected 403 -What 'acknowledge без права'

Assert-Status -Response (Send-Api -Method POST -Path "/unresolvable-events/$someId/resolve" -Token $plain.Token) `
    -Expected 403 -What 'resolve без права'

Assert-Status -Response (Send-Api -Method GET -Path '/unresolvable-events') `
    -Expected 401 -What 'GET /unresolvable-events без токена'

Write-Step 'Выдаём роль admin через CLI'

$admin = New-TestUser -Label 'unresolvable-admin'
Write-Note "учётка: $($admin.Email)"

Invoke-Cli -Arguments @('grant-role', $admin.Email, 'admin') | Out-Null

$granted = $false
$deadline = (Get-Date).AddSeconds(20)

while ((Get-Date) -lt $deadline) {
    $admin = New-TestUser -Label 'unresolvable-admin'
    if ((Send-Api -Method GET -Path '/unresolvable-events' -Token $admin.Token).Status -eq 200) {
        $granted = $true
        break
    }
    Start-Sleep -Milliseconds 700
}

if (-not (Assert-True -Condition $granted -What 'роль admin доехала до проекции' -PassThru)) {
    Complete-Suite
    return
}

Write-Step 'Сеем два события напрямую в базу'

$older = New-SeededEvent -TypeCode 'transfer_compensation' -Reason 'refund refused' `
    -PayloadSql "json_build_object('amount', 100)::jsonb" -MinutesAgo 30

$newer = New-SeededEvent -TypeCode 'outbox_dead_letter' -Reason 'max retries exceeded' `
    -PayloadSql "json_build_object('attempts', 5)::jsonb" -MinutesAgo 5

Write-Note "старое: $older"
Write-Note "новое:  $newer"

try {

Write-Step 'Список'

$list = Send-Api -Method GET -Path '/unresolvable-events?pageSize=100' -Token $admin.Token
Assert-Status -Response $list -Expected 200 -What 'GET /unresolvable-events'

$ids = Get-EventIds -Response $list
Assert-True -Condition ($ids -contains $older -and $ids -contains $newer) -What 'оба посеянных события в выдаче'

$positionOlder = [array]::IndexOf($ids, $older)
$positionNewer = [array]::IndexOf($ids, $newer)
Assert-True -Condition ($positionOlder -lt $positionNewer) `
    -What 'старое идёт раньше нового — очередь разбирают с головы'

$single = Send-Api -Method GET -Path "/unresolvable-events/$older" -Token $admin.Token
Assert-Status -Response $single -Expected 200 -What 'GET /unresolvable-events/{id}'

$detail = Read-Json -Response $single
Assert-True -Condition ($detail.reason -eq 'refund refused') -What 'причина на месте'
Assert-True -Condition ($detail.type -eq 'transferCompensation') -What 'тип отдан строкой в camelCase'
Assert-True -Condition ($null -ne $detail.payload -and "$($detail.payload)".Contains('100')) `
    -What 'карточка несёт payload, которого нет в списке'
Assert-True -Condition ($null -eq $detail.acknowledgedAt -and $null -eq $detail.resolvedAt) `
    -What 'свежее событие ни взято, ни закрыто'

$listItem = (Read-Json -Response $list).items | Where-Object { $_.id -eq $older }
Assert-True -Condition ($null -eq $listItem.payload) -What 'в списке payload отсутствует'

Assert-Status -Response (Send-Api -Method GET -Path "/unresolvable-events/$([guid]::NewGuid())" -Token $admin.Token) `
    -Expected 404 -What 'GET несуществующего события'

Write-Step 'Фильтры'

$byType = Send-Api -Method GET -Path '/unresolvable-events?type=outboxDeadLetter&pageSize=100' -Token $admin.Token
Assert-Status -Response $byType -Expected 200 -What 'фильтр по типу'

$typeIds = Get-EventIds -Response $byType
Assert-True -Condition ($typeIds -contains $newer -and $typeIds -notcontains $older) -What 'тип отсекает чужое'

$byTypeUpper = Send-Api -Method GET -Path '/unresolvable-events?type=OUTBOX_DEAD_LETTER&pageSize=100' -Token $admin.Token
Assert-Status -Response $byTypeUpper -Expected 400 -What 'тип принимает имя перечисления, а не код в базе'

$byTypeCasing = Send-Api -Method GET -Path '/unresolvable-events?type=OutboxDeadLetter&pageSize=100' -Token $admin.Token
Assert-Status -Response $byTypeCasing -Expected 200 -What 'тип принимает любой регистр'

Assert-Status -Response (Send-Api -Method GET -Path '/unresolvable-events?type=nonsense' -Token $admin.Token) `
    -Expected 400 -What 'неизвестный тип отвергнут'

$open = Send-Api -Method GET -Path '/unresolvable-events?isResolved=false&pageSize=100' -Token $admin.Token
Assert-True -Condition ((Get-EventIds -Response $open) -contains $older) -What 'isResolved=false отдаёт открытое'

$closed = Send-Api -Method GET -Path '/unresolvable-events?isResolved=true&pageSize=100' -Token $admin.Token
Assert-True -Condition ((Get-EventIds -Response $closed) -notcontains $older) -What 'isResolved=true не отдаёт открытое'

$untouched = Send-Api -Method GET -Path '/unresolvable-events?isAcknowledged=false&pageSize=100' -Token $admin.Token
Assert-True -Condition ((Get-EventIds -Response $untouched) -contains $older) -What 'isAcknowledged=false отдаёт нетронутое'

Write-Step 'Валидация'

Assert-Status -Response (Send-Api -Method GET -Path '/unresolvable-events?pageSize=0' -Token $admin.Token) `
    -Expected 400 -What 'pageSize=0 отвергнут'

Assert-Status -Response (Send-Api -Method GET -Path '/unresolvable-events?pageSize=101' -Token $admin.Token) `
    -Expected 400 -What 'pageSize=101 отвергнут'

Assert-Status -Response (Send-Api -Method GET -Path "/unresolvable-events?cursorId=$([guid]::NewGuid())" -Token $admin.Token) `
    -Expected 400 -What 'курсор без даты отвергнут'

Assert-Status -Response (Send-Api -Method GET -Path '/unresolvable-events?cursorOccurredAt=2026-01-01T00:00:00Z' -Token $admin.Token) `
    -Expected 400 -What 'дата курсора без id отвергнута'

Write-Step 'Курсор'

$firstPage = Send-Api -Method GET -Path '/unresolvable-events?pageSize=1&isResolved=false' -Token $admin.Token
Assert-Status -Response $firstPage -Expected 200 -What 'первая страница'

$firstBody = Read-Json -Response $firstPage
Assert-True -Condition ($firstBody.items.Count -eq 1) -What 'на странице одна запись'
Assert-True -Condition ($firstBody.hasNextPage -eq $true) -What 'заявлена следующая страница'
Assert-True -Condition ($null -ne $firstBody.nextCursorId -and $null -ne $firstBody.nextCursorDate) -What 'курсор отдан целиком'

$query = ConvertTo-Query -Parameters @{
    pageSize         = 100
    isResolved       = 'false'
    cursorOccurredAt = (ConvertTo-IsoUtc -Instant $firstBody.nextCursorDate)
    cursorId         = $firstBody.nextCursorId
}

$secondPage = Send-Api -Method GET -Path "/unresolvable-events$query" -Token $admin.Token
Assert-Status -Response $secondPage -Expected 200 -What 'вторая страница по курсору'

$secondIds = Get-EventIds -Response $secondPage
Assert-True -Condition ($secondIds -notcontains $firstBody.items[0].id) `
    -What 'вторая страница не повторяет первую — курсор идёт вперёд по возрастанию'

Write-Step 'Acknowledge'

Assert-Status -Response (Send-Api -Method POST -Path "/unresolvable-events/$older/acknowledge" -Token $admin.Token) `
    -Expected 204 -What 'событие взято в работу'

$afterAck = Read-Json -Response (Send-Api -Method GET -Path "/unresolvable-events/$older" -Token $admin.Token)
Assert-True -Condition ($null -ne $afterAck.acknowledgedAt) -What 'acknowledgedAt проставлен'
Assert-True -Condition ($null -eq $afterAck.resolvedAt) -What 'взятие в работу не закрывает событие'

$stamp = $afterAck.acknowledgedAt

Assert-Status -Response (Send-Api -Method POST -Path "/unresolvable-events/$older/acknowledge" -Token $admin.Token) `
    -Expected 204 -What 'повторное взятие отвечает так же'

$afterSecondAck = Read-Json -Response (Send-Api -Method GET -Path "/unresolvable-events/$older" -Token $admin.Token)
Assert-True -Condition ((ConvertTo-IsoUtc -Instant $afterSecondAck.acknowledgedAt) -eq (ConvertTo-IsoUtc -Instant $stamp)) `
    -What 'повтор сохраняет первую отметку, а не переписывает её'

$taken = Send-Api -Method GET -Path '/unresolvable-events?isAcknowledged=true&pageSize=100' -Token $admin.Token
Assert-True -Condition ((Get-EventIds -Response $taken) -contains $older) -What 'isAcknowledged=true отдаёт взятое'

$inProgress = Send-Api -Method GET -Path '/unresolvable-events?isAcknowledged=true&isResolved=false&pageSize=100' -Token $admin.Token
Assert-True -Condition ((Get-EventIds -Response $inProgress) -contains $older) `
    -What 'два флага вместе сужают до «взято и не закрыто»'

Assert-Status -Response (Send-Api -Method POST -Path "/unresolvable-events/$([guid]::NewGuid())/acknowledge" -Token $admin.Token) `
    -Expected 404 -What 'acknowledge несуществующего события'

Write-Step 'Resolve'

Assert-Status -Response (Send-Api -Method POST -Path "/unresolvable-events/$older/resolve" -Token $admin.Token) `
    -Expected 204 -What 'событие закрыто'

$afterResolve = Read-Json -Response (Send-Api -Method GET -Path "/unresolvable-events/$older" -Token $admin.Token)
Assert-True -Condition ($null -ne $afterResolve.resolvedAt) -What 'resolvedAt проставлен'

$resolvedStamp = $afterResolve.resolvedAt

Assert-Status -Response (Send-Api -Method POST -Path "/unresolvable-events/$older/resolve" -Token $admin.Token) `
    -Expected 204 -What 'повторное закрытие отвечает так же'

$afterSecondResolve = Read-Json -Response (Send-Api -Method GET -Path "/unresolvable-events/$older" -Token $admin.Token)
Assert-True -Condition ((ConvertTo-IsoUtc -Instant $afterSecondResolve.resolvedAt) -eq (ConvertTo-IsoUtc -Instant $resolvedStamp)) `
    -What 'повтор сохраняет первую отметку закрытия'

$closedNow = Send-Api -Method GET -Path '/unresolvable-events?isResolved=true&pageSize=100' -Token $admin.Token
Assert-True -Condition ((Get-EventIds -Response $closedNow) -contains $older) -What 'isResolved=true отдаёт закрытое'

$openNow = Send-Api -Method GET -Path '/unresolvable-events?isResolved=false&pageSize=100' -Token $admin.Token
Assert-True -Condition ((Get-EventIds -Response $openNow) -notcontains $older) -What 'закрытое ушло из открытых'
Assert-True -Condition ((Get-EventIds -Response $openNow) -contains $newer) -What 'соседнее событие закрытие не задело'

Assert-Status -Response (Send-Api -Method POST -Path "/unresolvable-events/$([guid]::NewGuid())/resolve" -Token $admin.Token) `
    -Expected 404 -What 'resolve несуществующего события'

Write-Step 'Изоляция на существующем событии'

Assert-Status -Response (Send-Api -Method GET -Path "/unresolvable-events/$newer" -Token $plain.Token) `
    -Expected 403 -What 'обычный пользователь не читает существующее событие'

Assert-Status -Response (Send-Api -Method POST -Path "/unresolvable-events/$newer/resolve" -Token $plain.Token) `
    -Expected 403 -What 'обычный пользователь не закрывает существующее событие'

}
finally {
    Write-Step 'Уборка'

    Invoke-Sql -Sql "DELETE FROM unresolvable_events WHERE id IN ('$older', '$newer');" | Out-Null
    Write-Note 'посеянные события удалены'
}

Complete-Suite
