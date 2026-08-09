#Requires -Version 5.1
<#
.SYNOPSIS
  MAUI Android one-click package: find SDK tools, list keystores, prompt password, align+sign.

.DESCRIPTION
  1) Auto-detect Android SDK (env / common paths)
  2) Resolve zipalign + apksigner from newest build-tools
  3) Scan available keystores and let you pick one
  4) Secure password prompt (not stored on disk)
  5) dotnet publish, then zipalign + apksigner, then verify

.EXAMPLE
  .\publish-android.ps1

.EXAMPLE
  .\publish-android.ps1 -SkipBuild

.EXAMPLE
  .\publish-android.ps1 -KeystorePath "C:\keys\release.keystore" -Alias "myalias"
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0-android",
    [string]$RuntimeIdentifier = "android-arm64",
    [string]$KeystorePath,
    [string]$Alias,
    [string]$AndroidSdkRoot,
    [string]$OutputName,
    [switch]$SkipBuild,
    [switch]$NonInteractive
)

$ErrorActionPreference = "Stop"
$ScriptRoot = $PSScriptRoot
if (-not $ScriptRoot) {
    $ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$ProjectFile = Get-ChildItem -Path $ScriptRoot -Filter "*.csproj" | Select-Object -First 1
if (-not $ProjectFile) {
    throw "No .csproj found in: $ScriptRoot"
}

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host ("==> {0}" -f $Message) -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host ("    [OK] {0}" -f $Message) -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host ("    {0}" -f $Message) -ForegroundColor Gray
}

function Write-WarnLine {
    param([string]$Message)
    Write-Host ("    [!] {0}" -f $Message) -ForegroundColor Yellow
}

# External tools (apksigner/java) often write harmless WARNINGs to stderr.
# With $ErrorActionPreference=Stop, PowerShell turns those into terminating errors.
function Invoke-External {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $false)]
        [string[]]$ToolArgs
    )

    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        # Use call operator with splatting; never let stderr ErrorRecords stop the script.
        if ($ToolArgs -and $ToolArgs.Count -gt 0) {
            $raw = & $FilePath @ToolArgs 2>&1
        }
        else {
            $raw = & $FilePath 2>&1
        }
        $code = $LASTEXITCODE
        $lines = foreach ($item in @($raw)) {
            if ($null -eq $item) { continue }
            if ($item -is [System.Management.Automation.ErrorRecord]) {
                $item.ToString()
            }
            else {
                [string]$item
            }
        }
        return [pscustomobject]@{
            ExitCode = $code
            Output   = ($lines -join [Environment]::NewLine)
            Lines    = @($lines)
        }
    }
    finally {
        $ErrorActionPreference = $prevEap
    }
}

function ConvertFrom-SecureStringPlain {
    param([System.Security.SecureString]$Secure)
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Read-Secret {
    param(
        [string]$Prompt,
        [switch]$AllowEmpty
    )
    while ($true) {
        $secure = Read-Host -Prompt $Prompt -AsSecureString
        $plain = ConvertFrom-SecureStringPlain $secure
        if ($AllowEmpty -or -not [string]::IsNullOrEmpty($plain)) {
            return $plain
        }
        Write-WarnLine "Password cannot be empty."
    }
}

function Find-AndroidSdk {
    param([string]$Override)

    if ($Override -and (Test-Path -LiteralPath $Override)) {
        return (Resolve-Path -LiteralPath $Override).Path
    }

    $candidates = @(
        $env:ANDROID_HOME,
        $env:ANDROID_SDK_ROOT,
        (Join-Path $env:LOCALAPPDATA "Android\Sdk"),
        (Join-Path $env:USERPROFILE "AppData\Local\Android\Sdk"),
        "C:\Program Files (x86)\Android\android-sdk",
        "C:\Android\android-sdk",
        "D:\Android\Sdk",
        "E:\Android\Sdk"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    foreach ($sdk in $candidates) {
        $bt = Join-Path $sdk "build-tools"
        if (Test-Path -LiteralPath $bt) {
            return (Resolve-Path -LiteralPath $sdk).Path
        }
    }

    return $null
}

function Find-BuildTools {
    param([string]$SdkRoot)

    $btRoot = Join-Path $SdkRoot "build-tools"
    if (-not (Test-Path -LiteralPath $btRoot)) {
        throw ("build-tools not found under SDK: {0}" -f $btRoot)
    }

    $versions = Get-ChildItem -Path $btRoot -Directory | Sort-Object {
        $n = $_.Name -replace '[^\d\.]', ''
        try { [version]$n } catch { [version]"0.0" }
    } -Descending

    foreach ($dir in $versions) {
        $zipalign = Join-Path $dir.FullName "zipalign.exe"
        $apksigner = Join-Path $dir.FullName "apksigner.bat"
        if ((Test-Path -LiteralPath $zipalign) -and (Test-Path -LiteralPath $apksigner)) {
            return [pscustomobject]@{
                Version   = $dir.Name
                Path      = $dir.FullName
                ZipAlign  = $zipalign
                ApkSigner = $apksigner
            }
        }
    }

    throw "No usable zipalign.exe / apksigner.bat found in build-tools."
}

function Find-Keytool {
    $candidates = New-Object System.Collections.Generic.List[string]

    if ($env:JAVA_HOME) {
        $candidates.Add((Join-Path $env:JAVA_HOME "bin\keytool.exe"))
    }

    $vsJdks = Get-ChildItem "C:\Program Files\Microsoft\jdk-*" -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending
    foreach ($jdk in $vsJdks) {
        $candidates.Add((Join-Path $jdk.FullName "bin\keytool.exe"))
    }

    $searchRoots = @(
        "C:\Program Files\Android\jdk",
        "C:\Program Files\Eclipse Adoptium",
        "C:\Program Files\Java"
    )
    foreach ($root in $searchRoots) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        Get-ChildItem -Path $root -Filter "keytool.exe" -Recurse -ErrorAction SilentlyContinue |
            Select-Object -First 2 -ExpandProperty FullName |
            ForEach-Object { $candidates.Add($_) }
    }

    foreach ($c in ($candidates | Select-Object -Unique)) {
        if ($c -and (Test-Path -LiteralPath $c)) {
            return $c
        }
    }
    return $null
}

function Get-KeystoreCandidates {
    $results = New-Object System.Collections.Generic.List[object]
    $seen = @{}

    $scanRoots = @(
        (Join-Path $env:LOCALAPPDATA "Xamarin\Mono for Android\Keystore"),
        (Join-Path $env:USERPROFILE ".android"),
        (Join-Path $ScriptRoot "keystore"),
        (Join-Path $ScriptRoot "keystores"),
        (Join-Path $ScriptRoot "signing"),
        $ScriptRoot
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    foreach ($root in $scanRoots) {
        # Project root: only top-level files (avoid walking bin/obj).
        # Other roots: recurse, but skip huge build folders if present.
        if ($root -eq $ScriptRoot) {
            $files = Get-ChildItem -Path $root -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Extension -match '\.(keystore|jks|keys)$' }
        }
        else {
            $files = Get-ChildItem -Path $root -Recurse -File -ErrorAction SilentlyContinue |
                Where-Object {
                    $_.Extension -match '\.(keystore|jks|keys)$' -and
                    $_.FullName -notmatch '\\(bin|obj|\.git)\\'
                }
        }

        foreach ($f in $files) {
            if ($f.Name -ieq "debug.keystore") { continue }
            $key = $f.FullName.ToLowerInvariant()
            if ($seen.ContainsKey($key)) { continue }
            $seen[$key] = $true

            $defaultAlias = [IO.Path]::GetFileNameWithoutExtension($f.Name)
            $parentName = $f.Directory.Name
            if ($parentName -and $parentName -ne "Keystore" -and $parentName -ne ".android") {
                # VS Archive layout: Keystore\<Alias>\<Alias>.keystore
                $defaultAlias = $parentName
            }

            $results.Add([pscustomobject]@{
                Path         = $f.FullName
                Name         = $f.Name
                DefaultAlias = $defaultAlias
                SizeKB       = [math]::Round($f.Length / 1KB, 1)
                Modified     = $f.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                Source       = $root
            })
        }
    }

    return $results
}

function Get-KeystoreAliases {
    param(
        [string]$Keystore,
        [string]$StorePass,
        [string]$KeytoolPath
    )

    if (-not $KeytoolPath) {
        return @()
    }

    $result = Invoke-External -FilePath $KeytoolPath -ToolArgs @(
        "-list",
        "-keystore", $Keystore,
        "-storepass", $StorePass
    )
    if ($result.ExitCode -ne 0) {
        return @()
    }

    $aliases = New-Object System.Collections.Generic.List[string]
    foreach ($text in $result.Lines) {
        if ($text -match 'Alias name:\s*(.+)$') {
            $aliases.Add($Matches[1].Trim())
            continue
        }
        if ($text -match '别名[：:]\s*(.+)$') {
            $aliases.Add($Matches[1].Trim())
            continue
        }
        # short form: alias, date, PrivateKeyEntry
        if ($text -match '^([^,\s]+),\s*.*PrivateKeyEntry') {
            $aliases.Add($Matches[1].Trim())
        }
    }

    return @($aliases | Select-Object -Unique)
}

function Select-FromList {
    param(
        [string]$Title,
        [object[]]$Items,
        [scriptblock]$Formatter
    )

    if (-not $Items -or $Items.Count -eq 0) {
        throw ("No items to select: {0}" -f $Title)
    }

    Write-Host ""
    Write-Host $Title -ForegroundColor White
    for ($i = 0; $i -lt $Items.Count; $i++) {
        $label = & $Formatter $Items[$i]
        Write-Host ("  [{0}] {1}" -f ($i + 1), $label)
    }

    if ($Items.Count -eq 1) {
        Write-Info "Only one item, auto-select [1]"
        return $Items[0]
    }

    while ($true) {
        $raw = Read-Host ("Enter number 1-{0}" -f $Items.Count)
        $n = 0
        if ([int]::TryParse($raw, [ref]$n) -and $n -ge 1 -and $n -le $Items.Count) {
            return $Items[$n - 1]
        }
        Write-WarnLine "Invalid number."
    }
}

function Find-UnsignedApk {
    param(
        [string]$ProjectDir,
        [string]$Configuration,
        [string]$Framework,
        [string]$RuntimeIdentifier
    )

    $patterns = @(
        (Join-Path $ProjectDir ("obj\{0}\{1}\{2}\android\bin\*.apk" -f $Configuration, $Framework, $RuntimeIdentifier)),
        (Join-Path $ProjectDir ("bin\{0}\{1}\{2}\*.apk" -f $Configuration, $Framework, $RuntimeIdentifier)),
        (Join-Path $ProjectDir ("bin\{0}\{1}\{2}\publish\*.apk" -f $Configuration, $Framework, $RuntimeIdentifier)),
        (Join-Path $ProjectDir ("obj\{0}\{1}\android\bin\*.apk" -f $Configuration, $Framework))
    )

    $found = @()
    foreach ($pattern in $patterns) {
        $found += Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Name -notmatch '-Signed\.apk$' -and
                $_.Name -notmatch 'aligned' -and
                $_.Name -notmatch '-signed\.apk$'
            }
    }

    if (-not $found) {
        $objRoot = Join-Path $ProjectDir ("obj\{0}\{1}" -f $Configuration, $Framework)
        if (Test-Path -LiteralPath $objRoot) {
            $found = Get-ChildItem -Path $objRoot -Recurse -Filter "*.apk" -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -notmatch '-Signed\.apk$' } |
                Sort-Object LastWriteTime -Descending
        }
    }

    if (-not $found) {
        throw "Unsigned APK not found. Build first (or remove -SkipBuild)."
    }

    return ($found | Sort-Object LastWriteTime -Descending | Select-Object -First 1)
}

function Invoke-DotnetPublish {
    param(
        [string]$ProjectPath,
        [string]$Configuration,
        [string]$Framework
    )

    Write-Step ("dotnet publish [{0} / {1}]" -f $Configuration, $Framework)
    Write-Info ("Project: {0}" -f $ProjectPath)

    $publishArgs = @(
        "publish", $ProjectPath,
        "-c", $Configuration,
        "-f", $Framework,
        "-p:AndroidPackageFormat=apk",
        "-p:AndroidUseApkSigner=true",
        "-p:AndroidZipAlign=true",
        # Do not rely on MSBuild release signing here; always re-sign with apksigner after zipalign.
        "-p:AndroidKeyStore=false"
    )

    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw ("dotnet publish failed, exit code {0}" -f $LASTEXITCODE)
    }
    Write-Ok "Build finished"
}

function Invoke-ZipAlign {
    param(
        [string]$ZipAlign,
        [string]$InputApk,
        [string]$OutputApk
    )

    Write-Step "zipalign (4-byte boundary)"
    Write-Info ("In : {0}" -f $InputApk)
    Write-Info ("Out: {0}" -f $OutputApk)

    if (Test-Path -LiteralPath $OutputApk) {
        Remove-Item -LiteralPath $OutputApk -Force
    }

    $align = Invoke-External -FilePath $ZipAlign -ToolArgs @("-f", "-p", "4", $InputApk, $OutputApk)
    if ($align.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $OutputApk)) {
        if ($align.Output) { Write-Host $align.Output }
        throw "zipalign failed"
    }

    $check = Invoke-External -FilePath $ZipAlign -ToolArgs @("-c", "-v", "4", $OutputApk)
    if ($check.Output -notmatch "Verification successful") {
        Write-Host $check.Output
        throw "zipalign verification failed"
    }
    if ($check.Output -match "resources\.arsc \(BAD") {
        Write-Host $check.Output
        throw "resources.arsc is not 4-byte aligned"
    }

    Write-Ok "zipalign verification successful"
}

function Invoke-ApkSign {
    param(
        [string]$ApkSigner,
        [string]$ApkPath,
        [string]$Keystore,
        [string]$Alias,
        [string]$StorePass,
        [string]$KeyPass
    )

    Write-Step "apksigner sign"
    Write-Info ("Keystore: {0}" -f $Keystore)
    Write-Info ("Alias   : {0}" -f $Alias)
    Write-Info ("APK     : {0}" -f $ApkPath)

    # Pass secrets via env vars to reduce process-list exposure
    $env:HAIYU_KS_PASS = $StorePass
    $env:HAIYU_KEY_PASS = $KeyPass
    try {
        $sign = Invoke-External -FilePath $ApkSigner -ToolArgs @(
            "sign",
            "--v1-signing-enabled", "true",
            "--v2-signing-enabled", "true",
            "--v3-signing-enabled", "true",
            "--ks", $Keystore,
            "--ks-key-alias", $Alias,
            "--ks-pass", "env:HAIYU_KS_PASS",
            "--key-pass", "env:HAIYU_KEY_PASS",
            $ApkPath
        )

        # Filter noisy Java module warnings; keep real errors
        $useful = @($sign.Lines | Where-Object {
            $_ -and
            $_ -notmatch 'WARNING: A restricted method' -and
            $_ -notmatch 'WARNING: java\.lang\.System::loadLibrary' -and
            $_ -notmatch 'WARNING: Use --enable-native-access' -and
            $_ -notmatch 'WARNING: Restricted methods will be blocked' -and
            $_ -notmatch 'WARNING: META-INF/'
        })
        if ($useful.Count -gt 0) {
            $useful | ForEach-Object { Write-Info $_ }
        }

        if ($sign.ExitCode -ne 0) {
            if ($sign.Output) { Write-Host $sign.Output }
            throw ("apksigner sign failed, exit code {0}. Check password/alias." -f $sign.ExitCode)
        }
    }
    finally {
        Remove-Item Env:\HAIYU_KS_PASS -ErrorAction SilentlyContinue
        Remove-Item Env:\HAIYU_KEY_PASS -ErrorAction SilentlyContinue
    }

    Write-Ok "Signed"
}

function Invoke-VerifyApk {
    param(
        [string]$ZipAlign,
        [string]$ApkSigner,
        [string]$ApkPath
    )

    Write-Step "Final verification"

    $align = Invoke-External -FilePath $ZipAlign -ToolArgs @("-c", "-v", "4", $ApkPath)
    $arscLine = @($align.Lines | Where-Object { $_ -match "resources\.arsc" } | Select-Object -First 1)
    if ($arscLine) {
        Write-Info $arscLine.Trim()
    }

    if ($align.Output -notmatch "Verification successful") {
        Write-Host $align.Output
        throw "Final zipalign verification failed"
    }
    Write-Ok "zipalign Verification successful"

    $verify = Invoke-External -FilePath $ApkSigner -ToolArgs @("verify", "--print-certs", $ApkPath)
    if ($verify.ExitCode -ne 0) {
        Write-Host $verify.Output
        throw "apksigner verify failed"
    }

    $dn = @($verify.Lines | Where-Object { $_ -match "Signer #1 certificate DN:" } | Select-Object -First 1)
    $sha = @($verify.Lines | Where-Object { $_ -match "SHA-256 digest:" } | Select-Object -First 1)
    if ($dn) { Write-Info $dn.Trim() }
    if ($sha) { Write-Info $sha.Trim() }
    Write-Ok "Signature verification successful"
}

# =========================
# Main
# =========================

Write-Host ""
Write-Host "========================================" -ForegroundColor White
Write-Host "  Haiyu.Mobile Android Publish Script" -ForegroundColor White
Write-Host "========================================" -ForegroundColor White
Write-Info ("Project: {0}" -f $ProjectFile.FullName)

Write-Step "Locate Android SDK and tools"
$sdk = Find-AndroidSdk -Override $AndroidSdkRoot
if (-not $sdk) {
    throw "Android SDK not found. Set ANDROID_HOME / ANDROID_SDK_ROOT, or pass -AndroidSdkRoot."
}
Write-Ok ("SDK: {0}" -f $sdk)

$tools = Find-BuildTools -SdkRoot $sdk
Write-Ok ("build-tools: {0}" -f $tools.Version)
Write-Info ("zipalign : {0}" -f $tools.ZipAlign)
Write-Info ("apksigner: {0}" -f $tools.ApkSigner)

$keytool = Find-Keytool
if ($keytool) {
    Write-Ok ("keytool  : {0}" -f $keytool)
}
else {
    Write-WarnLine "keytool not found; cannot auto-list aliases (you can type alias manually)."
}

Write-Step "Select keystore"
$selectedKeystore = $null
$selectedAlias = $Alias

if ($KeystorePath) {
    if (-not (Test-Path -LiteralPath $KeystorePath)) {
        throw ("Keystore not found: {0}" -f $KeystorePath)
    }
    $selectedKeystore = (Resolve-Path -LiteralPath $KeystorePath).Path
    Write-Ok ("Using: {0}" -f $selectedKeystore)
}
else {
    $stores = @(Get-KeystoreCandidates)
    if ($stores.Count -eq 0) {
        throw @"
No .keystore / .jks found.
Put one under:
  %LOCALAPPDATA%\Xamarin\Mono for Android\Keystore\
  or project\keystore\
  or pass -KeystorePath
"@
    }

    if ($NonInteractive) {
        throw "NonInteractive mode requires -KeystorePath and -Alias."
    }

    $pick = Select-FromList -Title "Available keystores:" -Items $stores -Formatter {
        param($s)
        "{0}  | suggested-alias={1}  | {2} KB  | {3}" -f $s.Path, $s.DefaultAlias, $s.SizeKB, $s.Modified
    }
    $selectedKeystore = $pick.Path
    if (-not $selectedAlias) {
        $selectedAlias = $pick.DefaultAlias
    }
    Write-Ok ("Selected: {0}" -f $selectedKeystore)
}

if ($NonInteractive) {
    throw "NonInteractive mode cannot prompt for passwords. Run in an interactive terminal."
}

Write-Step "Enter signing passwords"
$storePass = Read-Secret -Prompt "Keystore password (StorePass)"
$keyPassSecure = Read-Host -Prompt "Key password (Enter if same as StorePass)" -AsSecureString
$keyPass = ConvertFrom-SecureStringPlain $keyPassSecure
if ([string]::IsNullOrEmpty($keyPass)) {
    $keyPass = $storePass
    Write-Info "Key password = Store password"
}

if ($keytool) {
    $aliases = @(Get-KeystoreAliases -Keystore $selectedKeystore -StorePass $storePass -KeytoolPath $keytool)
    if ($aliases.Count -gt 0) {
        Write-Ok ("Aliases in keystore: {0}" -f ($aliases -join ", "))
        if ($selectedAlias -and ($aliases -contains $selectedAlias)) {
            Write-Info ("Using alias: {0}" -f $selectedAlias)
        }
        elseif ($aliases.Count -eq 1) {
            $selectedAlias = $aliases[0]
            Write-Info ("Auto alias: {0}" -f $selectedAlias)
        }
        else {
            $selectedAlias = Select-FromList -Title "Select alias:" -Items $aliases -Formatter { param($a) $a }
        }
    }
    else {
        Write-WarnLine "Could not list aliases with current password (wrong password or keytool parse issue). Continue with suggested alias."
    }
}

if (-not $selectedAlias) {
    $selectedAlias = Read-Host "Enter Key Alias"
}
if ([string]::IsNullOrWhiteSpace($selectedAlias)) {
    throw "Alias is required."
}
Write-Ok ("Alias: {0}" -f $selectedAlias)

if (-not $SkipBuild) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet CLI not found. Install .NET SDK first."
    }
    Invoke-DotnetPublish -ProjectPath $ProjectFile.FullName -Configuration $Configuration -Framework $Framework
}
else {
    Write-Step "Skip build (-SkipBuild)"
}

Write-Step "Locate unsigned APK"
$unsigned = Find-UnsignedApk -ProjectDir $ScriptRoot -Configuration $Configuration -Framework $Framework -RuntimeIdentifier $RuntimeIdentifier
Write-Ok ("Found: {0}" -f $unsigned.FullName)
Write-Info ("Size: {0:N2} MB | Time: {1}" -f ($unsigned.Length / 1MB), $unsigned.LastWriteTime)

$outDir = Join-Path $ScriptRoot ("bin\{0}\{1}\{2}\publish" -f $Configuration, $Framework, $RuntimeIdentifier)
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$appId = "app"
try {
    $rawProj = Get-Content -LiteralPath $ProjectFile.FullName -Raw
    if ($rawProj -match '<ApplicationId>\s*([^<]+)\s*</ApplicationId>') {
        $appId = $Matches[1].Trim()
    }
}
catch {
    # ignore
}

if (-not $OutputName) {
    $safeAlias = ($selectedAlias -replace '[^\w\-]', '_')
    $OutputName = "{0}-{1}-signed.apk" -f $appId, $safeAlias
}

$alignedApk = Join-Path $outDir ("{0}.aligned.apk" -f [IO.Path]::GetFileNameWithoutExtension($OutputName))
$finalApk = Join-Path $outDir $OutputName

Invoke-ZipAlign -ZipAlign $tools.ZipAlign -InputApk $unsigned.FullName -OutputApk $alignedApk

if (Test-Path -LiteralPath $finalApk) {
    Remove-Item -LiteralPath $finalApk -Force
}
Copy-Item -LiteralPath $alignedApk -Destination $finalApk -Force

try {
    Invoke-ApkSign -ApkSigner $tools.ApkSigner -ApkPath $finalApk `
        -Keystore $selectedKeystore -Alias $selectedAlias `
        -StorePass $storePass -KeyPass $keyPass
}
catch {
    if (Test-Path -LiteralPath $finalApk) {
        Remove-Item -LiteralPath $finalApk -Force -ErrorAction SilentlyContinue
    }
    throw
}
finally {
    $storePass = $null
    $keyPass = $null
    [GC]::Collect()
}

Invoke-VerifyApk -ZipAlign $tools.ZipAlign -ApkSigner $tools.ApkSigner -ApkPath $finalApk

if ((Test-Path -LiteralPath $alignedApk) -and ($alignedApk -ne $finalApk)) {
    Remove-Item -LiteralPath $alignedApk -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  SUCCESS" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ("APK : {0}" -f $finalApk) -ForegroundColor Green
Write-Host ("Size: {0:N2} MB" -f ((Get-Item -LiteralPath $finalApk).Length / 1MB)) -ForegroundColor Green
Write-Host ("Time: {0}" -f (Get-Item -LiteralPath $finalApk).LastWriteTime) -ForegroundColor Green
Write-Host ""
Write-Host "Install tips:" -ForegroundColor Yellow
Write-Host "  1) Uninstall old app if signature differs"
Write-Host ("  2) adb install -r `"{0}`"" -f $finalApk)
Write-Host ""
