#Requires -Module SqlServer

# Parameters
$config = Get-Content .\test-config.json -Raw | ConvertFrom-Json

$sqlServerName = $config.SqlServerName
$fsDbName = $config.FileStreamDatabaseName
$fsLocation = $config.FileStreamFileLocation
$varbinDbName = $config.VarBinaryDatabaseName
$masterConnString = $config.MasterConnectionString

# Computed parameters
$fsDbConnString = "Server=$($sqlServerName);Database=$($fsDbName);Integrated Security=true;"
$varbinDbConnString = "Server=$($sqlServerName);Database=$($varbinDbName);Integrated Security=true;"

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

Push-Location .\test\Rhetos.LightDMS.TestApp\
dotnet restore

$originalAppSettings = Get-Content appsettings.json -Raw

Write-Host "Deploying test app to $($varbinDbName)..."
$appSettingsObj = ConvertFrom-Json $originalAppSettings

$appSettingsObj.ConnectionStrings.RhetosConnectionString = $varbinDbConnString
ConvertTo-Json $appSettingsObj -Depth 5 | Set-Content -Path appsettings.json
dotnet build

Write-Host "Deploying test app to $($fsDbName)..."
$appSettingsObj.ConnectionStrings.RhetosConnectionString = $fsDbConnString
ConvertTo-Json $appSettingsObj -Depth 5 | Set-Content -Path appsettings.json
dotnet build

$enableFileStreamCmd = Get-Content ".\AfterDeploy\Use filestream if supported.sql" -Raw
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

Push-Location .\test\Rhetos.LightDMS.IntegrationTest
dotnet test

Pop-Location
Set-Content -Path .\test\Rhetos.LightDMS.TestApp\appsettings.json $originalAppSettings
