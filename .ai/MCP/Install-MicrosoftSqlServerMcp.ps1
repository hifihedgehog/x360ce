Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
Installs prerequisites for the `mssql-readonly` MCP server configuration.

.DESCRIPTION
The `mssql-readonly` MCP server in [`mcp.json`](.roo/mcp.json:46) is configured to run via `uvx`:

  uvx --from microsoft-sql-server-mcp mssql_mcp_server.exe

This script ensures `uvx` is available on PATH by installing **uv** using `winget`.

.NOTES
- Requires Windows and `winget`.
- After installation, restart VS Code (or your shell) so PATH updates take effect.
#>

function Write-Info {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "[Install-MicrosoftSqlServerMcp] $Message"
}

function Assert-CommandAvailable {
    param([Parameter(Mandatory = $true)][string]$Name)
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $cmd) {
        throw "Required command not found on PATH: $Name"
    }
}

function Ensure-UvxAvailable {
    $uvx = Get-Command uvx -ErrorAction SilentlyContinue
    if ($uvx) {
        Write-Info "uvx found: $($uvx.Source)"
        return
    }

    Write-Info 'uvx not found. Installing uv via winget...'
    Assert-CommandAvailable -Name winget

    # Astral's uv provides `uv` and `uvx`.
    & winget install --id Astral-sh.uv --exact --silent --accept-package-agreements --accept-source-agreements | Out-Host

    # Attempt to locate uvx in the current session (PATH changes may require a new shell).
    $uvx = Get-Command uvx -ErrorAction SilentlyContinue
    if (-not $uvx) {
        throw 'Installed uv, but `uvx` is still not available in the current shell. Restart VS Code / terminal and run this script again.'
    }

    Write-Info "uvx installed: $($uvx.Source)"
}

Ensure-UvxAvailable

Write-Info 'Done. `mssql-readonly` can now run via uvx (see .roo/mcp.json).'
