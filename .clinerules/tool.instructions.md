## Tools

### Diff Export

**.ai\Scripts\export_diffs_and_content.ps1**: Get all changes made relative to the base branch. Run this script from the repo root and review the generated files under `.ai/Temp/pr/` when continuing work on a non-master branch and work item requires prior change analysis.

Outputs:

    - `.ai/Temp/pr/all-diffs.txt`: consolidated diffs.
    - `.ai/Temp/pr/all-pre-content.txt`: consolidated pre-content.
    - `.ai/Temp/pr/all-post-content.txt`: consolidated post-content.

## Environment

Terminal sessions use PowerShell by default; always invoke scripts directly from the repository root.

### Forbidden PowerShell Host Prefixes

Do not prefix any command or script execution with `pwsh`, `powershell`, or `powershell.exe`, and do not use host-wrapper flags such as `-Command`, `-File`, `-NoProfile`, or `-ExecutionPolicy`. Commands must be provided as native PowerShell statements, and scripts must be invoked directly.

Examples:

```powershell
# WRONG (host prefix + host wrapper flags)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\.ai\Scripts\Start-Local.ps1 MONITOR
powershell -Command "Get-ChildItem"

# VALID (direct invocation)
.\.ai\Scripts\Start-Local.ps1 MONITOR

# VALID (native PowerShell statement)
Get-ChildItem -Path . -Recurse -Filter *.csproj -File
```
