# Kiem thu HTTP Recipe tuan 3-5 tren PostgreSQL 16 + Redis that.
# Tao hai container TAM voi cong ngau nhien; tu don khi test ket thuc.
# Can Docker Desktop va image postgres:16-alpine, redis:7-alpine da co tren may.

param([string]$TestFilter = 'FullyQualifiedName~CulinaryBlog.UnitTests.Recipes.RecipeApiTests')

$ErrorActionPreference = 'Stop'
$testSuffix = [Guid]::NewGuid().ToString('N')
$testDatabase = "culinary_recipe_tests_$testSuffix"
$testRedis = "culinary-recipe-tests-$testSuffix"
$testPostgres = "culinary-recipe-tests-pg-$testSuffix"
$testPostgresPassword = [Guid]::NewGuid().ToString('N')
$postgresCreated = $false
$redisCreated = $false
$previousPostgres = $env:CULINARY_RECIPE_TEST_POSTGRES
$previousRedis = $env:CULINARY_RECIPE_TEST_REDIS
$previousNodeReuse = $env:MSBUILDDISABLENODEREUSE
$previousOutputEncoding = $OutputEncoding
$testExitCode = 1

Push-Location -LiteralPath $PSScriptRoot
try {
    docker run --detach --rm --pull=never --name $testPostgres --publish '127.0.0.1::5432' `
        --env 'POSTGRES_USER=culinary' --env "POSTGRES_PASSWORD=$testPostgresPassword" `
        --env 'POSTGRES_DB=postgres' postgres:16-alpine
    if ($LASTEXITCODE -ne 0) { throw 'Can image postgres:16-alpine san tren may de chay test.' }
    $postgresCreated = $true
    $postgresReady = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        docker exec $testPostgres pg_isready -h 127.0.0.1 -U culinary -d postgres 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { $postgresReady = $true; break }
        Start-Sleep -Milliseconds 250
    }
    if (!$postgresReady) { throw 'PostgreSQL tam chua san sang.' }
    $postgresInfo = docker inspect $testPostgres
    if ($LASTEXITCODE -ne 0) { throw 'Khong inspect duoc PostgreSQL tam.' }
    $postgresPort = (($postgresInfo | ConvertFrom-Json)[0].NetworkSettings.Ports.'5432/tcp')[0].HostPort
    if (!$postgresPort) { throw 'Khong doc duoc cong PostgreSQL tam.' }
    docker exec $testPostgres createdb -U culinary $testDatabase
    if ($LASTEXITCODE -ne 0) { throw 'Khong tao duoc database tam. Kiem tra Docker/PostgreSQL.' }

    $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
    foreach ($schemaFile in @(
        'db/init/01-extensions.sql', 'db/init/02-schema.sql',
        'db/patches/recipe-instructions-nullable.sql', 'db/patches/recipe-instructions-nullable.sql'
    )) {
        Get-Content -LiteralPath $schemaFile -Raw -Encoding utf8 |
            docker exec -i $testPostgres psql -U culinary -d $testDatabase -v ON_ERROR_STOP=1 -q
        if ($LASTEXITCODE -ne 0) { throw "Loi khoi tao schema: $schemaFile" }
    }

    docker run --detach --rm --pull=never --name $testRedis --publish '127.0.0.1::6379' redis:7-alpine
    if ($LASTEXITCODE -ne 0) { throw 'Can image redis:7-alpine san tren may de chay test.' }
    $redisCreated = $true
    $redisInfo = docker inspect $testRedis
    if ($LASTEXITCODE -ne 0) { throw 'Khong inspect duoc Redis tam.' }
    $redisPort = (($redisInfo | ConvertFrom-Json)[0].NetworkSettings.Ports.'6379/tcp')[0].HostPort
    if (!$redisPort) { throw 'Khong doc duoc cong Redis tam.' }
    $redisAddress = "127.0.0.1:$redisPort"

    $env:CULINARY_RECIPE_TEST_POSTGRES = "Host=127.0.0.1;Port=$postgresPort;Database=$testDatabase;Username=culinary;Password=$testPostgresPassword"
    $env:CULINARY_RECIPE_TEST_REDIS = "$redisAddress,abortConnect=false"
    $env:MSBUILDDISABLENODEREUSE = '1'
    Write-Output "Kiem thu tren database tam: $testDatabase"
    dotnet test tests/CulinaryBlog.UnitTests/CulinaryBlog.UnitTests.csproj --no-restore `
        --disable-build-servers -m:1 -p:UseSharedCompilation=false `
        --filter $TestFilter --verbosity minimal
    $testExitCode = $LASTEXITCODE
}
finally {
    $env:CULINARY_RECIPE_TEST_POSTGRES = $previousPostgres
    $env:CULINARY_RECIPE_TEST_REDIS = $previousRedis
    $env:MSBUILDDISABLENODEREUSE = $previousNodeReuse
    $OutputEncoding = $previousOutputEncoding
    if ($redisCreated) {
        docker stop $testRedis
        if ($LASTEXITCODE -ne 0) { Write-Warning "Can don Redis tam: $testRedis" }
    }
    if ($postgresCreated) {
        docker stop $testPostgres
        if ($LASTEXITCODE -ne 0) { Write-Warning "Can don PostgreSQL tam: $testPostgres" }
    }
    Pop-Location
}
exit $testExitCode
