#Requires -Version 5.1
<#
.SYNOPSIS
    Печатает фактические цифры прогресса по всем бюджетам учётки набора Budgets.

.DESCRIPTION
    Нужен, чтобы понять, в каких единицах считается percentage и пересчитывается ли прогресс
    после переноса периода. Проверок не делает — только показывает, что есть на самом деле.

.EXAMPLE
    ./Check-BudgetProgress.ps1
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

$storePath = Join-Path -Path $PSScriptRoot -ChildPath '.api-users.json'

if (-not (Test-Path -Path $storePath)) {
    Write-Host "Нет '$storePath'. Прогони сначала Test-Budgets.ps1." -ForegroundColor Red
    exit 1
}

$store = Get-Content -Path $storePath -Raw | ConvertFrom-Json

if (-not $store.bud) {
    Write-Host "В хранилище нет учётки набора Budgets. Прогони Test-Budgets.ps1." -ForegroundColor Red
    exit 1
}

$login = Send-Api -Method POST -Path '/auth/login' -Body @{ email = $store.bud; password = $script:DefaultPassword }

if ($login.Status -ne 200) {
    Write-Host "Не удалось войти как '$($store.bud)': $($login.Status)" -ForegroundColor Red
    exit 1
}

$token = (Read-Json -Response $login).accessToken

Write-Host "Учётка: $($store.bud)`n" -ForegroundColor DarkGray

$budgets = @()
$cursor = @{ pageSize = 50 }

do {
    $page = Send-Api -Method GET -Path ('/budgets' + (ConvertTo-Query -Parameters $cursor)) -Token $token

    if ($page.Status -ne 200) {
        Write-Host "Не удалось получить список бюджетов: $($page.Status) $(ConvertTo-Text -Raw $page.Content)" -ForegroundColor Red
        exit 1
    }

    $parsed = Read-Json -Response $page
    $budgets += $parsed.items

    if ($parsed.hasNextPage) {
        $cursor.cursorCreatedAt = ConvertTo-IsoUtc -Instant $parsed.nextCursorDate
        $cursor.cursorId = $parsed.nextCursorId
    }
} while ($parsed.hasNextPage)

Write-Host ("{0,-10} {1,-12} {2,-12} {3,10} {4,10} {5,10} {6,12}" -f `
    'бюджет', 'с', 'по', 'лимит', 'spent', 'remaining', 'percentage') -ForegroundColor White
Write-Host ('-' * 82) -ForegroundColor DarkGray

foreach ($budget in $budgets) {
    $progressResponse = Send-Api -Method GET -Path "/budgets/$($budget.id)/progress" -Token $token

    if ($progressResponse.Status -ne 200) {
        Write-Host ("{0,-10} прогресс недоступен: {1}" -f $budget.id.Substring(0, 8), $progressResponse.Status) -ForegroundColor Red
        continue
    }

    $progress = Read-Json -Response $progressResponse

    # Что мы хотим увидеть: совпадает ли percentage с spent/лимит*100, или он доля, или
    # считается от чего-то ещё. И держится ли spent после переноса периода.
    $expected = if ($budget.amount.amount -ne 0) {
        [math]::Round(($progress.spent / $budget.amount.amount) * 100, 2)
    } else { 0 }

    $matches = [math]::Abs($progress.percentage - $expected) -lt 0.01
    $color = if ($matches) { 'Green' } else { 'Yellow' }

    Write-Host ("{0,-10} {1,-12} {2,-12} {3,10} {4,10} {5,10} {6,12}" -f `
        $budget.id.Substring(0, 8),
        $budget.from,
        $budget.to,
        $budget.amount.amount,
        $progress.spent,
        $progress.remaining,
        $progress.percentage) -ForegroundColor $color

    if (-not $matches) {
        Write-Host ("{0,-10} ожидалось spent/лимит*100 = {1}" -f '', $expected) -ForegroundColor DarkYellow
    }
}

Write-Host "`nЖёлтым отмечены строки, где percentage не равен spent/лимит*100." -ForegroundColor DarkGray
Write-Host "Если такие есть — процент считается иначе, и мои ожидания в наборе надо править." -ForegroundColor DarkGray
