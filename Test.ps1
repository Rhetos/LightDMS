# This script expects either Docker Desktop installed on Windows (https://www.docker.com/products/docker-desktop/),
# or a free Docker Engine installed on WSL in the Windows (https://docs.docker.com/engine/install/).

$ErrorActionPreference = 'Stop'

# Prequisites

# Testing if Build.bat has completed successfully.
if (-Not (Test-Path 'test\Rhetos.LightDMS.TestApp\bin\Debug\net8.0\Rhetos.LightDMS.TestApp.dll' -PathType Leaf)) {
    throw "Please execute Build.bat successfully before running Test.ps1. Build output file 'Rhetos.LightDMS.TestApp.dll' does not exist."
}

# Testing if the test settings have been configured.
if (-Not (Test-Path '.\test-config.json' -PathType Leaf)) {
    throw "Please create and configure test settings in 'test-config.json'. See Readme.md for the instructions."
}

# Testing if SQL commands are available.
# Note that on some environments, the commands are available with SQL Server client tools, even without installing PowerShell Module 'SqlServer'.
If ((Get-Command -CommandType Cmdlet -Name 'Invoke-Sqlcmd*').Count -eq 0) {
    throw "Please install PowerShell module 'SqlServer'."
}

# Parameters
$config = Get-Content .\test-config.json -Raw | ConvertFrom-Json

$sqlServerName = $config.SqlServerName
$sqlCredential = $config.SqlServerCredential
$fsDbName = $config.FileStreamDatabaseName
$fsLocation = $config.FileStreamFileLocation
$varbinDbName = $config.VarBinaryDatabaseName

# Computed parameters
$masterConnString = "Server=$($sqlServerName);Database=master;$($sqlCredential);"
$fsDbConnString = "Server=$($sqlServerName);Database=$($fsDbName);$($sqlCredential);"
$varbinDbConnString = "Server=$($sqlServerName);Database=$($varbinDbName);$($sqlCredential);"

Write-Output "Setting up database without FILESTREAM enabled (use varbinary to store file content)... "
$cmd = "IF DB_ID('$($varbinDbName)') IS NULL CREATE DATABASE $($varbinDbName)"
Invoke-Sqlcmd -ConnectionString $masterConnString -Query $cmd
Write-Output "OK!"

# BEGIN Create FS DB
Write-Output "Setting up database with FILESTREAM enabled... "

$cmd = "SELECT DB_ID('$($fsDbName)')"
$r = Invoke-Sqlcmd -ConnectionString $masterConnString -Query $cmd

if ([string]::IsNullOrEmpty($r[0])) {
    "Creating database $fsDbName"
    $cmd = "CREATE DATABASE $($fsDbName)"
    Invoke-Sqlcmd -ConnectionString $masterConnString -Query $cmd

    $cmd = "EXEC sp_configure filestream_access_level, 2; RECONFIGURE;"
    Invoke-Sqlcmd -ConnectionString $fsDbConnString -Query $cmd

    $cmd = "ALTER DATABASE $($fsDbName)
        ADD FILEGROUP fs_Group CONTAINS FILESTREAM;
        GO

        ALTER DATABASE $($fsDbName)
        ADD FILE ( NAME = 'fs_$($fsDbName)', FILENAME = '$($fsLocation)' )
        TO FILEGROUP fs_Group;"
    Invoke-Sqlcmd -ConnectionString $masterConnString -Query $cmd

    Write-Output "OK!"
}
# END Create FS DB

Push-Location .\test\Rhetos.LightDMS.TestApp\bin\Debug\net8.0\

Write-Output "Deploying test app to $($varbinDbName)..."
$appSettingsObj = [pscustomobject]@{
    ConnectionStrings = [pscustomobject]@{
        RhetosConnectionString = '';
    }
}

$appSettingsObj.ConnectionStrings.RhetosConnectionString = $varbinDbConnString
ConvertTo-Json $appSettingsObj -Depth 5 | Set-Content -Path rhetos-app.local.settings.json
& .\rhetos dbupdate .\Rhetos.LightDMS.TestApp.dll
if ($LastExitCode -ne 0) { throw "rhetos dbupdate failed on varbinDbConnString." }

Write-Output "Deploying test app to $($fsDbName)..."
$appSettingsObj.ConnectionStrings.RhetosConnectionString = $fsDbConnString
ConvertTo-Json $appSettingsObj -Depth 5 | Set-Content -Path rhetos-app.local.settings.json
& .\rhetos dbupdate .\Rhetos.LightDMS.TestApp.dll
if ($LastExitCode -ne 0) { throw "rhetos dbupdate failed on fsDbConnString." }

$enableFileStreamCmd = Get-Content "..\..\..\AfterDeploy\Use filestream if supported.sql" -Raw
Invoke-Sqlcmd -ConnectionString $fsDbConnString -Query $enableFileStreamCmd

Pop-Location

$winDocker = [bool](Get-Command docker -CommandType Application -ErrorAction SilentlyContinue)
$wslDocker = &{ wsl.exe sh -lc 'command -v docker >/dev/null 2>&1'; $LASTEXITCODE -eq 0 }

function Invoke-Docker {
    $argList = @($args | ForEach-Object { '"' + $_.ToString() + '"' })
    if ($winDocker) {
        Write-Host -- docker @argList
        & docker @argList
    } elseif ($wslDocker) {
        Write-Host -- wsl.exe -- docker @argList
        & wsl.exe -- docker @argList
    } else {
        throw "Docker is NOT available."
    }
    if ($LASTEXITCODE -ne 0) { throw "Docker command error." }
}

Write-Output "Checking Docker install... "
$DockerOS = Invoke-Docker info -f "{{.OSType}}"
if ($DockerOS -eq "linux") {
    Write-Output "OK!"
} else {
    Write-Output "FAIL!"
    Write-Error "You need to run Docker in Linux container mode."
    exit 1
}

$containerId = Invoke-Docker ps -aqf "name=lightdms_s3ninja"
if (-not ([string]::IsNullOrEmpty($containerId)))
{
    Invoke-Docker start lightdms_s3ninja
} else {
    Invoke-Docker run --name lightdms_s3ninja -p 9444:9000 -d scireum/s3-ninja
}

$containerId = Invoke-Docker ps -aqf "name=lightdms_azurite"
if (-not ([string]::IsNullOrEmpty($containerId)))
{
    Invoke-Docker start lightdms_azurite
} else {
    Invoke-Docker run --name lightdms_azurite -p 10000:10000 -d mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0
}

# Using "no-build" option as optimization, because Test.bat should always be executed after Build.bat.
Write-Output 'dotnet test'
& dotnet test --no-build
if ($LastExitCode -ne 0) { throw "dotnet test failed." }

Write-Output 'Test completed, cleaning up test resources ...'

Remove-Item '.\test\Rhetos.LightDMS.TestApp\bin\Debug\net8.0\rhetos-app.local.settings.json'
Invoke-Docker stop lightdms_s3ninja lightdms_azurite

Write-Output 'Done!'

# To remove all images after tests, run this in Windows command line or in WSL:
# docker rm lightdms_s3ninja lightdms_azurite
# docker rmi scireum/s3-ninja mcr.microsoft.com/azure-storage/azurite
