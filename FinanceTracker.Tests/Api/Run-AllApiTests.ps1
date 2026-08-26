#Requires -Version 5.1
<#
.SYNOPSIS
    Прогоняет все API-наборы и сводит результат в одну таблицу.

.DESCRIPTION
    Находит соседние Test-*.ps1 и запускает каждый в своём процессе.
    Полный вывод возвращает -Verbose.

.PARAMETER Only
    Запустить лишь часть наборов, по имени без префикса: -Only Categories,Transactions

.PARAMETER FailFast
    Остановиться на первом упавшем наборе.

.EXAMPLE
    ./Run-AllApiTests.ps1
    ./Run-AllApiTests.ps1 -Only Transactions -Verbose
    ./Run-AllApiTests.ps1 -BaseUrl http://staging.local:8080 -FailFast
#>
[CmdletBinding()]
param(
    [string]   $BaseUrl   = 'http://localhost:8080',
    [string]   $ApiPrefix = '/api/v1',
    [string[]] $Only      = @(),
    [switch]   $FailFast
)

$ErrorActionPreference = 'Stop'

$started = [datetime]::UtcNow
$showEverything = $PSBoundParameters.ContainsKey('Verbose')

Write-Host "FinanceTracker — прогон API-наборов" -ForegroundColor White
Write-Host "Цель: $BaseUrl$ApiPrefix" -ForegroundColor DarkGray

try {
    Invoke-WebRequest -Uri "$BaseUrl$ApiPrefix/auth/login" -Method POST `
        -ContentType 'application/json' -Body '{"email":"probe@none.test","password":"x"}' `
        -UseBasicParsing -SkipHttpErrorCheck:($PSVersionTable.PSVersion.Major -ge 7) -ErrorAction Stop | Out-Null
}
catch {
    # На PowerShell 5.1 неуспешный код прилетает исключением — это тоже признак живого API.
    if ($null -eq $_.Exception.Response) {
        Write-Host "API недоступен: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Подними стенд: docker compose --profile app up -d" -ForegroundColor Red
        exit 2
    }
}

$suites = Get-ChildItem -Path $PSScriptRoot -Filter 'Test-*.ps1' | Sort-Object Name

if ($Only.Count -gt 0) {
    $suites = $suites | Where-Object { $Only -contains ($_.BaseName -replace '^Test-', '') }

    if (-not $suites) {
        Write-Host "Ни один набор не совпал с: $($Only -join ', ')" -ForegroundColor Red
        exit 2
    }
}

Write-Host "Наборов к запуску: $($suites.Count)`n" -ForegroundColor DarkGray

$results = @()

foreach ($suite in $suites) {
    $name = $suite.BaseName -replace '^Test-', ''
    $suiteStarted = [datetime]::UtcNow

    Write-Host ("  … {0}" -f $name) -NoNewline -ForegroundColor DarkGray

    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $suite.FullName `
        -BaseUrl $BaseUrl -ApiPrefix $ApiPrefix 2>&1

    $exitCode = $LASTEXITCODE

    $summary = $output | Select-String -Pattern "$name`: (?:провалено (\d+) из (\d+)|все (\d+) проверок)" | Select-Object -Last 1

    $failures = 0
    $checks   = 0

    if ($summary) {
        $groups = $summary.Matches[0].Groups
        if ($groups[1].Success) {
            $failures = [int]$groups[1].Value
            $checks   = [int]$groups[2].Value
        }
        else {
            $checks = [int]$groups[3].Value
        }
    }

    $passed  = ($failures -eq 0 -and $exitCode -eq 0 -and $checks -gt 0)
    $crashed = ($checks -eq 0)

    # Затираем строку прогресса — она своё отработала.
    Write-Host ("`r{0}`r" -f (' ' * 50)) -NoNewline

    if ($showEverything -or -not $passed) {
        Write-Host "########## $name" -ForegroundColor White

        foreach ($line in $output) {
            $text = [string]$line

            $isFailureLine = $text -match '^\s*\[(FAIL|\s*[45]\d\d)\]'
            $isSection     = $text -match '^\s*==='
            $isPayload     = $text -match '^\s{6,}\{'

            if ($showEverything -or $crashed -or $isFailureLine -or $isSection -or $isPayload -or $text -match 'провалено') {
                $color = if ($isFailureLine) { 'Red' } elseif ($isSection) { 'Cyan' } else { 'Gray' }
                Write-Host $text -ForegroundColor $color
            }
        }

        Write-Host ''
    }

    $results += [pscustomobject]@{
        Suite    = $name
        Checks   = $checks
        Failures = $failures
        Passed   = $passed
        Duration = [math]::Round(([datetime]::UtcNow - $suiteStarted).TotalSeconds, 1)
    }

    if ($FailFast -and -not $passed) {
        Write-Host "Остановлено на первом упавшем наборе (-FailFast).`n" -ForegroundColor Yellow
        break
    }
}

$results | ForEach-Object {
    $mark  = if ($_.Passed) { 'ok  ' } else { 'FAIL' }
    $color = if ($_.Passed) { 'Green' } else { 'Red' }

    Write-Host ("  [{0}] {1,-22} {2,4} проверок, {3,2} провалов, {4,5} с" -f `
        $mark, $_.Suite, $_.Checks, $_.Failures, $_.Duration) -ForegroundColor $color
}

$totalChecks   = ($results | Measure-Object -Property Checks -Sum).Sum
$totalFailures = ($results | Measure-Object -Property Failures -Sum).Sum
$failedSuites  = @($results | Where-Object { -not $_.Passed })
$elapsed       = [math]::Round(([datetime]::UtcNow - $started).TotalSeconds, 1)

Write-Host ('-' * 62) -ForegroundColor DarkGray

if ($failedSuites.Count -eq 0) {
    Write-Host ("  {0} проверок за {1} с — всё прошло." -f $totalChecks, $elapsed) -ForegroundColor Green
    exit 0
}

Write-Host ("  {0} проверок за {1} с, провалено {2} в: {3}" -f `
    $totalChecks, $elapsed, $totalFailures, ($failedSuites.Suite -join ', ')) -ForegroundColor Red
exit 1
