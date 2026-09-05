#Requires -Version 5.1
<#
.SYNOPSIS
    Группа Categories: справочник трат пользователя, помесячные тоталы и архивация.

.DESCRIPTION
    Основное внимание — курсорной пагинации и фильтрам: именно там расходились формат
    ответа и формат запроса. Плюс идемпотентность создания и изоляция между учётками.

.EXAMPLE
    ./Test-Categories.ps1
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

Start-Suite -Name 'Categories'

$user = New-TestUser -Label 'cat'
Write-Note "учётка: $($user.Email)"

Write-Step 'Создание'

$key = New-Key
$created = Send-Api -Method POST -Path '/categories' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = $key } `
    -Body @{ name = 'Продукты'; type = 'expense'; parentId = $null }

if (-not (Assert-Status -Response $created -Expected 201 -What 'POST /categories' -PassThru)) {
    Complete-Suite
    return
}

$categoryId = (Read-Json -Response $created).id
Assert-True -Condition ($null -ne $created.Headers['Location']) -What 'ответ несёт Location'

$repeat = Send-Api -Method POST -Path '/categories' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = $key } `
    -Body @{ name = 'Продукты'; type = 'expense'; parentId = $null }

Assert-Status -Response $repeat -Expected 201 -What 'повтор с тем же Idempotency-Key'
Assert-True -Condition ((Read-Json -Response $repeat).id -eq $categoryId) `
    -What 'повтор вернул исходный идентификатор, а не создал вторую категорию'

Assert-Status -Response (Send-Api -Method POST -Path '/categories' -Token $user.Token `
    -Body @{ name = 'Без ключа'; type = 'expense' }) `
    -Expected 400 -What 'создание без Idempotency-Key'

Assert-Status -Response (Send-Api -Method POST -Path '/categories' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } -Body @{ name = ''; type = 'expense' }) `
    -Expected 400 -What 'пустое имя'

Assert-Status -Response (Send-Api -Method POST -Path '/categories' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ name = 'Овощи'; type = 'expense'; parentId = $categoryId }) `
    -Expected 201 -What 'вложенная категория'

Assert-Status -Response (Send-Api -Method POST -Path '/categories' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ name = 'Зарплата'; type = 'income' }) `
    -Expected 201 -What 'категория дохода'

Write-Step 'Чтение'

Assert-Status -Response (Send-Api -Method GET -Path "/categories/$categoryId" -Token $user.Token) `
    -Expected 200 -What 'GET /categories/{id}'

Assert-Status -Response (Send-Api -Method GET -Path "/categories/$([guid]::NewGuid())" -Token $user.Token) `
    -Expected 404 -What 'несуществующая категория'

Write-Step 'Пагинация'

$page = Send-Api -Method GET -Path '/categories?pageSize=1' -Token $user.Token
if (Assert-Status -Response $page -Expected 200 -What 'первая страница' -PassThru) {
    $first = Read-Json -Response $page

    Assert-True -Condition ($first.items.Count -eq 1) -What 'страница содержит ровно один элемент'
    Assert-True -Condition ($first.hasNextPage -eq $true) -What 'страница сообщает о продолжении'
    Assert-True -Condition ($null -ne $first.nextCursorId) -What 'курсор возвращён'

    $cursor = @{
        pageSize        = 1
        cursorCreatedAt = ConvertTo-IsoUtc -Instant $first.nextCursorDate
        cursorId        = $first.nextCursorId
    }

    $next = Send-Api -Method GET -Path ('/categories' + (ConvertTo-Query -Parameters $cursor)) -Token $user.Token
    if (Assert-Status -Response $next -Expected 200 -What 'вторая страница по курсору' -PassThru) {
        $second = Read-Json -Response $next
        Assert-True -Condition ($second.items[0].id -ne $first.items[0].id) -What 'вторая страница отдаёт другой элемент'

        $cursor.cursorCreatedAt = ConvertTo-IsoOffset -Instant $first.nextCursorDate
        $offset = Send-Api -Method GET -Path ('/categories' + (ConvertTo-Query -Parameters $cursor)) -Token $user.Token

        if (Assert-Status -Response $offset -Expected 200 -What 'курсор со смещением +03:00' -PassThru) {
            Assert-True -Condition ((Read-Json -Response $offset).items[0].id -eq $second.items[0].id) `
                -What 'смещение не меняет момент — та же страница, что и по UTC'
        }
    }
}

Assert-Status -Response (Send-Api -Method GET -Path '/categories?pageSize=101' -Token $user.Token) `
    -Expected 400 -What 'pageSize сверх потолка'

Assert-Status -Response (Send-Api -Method GET -Path '/categories?pageSize=0' -Token $user.Token) `
    -Expected 400 -What 'pageSize ниже единицы'

Assert-Status -Response (Send-Api -Method GET -Path "/categories?cursorId=$([guid]::NewGuid())" -Token $user.Token) `
    -Expected 400 -What 'половина курсора без второй'

Write-Step 'Фильтры'

$byType = Send-Api -Method GET -Path '/categories?type=expense' -Token $user.Token
if (Assert-Status -Response $byType -Expected 200 -What 'type в нижнем регистре — тот же вид, в каком приходит в ответах' -PassThru) {
    $items = (Read-Json -Response $byType).items
    Assert-True -Condition (@($items | Where-Object { $_.type -ne 'expense' }).Count -eq 0) `
        -What 'фильтр по типу действительно отсекает'
}

Assert-Status -Response (Send-Api -Method GET -Path '/categories?type=Expense' -Token $user.Token) `
    -Expected 200 -What 'type в верхнем регистре'

$badType = Send-Api -Method GET -Path '/categories?type=nonsense' -Token $user.Token
if (Assert-Status -Response $badType -Expected 400 -What 'недопустимое значение type' -PassThru) {
    Assert-True -Condition ($null -ne (Read-Json -Response $badType).errors.type) `
        -What 'ошибка называет параметр, а не приходит пустым телом'
}

Assert-Status -Response (Send-Api -Method GET -Path '/categories?isArchived=false&type=expense' -Token $user.Token) `
    -Expected 200 -What 'несколько фильтров вместе'

Assert-Status -Response (Send-Api -Method GET -Path "/categories?parentId=$categoryId" -Token $user.Token) `
    -Expected 200 -What 'фильтр по родителю'

Write-Step 'Тоталы'

$period = (Get-Date).ToString('yyyy-MM-01')

$total = Send-Api -Method GET -Path "/categories/$categoryId/totals/$period" -Token $user.Token
if (Assert-Status -Response $total -Expected 200 -What 'тотал одной категории' -PassThru) {
    Assert-True -Condition ((Read-Json -Response $total).PSObject.Properties.Name -contains 'recalculationPending') `
        -What 'ответ несёт recalculationPending'
}

Assert-Status -Response (Send-Api -Method GET -Path "/categories/totals/$period" -Token $user.Token) `
    -Expected 200 -What 'тоталы всех категорий — маршрут не съеден шаблоном {categoryId}'

Assert-Status -Response (Send-Api -Method GET -Path "/categories/totals/$((Get-Date).ToString('yyyy-MM-17'))" -Token $user.Token) `
    -Expected 200 -What 'любая дата месяца принимается'

Write-Step 'Переименование и архивация'

Assert-Status -Response (Send-Api -Method PATCH -Path "/categories/$categoryId/name" -Token $user.Token -Body @{ name = 'Еда' }) `
    -Expected 204 -What 'переименование'

Assert-Status -Response (Send-Api -Method PATCH -Path "/categories/$categoryId/name" -Token $user.Token -Body @{ name = '' }) `
    -Expected 400 -What 'переименование в пустую строку'

Assert-Status -Response (Send-Api -Method POST -Path "/categories/$categoryId/archive" -Token $user.Token) `
    -Expected 204 -What 'архивация'

Assert-Status -Response (Send-Api -Method POST -Path "/categories/$categoryId/archive" -Token $user.Token) `
    -Expected 204 -What 'повторная архивация идемпотентна'

$renameArchived = Send-Api -Method PATCH -Path "/categories/$categoryId/name" -Token $user.Token -Body @{ name = 'Питание' }
if (Assert-Status -Response $renameArchived -Expected 422 -What 'переименование архивной отклонено' -PassThru) {
    $code = (Read-Json -Response $renameArchived).code
    Assert-True -Condition ([string]::IsNullOrWhiteSpace($code) -eq $false) `
        -What "422 несёт код ошибки (получен '$code')"
}

Assert-Status -Response (Send-Api -Method POST -Path "/categories/$categoryId/unarchive" -Token $user.Token) `
    -Expected 204 -What 'разархивация'

Assert-Status -Response (Send-Api -Method PATCH -Path "/categories/$categoryId/name" -Token $user.Token -Body @{ name = 'Питание' }) `
    -Expected 204 -What 'после разархивации переименование снова работает'

Write-Step 'Иерархия'

function New-Category {
    param([string] $Name, [string] $Type = 'expense', $ParentId = $null, [string] $Token = $user.Token)

    $response = Send-Api -Method POST -Path '/categories' -Token $Token `
        -Headers @{ 'Idempotency-Key' = New-Key } `
        -Body @{ name = $Name; type = $Type; parentId = $ParentId }

    if ($response.Status -ne 201) { throw "Не удалось создать категорию '$Name': $($response.Status) $($response.Content)" }
    return (Read-Json -Response $response).id
}

function Move-Category {
    param([string] $CategoryId, $ParentId, [string] $Token = $user.Token)

    return Send-Api -Method PATCH -Path "/categories/$CategoryId/parent" -Token $Token -Body @{ parentId = $ParentId }
}

$level1 = New-Category -Name 'Продукты'
$level2 = New-Category -Name 'Еда для кота' -ParentId $level1
$level3 = New-Category -Name 'Корм'         -ParentId $level2
$level4 = New-Category -Name 'Шеав'         -ParentId $level3

Write-Note "цепочка из четырёх уровней построена"

Assert-Status -Response (Send-Api -Method POST -Path '/categories' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ name = 'Пятый уровень'; type = 'expense'; parentId = $level4 }) `
    -Expected 422 -What 'пятый уровень при создании отклонён'

Assert-Status -Response (Send-Api -Method POST -Path '/categories' -Token $user.Token `
    -Headers @{ 'Idempotency-Key' = New-Key } `
    -Body @{ name = 'Доход внутри расхода'; type = 'income'; parentId = $level1 }) `
    -Expected 422 -What 'создание с типом, отличным от родителя, отклонено'

$standalone = New-Category -Name 'Транспорт'

Assert-Status -Response (Move-Category -CategoryId $standalone -ParentId $level1) `
    -Expected 204 -What 'перенос под другую категорию'

$moved = Read-Json -Response (Send-Api -Method GET -Path "/categories/$standalone" -Token $user.Token)
Assert-True -Condition ($moved.parentId -eq $level1) -What 'родитель обновился'

Assert-Status -Response (Move-Category -CategoryId $level1 -ParentId $level1) `
    -Expected 422 -What 'категория сама себе родитель — 422'

Assert-Status -Response (Move-Category -CategoryId $level1 -ParentId $level3) `
    -Expected 422 -What 'перенос под собственного потомка — 422'

Assert-Status -Response (Move-Category -CategoryId $level2 -ParentId $standalone) `
    -Expected 422 -What 'перенос ветки, которая не влезает по глубине — 422'

$income = New-Category -Name 'Зарплата' -Type 'income'

Assert-Status -Response (Move-Category -CategoryId $income -ParentId $level1) `
    -Expected 422 -What 'перенос под родителя другого типа — 422'

Assert-Status -Response (Move-Category -CategoryId $standalone -ParentId $null) `
    -Expected 204 -What 'перенос в корень'

$rooted = Read-Json -Response (Send-Api -Method GET -Path "/categories/$standalone" -Token $user.Token)
Assert-True -Condition ($null -eq $rooted.parentId) -What 'категория стала корневой'

Assert-Status -Response (Move-Category -CategoryId $standalone -ParentId ([guid]::NewGuid())) `
    -Expected 404 -What 'несуществующий родитель — 404'

Write-Step 'Изоляция между учётками'

$other = New-TestUser -Label 'cat-other'

Assert-Status -Response (Send-Api -Method GET -Path "/categories/$categoryId" -Token $other.Token) `
    -Expected 404 -What 'чужая категория не читается'

Assert-Status -Response (Send-Api -Method POST -Path "/categories/$categoryId/archive" -Token $other.Token) `
    -Expected 404 -What 'чужая категория не архивируется'

Assert-Status -Response (Move-Category -CategoryId $categoryId -ParentId $null -Token $other.Token) `
    -Expected 404 -What 'чужая категория не переносится'

Assert-Status -Response (Send-Api -Method GET -Path "/categories/$categoryId") `
    -Expected 401 -What 'без токена — 401'

Complete-Suite
