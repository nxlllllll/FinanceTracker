#Requires -Version 5.1
<#
.SYNOPSIS
    Оповещения о бюджете: какие пороги попадают в outbox при тратах и при смене лимита.

.DESCRIPTION
    Набор читает таблицу через psql, требует локального docker и живёт среди Check-*. 

    Пороги берутся из BudgetAlerts:Thresholds — набор рассчитан на значения по умолчанию, 80 и 100.

.EXAMPLE
    ./Check-BudgetAlerts.ps1
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

Start-Suite -Name 'BudgetAlerts'

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

<#
.SYNOPSIS
    Пороги оповещений, поставленных в outbox по бюджету, в порядке постановки, через запятую.
#>
function Get-AlertThresholds {
    param([Parameter(Mandatory)][string] $BudgetId)

    $rows = Invoke-Sql -Sql @"
SELECT (e->>'EventPayload')::jsonb->>'Threshold'
FROM outbox_messages m, jsonb_array_elements(m.payload->'Events') e
WHERE m.aggregate_type = 'Budget'
  AND m.aggregate_id = '$BudgetId'
  AND e->>'EventType' = 'budget.threshold_reached'
ORDER BY m.id;
"@

    return ($rows -join ',')
}

$user = New-TestUser -Label 'budget-alerts'
Write-Note "учётка: $($user.Email)"

# ---------------------------------------------------------------- подготовка

Write-Step 'Подготовка'

function New-Category {
    param([Parameter(Mandatory)][string] $Name)

    $response = Send-Api -Method POST -Path '/categories' -Token $user.Token `
        -Headers @{ 'Idempotency-Key' = New-Key } -Body @{ name = $Name; type = 'expense' }

    if ($response.Status -ne 201) { throw "Не удалось создать категорию '$Name': $($response.Status) $(ConvertTo-Text -Raw $response.Content)" }
    return (Read-Json -Response $response).id
}

function New-Budget {
    param(
        [Parameter(Mandatory)][string] $CategoryId,
        [Parameter(Mandatory)][decimal] $Amount
    )

    $response = Send-Api -Method POST -Path '/budgets' -Token $user.Token `
        -Headers @{ 'Idempotency-Key' = New-Key } `
        -Body @{ categoryId = $CategoryId; amount = $Amount; currency = 'RUB'; from = $monthStart; to = $monthEnd }

    if ($response.Status -ne 201) { throw "Не удалось создать бюджет: $($response.Status) $(ConvertTo-Text -Raw $response.Content)" }
    return (Read-Json -Response $response).id
}

function Add-Spending {
    param(
        [Parameter(Mandatory)][string] $CategoryId,
        [Parameter(Mandatory)][decimal] $Amount
    )

    $response = Send-Api -Method POST -Path "/accounts/$accountId/transactions" -Token $user.Token `
        -Headers @{ 'Idempotency-Key' = New-Key } `
        -Body @{ categoryId = $CategoryId; amount = $Amount; currency = 'RUB'; direction = 'Debit'
                 occurredAt = (Get-Date).ToUniversalTime().ToString('o') }

    if ($response.Status -ne 201) { throw "Не удалось записать трату $($Amount): $($response.Status) $(ConvertTo-Text -Raw $response.Content)" }
}

$stamp = [guid]::NewGuid().ToString('N').Substring(0, 6)

$today        = Get-Date
$firstOfMonth = $today.AddDays(-$today.Day + 1)
$monthStart   = $firstOfMonth.ToString('yyyy-MM-dd')
$monthEnd     = $firstOfMonth.AddMonths(1).AddDays(-1).ToString('yyyy-MM-dd')

$accountResponse = Send-Api -Method POST -Path '/accounts' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ name = "Оповещения-$stamp"; type = 'Checking'; currency = 'RUB'; initialBalance = 100000 }

if (-not (Assert-Status -Response $accountResponse -Expected 201 -What 'счёт создан' -PassThru)) {
    Complete-Suite
    return
}

$accountId = (Read-Json -Response $accountResponse).id

$groceries = New-Category -Name "Продукты-$stamp"
$budgetId  = New-Budget -CategoryId $groceries -Amount 10000

Write-Note "бюджет 10000 на $monthStart … $monthEnd"

Write-Step 'Траты'

Add-Spending -CategoryId $groceries -Amount 5000
$thresholds = Get-AlertThresholds -BudgetId $budgetId
Assert-True -Condition ($thresholds -eq '') -What "50% — оповещений нет (получено '$thresholds')"

Add-Spending -CategoryId $groceries -Amount 3500
$thresholds = Get-AlertThresholds -BudgetId $budgetId
Assert-True -Condition ($thresholds -eq '80') -What "85% — оповещение о 80% (получено '$thresholds')"

Add-Spending -CategoryId $groceries -Amount 500
$thresholds = Get-AlertThresholds -BudgetId $budgetId
Assert-True -Condition ($thresholds -eq '80') -What "90% — 80% уже пройден, нового оповещения нет (получено '$thresholds')"

Write-Step 'Лимит'

Assert-Status -Response (Send-Api -Method PATCH -Path "/budgets/$budgetId/amount" -Token $user.Token `
    -Body @{ amount = 9000 }) -Expected 204 -What 'лимит снижен до потраченного'

$thresholds = Get-AlertThresholds -BudgetId $budgetId
Assert-True -Condition ($thresholds -eq '80,100') -What "100% без новой траты — оповещение о 100% (получено '$thresholds')"

Assert-Status -Response (Send-Api -Method PATCH -Path "/budgets/$budgetId/amount" -Token $user.Token `
    -Body @{ amount = 20000 }) -Expected 204 -What 'лимит повышен'

$thresholds = Get-AlertThresholds -BudgetId $budgetId
Assert-True -Condition ($thresholds -eq '80,100') -What "повышение лимита оповещений не добавляет (получено '$thresholds')"

Write-Step 'Неактивный бюджет'

$transport = New-Category -Name "Транспорт-$stamp"
$inactiveId = New-Budget -CategoryId $transport -Amount 10000

Assert-Status -Response (Send-Api -Method POST -Path "/budgets/$inactiveId/deactivate" -Token $user.Token) `
    -Expected 204 -What 'бюджет деактивирован'

Add-Spending -CategoryId $transport -Amount 9000
$thresholds = Get-AlertThresholds -BudgetId $inactiveId
Assert-True -Condition ($thresholds -eq '') -What "90% неактивного бюджета — оповещений нет (получено '$thresholds')"

Complete-Suite
