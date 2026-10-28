# Chi tao/dung cac container TAM co ten duy nhat; khong dung DB nhom hoac LMS.
param(
    [string]$DotnetExecutable = 'dotnet',
    [string]$MinioImage = 'culinaryblog-minio-local:2025-10-15',
    [string]$MailhogImage = 'mailhog/mailhog:v1.0.1',
    [switch]$UseDockerSdk
)
$ErrorActionPreference = 'Stop'
$suffix = [Guid]::NewGuid().ToString('N')
$pg = "culinary-week7-pg-$suffix"
$redis = "culinary-week7-redis-$suffix"
$mail = "culinary-week7-mail-$suffix"
$minio = "culinary-week7-minio-$suffix"
$database = "culinary_week7_tests_$suffix"
$password = [Guid]::NewGuid().ToString('N')
$created = [System.Collections.Generic.List[string]]::new()
$variables = @('CULINARY_WEEK7_POSTGRES','CULINARY_WEEK7_REDIS','CULINARY_WEEK7_SMTP_PORT','CULINARY_WEEK7_MAILHOG','CULINARY_WEEK7_MINIO','CULINARY_WEEK7_MINIO_DOWN')
$previous = @{}
foreach ($key in $variables) { $previous[$key] = [Environment]::GetEnvironmentVariable($key) }
$previousEncoding = $OutputEncoding
$exitCode = 1

function Assert-Docker { if ($LASTEXITCODE -ne 0) { throw 'Docker command failed; check Docker Desktop and required images.' } }
function Get-Port([string]$Name, [string]$Port) {
    $raw = docker inspect $Name
    Assert-Docker
    $info = ($raw | ConvertFrom-Json)[0]
    return $info.NetworkSettings.Ports.$Port[0].HostPort
}

function Invoke-Week7Tests([string[]]$TestArguments) {
    if (!$UseDockerSdk) { & $DotnetExecutable @TestArguments; return }
    $sdkImage='culinary-week7-sdk:local'
    docker image inspect $sdkImage 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        $sdkContext=Join-Path $env:TEMP 'culinary-week7-sdk-image'
        New-Item -ItemType Directory -Force -Path $sdkContext | Out-Null
        Set-Content -LiteralPath (Join-Path $sdkContext 'Dockerfile') -Encoding ascii -Value 'FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:35d40304542c8689331f8cab17c65926cdf48fe711e289321d71924b230a7d29'
        docker build --pull=false --tag $sdkImage $sdkContext
        Assert-Docker
    }
    $artifacts=Join-Path $env:TEMP 'culinary-week7-artifacts'
    $packages=Join-Path $env:TEMP 'culinary-week7-packages'
    $nuget=Join-Path $env:USERPROFILE '.nuget\packages'
    New-Item -ItemType Directory -Force -Path $artifacts,$packages,$nuget | Out-Null
    $argsDocker=@('run','--rm','--mount',"type=bind,source=$PSScriptRoot,target=/work",'--mount',"type=bind,source=$nuget,target=/nuget-cache,readonly",'--mount',"type=bind,source=$artifacts,target=/artifacts",'--mount',"type=bind,source=$packages,target=/packages",'-e','NUGET_PACKAGES=/packages','-e','DOTNET_CLI_TELEMETRY_OPTOUT=1','-w','/work')
    $settings=@{
        CULINARY_WEEK7_POSTGRES="Host=$pgIp;Port=5432;Database=$database;Username=culinary;Password=$password"
        CULINARY_WEEK7_REDIS="$($redisIp):6379,abortConnect=false"
        CULINARY_WEEK7_SMTP_HOST=$mailIp
        CULINARY_WEEK7_SMTP_PORT='1025'
        CULINARY_WEEK7_MAILHOG="http://$($mailIp):8025"
        CULINARY_WEEK7_MINIO="$($minioIp):9000"
        CULINARY_WEEK7_MINIO_DOWN=$env:CULINARY_WEEK7_MINIO_DOWN
    }
    foreach($key in $settings.Keys) { $argsDocker+=@('-e',("{0}={1}" -f $key,$settings[$key])) }
    $argsDocker+=@($sdkImage,'dotnet')
    $argsDocker+=$TestArguments
    $argsDocker+=@('-p:RestoreFallbackFolders=/nuget-cache','-p:UseArtifactsOutput=true','-p:ArtifactsPath=/artifacts','-p:NuGetAudit=false')
    & docker @argsDocker
}

Push-Location -LiteralPath $PSScriptRoot
try {
    docker run -d --rm --pull=never --name $pg -p '127.0.0.1::5432' -e POSTGRES_USER=culinary -e "POSTGRES_PASSWORD=$password" -e "POSTGRES_DB=$database" postgres:16-alpine
    Assert-Docker; $created.Add($pg)
    for ($i=0; $i -lt 60; $i++) {
        docker exec $pg pg_isready -h 127.0.0.1 -U culinary -d $database 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Milliseconds 250
    }
    if ($LASTEXITCODE -ne 0) { throw 'Temporary PostgreSQL did not become ready.' }
    $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
    foreach ($sql in @('db/init/01-extensions.sql','db/init/02-schema.sql')) {
        Get-Content -LiteralPath $sql -Raw -Encoding utf8 | docker exec -i $pg psql -U culinary -d $database -v ON_ERROR_STOP=1 -q
        Assert-Docker
    }
    docker run -d --rm --pull=never --name $redis -p '127.0.0.1::6379' redis:7-alpine
    Assert-Docker; $created.Add($redis)
    docker run -d --rm --pull=never --name $mail -p '127.0.0.1::1025' -p '127.0.0.1::8025' $MailhogImage
    Assert-Docker; $created.Add($mail)
    docker run -d --rm --pull=never --name $minio -p '127.0.0.1::9000' -e MINIO_ROOT_USER=minioadmin -e MINIO_ROOT_PASSWORD=minioadmin --entrypoint /usr/local/bin/minio $MinioImage server /data
    Assert-Docker; $created.Add($minio)
    for ($i=0; $i -lt 60; $i++) {
        docker exec $minio curl --fail --silent http://127.0.0.1:9000/minio/health/ready | Out-Null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Milliseconds 250
    }
    docker exec $minio /usr/local/bin/minio-local-init
    Assert-Docker
    $pgPort=Get-Port $pg '5432/tcp'
    $redisPort=Get-Port $redis '6379/tcp'
    $smtpPort=Get-Port $mail '1025/tcp'
    $mailPort=Get-Port $mail '8025/tcp'
    $minioPort=Get-Port $minio '9000/tcp'
    $pgIp=(docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' $pg)
    Assert-Docker
    $redisIp=(docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' $redis)
    Assert-Docker
    $mailIp=(docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' $mail)
    Assert-Docker
    $minioIp=(docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' $minio)
    Assert-Docker
    $env:CULINARY_WEEK7_POSTGRES="Host=127.0.0.1;Port=$pgPort;Database=$database;Username=culinary;Password=$password"
    $env:CULINARY_WEEK7_REDIS="127.0.0.1:$redisPort,abortConnect=false"
    $env:CULINARY_WEEK7_SMTP_PORT=$smtpPort
    $env:CULINARY_WEEK7_MAILHOG="http://127.0.0.1:$mailPort"
    $env:CULINARY_WEEK7_MINIO="127.0.0.1:$minioPort"
    $env:CULINARY_WEEK7_MINIO_DOWN='0'
    Write-Output 'Phase 1: registration, Hangfire/PostgreSQL, SMTP/MailHog, healthy MinIO.'
    Invoke-Week7Tests @('test','tests/CulinaryBlog.UnitTests/CulinaryBlog.UnitTests.csproj','--disable-build-servers','-m:1','-p:UseSharedCompilation=false','--filter','FullyQualifiedName~Week7IntegrationTests','--verbosity','minimal')
    if ($LASTEXITCODE -ne 0) { throw 'Week 7 integration phase 1 failed.' }

    # Chi dung MinIO TAM vua tao; khong dung culinaryblog-minio cua nhom.
    docker stop $minio | Out-Null
    Assert-Docker
    $env:CULINARY_WEEK7_MINIO_DOWN='1'
    Write-Output 'Phase 2: temporary MinIO stopped; readiness=503, liveness=200.'
    Invoke-Week7Tests @('test','tests/CulinaryBlog.UnitTests/CulinaryBlog.UnitTests.csproj','--no-build','--no-restore','--filter','FullyQualifiedName~Week7IntegrationTests','--verbosity','minimal')
    $exitCode=$LASTEXITCODE
}
finally {
    foreach ($key in $variables) { [Environment]::SetEnvironmentVariable($key,$previous[$key]) }
    $OutputEncoding=$previousEncoding
    foreach ($name in $created) {
        # Name comes only from this script's successful docker run calls.
        $existing=docker ps -a --filter "name=^/$name$" --format '{{.Names}}'
        if ($existing -eq $name) { docker stop $name | Out-Null }
    }
    Pop-Location
}
exit $exitCode
