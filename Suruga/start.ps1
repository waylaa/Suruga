Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

param (
[string]$token
)

# =========================
# Paths
# =========================
$Root    = Resolve-Path $PSScriptRoot
$Runtime = Join-Path $Root "runtime"
$StateFile = Join-Path $Runtime "state.json"

$GitRoot = Join-Path $Runtime "git"
$GitExe  = Join-Path $GitRoot "cmd\git.exe"

$DenoRoot = Join-Path $Runtime "deno"
$DenoExe  = Join-Path $DenoRoot "deno.exe"

$CipherDir = Join-Path $Runtime "yt-cipher"
$EjsDir    = Join-Path $CipherDir "ejs"

$Config = Join-Path $Root "config.json"
$BotExe = Join-Path $Root "Suruga.exe"

# =========================
# Constants
# =========================
$EjsCommit = "5d7bf090bb9a3e2dd13ded4a21a009224f87"
$CipherHealth = "http://127.0.0.1:8001/health"

# =========================
# State handling
# =========================
if (-not (Test-Path $StateFile)) {
    $State = @{
        git = @{}
        deno = @{}
        "yt-cipher" = @{}
        ejs = @{}
    }
} else {
    $State = Get-Content $StateFile | ConvertFrom-Json
}

function Save-State {
    $State | ConvertTo-Json -Depth 5 | Set-Content $StateFile -Encoding UTF8
}

# =========================
# GitHub helpers
# =========================
function Get-LatestRelease($Repo) {
    Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/latest"
}

# =========================
# Ensure Git (self-updating)
# =========================
$GitRelease = Get-LatestRelease "git-for-windows/git"
$GitTag = $GitRelease.tag_name

$NeedsGit = -not (Test-Path $GitExe) -or ($State.git.version -ne $GitTag)

if ($NeedsGit) {
    Write-Host "Updating Git to $GitTag"

    Remove-Item $GitRoot -Recurse -Force -ErrorAction SilentlyContinue

    $Asset = $GitRelease.assets |
            Where-Object { $_.name -match "PortableGit.*64-bit.*\.7z\.exe$" } |
            Select-Object -First 1

    $Installer = Join-Path $Runtime $Asset.name
    Invoke-WebRequest $Asset.browser_download_url -OutFile $Installer
    & $Installer -o"$GitRoot" -y | Out-Null
    Remove-Item $Installer

    $State.git.version = $GitTag
    Save-State
}

# =========================
# Ensure Deno (self-updating, verified)
# =========================
$DenoRelease = Get-LatestRelease "denoland/deno"
$DenoTag = $DenoRelease.tag_name.TrimStart("v")

$NeedsDeno = -not (Test-Path $DenoExe) -or ($State.deno.version -ne $DenoTag)

if ($NeedsDeno) {
    Write-Host "Updating Deno to $DenoTag"

    Remove-Item $DenoRoot -Recurse -Force -ErrorAction SilentlyContinue

    $Asset = $DenoRelease.assets |
            Where-Object { $_.name -eq "deno-x86_64-pc-windows-msvc.zip" } |
            Select-Object -First 1

    $Zip = Join-Path $Runtime $Asset.name
    $ShaFile = "$Zip.sha256"

    Invoke-WebRequest $Asset.browser_download_url -OutFile $Zip
    Invoke-WebRequest "$($Asset.browser_download_url).sha256sum" -OutFile $ShaFile

    $Expected = (Get-Content $ShaFile).Split(" ")[0]
    $Actual = (Get-FileHash $Zip -Algorithm SHA256).Hash

    if ($Expected -ne $Actual) {
        throw "Deno checksum verification failed"
    }

    Expand-Archive $Zip $DenoRoot -Force
    Remove-Item $Zip, $ShaFile

    $State.deno.version = $DenoTag
    Save-State
}

# =========================
# Ensure yt-cipher (self-updating)
# =========================
if (-not (Test-Path $CipherDir)) {
    & $GitExe clone https://github.com/kikkia/yt-cipher.git $CipherDir
}

Push-Location $CipherDir
& $GitExe fetch origin
$Remote = & $GitExe rev-parse origin/master
$Local  = & $GitExe rev-parse HEAD

if ($Remote -ne $Local) {
    Write-Host "Updating yt-cipher"
    & $GitExe reset --hard origin/master
}

$State."yt-cipher".commit = $Remote
Save-State
Pop-Location

# =========================
# Ensure ejs (pinned)
# =========================
if (-not (Test-Path $EjsDir)) {
    & $GitExe clone https://github.com/yt-dlp/ejs.git $EjsDir
}

Push-Location $EjsDir
& $GitExe fetch origin
$Current = & $GitExe rev-parse HEAD

if ($Current -ne $EjsCommit) {
    Write-Host "Resetting ejs to pinned commit"
    & $GitExe reset --hard $EjsCommit
}

$State.ejs.commit = $EjsCommit
Save-State
Pop-Location

# =========================
# Patch ejs
# =========================
Push-Location $CipherDir
& $DenoExe run --allow-read --allow-write scripts/patch-ejs.ts
Pop-Location

# =========================
# Token resolution
# =========================
if (-not $token) {
    if (-not (Test-Path $Config)) {
        '{ "token": "" }' | Set-Content $Config -Encoding UTF8
        throw "Discord token missing"
    }
    $token = (Get-Content $Config | ConvertFrom-Json).token
    if (-not $token) { throw "Discord token missing" }
}

# =========================
# Start yt-cipher
# =========================
$CipherProc = Start-Process `
    -FilePath $DenoExe `
    -ArgumentList "run --allow-net --allow-read --allow-write server.ts" `
    -WorkingDirectory $CipherDir `
    -PassThru `
    -NoNewWindow

# =========================
# Readiness probe
# =========================
$Deadline = [DateTime]::UtcNow.AddSeconds(30)
while ($true) {
    try {
        Invoke-WebRequest $CipherHealth -UseBasicParsing | Out-Null
        break
    } catch {
        if ([DateTime]::UtcNow -gt $Deadline) {
            $CipherProc.Kill()
            throw "yt-cipher failed to start"
        }
    }
}

# =========================
# Start bot
# =========================
$BotProc = Start-Process `
    -FilePath $BotExe `
    -ArgumentList "--token $token" `
    -PassThru `
    -NoNewWindow

$BotProc.WaitForExit()
$CipherProc.Kill()
