#Requires -Version 5.1
<#
.SYNOPSIS
    Оповещения о бюджете: какие письма доходят до почты при тратах, смене лимита и выключенных уведомлениях.

.DESCRIPTION
    Письма читаются из Mailpit через его HTTP API, поэтому набор требует локального стенда и живёт среди Check-*.

    Письмо уходит асинхронно: outbox, RabbitMQ, воркер уведомлений. Отсутствие письма проверяется после
    письма, которое точно должно прийти позже, а не сразу после действия.

    Настройку уведомлений воркер читает в момент доставки, поэтому выключенные уведомления проверяются на
    отдельной учётке, где они не включаются обратно.

    Пороги берутся из BudgetAlerts:Thresholds — набор рассчитан на значения по умолчанию, 80 и 100.

.EXAMPLE
    ./Check-BudgetAlerts.ps1
#>
[CmdletBinding()]
param(
    [string] $BaseUrl    = 'http://localhost:8080',
    [string] $ApiPrefix  = '/api/v1',
    [string] $MailpitUrl = 'http://localhost:8025'
)

$ErrorActionPreference = 'Stop'
$script:BaseUrl   = $BaseUrl
$script:ApiPrefix = $ApiPrefix

. "$PSScriptRoot\_Common.ps1"

Start-Suite -Name 'BudgetAlerts'

<#
.SYNOPSIS
    Письма на адрес по бюджету категории, от старых к новым.

.DESCRIPTION
    Вызывающий оборачивает результат в @(): одно письмо PowerShell отдаёт не массивом, а объектом, а у
    одиночного объекта в Windows PowerShell 5.1 нет Count.
#>
function Get-Mail {
    param(
        [Parameter(Mandatory)][string] $Email,
        [Parameter(Mandatory)][string] $Category
    )

    $query = [uri]::EscapeDataString("to:`"$Email`"")
    $found = Invoke-RestMethod -Uri "$MailpitUrl/api/v1/search?query=$query&limit=200" -UseBasicParsing

    return @($found.messages | Where-Object { $_.Subject -like "Budget `"$Category`"*" } | Sort-Object -Property Created)
}

function Wait-MailCount {
    param(
        [Parameter(Mandatory)][string] $Email,
        [Parameter(Mandatory)][string] $Category,
        [Parameter(Mandatory)][int] $Expected,
        [int] $TimeoutSeconds = 30
    )

    $watch = [Diagnostics.Stopwatch]::StartNew()

    do {
        $mail = @(Get-Mail -Email $Email -Category $Category)
        if ($mail.Count -ge $Expected) { return $mail }
        Start-Sleep -Milliseconds 500
    } while ($watch.Elapsed.TotalSeconds -lt $TimeoutSeconds)

    return $mail
}

function New-Category {
    param(
        [Parameter(Mandatory)][string] $Token,
        [Parameter(Mandatory)][string] $Name
    )

    $response = Send-Api -Method POST -Path '/categories' -Token $Token `
        -Headers @{ 'Idempotency-Key' = New-Key } -Body @{ name = $Name; type = 'expense' }

    if ($response.Status -ne 201) { throw "Не удалось создать категорию '$Name': $($response.Status) $(ConvertTo-Text -Raw $response.Content)" }
    return (Read-Json -Response $response).id
}

function New-Budget {
    param(
        [Parameter(Mandatory)][string] $Token,
        [Parameter(Mandatory)][string] $CategoryId,
        [Parameter(Mandatory)][decimal] $Amount
    )

    $response = Send-Api -Method POST -Path '/budgets' -Token $Token `
        -Headers @{ 'Idempotency-Key' = New-Key } `
        -Body @{ categoryId = $CategoryId; amount = $Amount; currency = 'RUB'; from = $monthStart; to = $monthEnd }

    if ($response.Status -ne 201) { throw "Не удалось создать бюджет: $($response.Status) $(ConvertTo-Text -Raw $response.Content)" }
    return (Read-Json -Response $response).id
}

function New-Account {
    param([Parameter(Mandatory)][string] $Token)

    $response = Send-Api -Method POST -Path '/accounts' -Token $Token `
        -Headers @{ 'Idempotency-Key' = New-Key } `
        -Body @{ name = "Alerts-$stamp"; type = 'Checking'; currency = 'RUB'; initialBalance = 100000 }

    if ($response.Status -ne 201) { throw "Не удалось создать счёт: $($response.Status) $(ConvertTo-Text -Raw $response.Content)" }
    return (Read-Json -Response $response).id
}

function Add-Spending {
    param(
        [Parameter(Mandatory)][string] $Token,
        [Parameter(Mandatory)][string] $AccountId,
        [Parameter(Mandatory)][string] $CategoryId,
        [Parameter(Mandatory)][decimal] $Amount
    )

    $response = Send-Api -Method POST -Path "/accounts/$AccountId/transactions" -Token $Token `
        -Headers @{ 'Idempotency-Key' = New-Key } `
        -Body @{ categoryId = $CategoryId; amount = $Amount; currency = 'RUB'; direction = 'Debit'
                 occurredAt = (Get-Date).ToUniversalTime().ToString('o') }

    if ($response.Status -ne 201) { throw "Не удалось записать трату $($Amount): $($response.Status) $(ConvertTo-Text -Raw $response.Content)" }
}

try {
    Invoke-RestMethod -Uri "$MailpitUrl/api/v1/info" -UseBasicParsing | Out-Null
}
catch {
    Write-Host "Mailpit недоступен на $($MailpitUrl): подними стенд — docker compose --profile app up -d" -ForegroundColor Red
    exit 2
}

$user   = New-TestUser -Label 'budget-alerts'
$silent = New-TestUser -Label 'budget-alerts-silent'
Write-Note "учётки: $($user.Email), $($silent.Email)"

Write-Step 'Подготовка'

$stamp = [guid]::NewGuid().ToString('N').Substring(0, 6)

$today        = Get-Date
$firstOfMonth = $today.AddDays(-$today.Day + 1)
$monthStart   = $firstOfMonth.ToString('yyyy-MM-dd')
$monthEnd     = $firstOfMonth.AddMonths(1).AddDays(-1).ToString('yyyy-MM-dd')

$accountId       = New-Account -Token $user.Token
$silentAccountId = New-Account -Token $silent.Token

$groceriesName = "Groceries-$stamp"
$transportName = "Transport-$stamp"
$silentName    = "Silent-$stamp"

$groceries      = New-Category -Token $user.Token -Name $groceriesName
$transport      = New-Category -Token $user.Token -Name $transportName
$silentCategory = New-Category -Token $silent.Token -Name $silentName

$budgetId   = New-Budget -Token $user.Token -CategoryId $groceries -Amount 10000
$inactiveId = New-Budget -Token $user.Token -CategoryId $transport -Amount 10000
New-Budget -Token $silent.Token -CategoryId $silentCategory -Amount 10000 | Out-Null

Write-Note "бюджеты по 10000 на $monthStart … $monthEnd"

Write-Step 'Без письма'

Assert-Status -Response (Send-Api -Method PATCH -Path '/users/me/notifications' -Token $silent.Token -Body @{ type = $null }) `
    -Expected 204 -What 'у второй учётки уведомления выключены'

Add-Spending -Token $silent.Token -AccountId $silentAccountId -CategoryId $silentCategory -Amount 9000

Assert-Status -Response (Send-Api -Method POST -Path "/budgets/$inactiveId/deactivate" -Token $user.Token) `
    -Expected 204 -What 'бюджет деактивирован'

Add-Spending -Token $user.Token -AccountId $accountId -CategoryId $transport -Amount 9000

Write-Step 'Траты'

Add-Spending -Token $user.Token -AccountId $accountId -CategoryId $groceries -Amount 5000
Add-Spending -Token $user.Token -AccountId $accountId -CategoryId $groceries -Amount 3500

$mail = @(Wait-MailCount -Email $user.Email -Category $groceriesName -Expected 1)

if (Assert-True -Condition ($mail.Count -eq 1) -What "85% — пришло одно письмо (получено $($mail.Count))" -PassThru) {
    Assert-True -Condition ($mail[0].Subject -eq "Budget `"$groceriesName`" reached 80% of its limit") `
        -What "тема про 80% (получено '$($mail[0].Subject)')"

    $text = (Invoke-RestMethod -Uri "$MailpitUrl/api/v1/message/$($mail[0].ID)" -UseBasicParsing).Text
    Assert-True -Condition ($text -like '*8,500.00 RUB of the 10,000.00 RUB*(85%)*') `
        -What 'в письме потрачено, лимит и процент'
}

Add-Spending -Token $user.Token -AccountId $accountId -CategoryId $groceries -Amount 500

Write-Step 'Лимит'

Assert-Status -Response (Send-Api -Method PATCH -Path "/budgets/$budgetId/amount" -Token $user.Token `
    -Body @{ amount = 9000 }) -Expected 204 -What 'лимит снижен до потраченного'

$mail = @(Wait-MailCount -Email $user.Email -Category $groceriesName -Expected 2)

Assert-True -Condition ($mail.Count -eq 2) `
    -What "90% письма не дало, снижение лимита дало одно (всего $($mail.Count))"

if ($mail.Count -ge 2) {
    Assert-True -Condition ($mail[1].Subject -eq "Budget `"$groceriesName`" reached its limit") `
        -What "тема про 100% (получено '$($mail[1].Subject)')"
}

Assert-Status -Response (Send-Api -Method PATCH -Path "/budgets/$budgetId/amount" -Token $user.Token `
    -Body @{ amount = 20000 }) -Expected 204 -What 'лимит повышен'

# Ориентира, после которого письмо точно пришло бы, тут нет — ждём с запасом.
Start-Sleep -Seconds 5

$mail = @(Get-Mail -Email $user.Email -Category $groceriesName)
Assert-True -Condition ($mail.Count -eq 2) -What "повышение лимита писем не добавляет (всего $($mail.Count))"

Write-Step 'Без письма: итог'

# К этому моменту воркер доставил письма по тратам, сделанным позже, значит, и эти сообщения уже обработал.
$inactiveMail = @(Get-Mail -Email $user.Email -Category $transportName)
Assert-True -Condition ($inactiveMail.Count -eq 0) -What "90% неактивного бюджета — писем нет (получено $($inactiveMail.Count))"

$silentMail = @(Get-Mail -Email $silent.Email -Category $silentName)
Assert-True -Condition ($silentMail.Count -eq 0) -What "90% при выключенных уведомлениях — писем нет (получено $($silentMail.Count))"

Complete-Suite
