# Defense AI Pipeline

This document describes the current defense AI diagnostic pipeline in `CalradiaStrategicMind`.

Current state:
- The system is diagnostic/read-only only.
- It does not move parties.
- It does not change lord AI.
- It does not change army AI.
- It does not change settlement behavior.
- It does not change kingdom diplomacy.
- It does not issue orders.
- It only logs observations and diagnostic recommendations.

## Pipeline

`PartyStrength` -> `PartyClassifier` -> `SettlementThreat` -> `SettlementValue` -> `DefensePriority` -> `DefenseCandidates` -> `DefenseCoverage` -> `DefenseNeed` -> `DefenseEvaluationSnapshot` -> `DefenseActionPlan` -> `DefenseActionPlanHistory` -> `DefenseDiagnosticsSummary` -> `DryRunDefenseController` -> `DryRunDecisionHistory` -> `DryRunDefenseReportAggregator` -> `DefenseControllerScaffold` -> `DefenseControllerSafetyGuard` -> `DefenseCommandInterface` -> `DefenseScoreSimulation` -> `DefenseScoreSimulationDailySummary` -> `ExperimentalDefenseScoreInfluence`

## Layers

### PartyStrength

Class: `PartyStrengthEvaluator`

Calculates approximate party strength from troops, wounded troops, and leader level.

Does not change party state, movement, combat behavior, recruitment, or AI decisions.

Provides strength numbers to party observation, settlement threat evaluation, settlement value evaluation, and defense candidate scoring.

### PartyClassifier

Class: `PartyClassifier`

Classifies mobile parties into strategic categories such as lord party, army leader party, garrison, caravan, villager, militia, bandit, and other.

Does not change party type, army membership, or AI behavior.

Provides category data to party observation, settlement threat evaluation, and defense candidate selection.

### SettlementThreat

Class: `SettlementThreatEvaluator`

Calculates settlement threat diagnostics, including active siege, siege threat score, army siege threat, regional enemy pressure, enemy lord pressure, nearby enemy army leader parties, nearby army member parties, and nearby lone lord parties.

Does not start or stop sieges, move defenders, alter garrisons, or change lord/army decisions.

Provides threat data to defense priority, defense coverage, diagnostics summary, and logs.

### SettlementValue

Class: `SettlementValueEvaluator`

Calculates strategic settlement value from settlement type, prosperity, and garrison strength.

Does not change prosperity, ownership, construction, settlement behavior, or economy.

Provides strategic value to defense priority and logs.

### DefensePriority

Class: `DefensePriorityEvaluator`

Combines threat and settlement value into a read-only defense priority. It distinguishes active siege, direct army siege threat, army presence, and regional pressure.

Does not request help, move parties, alter settlement behavior, or issue defense orders.

Provides priority, request recommendation diagnostics, threat components, and reason text to candidate logging filters, coverage, need, action planning, summary, and logs.

### DefenseCandidates

Class: `DefenseCandidateSelector`

Finds friendly strategic parties that could theoretically help defend a settlement. It evaluates strength, distance, army leadership, army membership, wounded ratio, weak parties, busy parties, availability, and suitability.

Does not assign parties, redirect armies, detach army members, or issue orders.

Provides suitable candidate diagnostics to coverage, action planning, and logs.

### DefenseCoverage

Class: `DefenseCoverageEvaluator`

Evaluates whether current and potential defense appears sufficient against relevant threat. It combines garrison strength, nearby friendly strength, and suitable candidate strength against direct siege threat plus softened regional pressure.

Does not reinforce settlements or change any party assignments.

Provides coverage ratio, reinforcement need, direct siege threat flags, army presence flags, regional pressure flags, and reason text to defense need, summary, and logs.

### DefenseNeed

Class: `DefenseNeedEvaluator`

Combines defense priority and coverage into a final diagnostic need: `None`, `Monitor`, `Reinforce`, or `UrgentDefense`.

Does not execute the recommended action.

Provides recommended action, needs-defense-action flag, and reason text to action planning, stability history, summary, and logs.

### DefenseActionPlan

Class: `DefenseActionPlanner`

Creates a diagnostic action plan from a `DefenseEvaluationSnapshot`. It selects suitable candidates for diagnostic planning, chooses a primary candidate, totals selected strength, and calculates plan confidence.

Does not command the selected candidate, reserve the candidate, move the candidate, or alter AI.

Provides candidate selection diagnostics and plan confidence to history, summary, and logs.

### DefenseActionPlanHistory

Classes: `DefenseActionPlanHistory`, `DefenseActionPlanHistoryEntry`, `DefenseActionPlanStabilityReport`

Tracks recent runtime-only action plan history per settlement name. It stores up to five entries and evaluates stability, escalation, and deescalation.

Does not persist data to savegames yet and does not trigger AI actions.

Provides stable action diagnostics, escalation/deescalation flags, and reason text to summary and logs.

### DefenseDiagnosticsSummary

Classes: `DefenseDiagnosticsSummary`, `DefenseDiagnosticsSummaryBuilder`

Builds a short human-readable diagnostic summary from the snapshot, action plan, and stability report.

Does not change any game state or execute any action.

Provides a compact log line so future debugging does not require reading every detailed diagnostic line.

### DryRunDefenseController

Classes: `DryRunDefenseController`, `DryRunDefenseDecision`

Reads the diagnostics summary, action plan, and stability report, then logs what a future defense controller would do.

Does not change AI, does not move parties, does not issue orders, does not request reinforcements, and does not alter any game state.

Provides dry-run decision diagnostics such as `WouldAct`, `WouldMonitor`, `WouldRequestReinforcement`, and `WouldRequestUrgentDefense`.

### DryRunDecisionHistory

Classes: `DryRunDefenseDecisionHistory`, `DryRunDefenseDecisionHistoryEntry`, `DryRunDefenseDecisionStabilityReport`

Tracks recent runtime-only dry-run decisions per settlement name. It stores up to five entries and evaluates whether dry-run actions, monitor signals, and would-act signals are stable.

Does not persist data to savegames, does not issue orders, does not request reinforcements, and does not change AI.

Provides dry-run stability diagnostics to logs so future AI integration can avoid reacting to a single random tick.

### DryRunDefenseReportAggregator

Classes: `DryRunDefenseReportAggregator`, `DryRunDefenseDailyReport`

Collects a short runtime-only daily summary of dry-run decisions for settlements processed during the current observation tick. It counts ignore, monitor, wait, reinforcement, urgent defense, would-act, and stable-signal diagnostics.

Does not persist data to savegames, does not issue orders, does not request reinforcements, and does not change AI.

Provides one compact daily dry-run report log line for short-log mode.

### DefenseControllerScaffold

Classes: `DefenseController`, `DefenseControllerDecision`

Evaluates the future real defense controller boundary after dry-run decision stability and report aggregation.

The real controller is disabled by default through `DefenseControllerSettings.EnableRealDefenseController = false`.

When disabled, it only logs `Action="Disabled"`, `WouldExecute=false`, and the reason `Real defense controller disabled`.

When enabled in code, it is still scaffold-only and returns `Action="SafetyBlocked"` with `WouldExecute=false`.

It does not issue orders, does not move parties, does not change armies, settlements, kingdoms, diplomacy, or AI behavior.

Provides the `Observed defense controller scaffold` short-log line.

### DefenseControllerSafetyGuard

Classes: `DefenseControllerSafetyGuard`, `DefenseControllerSafetyReport`

Evaluates whether a future real defense controller would pass basic safety gates.

The guard checks that the real controller is enabled, controller execution is not blocked, dry-run requests an action, dry-run has a stable would-act signal, the dry-run action is executable, and a primary candidate exists.

`Allowed=true` is only a diagnostic signal. It does not execute an action, does not issue orders, does not move parties, and does not change armies, settlements, kingdoms, diplomacy, or AI behavior.

Provides the `Observed defense controller safety` short-log line.

### DefenseCommandInterface

Classes: `DefenseCommandInterface`, `DefenseCommandReport`

Provides a future command boundary for defense controller actions.

The command interface currently does not execute actions. `RequestReinforcement` only reports whether the safety guard blocked or allowed the diagnostic command.

`WasExecuted` is always `false`, even when `IsAllowed=true`.

It does not issue orders, does not move parties, does not change party AI, armies, settlements, kingdoms, diplomacy, or any other game state.

Provides the `Observed defense command` short-log line.

### DefenseScoreSimulation

Classes: `DefenseScoreSimulator`, `DefenseScoreSimulationReport`

Calculates a hypothetical defense score from existing diagnostic fields only.

The formula is `DefensePriority * 0.6 + PlanConfidence * 0.4`, clamped to `0..100`.

It does not create `AIBehaviorData`, does not access `PartyThinkParams`, does not call `AddBehaviorScore` or `SetBehaviorScore`, does not call `SetPartyAiAction`, and does not move parties.

`WouldAddScore` is always `false`; this layer is simulation only.

Provides the `Observed defense score simulation` short-log line.

### DefenseScoreSimulationDailySummary

Classes: `DefenseScoreSimulationSummaryBuilder`, `DefenseScoreSimulationDailySummary`

Collects runtime-only score simulation summaries for the current observation tick.

It counts total score simulations, safety-blocked simulations, unexpected would-add-score signals, max and average hypothetical scores, and the top score settlement/candidate/action.

It does not create `AIBehaviorData`, does not access `PartyThinkParams`, does not call `AddBehaviorScore` or `SetBehaviorScore`, does not call `SetPartyAiAction`, and does not move parties.

Provides the `Observed defense score simulation summary` short-log line.

### ExperimentalDefenseScoreInfluence

Classes: `ExperimentalDefenseScoreInfluenceBehavior`, `ExperimentalDefenseScoreInfluenceRegistry`, `ExperimentalDefenseScoreInfluenceReport`

This is an experimental AI-influencing layer and is disabled by default through `ExperimentalDefenseScoreInfluenceSettings.EnableExperimentalDefenseScoreInfluence = false`.

It records recent score simulation reports in runtime-only memory and, only when explicitly enabled and gated by filters, may add a small `AiBehavior.DefendSettlement` score into live `PartyThinkParams`.

This is not diagnostic-only when enabled. `PartyThinkParams` participates in vanilla AI selection, so the score can indirectly lead vanilla AI to apply a real defend-settlement behavior if it wins selection.

It does not directly call `MobileParty.SetMove...`, `SetPartyAiAction`, target/objective setters, army actions, settlement mutations, kingdom mutations, Harmony, or MCM.

Provides the `Observed experimental defense score influence` log line when an influence attempt is made.

### DefenseEvaluationSnapshot

Classes: `DefenseEvaluationSnapshot`, `DefenseEvaluationSnapshotBuilder`

Groups threat, value, priority, candidates, coverage, and need reports for one settlement evaluation. The snapshot is created before action plan, history, summary, and dry-run evaluation, then passes ready reports to those later diagnostic layers. The builder reduces repeated calculations by passing already computed reports into later evaluators where possible.

Does not cache long-term game state and does not modify AI.

Provides one read-only evaluation bundle to logging, action planning, history, summary, and dry-run evaluation.

## Important Design Decisions

- Lone lord parties are regional pressure, not direct siege threat.
- Army leader parties represent real army-level siege risk.
- Army member parties are not counted as separate armies.
- Active siege is critical threat.
- Monitor action must not trigger escalation.
- Reinforce should require stable repeated need.
- UrgentDefense can escalate immediately.
- Runtime history is not saved yet and resets after game restart.
- Dry-run decision history is runtime-only and resets after game restart.
- Dry-run daily reports are runtime-only summaries for the current observation tick.
- The real defense controller scaffold is disabled by default and remains non-executing even if enabled in code.
- The defense controller safety guard only reports whether future execution would pass safety checks; `Allowed=true` does not execute anything.
- The defense command interface only reports blocked or allowed diagnostic commands; `WasExecuted` remains `false`.
- The defense score simulation calculates only hypothetical scores and never writes to `PartyThinkParams`.
- The defense score simulation daily summary aggregates simulation reports only and never writes to `PartyThinkParams`.
- The experimental defense score influence layer is disabled by default. If enabled, it is real AI influence through `PartyThinkParams`, not a direct movement/order command.

## Current Logs

- `Observed party strength`
- `Observed settlement threat`
- `Observed settlement value`
- `Observed defense priority`
- `Observed defense candidate`
- `Observed defense coverage`
- `Observed defense need`
- `Observed defense action plan`
- `Observed defense action stability`
- `Observed defense summary`
- `Observed dry-run defense decision`
- `Observed dry-run defense stability`
- `Observed dry-run defense daily report`
- `Observed defense controller scaffold`
- `Observed defense controller safety`
- `Observed defense command`
- `Observed defense score simulation`
- `Observed defense score simulation summary`
- `Observed experimental defense score influence`

## Future AI Integration Boundary

Future real AI integration must start only after a separate explicit task.

It should:
- use only stable action or summary data;
- not react to one random tick;
- have a feature flag or setting;
- start in dry-run mode;
- have a complete disable option.

The current dry-run controller is still diagnostic only. It logs what the mod would do in the future, but it does not change AI and does not issue orders.

Until a separate real integration task exists, all defense action outputs are diagnostics only.

## Code Settings

The project currently uses grouped static code settings for defense diagnostics, dry-run behavior, controller scaffolding, and score simulation. These settings are compile-time/static values only. There is no config file and MCM is not connected yet.

This split is preparation for a possible future MCM task, but MCM is not currently integrated.

Classes:
- `DefenseDiagnosticsSettings`: diagnostic and logging switches.
- `DefenseDryRunSettings`: dry-run controller, history, and daily report switches.
- `DefenseControllerSettings`: real controller scaffold switch.
- `DefenseScoreSimulationSettings`: diagnostic score simulation switches.
- `ExperimentalDefenseScoreInfluenceSettings`: disabled-by-default experimental AI influence switches.
- `DryRunDefenseSettings`: temporary compatibility wrapper over the grouped settings classes.

Diagnostics settings:
- `DefenseDiagnosticsSettings.EnableDefenseDiagnostics`: enables or disables the settlement defense diagnostic pipeline logs.
- `DefenseDiagnosticsSettings.EnableVerboseDefenseLogs`: enables or disables long detailed defense logs such as threat, value, priority, coverage, need, action plan, and stability.
- `DefenseDiagnosticsSettings.EnableDefenseCandidateLogs`: enables or disables `Observed defense candidate` logs. Candidate calculations can still be used internally by coverage and action planning.
- `DefenseDiagnosticsSettings.EnableDefenseSummaryLogs`: enables or disables `Observed defense summary` logs.

Dry-run settings:
- `DefenseDryRunSettings.EnableDryRunDefenseController`: enables or disables dry-run decision evaluation and `Observed dry-run defense decision` logs.
- `DefenseDryRunSettings.EnableDefenseActionHistory`: enables or disables runtime action-plan history and stability logging.
- `DefenseDryRunSettings.EnableDryRunDecisionHistory`: enables or disables runtime dry-run decision history and `Observed dry-run defense stability` logs.
- `DefenseDryRunSettings.EnableDryRunDailyReport`: enables or disables the runtime daily dry-run report log.

Controller settings:
- `DefenseControllerSettings.EnableRealDefenseController`: enables or disables the real defense controller scaffold. It defaults to `false`; even when set to `true`, the scaffold does not execute game actions.

Score simulation settings:
- `DefenseScoreSimulationSettings.EnableDefenseScoreSimulation`: enables or disables diagnostic-only hypothetical defense score simulation logs.
- `DefenseScoreSimulationSettings.EnableDefenseScoreSimulationSummary`: enables or disables diagnostic-only score simulation summary logs.

Experimental influence settings:
- `ExperimentalDefenseScoreInfluenceSettings.EnableExperimentalDefenseScoreInfluence`: enables or disables experimental score influence. It defaults to `false`.
- `ExperimentalDefenseScoreInfluenceSettings.RequireSettlementNameFilter`: requires a settlement name filter before experimental influence can run.
- `ExperimentalDefenseScoreInfluenceSettings.SettlementNameFilter`: limits influence to a single settlement name.
- `ExperimentalDefenseScoreInfluenceSettings.MaxScoreBoost`: caps the score boost added to vanilla AI scoring.
- `ExperimentalDefenseScoreInfluenceSettings.MinimumHypotheticalScore`: blocks weak diagnostic signals.
- `ExperimentalDefenseScoreInfluenceSettings.MaxInfluenceAgeTicks`: blocks stale diagnostic reports.

These settings remain a simple bridge toward future configuration. MCM must only be added in a separate explicit task.

## Next Possible Stages

- Stable settlement id instead of settlement name for history.
- Save/load history through `IDataStore` later.
- Config/MCM later.
- Dry-run defense controller.
- Real defense controller only after multiple successful diagnostics.
