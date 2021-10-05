$ErrorActionPreference = 'Stop'

# Prequisites

# Testing if Build.bat has completed successfully.
if (-Not (Test-Path 'test\Rhetos.LightDMS.TestApp\bin\Debug\net5.0\Rhetos.LightDMS.TestApp.dll' -PathType Leaf)) {
    throw "Please execute Build.bat successfully before running Test.ps1. Build output file 'Rhetos.LightDMS.TestApp.dll' does not exist."
}

# Testing if the test settings have been configured.
if (-Not (Test-Path '.\test-config.json' -PathType Leaf)) {
    throw "Please create and configure test settings in 'test-config.json'. See Readme.md for the instructions."
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

Write-Prompt "Setting up database without FILESTREAM enabled (use varbinary to store file content)... "
$cmd = "IF DB_ID('$($varbinDbName)') IS NULL CREATE DATABASE $($varbinDbName)"
Invoke-Sqlcmd -ConnectionString $masterConnString -Query $cmd
Write-Host "OK!"

# BEGIN Create FS DB
Write-Prompt "Setting up database with FILESTREAM enabled... "

$cmd = "SELECT DB_ID('$($fsDbName)')"
$r = Invoke-Sqlcmd -ConnectionString $masterConnString -Query $cmd -OutputAs DataRows

if ([string]::IsNullOrEmpty($r[0])) {
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

    Write-Host "OK!"
}
# END Create FS DB

Push-Location .\test\Rhetos.LightDMS.TestApp\bin\Debug\net5.0\

Write-Host "Deploying test app to $($varbinDbName)..."
$appSettingsObj = [pscustomobject]@{
    ConnectionStrings = [pscustomobject]@{
        RhetosConnectionString = '';
    }
}

$appSettingsObj.ConnectionStrings.RhetosConnectionString = $varbinDbConnString
ConvertTo-Json $appSettingsObj -Depth 5 | Set-Content -Path rhetos-app.local.settings.json
& .\rhetos dbupdate .\Rhetos.LightDMS.TestApp.dll
if ($LastExitCode -ne 0) { throw "rhetos dbupdate failed on varbinDbConnString." }

Write-Host "Deploying test app to $($fsDbName)..."
$appSettingsObj.ConnectionStrings.RhetosConnectionString = $fsDbConnString
ConvertTo-Json $appSettingsObj -Depth 5 | Set-Content -Path rhetos-app.local.settings.json
& .\rhetos dbupdate .\Rhetos.LightDMS.TestApp.dll
if ($LastExitCode -ne 0) { throw "rhetos dbupdate failed on fsDbConnString." }

$enableFileStreamCmd = Get-Content "..\..\..\AfterDeploy\Use filestream if supported.sql" -Raw
Invoke-Sqlcmd -ConnectionString $fsDbConnString -Query $enableFileStreamCmd

Pop-Location

Write-Prompt "Checking Docker install... "
$DockerOS = docker info -f "{{.OSType}}"
if ($DockerOS -eq "linux") {
    Write-Host "OK!"
} else {
    Write-Host "FAIL!"
    Write-Error "You need to run Docker in Linux container mode."
    exit 1
}

$containerId = docker ps -aqf "name=lightdms_s3ninja"
if (-not ([string]::IsNullOrEmpty($containerId)))
{
    docker start lightdms_s3ninja
} else {
    docker run --name lightdms_s3ninja -p 9444:9000 -d scireum/s3-ninja
}

$containerId = docker ps -aqf "name=lightdms_azurite"
if (-not ([string]::IsNullOrEmpty($containerId)))
{
    docker start lightdms_azurite
} else {
    docker run --name lightdms_azurite -p 10000:10000 -d mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0
}

# Using "no-build" option as optimization, because Test.bat should always be executed after Build.bat.
& dotnet test --no-build
if ($LastExitCode -ne 0) { throw "dotnet test failed." }

Remove-Item '.\test\Rhetos.LightDMS.TestApp\bin\Debug\net5.0\rhetos-app.local.settings.json'
