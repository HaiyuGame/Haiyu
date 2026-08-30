param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$abiProject = Join-Path $root "src\Extensions\Haiyu.ABI\Haiyu.ABI.csproj"
$testProject = Join-Path $PSScriptRoot "ABIRuntime.TransportTests.csproj"
$publishDir = Join-Path $root "artifacts\Haiyu.ABI\$Configuration\$RuntimeIdentifier"
$appAbiDir = Join-Path $root "src\WutheringWavesTool\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Haiyu.ABI"

dotnet publish $abiProject -c $Configuration -r $RuntimeIdentifier -o $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force -Path $appAbiDir | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $appAbiDir -Recurse -Force

$deployedDll = Join-Path $appAbiDir "Haiyu.ABI.dll"
dotnet run --project $testProject -c $Configuration -- $deployedDll
exit $LASTEXITCODE
