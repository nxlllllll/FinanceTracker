#Requires -Version 5.1
<#
.SYNOPSIS
    Проверяет, что rebuild-projection действительно восстанавливает проекцию из журнала событий.

.DESCRIPTION
    Создаёт данные через API, ломает проекции напрямую в базе, зовёт CLI и сверяет.
    Ключевая проверка — не «числа вернулись», а то, что подложенная в проекцию строка,
    которой нет ни в одном событии, после ребилда исчезает. Это отличает восстановление
    из журнала от простой правки того, что нашлось.

.EXAMPLE
    ./Check-RebuildProjection.ps1
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

Start-Suite -Name 'RebuildProjection'

function Invoke-Sql {
    param([Parameter(Mandatory)][string] $Sql)

    $output = docker compose exec -T postgres psql -U $DbUser -d $Database -t -A -F ',' -c $Sql 2>&1

    if ($LASTEXITCODE -ne 0) { throw "psql failed: $output" }

    return @($output | Where-Object { $_ -and $_.Trim() })
}

function Invoke-Cli {
    param([Parameter(Mandatory)][string[]] $Arguments)

    $output = docker compose --profile tools run --rm cli @Arguments 2>&1
    Write-Note ($output -join "`n    ")

    return $LASTEXITCODE
}

function Get-ProjectionState {
    param([Parameter(Mandatory)][string] $UserId)

    return Invoke-Sql -Sql @"
SELECT 'role:' || role_id, is_active, last_version FROM user_roles WHERE user_id = '$UserId'
UNION ALL
SELECT 'perm:' || permission, is_active, last_version FROM user_permissions WHERE user_id = '$UserId'
ORDER BY 1;
"@
}

Write-Step 'Подготовка: пользователь, root, право'

$user = New-TestUser -Label 'rebuild'
Write-Note "учётка: $($user.Email)"

$me = Send-Api -Method GET -Path '/users/me' -Token $user.Token
if (-not (Assert-Status -Response $me -Expected 200 -What 'GET /users/me' -PassThru)) {
    Complete-Suite
    return
}

$userId = (Read-Json -Response $me).id
Write-Note "userId: $userId"

# grant-root идемпотентен: повторный прогон скрипта на той же учётке просто ничего не изменит.
Invoke-Cli -Arguments @('grant-root', $user.Email) | Out-Null

# Токен выдан до назначения роли и прав в нём не несёт — нужен новый.
$user = New-TestUser -Label 'rebuild' -Fresh:$false

$grant = Send-Api -Method POST -Path "/users/$userId/permissions" -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body @{ permission = 'account:write' }

Assert-True -Condition ($grant.Status -in 200, 201, 202, 204, 409) `
    -What "право выдано или уже было (получен $($grant.Status))"

Write-Step 'Ждём проекции'

$before = $null
$settled = $false
$deadline = (Get-Date).AddSeconds(15)

while ((Get-Date) -lt $deadline) {
    $before = Get-ProjectionState -UserId $userId
    if (($before | Where-Object { $_ -like 'perm:account:write*' }) -and ($before | Where-Object { $_ -like 'role:*' })) {
        $settled = $true
        break
    }
    Start-Sleep -Milliseconds 500
}

if (-not (Assert-True -Condition $settled -What 'роль и право доехали до проекций' -PassThru)) {
    Write-Note "получено: $($before -join ' | ')"
    Complete-Suite
    return
}

Write-Note "до поломки: $($before -join ' | ')"

Write-Step 'Ломаем проекции'

Invoke-Sql -Sql @"
UPDATE user_roles       SET is_active = false WHERE user_id = '$userId';
UPDATE user_permissions SET is_active = false WHERE user_id = '$userId';
INSERT INTO user_permissions (user_id, permission, is_active, last_version, granted_at)
VALUES ('$userId', 'account:delete', true, 99, now())
ON CONFLICT (user_id, permission) DO NOTHING;
"@ | Out-Null

$broken = Get-ProjectionState -UserId $userId
Write-Note "после поломки: $($broken -join ' | ')"

Assert-True -Condition (($broken -join '') -match 'account:delete') -What 'подложенное право на месте'
Assert-True -Condition (($broken -join '') -ne ($before -join '')) -What 'проекции действительно испорчены'

Write-Step 'Ребилд'

Assert-True -Condition ((Invoke-Cli -Arguments @('rebuild-projection', '--projection', 'user-role', $userId)) -eq 0) `
    -What 'rebuild-projection --projection user-role'

Assert-True -Condition ((Invoke-Cli -Arguments @('rebuild-projection', '--projection', 'permission', $userId)) -eq 0) `
    -What 'rebuild-projection --projection permission'

Write-Step 'Сверка'

$after = Get-ProjectionState -UserId $userId
Write-Note "после ребилда: $($after -join ' | ')"

Assert-True -Condition (($after -join '') -notmatch 'account:delete') `
    -What 'подложенное право исчезло — проекция собрана из журнала, а не подправлена'

Assert-True -Condition (($after -join '') -eq ($before -join '')) `
    -What 'состояние совпадает с тем, что было до поломки'

Complete-Suite
