<#
.SYNOPSIS
    Export diffs and pre/post content for the current work item branch in a single run.

.DESCRIPTION
    This single script resets previous artifacts (optional), detects base/head refs,
    exports per-file diffs, base and current versions, and consolidates all diffs
    plus before/after content snapshots for review by AI agents.

.PARAMETER RepoPath
    Local Git repository path. Defaults to repo root containing the .ai folder.

.PARAMETER BaseRef
    Base ref, e.g. 'origin/master'. If omitted, determined from pr/context.json or origin/master|main.

.PARAMETER HeadRef
    Head/feature ref, e.g. 'origin/feature/XYZ'. If omitted, uses current branch: 'origin/<branch>'.

.PARAMETER MaxFileBytes
    Skip files larger than this size (default: 10MB).

.PARAMETER ExcludeDotFolders
    Exclude files under any folder starting with a dot (e.g., .github, .vscode, .ai). Enabled by default; pass -ExcludeDotFolders:$false to include them.

.NOTES
    This script always resets .ai\Temp\pr and enables rename detection in diffs and changed-files.

.EXAMPLE
    .\.ai\Scripts\export_diffs_and_content.ps1
    .\.ai\Scripts\export_diffs_and_content.ps1 -BaseRef origin/master
    .\.ai\Scripts\export_diffs_and_content.ps1 -BaseRef origin/develop -HeadRef origin/feature/my-branch
    .\.ai\Scripts\export_diffs_and_content.ps1 -ExcludeDotFolders:$false
#>

[CmdletBinding()]
param(
    [string]$RepoPath,
    [string]$BaseRef,
    [string]$HeadRef,
    [int]$MaxFileBytes = 10485760,
    [switch]$ExcludeDotFolders = $true
)

$ErrorActionPreference = 'Stop'

Write-Host "=== Export Diffs & Content (Single Script) ===" -ForegroundColor Cyan

# Resolve root and default RepoPath
$Root = Split-Path $PSScriptRoot -Parent
if (-not $RepoPath) { $RepoPath = [System.IO.Path]::GetFullPath((Join-Path $Root '..')) }

# Output paths under .ai\Temp\pr
$TempDir     = Join-Path $Root 'Temp'
$PrDir       = Join-Path $TempDir 'pr'
$WorkspaceDir= Join-Path $PrDir 'workspace'
$DiffsDir    = Join-Path $PrDir 'diffs'
$ChangesDir  = Join-Path $PrDir 'changes'
$BaseDir     = Join-Path $PrDir 'base'
$MetaFile    = Join-Path $PrDir 'meta.json'
$ContextFile = Join-Path $PrDir 'context.json'
$AllDiffs    = Join-Path $PrDir 'all-diffs.txt'
$AllPre      = Join-Path $PrDir 'all-pre-content.txt'
$AllPost     = Join-Path $PrDir 'all-post-content.txt'
$ChangedList = Join-Path $DiffsDir 'changed-files.txt'

Write-Host "RepoPath: $RepoPath" -ForegroundColor Gray
Write-Host "Output root: $PrDir" -ForegroundColor Gray
Write-Host ""

# Always reset previous artifacts
Write-Host "Resetting previous artifacts..." -ForegroundColor Yellow
$itemsToRemove = @()
foreach ($p in @($DiffsDir,$ChangesDir,$BaseDir,$MetaFile,$ContextFile,$AllDiffs,$AllPre,$AllPost)) {
    if (Test-Path $p) { $itemsToRemove += $p }
}
foreach ($item in $itemsToRemove) {
    try {
        Remove-Item $item -Recurse -Force -ErrorAction Stop
        Write-Host "  Removed: $item" -ForegroundColor Green
    } catch {
        Write-Warning "Failed to remove $item : $_"
    }
}

# Ensure output directories
New-Item -ItemType Directory -Force -Path $PrDir,$DiffsDir,$ChangesDir,$BaseDir | Out-Null

# Verify git repository
if (-not (Test-Path (Join-Path $RepoPath '.git'))) {
    throw "Not a Git repository: $RepoPath"
}

Push-Location $RepoPath
try {
    # Determine BaseRef and HeadRef
    $branchFromContext = $null
    $targetFromContext = $null

    # Prefer context.json in .ai\Temp\pr, fallback to legacy .ai\pr
    $ContextFileToUse = $ContextFile
    $LegacyPrDir      = Join-Path $Root 'pr'
    $LegacyContext    = Join-Path $LegacyPrDir 'context.json'
    if (-not (Test-Path $ContextFileToUse) -and (Test-Path $LegacyContext)) {
        $ContextFileToUse = $LegacyContext
    }

    if (Test-Path $ContextFileToUse) {
        try {
            $ctx = Get-Content $ContextFileToUse -Raw | ConvertFrom-Json
            $branchFromContext = $ctx.BranchName
            $targetFromContext = $ctx.TargetBranchName
            Write-Host "Loaded branch context from: $ContextFileToUse" -ForegroundColor Green
        } catch {
            Write-Warning "Failed to parse context.json: $_"
        }
    }

    if (-not $HeadRef) {
        if ($branchFromContext) {
            $HeadRef = "origin/$branchFromContext"
        } else {
            $currentBranch = (& git rev-parse --abbrev-ref HEAD).Trim()
            if (-not $currentBranch) { throw "Failed to detect current branch" }
            $HeadRef = "origin/$currentBranch"
        }
    }

    if (-not $BaseRef) {
        if ($targetFromContext) {
            $BaseRef = "origin/$targetFromContext"
        } else {
            # Prefer origin/master else origin/main (robust detection without erroring on stderr)
            $BaseRef = $null
            foreach ($candidate in @('origin/master','origin/main')) {
                & git cat-file -e "$candidate^{commit}" 2>$null
                if ($LASTEXITCODE -eq 0) { $BaseRef = $candidate; break }
            }
            if (-not $BaseRef) {
                Write-Warning "Unable to auto-detect base ref from origin/master or origin/main."
                throw "Failed to determine base ref. Provide -BaseRef explicitly."
            }
        }
    }

    Write-Host "Base ref: $BaseRef" -ForegroundColor White
    Write-Host "Head ref: $HeadRef" -ForegroundColor White
    Write-Host ""

    # Fetch refs to ensure availability
    foreach ($ref in @($BaseRef,$HeadRef)) {
        $branchName = ($ref -replace '^origin/','')
        Write-Host "Fetching origin/$branchName..." -ForegroundColor Yellow
        git fetch origin $branchName
        if ($LASTEXITCODE -ne 0) { throw "Failed to fetch $ref" }
    }

    # Changed files with status
    $statusArgs = @('diff','--name-status','--find-renames',"$BaseRef...$HeadRef")
    $statusEntriesRaw = & git @statusArgs | Where-Object { $_ -match '\S' }

    $statusEntries = $statusEntriesRaw
    if ($ExcludeDotFolders) {
        # Exclude any file under a folder starting with '.' (e.g., .github, .vscode, .ai)
        $dotFolderRegex = '(^|[\\/])\.[^\\/]+([\\/]|$)'
        $statusEntries = $statusEntriesRaw | ForEach-Object {
            $parts = $_ -split "`t"
            $path = if ($parts.Length -gt 1) { $parts[-1] } else { $parts[0] }
            if ($path -notmatch $dotFolderRegex) { $_ }
        }
        $rawCount = ($statusEntriesRaw | Measure-Object).Count
        $filteredCount = ($statusEntries | Measure-Object).Count
        if ($rawCount -ne $filteredCount) {
            Write-Host "Excluded $($rawCount - $filteredCount) file(s) under dot-folders" -ForegroundColor Gray
        }
    }

    if (-not $statusEntries) {
        Write-Host "No files changed between $BaseRef and $HeadRef" -ForegroundColor Yellow
        # Clear outputs
        Remove-Item -Path $AllDiffs,$AllPre,$AllPost,$ChangedList -Force -ErrorAction SilentlyContinue
        exit 0
    }

    $statusEntries | Set-Content -Path $ChangedList -Encoding UTF8
    Write-Host "Changed files list: $ChangedList" -ForegroundColor Green

    # Extract file paths
    $files = $statusEntries | ForEach-Object {
        $parts = $_ -split "`t"
        if ($parts.Length -gt 1) { $parts[-1] } else { $parts[0] }
    }
    Write-Host "Found $($files.Count) changed file(s)" -ForegroundColor Green
    Write-Host ""

    $exported = 0
    $skipped  = 0

    foreach ($file in $files) {
        Write-Host "Processing: $file" -ForegroundColor Gray

        # Size check on working tree file
        if (Test-Path $file) {
            $fileSize = (Get-Item $file).Length
            if ($fileSize -gt $MaxFileBytes) {
                Write-Host "  Skipped (size: $fileSize bytes > $MaxFileBytes)" -ForegroundColor Yellow
                $skipped++
                continue
            }
        }

        # Export current (head) version
        $destPath = Join-Path $ChangesDir $file
        $destDir  = Split-Path $destPath -Parent
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Force -Path $destDir | Out-Null }
        if (Test-Path $file) {
            Copy-Item -Path $file -Destination $destPath -Force
            Write-Host "  Exported current version" -ForegroundColor Green
        } else {
            Write-Host "  File deleted in head" -ForegroundColor Yellow
        }

        # Export base version
        $baseDestPath = Join-Path $BaseDir $file
        $baseDestDir  = Split-Path $baseDestPath -Parent
        if (-not (Test-Path $baseDestDir)) { New-Item -ItemType Directory -Force -Path $baseDestDir | Out-Null }
        try {
            $baseContent = git show "${BaseRef}:$file" 2>$null
            if ($LASTEXITCODE -eq 0 -and $baseContent -ne $null) {
                $baseContent | Set-Content -Path $baseDestPath -Encoding UTF8
                Write-Host "  Exported base version" -ForegroundColor Green
            } else {
                Write-Host "  Base version unavailable (added file?)" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "  Base export error: $($_.Exception.Message)" -ForegroundColor Yellow
        }

        # Export unified diff patch
        $patchPath = Join-Path $DiffsDir ($file + '.patch')
        $patchDir  = Split-Path $patchPath -Parent
        if (-not (Test-Path $patchDir)) { New-Item -ItemType Directory -Force -Path $patchDir | Out-Null }
        $diffPatchArgs = @('diff','--unified=3','--find-renames',"$BaseRef...$HeadRef",'--',$file)
        & git @diffPatchArgs | Set-Content -Path $patchPath -Encoding UTF8
        Write-Host "  Exported diff patch" -ForegroundColor Green

        $exported++
    }

    # Consolidate diffs
    Remove-Item -Path $AllDiffs,$AllPre,$AllPost -Force -ErrorAction SilentlyContinue
    Get-ChildItem -Path $DiffsDir -Filter '*.patch' -Recurse | ForEach-Object {
        $relativePath = $_.FullName.Substring($DiffsDir.Length + 1).Replace('\','/')
        Add-Content -Path $AllDiffs -Value "//// FILE: $relativePath" -Encoding UTF8
        Add-Content -Path $AllDiffs -Value "//// -------" -Encoding UTF8
        Get-Content -Path $_.FullName -Encoding UTF8 | Add-Content -Path $AllDiffs -Encoding UTF8
        Add-Content -Path $AllDiffs -Value "" -Encoding UTF8
    }
    Write-Host "Generated diffs file: $AllDiffs" -ForegroundColor Green

    # Consolidate pre/post content using $ChangedList
    $filesForContent = Get-Content -Path $ChangedList -Encoding UTF8 | ForEach-Object {
        $parts = ($_ -split '\s+',2)
        if ($parts.Count -gt 1) { $parts[1].Trim() } else { $parts[0].Trim() }
    }
    foreach ($f in $filesForContent) {
        Add-Content -Path $AllPre  -Value "//// FILE: $f" -Encoding UTF8
        Add-Content -Path $AllPre  -Value "//// BEFORE" -Encoding UTF8
        $bPath = Join-Path $BaseDir $f
        if (Test-Path $bPath) {
            Get-Content -Path $bPath -Encoding UTF8 | Add-Content -Path $AllPre -Encoding UTF8
        } else {
            Add-Content -Path $AllPre -Value "[File missing or new]" -Encoding UTF8
        }
        Add-Content -Path $AllPre -Value "" -Encoding UTF8

        Add-Content -Path $AllPost -Value "//// FILE: $f" -Encoding UTF8
        Add-Content -Path $AllPost -Value "//// AFTER" -Encoding UTF8
        $pPath = Join-Path $ChangesDir $f
        if (Test-Path $pPath) {
            Get-Content -Path $pPath -Encoding UTF8 | Add-Content -Path $AllPost -Encoding UTF8
        } else {
            Add-Content -Path $AllPost -Value "[Deleted in feature branch]" -Encoding UTF8
        }
        Add-Content -Path $AllPost -Value "" -Encoding UTF8
    }
    Write-Host "Generated pre-content file: $AllPre" -ForegroundColor Green
    Write-Host "Generated post-content file: $AllPost" -ForegroundColor Green
    # ---- Artifact size summary & AI advisory ----
    if (-not (Get-Command Format-Size -ErrorAction SilentlyContinue)) {
        function Format-Size {
            param([long]$bytes)
            if ($bytes -ge 1MB) { return "$([math]::Round($bytes/1MB,2)) MB" }
            elseif ($bytes -ge 1KB) { return "$([math]::Round($bytes/1KB,2)) KB" }
            else { return "$bytes bytes" }
        }
    }

    $diffBytes = (Get-Item $AllDiffs).Length
    $preBytes  = (Get-Item $AllPre).Length
    $postBytes = (Get-Item $AllPost).Length

    Write-Host ""
    Write-Host "Artifacts summary (file sizes):" -ForegroundColor Cyan
    Write-Host ("  all-diffs.txt:        {0}" -f (Format-Size $diffBytes)) -ForegroundColor Gray
    Write-Host ("  all-pre-content.txt:  {0}" -f (Format-Size $preBytes)) -ForegroundColor Gray
    Write-Host ("  all-post-content.txt: {0}" -f (Format-Size $postBytes)) -ForegroundColor Gray

    # Advisory to prevent AI context overload
    Write-Host ""
    Write-Host "Advisory: If your AI context window cannot accommodate these consolidated files," -ForegroundColor Yellow
    Write-Host "          prefer reading per-file patches under: $DiffsDir" -ForegroundColor Yellow
    Write-Host "          or use the changed-files list: $ChangedList to focus only on relevant files." -ForegroundColor Yellow

    Write-Host ""
    Write-Host "=== Export Complete ===" -ForegroundColor Cyan
    Write-Host "Exported: $exported file(s)" -ForegroundColor Green
    Write-Host "Skipped:  $skipped file(s)" -ForegroundColor Yellow

} finally {
    Pop-Location
}