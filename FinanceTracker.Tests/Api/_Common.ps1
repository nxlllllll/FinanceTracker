#Requires -Version 5.1
<#
.SYNOPSIS
    Общая обвязка для API-наборов. Подключается через dot-source:  . "$PSScriptRoot\_Common.ps1"

.DESCRIPTION
    Держит счётчики в области видимости подключающего скрипта, поэтому каждый набор
    считает свои проверки независимо и может быть запущен как сам по себе, так и из
    Run-AllApiTests.ps1.
#>

$script:SupportsSkip = $PSVersionTable.PSVersion.Major -ge 7
$script:Checks       = 0
$script:Failures     = 0
$script:SuiteName    = 'unnamed'

if (-not $script:BaseUrl)   { $script:BaseUrl   = 'http://localhost:8080' }
if (-not $script:ApiPrefix) { $script:ApiPrefix = '/api/v1' }

# ---------------------------------------------------------------- вывод

function Start-Suite {
    param([Parameter(Mandatory)][string] $Name)

    $script:SuiteName = $Name
    $script:Checks    = 0
    $script:Failures  = 0

    Write-Host "`n########## $Name" -ForegroundColor White
}

function Write-Step { param([string]$Text) Write-Host "`n=== $Text" -ForegroundColor Cyan }
function Write-Note { param([string]$Text) Write-Host "    $Text" -ForegroundColor DarkGray }

<#
.SYNOPSIS
    Сводка набора. Возвращает объект, который собирает мажорный скрипт.
#>
function Complete-Suite {
    Write-Host ''

    if ($script:Failures -eq 0) {
        Write-Host "    $($script:SuiteName): все $($script:Checks) проверок прошли." -ForegroundColor Green
    } else {
        Write-Host "    $($script:SuiteName): провалено $($script:Failures) из $($script:Checks)." -ForegroundColor Red
    }

    return [pscustomobject]@{
        Suite    = $script:SuiteName
        Checks   = $script:Checks
        Failures = $script:Failures
        Passed   = ($script:Failures -eq 0)
    }
}

function Send-Api {
    param(
        [Parameter(Mandatory)][string] $Method,
        [Parameter(Mandatory)][string] $Path,
        [hashtable] $Headers = @{},
        $Body,
        [string] $Token
    )

    $requestHeaders = @{}
    foreach ($key in $Headers.Keys) { $requestHeaders[$key] = $Headers[$key] }
    if ($Token) { $requestHeaders['Authorization'] = "Bearer $Token" }

    $parameters = @{
        Method          = $Method
        Uri             = "$script:BaseUrl$script:ApiPrefix$Path"
        Headers         = $requestHeaders
        UseBasicParsing = $true
        ErrorAction     = 'Stop'
    }

    if ($null -ne $Body) {
        $parameters.Body        = ($Body | ConvertTo-Json -Depth 8 -Compress)
        $parameters.ContentType = 'application/json'
    }

    if ($script:SupportsSkip) { $parameters.SkipHttpErrorCheck = $true }

    try {
        $response = Invoke-WebRequest @parameters
        return [pscustomobject]@{
            Status  = [int]$response.StatusCode
            Content = ConvertTo-Text -Raw $response.Content
            Headers = $response.Headers
        }
    }
    catch {
        # PowerShell 5.1 отдаёт неуспешный код исключением.
        $webResponse = $_.Exception.Response
        if ($null -eq $webResponse) { throw }

        $text = ''
        try {
            $reader = New-Object System.IO.StreamReader($webResponse.GetResponseStream())
            $text = $reader.ReadToEnd()
        } catch { }

        return [pscustomobject]@{
            Status  = [int]$webResponse.StatusCode
            Content = $text
            Headers = $webResponse.Headers
        }
    }
}

<#
.SYNOPSIS
    Приводит тело ответа к строке.

.DESCRIPTION
    Invoke-WebRequest в PowerShell 7 иногда отдаёт Content массивом байт — при выводе он
    превращается в вереницу чисел вместо текста, и сообщение об ошибке становится нечитаемым.
#>
function ConvertTo-Text {
    param($Raw)

    if ($null -eq $Raw) { return '' }
    if ($Raw -is [byte[]]) { return [System.Text.Encoding]::UTF8.GetString($Raw) }
    return [string]$Raw
}

function Read-Json {
    param([Parameter(Mandatory)]$Response)

    $text = ConvertTo-Text -Raw $Response.Content
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return $text | ConvertFrom-Json
}

<#
.SYNOPSIS
    Сверяет код ответа. По умолчанию ничего не пишет в конвейер — используй -PassThru,
    когда нужно ветвиться по результату.
#>
function Assert-Status {
    param(
        [Parameter(Mandatory)]$Response,
        [Parameter(Mandatory)][int[]] $Expected,
        [Parameter(Mandatory)][string] $What,
        [switch] $PassThru
    )

    $script:Checks++
    $ok = $Expected -contains $Response.Status

    if ($ok) {
        Write-Host ("    [{0,3}] {1}" -f $Response.Status, $What) -ForegroundColor Green
    }
    else {
        $script:Failures++
        Write-Host ("    [{0,3}] {1} — ожидалось {2}" -f $Response.Status, $What, ($Expected -join '/')) -ForegroundColor Red

        $text = ConvertTo-Text -Raw $Response.Content
        if ($text) { Write-Host "          $text" -ForegroundColor DarkGray }
    }

    if ($PassThru) { return $ok }
}

function Assert-True {
    param(
        [bool] $Condition,
        [Parameter(Mandatory)][string] $What,
        [switch] $PassThru
    )

    $script:Checks++

    if ($Condition) {
        Write-Host "    [ ok] $What" -ForegroundColor Green
    }
    else {
        $script:Failures++
        Write-Host "    [FAIL] $What" -ForegroundColor Red
    }

    if ($PassThru) { return $Condition }
}

function New-Key { [guid]::NewGuid().ToString() }

$script:DefaultPassword = 'Pa55word!ForApiSuite'

<#
.SYNOPSIS
    Возвращает учётку набора, заводя её лишь при первом запуске.

.DESCRIPTION
    Регистрация ограничена двадцатью запросами на адрес за пять минут — той же защитой от
    перебора, что стоит в проде. Набор, регистрирующийся на каждом прогоне, съедает этот
    бюджет за два-три запуска подряд и начинает падать с 429, что выглядит как поломка API,
    хотя система работает ровно как задумано.

    Поэтому учётка заводится один раз, а её адрес запоминается в .api-users.json рядом со
    скриптами. Вход считается отдельным, куда более щедрым лимитом. Если базу почистили и
    учётки больше нет, вход не пройдёт и она будет создана заново.

    -Fresh форсирует новую и ничего не запоминает — для случаев, где нужна заведомо пустая
    учётка, например при проверке изоляции между пользователями.
#>
function New-TestUser {
    param(
        [string] $Label = 'api',
        [string] $BaseCurrency = 'RUB',
        [switch] $Fresh
    )

    $storePath = Join-Path -Path $PSScriptRoot -ChildPath '.api-users.json'
    $store = @{}

    if ((Test-Path -Path $storePath) -and -not $Fresh) {
        try {
            $raw = Get-Content -Path $storePath -Raw | ConvertFrom-Json
            foreach ($property in $raw.PSObject.Properties) { $store[$property.Name] = $property.Value }
        }
        catch {
            $store = @{}
        }
    }

    if (-not $Fresh -and $store.ContainsKey($Label)) {
        $login = Send-Api -Method POST -Path '/auth/login' `
            -Body @{ email = $store[$Label]; password = $script:DefaultPassword }

        if ($login.Status -eq 200) {
            return [pscustomobject]@{
                Email  = $store[$Label]
                UserId = $null
                Token  = (Read-Json -Response $login).accessToken
            }
        }
    }

    $email = "$Label-$([guid]::NewGuid().ToString('N').Substring(0,10))@financetracker.test"

    $register = Send-Api -Method POST -Path '/auth/register' `
        -Headers @{ 'Idempotency-Key' = New-Key } `
        -Body @{ email = $email; password = $script:DefaultPassword; baseCurrency = $BaseCurrency }

    if ($register.Status -eq 429) {
        throw "Регистрация упёрлась в лимит: 20 на адрес за 5 минут. Подожди несколько минут, либо удали '$storePath', если учётки в нём уже не существуют."
    }

    if ($register.Status -ne 201) {
        throw "Не удалось зарегистрировать '$email': $($register.Status) $(ConvertTo-Text -Raw $register.Content)"
    }

    $login = Send-Api -Method POST -Path '/auth/login' -Body @{ email = $email; password = $script:DefaultPassword }
    if ($login.Status -ne 200) {
        throw "Не удалось войти как '$email': $($login.Status) $(ConvertTo-Text -Raw $login.Content)"
    }

    if (-not $Fresh) {
        $store[$Label] = $email
        $store | ConvertTo-Json | Set-Content -Path $storePath -Encoding UTF8
    }

    return [pscustomobject]@{
        Email  = $email
        UserId = (Read-Json -Response $register).id
        Token  = (Read-Json -Response $login).accessToken
    }
}

<#
.SYNOPSIS
    Приводит момент к UTC в формате ISO — так, как его следует класть в строку запроса.

.DESCRIPTION
    ConvertFrom-Json отдаёт дату локальным DateTime, и интерполяция строкой теряет
    смещение. Через эту функцию курсор доезжает тем же моментом, каким пришёл.
#>
function ConvertTo-IsoUtc {
    param([Parameter(Mandatory)]$Instant)

    return ([datetimeoffset]$Instant).ToUniversalTime().ToString('o')
}

<#
.SYNOPSIS
    Тот же момент, но со смещением +03:00 — для проверки, что API принимает не только UTC.
#>
function ConvertTo-IsoOffset {
    param(
        [Parameter(Mandatory)]$Instant,
        [int] $Hours = 3
    )

    return ([datetimeoffset]$Instant).ToOffset([timespan]::FromHours($Hours)).ToString('o')
}

function ConvertTo-Query {
    param([Parameter(Mandatory)][hashtable] $Parameters)

    $pairs = foreach ($key in $Parameters.Keys) {
        if ($null -eq $Parameters[$key]) { continue }
        "$key=$([uri]::EscapeDataString([string]$Parameters[$key]))"
    }

    if (-not $pairs) { return '' }
    return '?' + ($pairs -join '&')
}
