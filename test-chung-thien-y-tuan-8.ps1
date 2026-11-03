# Auth + Recipe tuan 8: chi tao/dung container moi, khong dung volume hoac database nhom/LMS.
param(
    [switch]$Demo,
    [ValidateRange(0, 1800)][int]$DemoDurationSeconds = 0,
    [string]$MinioImage = 'culinaryblog-minio-local:2025-10-15'
)
$ErrorActionPreference = 'Stop'
$suffix = [Guid]::NewGuid().ToString('N')
$database = "culinary_week8_tests_$suffix"
$pg = "culinary-week8-pg-$suffix"
$redis = "culinary-week8-redis-$suffix"
$minio = "culinary-week8-minio-$suffix"
$password = [Guid]::NewGuid().ToString('N')
$created = [System.Collections.Generic.List[string]]::new()
$variables = @('CULINARY_WEEK8_POSTGRES', 'CULINARY_WEEK8_REDIS', 'CULINARY_WEEK8_MINIO', 'CULINARY_WEEK8_DEMO', 'CULINARY_WEEK8_DEMO_DIRECTORY', 'MSBUILDDISABLENODEREUSE')
$previous = @{}
foreach ($key in $variables) { $previous[$key] = [Environment]::GetEnvironmentVariable($key) }
$previousEncoding = $OutputEncoding
$exitCode = 1
$demoProcess = $null
$demoDirectory = $null

function Assert-Native([string]$Message) { if ($LASTEXITCODE -ne 0) { throw $Message } }
function Get-Port([string]$Name, [string]$Port) {
    $raw = docker inspect $Name
    Assert-Native 'Cannot inspect temporary container.'
    $value = (($raw | ConvertFrom-Json)[0].NetworkSettings.Ports.$Port)[0].HostPort
    if (!$value) { throw 'Cannot read temporary container port.' }
    return $value
}

Push-Location -LiteralPath $PSScriptRoot
try {
    docker info --format '{{.ServerVersion}}' | Out-Null
    Assert-Native 'Open Docker Desktop before running this script.'
    # Kiem tra image truoc khi tao bat ky container nao; khong tu pull/cap nhat image.
    foreach ($image in @('postgres:16-alpine', 'redis:7-alpine', $MinioImage)) {
        docker image inspect $image | Out-Null
        Assert-Native "Missing local image: $image"
    }
    docker run -d --rm --pull=never --name $pg -p '127.0.0.1::5432' -e POSTGRES_USER=culinary -e "POSTGRES_PASSWORD=$password" -e "POSTGRES_DB=$database" postgres:16-alpine | Out-Null
    Assert-Native 'Cannot start temporary PostgreSQL.'
    $created.Add($pg)
    $ready = $false
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        docker exec $pg pg_isready -h 127.0.0.1 -U culinary -d $database 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Milliseconds 250
    }
    if (!$ready) { throw 'Temporary PostgreSQL did not become ready.' }
    $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
    foreach ($sql in @('db/init/01-extensions.sql', 'db/init/02-schema.sql', 'db/patches/recipe-instructions-nullable.sql')) {
        Get-Content -LiteralPath $sql -Raw -Encoding utf8 | docker exec -i $pg psql -U culinary -d $database -v ON_ERROR_STOP=1 -q
        Assert-Native "Cannot initialize temporary schema: $sql"
    }
    docker run -d --rm --pull=never --name $redis -p '127.0.0.1::6379' redis:7-alpine | Out-Null
    Assert-Native 'Cannot start temporary Redis.'
    $created.Add($redis)
    $ready = $false
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        $pong = docker exec $redis redis-cli ping
        if ($LASTEXITCODE -eq 0 -and $pong -eq 'PONG') { $ready = $true; break }
        Start-Sleep -Milliseconds 250
    }
    if (!$ready) { throw 'Temporary Redis did not become ready.' }
    docker run -d --rm --pull=never --name $minio -p '127.0.0.1::9000' -e MINIO_ROOT_USER=minioadmin -e MINIO_ROOT_PASSWORD=minioadmin --entrypoint /usr/local/bin/minio $MinioImage server /data | Out-Null
    Assert-Native 'Cannot start temporary MinIO.'
    $created.Add($minio)
    $minioPort = Get-Port $minio '9000/tcp'
    $ready = $false
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        try {
            $probe = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$minioPort/minio/health/ready" -TimeoutSec 1
            if ($probe.StatusCode -eq 200) { $ready = $true; break }
        } catch { }
        Start-Sleep -Milliseconds 250
    }
    if (!$ready) { throw 'Temporary MinIO did not become ready.' }
    $pgPort = Get-Port $pg '5432/tcp'
    $redisPort = Get-Port $redis '6379/tcp'
    $env:CULINARY_WEEK8_POSTGRES = "Host=127.0.0.1;Port=$pgPort;Database=$database;Username=culinary;Password=$password"
    $env:CULINARY_WEEK8_REDIS = "127.0.0.1:$redisPort,abortConnect=false"
    $env:CULINARY_WEEK8_MINIO = "127.0.0.1:$minioPort"
    $env:CULINARY_WEEK8_DEMO = if ($Demo) { '1' } else { '0' }
    $env:MSBUILDDISABLENODEREUSE = '1'
    $project = Join-Path $PSScriptRoot 'tests/CulinaryBlog.UnitTests/CulinaryBlog.UnitTests.csproj'
    $results = Join-Path $PSScriptRoot 'TestResults/week8'
    if (!$Demo) {
        dotnet test $project --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false `
            --filter 'FullyQualifiedName~CulinaryBlog.UnitTests.Integration&FullyQualifiedName!~Week8DemoTests' `
            --results-directory $results --logger 'trx;LogFileName=auth-recipe-week8.trx' --verbosity minimal
        $exitCode = $LASTEXITCODE
    }
    else {
        dotnet build $project --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false --verbosity quiet
        Assert-Native 'Cannot build demo host.'
        $demoDirectory = Join-Path ([IO.Path]::GetTempPath()) "culinary-week8-demo-$suffix"
        New-Item -ItemType Directory -Path $demoDirectory | Out-Null
        $env:CULINARY_WEEK8_DEMO_DIRECTORY = $demoDirectory
        $demoProcess = Start-Process -FilePath (Get-Command dotnet).Source -WindowStyle Hidden -PassThru `
            -ArgumentList @('test', ('"{0}"' -f $project), '--no-build', '--no-restore', '--filter',
                'FullyQualifiedName~CulinaryBlog.UnitTests.Integration.Week8DemoTests', '--verbosity', 'minimal') `
            -RedirectStandardOutput (Join-Path $demoDirectory 'host.log') -RedirectStandardError (Join-Path $demoDirectory 'host-error.log')
        $manifest = Join-Path $demoDirectory 'demo.json'
        $ready = $false
        for ($attempt = 0; $attempt -lt 160; $attempt++) {
            if (Test-Path -LiteralPath $manifest) { $ready = $true; break }
            if ($demoProcess.HasExited) { break }
            Start-Sleep -Milliseconds 250
        }
        if (!$ready) { throw "Demo host did not become ready. Logs: $demoDirectory" }
        $info = Get-Content -LiteralPath $manifest -Raw -Encoding utf8 | ConvertFrom-Json
        Write-Output "Demo manifest: $manifest"
        Write-Output "API: $($info.apiBaseUrl)"
        Write-Output "Owner: $($info.ownerEmail)"
        Write-Output "Other Author: $($info.otherEmail)"
        Write-Output "Temporary demo password: $($info.password)"
        Write-Output "Draft slug: $($info.draftSlug)"
        Write-Output 'Frontend: follow spec/kich_ban_demo_ChungThienY_tuan_8.md in a second terminal.'
        Write-Output 'This isolated demo expires after 30 minutes; all temporary data is removed on stop.'
        if ($DemoDurationSeconds -gt 0) {
            for ($second = 0; $second -lt $DemoDurationSeconds -and !$demoProcess.HasExited; $second++) {
                Start-Sleep -Seconds 1
            }
        }
        else { Read-Host 'Press Enter to stop the demo and clean up its containers' | Out-Null }
        New-Item -ItemType File -Force -Path (Join-Path $demoDirectory 'stop') | Out-Null
        if (!$demoProcess.WaitForExit(15000)) { throw 'Demo host did not stop within 15 seconds.' }
        $demoProcess.Refresh()
        $exitCode = $demoProcess.ExitCode
    }
}
finally {
    if ($demoDirectory) {
        if (!(Test-Path -LiteralPath (Join-Path $demoDirectory 'stop'))) {
            New-Item -ItemType File -Path (Join-Path $demoDirectory 'stop') -ErrorAction SilentlyContinue | Out-Null
        }
    }
    if ($demoProcess -and !$demoProcess.HasExited) {
        if (!$demoProcess.WaitForExit(10000)) {
            # Chi PID/cay tien trinh do Start-Process trong lan demo nay tao ra.
            taskkill /PID $demoProcess.Id /T /F | Out-Null
        }
    }
    foreach ($key in $variables) { [Environment]::SetEnvironmentVariable($key, $previous[$key]) }
    $OutputEncoding = $previousEncoding
    foreach ($name in $created) {
        # Chi cac ten vua duoc docker run thanh cong trong lan chay nay.
        docker stop $name | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Warning "Check temporary container cleanup: $name" }
    }
    Pop-Location
}
exit $exitCode
