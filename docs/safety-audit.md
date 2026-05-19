# Safety Audit

`tools/check-safety.ps1` is a quick guardrail for detecting forbidden AI-intervention API strings in mod C# source.

It scans `Source/CalradiaStrategicMind/**/*.cs` for direct references to high-risk APIs such as behavior score mutation, party movement commands, army actions, siege actions, Harmony, and reflection.

Run it from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\check-safety.ps1
```

If no forbidden strings are found, it prints:

```text
Safety check passed.
```

If a forbidden string is found, it prints the file, line number, matched pattern, and line text.

Experimental AI influence must be checked explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\check-safety.ps1 -AllowExperimentalAi
```

Default strict mode still fails on forbidden strings in experimental AI files. Experimental mode allows `PartyThinkParams`, `AIBehaviorData`, `AddBehaviorScore`, and `SetBehaviorScore` only in `Behaviors/ExperimentalDefenseScoreInfluenceBehavior.cs`. All other files remain strict.

The script has a narrow explicit allowlist for known false positives:

- `Logging/CsmLogger.cs` uses `System.Reflection` only to find the executing assembly path for logging.
- `DefenseScoreSimulator.cs` contains `PartyThinkParams` only in a diagnostic reason string explaining that no score is inserted.

This script does not replace code review. It is only a fast automated guardrail for catching obvious unsafe API usage before deeper review.

The settings refactor into grouped static classes does not change the safety boundary. The script still checks mod C# source for forbidden AI-intervention API strings.
