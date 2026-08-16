#Requires -Version 5.1
<#
.SYNOPSIS
    Группа Currencies: справочник валют.

.DESCRIPTION
    Reference data, одинаковая для всех. Проверяется, что чтение доступно под currency:read,
    что код валюты принимается в любом регистре, и что некорректный код отличается от
    несуществующего — 400 против 404.

.EXAMPLE
    ./Test-Currencies.ps1
    ./Test-Currencies.ps1 -BaseUrl http://localhost:8080
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

Start-Suite -Name 'Currencies'

$user = New-TestUser -Label 'cur'
Write-Note "учётка: $($user.Email)"

Write-Step 'Список'

$list = Send-Api -Method GET -Path '/currencies' -Token $user.Token
if (Assert-Status -Response $list -Expected 200 -What 'GET /currencies' -PassThru) {
    $currencies = Read-Json -Response $list

    Assert-True -Condition ($currencies.Count -gt 0) -What 'справочник не пуст'
    Assert-True -Condition ($null -ne ($currencies | Where-Object { $_.code -eq 'RUB' })) -What 'RUB присутствует'

    $sample = $currencies | Select-Object -First 1
    Assert-True -Condition ($sample.PSObject.Properties.Name -contains 'isActive') `
        -What 'элемент несёт isActive — клиент должен уметь отличить выведенную из обращения валюту'
    Assert-True -Condition ($sample.PSObject.Properties.Name -contains 'symbol') -What 'элемент несёт symbol'
}

Write-Step 'Поиск по коду'

Assert-Status -Response (Send-Api -Method GET -Path '/currencies/RUB' -Token $user.Token) `
    -Expected 200 -What 'GET /currencies/RUB'

Assert-Status -Response (Send-Api -Method GET -Path '/currencies/rub' -Token $user.Token) `
    -Expected 200 -What 'нижний регистр принимается — клиент не обязан помнить про ISO-написание'

Assert-Status -Response (Send-Api -Method GET -Path '/currencies/RuB' -Token $user.Token) `
    -Expected 200 -What 'смешанный регистр принимается'

$found = Read-Json -Response (Send-Api -Method GET -Path '/currencies/rub' -Token $user.Token)
Assert-True -Condition ($found.code -eq 'RUB') -What 'ответ отдаёт код в каноническом виде независимо от запроса'

Write-Step 'Отказы'

Assert-Status -Response (Send-Api -Method GET -Path '/currencies/XXX' -Token $user.Token) `
    -Expected 404 -What 'формат верный, валюты нет — 404'

Assert-Status -Response (Send-Api -Method GET -Path '/currencies/XX' -Token $user.Token) `
    -Expected 400 -What 'код не соответствует ISO — 400, а не 404'

Assert-Status -Response (Send-Api -Method GET -Path '/currencies/TOOLONG' -Token $user.Token) `
    -Expected 400 -What 'слишком длинный код — 400'

$notFound = Read-Json -Response (Send-Api -Method GET -Path '/currencies/XXX' -Token $user.Token)
Assert-True -Condition ($notFound.code -eq 'currency.not_found') `
    -What "404 несёт код currency.not_found (получен '$($notFound.code)')"

Write-Step 'Доступ'

Assert-Status -Response (Send-Api -Method GET -Path '/currencies') `
    -Expected 401 -What 'без токена — 401'

Complete-Suite
