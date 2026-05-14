# AGENTS.md

Project: Bannerlord mod `CalradiaStrategicMind`.

Language: C#.

Main principle: make minimal, safe changes.

## Rules For Future Codex Tasks

- Do not break mod loading.
- Do not commit Bannerlord game DLLs.
- Do not add Harmony without a separate task that explains why it is required.
- Do not add MCM without a separate task that explains why it is required.
- All new systems must use null checks around Bannerlord campaign objects.
- All risky operations must run through `SafeExecutor`.
- Keep changes small and testable.
- After changes, always show the list of changed files.
- If the build cannot be checked because local Bannerlord DLLs are missing, say that directly.
