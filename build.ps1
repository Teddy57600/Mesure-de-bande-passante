param([switch]$Test)
$ErrorActionPreference = 'Stop'
# Keep build state inside this project; no user NuGet configuration is required.
$taskEnvironment = @{}
foreach ($taskName in @('APPDATA', 'DOTNET_CLI_HOME', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_GENERATE_ASPNET_CERTIFICATE')) {
    $taskEnvironment[$taskName] = [Environment]::GetEnvironmentVariable($taskName, 'Process')
}
Push-Location $PSScriptRoot
try {
    $env:APPDATA = Join-Path $PSScriptRoot 'artifacts\build-home'
    $env:DOTNET_CLI_HOME = $env:APPDATA
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    dotnet restore Bandwidth\Bandwidth.csproj --configfile "$PSScriptRoot\NuGet.Config"
    if ($LASTEXITCODE) { throw 'Échec de la restauration.' }
    dotnet publish Bandwidth\Bandwidth.csproj -c Release --no-restore --self-contained false -o "$PSScriptRoot\dist\MiniDebit"
    if ($LASTEXITCODE) { throw 'Échec de la compilation.' }
    if ($Test) {
        dotnet restore Tests\Tests.csproj --configfile "$PSScriptRoot\NuGet.Config"
        if ($LASTEXITCODE) { throw 'Échec de la restauration des tests.' }
        dotnet run --project Tests\Tests.csproj -c Release --no-restore -- "$PSScriptRoot\artifacts\test-settings"
        if ($LASTEXITCODE) { throw 'Échec des tests.' }
    }
    Write-Host "Prêt : $PSScriptRoot\dist\MiniDebit\MiniDebit.exe"
}
finally {
    Pop-Location
    foreach ($taskName in $taskEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($taskName, $taskEnvironment[$taskName], 'Process')
    }
}
