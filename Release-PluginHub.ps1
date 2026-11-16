<#
.SYNOPSIS
    Releases Protogen Systems Docking Solutions to PluginHub (StarCpt/PluginHub).

.DESCRIPTION
    1. Optionally commits your pending changes (-Message) and bumps the version (-Version).
    2. Builds the plugin, runs the tests, and compiles it the way Pulsar does on players' PCs
       (only ClientPlugin + Shared, no LOCAL_BUILD, no MSBuild-generated code).
    3. Pushes main to GitHub.
    4. Writes the hub manifest with the pushed commit into your PluginHub fork, validates it with the
       hub's own test.py, and pushes a branch.
    5. Opens GitHub's "new pull request" page with the title and text filled in, so you only click
       "Create pull request". If a pull request for that branch is already open, it was updated by the push.

    See RELEASING.md for the whole process.

.EXAMPLE
    .\Release-PluginHub.ps1 -Version 1.2.0 -Message "Add night-vision dock camera"
.EXAMPLE
    .\Release-PluginHub.ps1 -DryRun
#>
[CmdletBinding()]
param(
    # New version for ClientPlugin/BuildVersion.cs, e.g. 1.2.0. Leave out to release the current version.
    [string]$Version,
    # Commit all pending changes with this message before releasing. Without it the working tree must be clean.
    [string]$Message,
    # Build, test and write the hub manifest, but commit, push and open nothing.
    [switch]$DryRun,
    # Skip the Tests project (the builds still run).
    [switch]$SkipTests,
    # Print the pull request link instead of opening it in the browser.
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Repo        = $PSScriptRoot
$ManifestSrc = Join-Path $Repo 'ProtogenDockingSolutions.xml'
$VersionFile = Join-Path $Repo 'ClientPlugin\BuildVersion.cs'
$HubDir      = Join-Path $Repo 'artifacts\pluginhub\PluginHub'
$SimDir      = Join-Path $Repo 'artifacts\pulsar-sim'
$HubFile     = 'Plugins/ProtogenDockingSolutions.xml'
$Upstream    = 'StarCpt/PluginHub'
$FriendlyName = 'Protogen Systems Docking Solutions'
$AddBranch   = 'add-protogen-docking-solutions'

function Step([string]$Text) { Write-Host ''; Write-Host "== $Text" -ForegroundColor Cyan }

function Run([string]$Exe, [string[]]$ArgList, [string]$In = $Repo) {
    Push-Location $In
    try {
        & $Exe @ArgList
        if ($LASTEXITCODE -ne 0) { throw "'$Exe $($ArgList -join ' ')' failed with exit code $LASTEXITCODE." }
    } finally { Pop-Location }
}

function Invoke-Git([string[]]$ArgList, [string]$In = $Repo) {
    Push-Location $In
    try {
        $out = & git.exe @ArgList
        if ($LASTEXITCODE -ne 0) { throw "'git $($ArgList -join ' ')' failed with exit code $LASTEXITCODE." }
        return $out
    } finally { Pop-Location }
}

# True when the git command succeeds; its output is discarded. (Windows PowerShell turns redirected stderr
# into errors under ErrorActionPreference=Stop, so this runs with Continue.)
function Test-Git([string[]]$ArgList, [string]$In) {
    $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { & git.exe -C $In @ArgList 2>&1 | Out-Null; return ($LASTEXITCODE -eq 0) } finally { $ErrorActionPreference = $old }
}

function Write-Utf8([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText($Path, $Text, (New-Object Text.UTF8Encoding $false))
}

# ---------------------------------------------------------------- checks
Step 'Checking the repository'
$branch = (Invoke-Git -ArgList @('rev-parse', '--abbrev-ref', 'HEAD')).Trim()
if ($branch -ne 'main') { throw "Check out main first (you are on '$branch'). PluginHub reviews commits on the default branch." }

$ownerRepo = [regex]::Match([IO.File]::ReadAllText($ManifestSrc), '<RepoId>([^<]+)</RepoId>').Groups[1].Value
$owner = $ownerRepo.Split('/')[0]
if (-not $ownerRepo -or $owner -eq 'OWNER') { throw "Set <RepoId> in ProtogenDockingSolutions.xml first." }

$dirty = Invoke-Git -ArgList @('status', '--porcelain')
if ($dirty) {
    if ($Message) {
        if (-not $DryRun) { Invoke-Git -ArgList @('add', '-A') | Out-Null; Invoke-Git -ArgList @('commit', '-q', '-m', $Message) | Out-Null; Write-Host "Committed your changes: $Message" }
        else { Write-Host "Dry run: would commit your changes: $Message" }
    } elseif (-not $DryRun) {
        $dirty | ForEach-Object { Write-Host "  $_" }
        throw 'You have uncommitted changes. Commit them, or pass -Message "what changed" to commit them here.'
    } else { Write-Warning 'Uncommitted changes (ignored in a dry run).' }
}

# ---------------------------------------------------------------- version
$versionText = [IO.File]::ReadAllText($VersionFile)
$current = [regex]::Match($versionText, 'const string Version = "([^"]+)"').Groups[1].Value
if ($Version) {
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must look like 1.2.0 (got '$Version')." }
    if ([version]$Version -le [version]$current) { throw "Version $Version is not newer than the current $current." }
    Step "Bumping the version $current -> $Version"
    if (-not $DryRun) {
        Write-Utf8 $VersionFile ($versionText -replace 'const string Version = "[^"]+"', "const string Version = `"$Version`"")
        Invoke-Git -ArgList @('add', 'ClientPlugin/BuildVersion.cs') | Out-Null
        Invoke-Git -ArgList @('commit', '-q', '-m', "Version $Version") | Out-Null
    }
} else { $Version = $current }
Write-Host "Releasing version $Version"

# ---------------------------------------------------------------- builds
Step 'Building the plugin (local Release build)'
Run 'dotnet' -ArgList @('build', 'ClientPlugin\ClientPlugin.csproj', '-c', 'Release', '-p:RunPostBuildEvent=Never', '-v', 'minimal', '-nologo')

if (-not $SkipTests) {
    Step 'Running the tests'
    Run 'dotnet' -ArgList @('run', '--project', 'Tests', '-c', 'Release')
}

Step 'Compiling the way Pulsar does (ClientPlugin + Shared only, no LOCAL_BUILD)'
# A copy of ClientPlugin.csproj that only compiles what the manifest's SourceDirectories list, without
# LOCAL_BUILD and without the attribute file Krafs.Publicizer adds, like Pulsar's Roslyn build on players' PCs.
New-Item -ItemType Directory -Force $SimDir | Out-Null
$sim = foreach ($line in [IO.File]::ReadAllLines((Join-Path $Repo 'ClientPlugin\ClientPlugin.csproj'))) {
    if ($line -match '<Compile Include=' -and $line -match 'Api') { continue }
    if ($line -match '<Compile Include=' -and $line -match 'Shared') {
        '    <Compile Include="..\..\Shared\**\*.cs" /><Compile Include="..\..\ClientPlugin\*.cs" />'; continue
    }
    $line.Replace(';LOCAL_BUILD', '').
          Replace('Unsafe;IgnoresAccessChecksTo', 'Unsafe').
          Replace('native; contentfiles; analyzers', 'native; analyzers').
          Replace('<RootNamespace>', '<EnableDefaultCompileItems>false</EnableDefaultCompileItems><RootNamespace>')
}
$sim = $sim -join "`r`n"
if ($sim -match 'LOCAL_BUILD' -or $sim -notmatch 'ClientPlugin\\\*\.cs') { throw 'Could not build the Pulsar-style project file; ClientPlugin.csproj changed shape.' }
Write-Utf8 (Join-Path $SimDir 'PulsarSim.csproj') $sim
Run 'dotnet' -ArgList @('build', 'PulsarSim.csproj', '-c', 'Release', '-p:RunPostBuildEvent=Never', '-v', 'minimal', '-nologo') $SimDir

# ---------------------------------------------------------------- push the release
Step 'Pushing main to GitHub'
if (-not $DryRun) {
    Invoke-Git -ArgList @('push', 'origin', 'main') | Out-Null
    $commit = (Invoke-Git -ArgList @('rev-parse', 'HEAD')).Trim()
    $remote = ((Invoke-Git -ArgList @('ls-remote', 'origin', 'refs/heads/main')) -split '\s+')[0]
    if ($remote -ne $commit) { throw "GitHub's main ($remote) is not your HEAD ($commit). Pull or push by hand, then retry." }
} else {
    $commit = (Invoke-Git -ArgList @('rev-parse', 'HEAD')).Trim()
    Write-Host 'Dry run: not pushing.'
}
Write-Host "Release commit: $commit"

# ---------------------------------------------------------------- hub fork
Step "Updating your PluginHub fork ($owner/PluginHub)"
if (-not (Test-Path (Join-Path $HubDir '.git'))) {
    New-Item -ItemType Directory -Force (Split-Path $HubDir) | Out-Null
    Run 'git.exe' -ArgList @('clone', '-q', "https://github.com/$owner/PluginHub.git", $HubDir)
    Invoke-Git -ArgList @('remote', 'add', 'upstream', "https://github.com/$Upstream.git") $HubDir | Out-Null
}
# Commit with the same identity as the plugin repository.
foreach ($key in 'user.name', 'user.email') {
    $value = (& git.exe -C $Repo config $key)
    if ($value) { Invoke-Git -ArgList @('config', $key, $value) $HubDir | Out-Null }
}
Invoke-Git -ArgList @('reset', '-q', '--hard') $HubDir | Out-Null   # the clone is the script's own; drop a dry run's leftovers
Invoke-Git -ArgList @('fetch', '-q', 'upstream', 'main') $HubDir | Out-Null
Invoke-Git -ArgList @('fetch', '-q', 'origin') $HubDir | Out-Null

$listed = Test-Git -ArgList @('cat-file', '-e', "upstream/main:$HubFile") $HubDir
if ($listed) {
    $live = (Invoke-Git -ArgList @('show', "upstream/main:$HubFile") $HubDir) -join "`n"
    if ($live -match "<Commit>$commit</Commit>") { Write-Host 'PluginHub already lists this commit. Nothing to do.'; return }
    $hubBranch = "protogen-docking-solutions-$Version"
    $title = "$FriendlyName $Version"
    Invoke-Git -ArgList @('checkout', '-q', '-B', $hubBranch, 'upstream/main') $HubDir | Out-Null
} else {
    # The first submission isn't merged yet: update its branch, which updates the open pull request.
    $hubBranch = $AddBranch
    $title = "Add $FriendlyName"
    $base = if (Test-Git -ArgList @('rev-parse', '-q', '--verify', "origin/$hubBranch") $HubDir) { "origin/$hubBranch" } else { 'upstream/main' }
    Invoke-Git -ArgList @('checkout', '-q', '-B', $hubBranch, $base) $HubDir | Out-Null
}

# The hub copy of the manifest: no comments, the release commit, each description paragraph on one line, CRLF.
$xml = [IO.File]::ReadAllText($ManifestSrc) -replace "`r`n", "`n"
$xml = [regex]::Replace($xml, '\s*<!--.*?-->', '', 'Singleline')
$xml = [regex]::Replace($xml, '<Commit>[^<]*</Commit>', "<Commit>$commit</Commit>")
$descLength = 0
$xml = [regex]::Replace($xml, '(?s)<Description>(.*?)</Description>', [Text.RegularExpressions.MatchEvaluator] {
    param($m)
    $paras = $m.Groups[1].Value.Trim() -split "`n\s*`n" | ForEach-Object { (($_ -split "`n") | ForEach-Object { $_.Trim() }) -join ' ' }
    $script:descLength = ($paras -join "`n`n").Length
    "<Description>`n" + (($paras | ForEach-Object { "    $_" }) -join "`n`n") + "`n  </Description>"
})
if ($descLength -gt 1000) { throw "The description is $descLength characters; PluginHub allows 1000." }
$xml = ($xml.TrimEnd() + "`n") -replace "`n", "`r`n"
Write-Utf8 (Join-Path $HubDir $HubFile) $xml
Write-Host "Wrote $HubFile (description $descLength characters)"

$python = Get-Command python -ErrorAction SilentlyContinue
if ($python) { Run 'python' -ArgList @('-I', 'test.py', 'Plugins/') $HubDir } else { Write-Warning 'Python not found; skipped the hub validator (test.py). GitHub runs it on the pull request.' }

if ($DryRun) {
    Write-Host ''
    Write-Host "Dry run done. Would push branch '$hubBranch' to $owner/PluginHub and open: $title" -ForegroundColor Green
    return
}

Invoke-Git -ArgList @('add', $HubFile) $HubDir | Out-Null
if (-not (Test-Git -ArgList @('diff', '--cached', '--quiet') $HubDir)) { Invoke-Git -ArgList @('commit', '-q', '-m', $title) $HubDir | Out-Null }
$pushArgs = @('push', '-q', '-u', 'origin', $hubBranch)
if ($listed) { $pushArgs += '--force' }   # a version branch is rebuilt from upstream each run
Invoke-Git -ArgList $pushArgs $HubDir | Out-Null
Write-Host "Pushed branch '$hubBranch'."

# ---------------------------------------------------------------- pull request
Step 'Pull request'
$open = @()
try { $open = @(Invoke-RestMethod "https://api.github.com/repos/$Upstream/pulls?state=open&head=$($owner):$hubBranch") } catch { Write-Warning "Could not ask GitHub for open pull requests: $_" }
if ($open.Count -gt 0) {
    Write-Host "Pull request #$($open[0].number) is open for this branch and now points at $commit :" -ForegroundColor Green
    Write-Host "  $($open[0].html_url)"
    return
}

$previous = if ($listed) { [regex]::Match($live, '<Commit>([0-9a-f]+)</Commit>').Groups[1].Value } else { '' }
$body = @(
    "$FriendlyName $Version.",
    '',
    "- Source: https://github.com/$ownerRepo",
    "- Commit: $commit"
)
if ($previous) { $body += "- Changes since the listed commit: https://github.com/$ownerRepo/compare/$previous...$commit" }
$body = $body -join "`n"
$url = "https://github.com/$Upstream/compare/main...$($owner):PluginHub:$($hubBranch)?expand=1" +
       "&title=$([uri]::EscapeDataString($title))&body=$([uri]::EscapeDataString($body))"
Write-Host 'Opening the pull request page; check it and click "Create pull request":' -ForegroundColor Green
Write-Host "  $url"
if (-not $NoBrowser) { Start-Process $url }
