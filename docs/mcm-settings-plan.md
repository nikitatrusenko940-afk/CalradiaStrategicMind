# MCM Settings Plan

## Current State

- Settings are currently static code settings.
- MCM is not connected.
- No config file exists.
- `DryRunDefenseSettings` is only a compatibility wrapper.
- Real defense controller is disabled by default.

## Future MCM Groups

### Defense Diagnostics

#### EnableDefenseDiagnostics

- Default: `true`.
- Description: Enables the read-only settlement defense diagnostic pipeline logs.
- Warning: Disabling this should stop defense diagnostics from logging, but must not change AI behavior.

#### EnableVerboseDefenseLogs

- Default: `false`.
- Description: Enables detailed diagnostic logs for threat, value, priority, coverage, need, action planning, and stability.
- Warning: This can increase log volume. It must remain read-only.

#### EnableDefenseCandidateLogs

- Default: `false`.
- Description: Enables `Observed defense candidate` logs for candidate diagnostics.
- Warning: Candidate evaluation must remain diagnostic and must not reserve, move, or command parties.

#### EnableDefenseSummaryLogs

- Default: `true`.
- Description: Enables compact `Observed defense summary` logs.
- Warning: Summary logging must not change pipeline decisions or game state.

### Dry Run

#### EnableDryRunDefenseController

- Default: `true`.
- Description: Enables dry-run defense controller evaluation and `Observed dry-run defense decision` logs.
- Warning: Dry-run decisions must not execute commands or change AI behavior.

#### EnableDefenseActionHistory

- Default: `true`.
- Description: Enables runtime-only action-plan history and stability diagnostics.
- Warning: History must remain runtime-only until a separate save/load task is approved.

#### EnableDryRunDecisionHistory

- Default: `true`.
- Description: Enables runtime-only dry-run decision history and `Observed dry-run defense stability` logs.
- Warning: Stability history must not become a trigger for real execution by itself.

#### EnableDryRunDailyReport

- Default: `true`.
- Description: Enables compact runtime daily dry-run report logs.
- Warning: Report aggregation must remain diagnostic-only.

### Real Controller Safety

#### EnableRealDefenseController

- Default: `false`.
- Description: Enables the real defense controller scaffold switch.
- Warning: This must remain false until a separate real-controller implementation task is approved.
- Warning: Enabling this must not bypass `DefenseControllerSafetyGuard`.
- Warning: Enabling this must not make `WasExecuted=true` by itself.

### Score Simulation

#### EnableDefenseScoreSimulation

- Default: `true`.
- Description: Enables diagnostic-only hypothetical defense score simulation logs.
- Warning: Score simulation must not write to vanilla AI scoring or behavior state.

#### EnableDefenseScoreSimulationSummary

- Default: `true`.
- Description: Enables compact diagnostic-only score simulation summary logs.
- Warning: Summary aggregation must not write to vanilla AI scoring or behavior state.

## MCM Implementation Rules

- MCM must be added in a separate task.
- MCM dependency must be optional or clearly documented.
- MCM must not change AI behavior by itself.
- MCM must not enable real execution by default.
- Existing static defaults must remain safe.
- Every setting must preserve safety audit compatibility.

## Forbidden in MCM Task

- Implementing real party movement.
- Calling `SetMove...`.
- Writing to `PartyThinkParams`.
- Calling `AddBehaviorScore`.
- Calling `SetBehaviorScore`.
- Calling `SetPartyAiAction`.
- Changing army, siege, or kingdom behavior.
- Adding Harmony patches.
- Enabling real controller by default.

## Recommended MCM Order

1. Diagnostics settings only.
2. Dry-run settings.
3. Score simulation settings.
4. Controller safety setting last.
5. Real execution settings only after separate approval.
