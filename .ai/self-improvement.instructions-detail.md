## Self-Improvement Detailed Instructions

This file contains detailed instructions for self-improvement. It is read only when the user requests self-improvement to avoid watering down main instructions.

### Editable Instruction Files

You can update your own instruction files:
- `.ai/instructions.md` – the main system instructions file
- `.ai/*instructions.md` – additional instruction files (auto-included)
- `.ai/*instructions-detail.md` – detailed instruction files (read only when needed)

**Do not edit generated instruction files directly.** The following locations are generated/managed outputs and must not be modified by the agent:
- `.roo/rules/` (Roo Code)
- `.github/copilot-instructions.md` (GitHub Copilot)
- `AGENTS.md` (Codex)
If you need to change instructions, edit the master copies in `.ai/` and run the activation step below.

### Single Source of Truth

**NEVER embed template content in instructions - reference template files instead.**

Example:
- ✅ "Template maintained in pr/checklist.template.md"
- ❌ Pasting template content into instructions

### Activation Process

After editing instruction files, run from repository root:

```powershell
.\.ai\Update-AgentInstructions.ps1 AUTO
```

This command updates and activates the modified instruction files in `.roo/rules/`.
