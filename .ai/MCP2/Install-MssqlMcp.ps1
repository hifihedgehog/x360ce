Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
Downloads and builds the Azure-Samples SQL-AI-samples `MssqlMcp` project, then installs the built `MssqlMcp.dll` into this repository.

.DESCRIPTION
Clones `https://github.com/Azure-Samples/SQL-AI-samples.git` into a temporary folder, builds:
`SQL-AI-samples\MssqlMcp\dotnet\MssqlMcp`
then copies the resulting `MssqlMcp.dll` into:
`.\ai\MCP\MssqlMcp\bin\MssqlMcp.dll`

This allows MCP server config to reference a stable path:
`.\\.ai\\MCP\\MssqlMcp\\MssqlMcp.dll`

.NOTES
- Requires Git for Windows (git.exe) and .NET SDK.
- Uses Debug configuration to match existing repo config.
#>

function Write-Info {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "[Install-MssqlMcp] $Message"
}

function Stop-MssqlMcpProcesses {
    # Best-effort: kill any running `dotnet` processes that are executing `MssqlMcp.dll`.
    # This prevents file-lock issues while overwriting DLLs during install.

    $targetPath = (Join-Path $repoRoot '.ai\\MCP\\MssqlMcp\\MssqlMcp.dll')

    $wmiName = if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
        'Win32_Process'
    }
    else {
        $null
    }

    if (-not $wmiName) {
        return
    }

    $processes = Get-CimInstance -ClassName $wmiName -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -ieq 'dotnet.exe' -and $_.CommandLine -and ($_.CommandLine -match 'MssqlMcp\.dll')
        }

    foreach ($p in $processes) {
        try {
            Write-Info "Stopping running MssqlMcp instance (PID $($p.ProcessId))"
            Stop-Process -Id $p.ProcessId -Force -ErrorAction Stop
        }
        catch {
            # Best-effort cleanup; continue.
        }
    }
}

function Ensure-Directory {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

$repoRoot = (Resolve-Path -LiteralPath "$PSScriptRoot\..\..").Path

Stop-MssqlMcpProcesses

$sourceRepoUrl = 'https://github.com/Azure-Samples/SQL-AI-samples.git'
# Note: the repo is cloned directly into $tempRoot, so the project path is relative to that root.
$subRepoRelative = 'MssqlMcp\\dotnet\\MssqlMcp'

$installRoot = Join-Path $repoRoot '.ai\\MCP\\MssqlMcp'
$installBin = Join-Path $installRoot 'bin'
$installDll = Join-Path $installBin 'MssqlMcp.dll'

# The MCP config points at this path (not the bin folder):
#   dotnet .\.ai\MCP\MssqlMcp\MssqlMcp.dll
$installConfigDll = Join-Path $installRoot 'MssqlMcp.dll'

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('MssqlMcp-' + [System.Guid]::NewGuid().ToString('N'))

Write-Info "Repo root: $repoRoot"
Write-Info "Temp folder: $tempRoot"

try {
    Ensure-Directory -Path $tempRoot

    $git = (Get-Command git -ErrorAction SilentlyContinue)
    if (-not $git) {
        throw 'git is required to download SQL-AI-samples. Install Git for Windows and ensure it is on PATH.'
    }

    Write-Info "Cloning $sourceRepoUrl ..."
    & git clone --depth 1 $sourceRepoUrl $tempRoot | Out-Null

    $projectDir = Join-Path $tempRoot $subRepoRelative
    if (-not (Test-Path -LiteralPath $projectDir)) {
        throw "Expected project directory not found: $projectDir"
    }

    Write-Info "Building: $projectDir"
    & dotnet publish $projectDir -c Debug -r win-x64 --self-contained false | Out-Host

    $publishDir = Join-Path $projectDir 'bin\\Debug\\net8.0\\win-x64\\publish\\'

    if (-not (Test-Path -LiteralPath $publishDir)) {
        throw "Publish output folder not found: $publishDir"
    }

    $builtDll = Join-Path $publishDir 'MssqlMcp.dll'
    if (-not (Test-Path -LiteralPath $builtDll)) {
        throw "Build output not found: $builtDll"
    }

    Ensure-Directory -Path $installBin

    Write-Info "Copying to: $installDll"
    Copy-Item -LiteralPath $builtDll -Destination $installDll -Force

    $builtRuntimeConfig = Join-Path $publishDir 'MssqlMcp.runtimeconfig.json'
    if (-not (Test-Path -LiteralPath $builtRuntimeConfig)) {
        throw "Build output not found: $builtRuntimeConfig"
    }

    $builtDeps = Join-Path $publishDir 'MssqlMcp.deps.json'
    if (-not (Test-Path -LiteralPath $builtDeps)) {
        throw "Build output not found: $builtDeps"
    }

    Write-Info "Copying to: $installConfigDll"
    Copy-Item -LiteralPath $builtDll -Destination $installConfigDll -Force

    $installConfigRuntimeConfig = Join-Path $installRoot 'MssqlMcp.runtimeconfig.json'
    Write-Info "Copying to: $installConfigRuntimeConfig"
    Copy-Item -LiteralPath $builtRuntimeConfig -Destination $installConfigRuntimeConfig -Force

    $installConfigDeps = Join-Path $installRoot 'MssqlMcp.deps.json'
    Write-Info "Copying to: $installConfigDeps"
    Copy-Item -LiteralPath $builtDeps -Destination $installConfigDeps -Force

    # Copy any additional runtime dependencies that live next to the published DLL.
    # Without these, `dotnet <dll>` may fail at startup (e.g., missing Microsoft.Extensions.* assemblies).
    Write-Info "Copying publish folder dependencies into: $installRoot"
    Get-ChildItem -LiteralPath $publishDir -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $installRoot $_.Name) -Force
    }

    Write-Info 'Done.'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Write-Info "Cleaning temp folder: $tempRoot"
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
