# Decompiled AI Behavior Research

## Goal

Understand whether a future `RequestReinforcementCandidate` action can safely use `PartyThinkParams`, `AIBehaviorData`, or `AiMilitaryBehavior` to add a soft defense score without directly calling movement commands or breaking vanilla AI.

Main question: can a safe soft-score approach be used for `RequestReinforcementCandidate`?

Conclusion: No confirmed safe soft-score path found. Do not implement yet.

## Sources Inspected

- Decompiled file: `D:\BannerlordResearch\TaleWorlds.CampaignSystem.cs`
- Reflection findings from `TaleWorlds.CampaignSystem.dll` were used as supporting context.

Inspected decompiled areas:

- `AiPartyThinkBehavior`
- `AiMilitaryBehavior`
- `AiArmyMemberBehavior`
- `PartyThinkParams`
- `AIBehaviorData`
- `MobilePartyAi`
- `TargetScoreCalculatingModel`
- `DefaultTargetScoreCalculatingModel`
- `MobilePartyAIModel`
- `DefaultMobilePartyAIModel`
- `SetPartyAiAction`

Key line areas inspected:

- `AIBehaviorData` and `PartyThinkParams`: around lines `44190-44390`
- `DefaultMobilePartyAIModel`: around lines `63265-63820`
- `DefaultTargetScoreCalculatingModel`: around lines `70796-70960`
- `AiMilitaryBehavior`: around lines `220388-220960`
- `AiPartyThinkBehavior`: around lines `220967-221150`
- `SetPartyAiAction`: around lines `228160-228370`

## AI Tick Flow

`AiPartyThinkBehavior` owns the main party AI thinking flow.

Observed flow:

- `AiPartyThinkBehavior.RegisterEvents` subscribes to campaign AI ticking through `CampaignEvents`.
- During party AI thinking, `PartyHourlyAiTick` checks whether the party is allowed to make a new decision.
- The party's cached `PartyThinkParams` is reset for the current `MobileParty`.
- `CampaignEventDispatcher.Instance.AiHourlyTick(mobileParty, thinkParamsCache)` is called.
- Other campaign behaviors add behavior scores into `PartyThinkParams`.
- `AiPartyThinkBehavior` iterates over `thinkParamsCache.AIBehaviorScores` and selects the highest score.
- If the selected score passes threshold/random checks and behavior changing is not blocked, `AiPartyThinkBehavior` applies a real party AI action.
- For selected `AiBehavior.DefendSettlement`, it calls `SetPartyAiAction.GetActionForDefendingSettlement(...)`.

Important implication:

Adding a soft score is not just advisory. If the score wins the vanilla selection logic, vanilla applies a real action through `SetPartyAiAction`.

## PartyThinkParams Usage

`PartyThinkParams` is a mutable score container for one AI decision pass.

Observed members:

- `MobilePartyOf`
- `AIBehaviorScores`
- `CurrentObjectiveValue`
- `WillGatherAnArmy`
- `DoNotChangeBehavior`
- `SetArmyMembers(...)`
- `TryGetBehaviorScore(...)`
- `SetBehaviorScore(...)`
- `AddBehaviorScore(...)`

Observed behavior:

- `Reset(mobileParty)` clears previous scores and resets state.
- `Initialization()` calculates faction/clan/army strength values used by AI scoring.
- `AddBehaviorScore` appends an `(AIBehaviorData, score)` tuple.
- `SetBehaviorScore` updates an existing matching `AIBehaviorData` score and asserts if the behavior data is not present.
- Vanilla behaviors use both `AddBehaviorScore` and `SetBehaviorScore` during `AiHourlyTick`.

Risk:

- Mutating `PartyThinkParams` during `AiHourlyTickEvent` is part of the real AI decision path.
- A score inserted here can become the selected behavior for the party.
- This is not diagnostic-only and should be treated as behavior influence, not a harmless request.

## AIBehaviorData Usage

`AIBehaviorData` is a value struct describing one candidate AI behavior.

Observed fields:

- `Party`
- `Position`
- `AiBehavior`
- `WillGatherArmy`
- `IsFromPort`
- `IsTargetingPort`
- `NavigationType`

Relevant behavior values:

- `AiBehavior.DefendSettlement`
- `AiBehavior.BesiegeSettlement`
- `AiBehavior.RaidSettlement`
- `AiBehavior.PatrolAroundPoint`
- `AiBehavior.GoToSettlement`
- `AiBehavior.EscortParty`
- `AiBehavior.EngageParty`

For defense scoring, vanilla creates `AIBehaviorData` with:

- target settlement as `Party`
- `AiBehavior.DefendSettlement`
- selected navigation data
- optional `WillGatherArmy`
- port flags

Important implication:

Creating `AIBehaviorData` is safe as data construction. Adding it to live `PartyThinkParams` is not guaranteed safe because it can result in real AI behavior application.

## DefendSettlement Scoring

`AiMilitaryBehavior` is the main vanilla source for military behavior scores.

Observed defense flow:

- `AiMilitaryBehavior.RegisterEvents` subscribes to `CampaignEvents.AiHourlyTickEvent`.
- Its `AiHourlyTick` filters out militia, caravans, villagers, bandits, patrol parties, disbanding parties, parties without leaders, and invalid faction cases.
- Army members that are not the army leader return early.
- Parties already defending or marked with `PartyObjective.Defensive` get initiative adjustments.
- `PartyThinkParams.Initialization()` is called.
- The behavior loops through `ArmyTypes`, including `ArmyTypes.Defender`.
- For defender mission type, vanilla maps the mission to `AiBehavior.DefendSettlement`.
- `FindBestTargetAndItsValueForFaction` and `CalculateMilitaryBehaviorForSettlement` evaluate suitable settlements.
- Defense scoring requires a relevant `LastAttackerParty`, active threat, faction war, distance viability, food viability, party size, cohesion, and target score.
- If viable, vanilla adds an `AIBehaviorData(settlement, AiBehavior.DefendSettlement, ...)` score to `PartyThinkParams`.

`DefaultTargetScoreCalculatingModel` also has defense-specific logic:

- `GetTargetScoreForFaction(..., ArmyTypes.Defender, ...)`
- `CurrentObjectiveValue(...)`
- `DefendingFactor`
- defense calculations that consider existing defenders and attacker strength.

Important implication:

Vanilla already has a defense scoring path. Adding a mod score would compete directly in this path, not merely "request" help.

## Possible Soft-Score Insertion Points

### CampaignEvents.AiHourlyTickEvent

Potential approach:

- Subscribe a custom campaign behavior to `AiHourlyTickEvent`.
- Inspect `MobileParty` and `PartyThinkParams`.
- Add an `AIBehaviorData` score for `AiBehavior.DefendSettlement`.

Why it is not confirmed safe:

- This is the same score list vanilla uses for real behavior selection.
- If the inserted score wins, `AiPartyThinkBehavior` calls `SetPartyAiAction.GetActionForDefendingSettlement`.
- That action ultimately calls `MobileParty.SetMoveDefendSettlement`.
- Therefore, the soft score can indirectly cause direct movement/AI behavior changes.

Result: Maybe technically possible, but not safe under current constraints.

### Adjust Existing DefendSettlement Score

Potential approach:

- Use `TryGetBehaviorScore` and `SetBehaviorScore` to increase an existing vanilla `DefendSettlement` score for the same settlement.

Why it is not confirmed safe:

- It still changes the real AI selection result.
- If the boosted score wins, vanilla still applies `SetPartyAiAction.GetActionForDefendingSettlement`.
- `SetBehaviorScore` asserts if the behavior data is not already present.
- It depends on exact event ordering and matching all `AIBehaviorData` fields.

Result: Not safe for implementation yet.

### Add Diagnostic-Only Parallel Score

Potential approach:

- Calculate what score would be added, but do not insert it into `PartyThinkParams`.
- Log the hypothetical score.

Why it is safer:

- It does not affect AI selection.
- It preserves the current diagnostic-only project boundary.

Result: Safe as documentation/diagnostics, not a real action.

## Unsafe APIs

Forbidden for the first real defense action:

- `MobileParty.SetMoveDefendSettlement(...)`
- `MobileParty.SetMoveGoToSettlement(...)`
- `MobileParty.SetTargetSettlement(...)`
- `MobileParty.SetPartyObjective(...)`
- `SetPartyAiAction.GetActionForDefendingSettlement(...)`
- `GatherArmyAction.Apply(...)`
- Harmony patching of private methods.
- Reflection calls into private AI methods.

Additional unsafe or high-risk APIs observed:

- `MobileParty.SetMoveBesiegeSettlement(...)`
- `MobileParty.SetMoveEngageParty(...)`
- `MobileParty.SetMoveEscortParty(...)`
- `MobileParty.SetMoveModeHold()`
- Directly setting `DefaultBehavior`, `ShortTermBehavior`, `TargetParty`, `TargetPosition`, `Army`, `AttachedTo`, `CurrentSettlement`, or `Position`.
- `DisbandArmyAction.Apply...`
- `Kingdom.CreateArmy(...)` through AI selection side effects.
- `SiegeEventManager.StartSiegeEvent(...)`
- `SiegeEvent.FinalizeSiegeEvent()`
- `LiftSiegeAction.GetGameAction(...)`
- Direct garrison or troop roster edits.
- Replacing global models such as `TargetScoreCalculatingModel` or `MobilePartyAIModel`.

Reason:

These APIs either directly change party movement/AI state, manipulate armies or sieges, or globally alter vanilla AI behavior.

## Recommendation

No confirmed safe soft-score path found. Do not implement yet.

The decompiled code shows that a score inserted into `PartyThinkParams` is not just a passive request. It participates in vanilla's live AI behavior selection. If `AiBehavior.DefendSettlement` wins, vanilla applies `SetPartyAiAction.GetActionForDefendingSettlement`, which calls into direct party movement/AI state changes.

That means a soft-score approach can indirectly violate the current safety rules even if the mod never calls `MobileParty.SetMoveDefendSettlement` directly.

Safe current option:

- Keep `DefenseCommandInterface` diagnostic-only.
- Use `DefenseScoreSimulation` as a diagnostic-only score calculation without mutating `PartyThinkParams`.
- Use `DefenseScoreSimulationDailySummary` as diagnostic-only aggregation of score simulation reports without mutating `PartyThinkParams`.
- Keep `WouldAddScore=false` and never call `AddBehaviorScore`, `SetBehaviorScore`, `SetPartyAiAction`, or `MobileParty.SetMove...`.

Unsafe current option:

- Adding or boosting `AiBehavior.DefendSettlement` in live `PartyThinkParams`.

## Experimental Defense Score Influence

`ExperimentalDefenseScoreInfluence` intentionally crosses the previous diagnostic-only boundary as an experimental AI feature.

This is real AI influence because it can add an `AiBehavior.DefendSettlement` score into live `PartyThinkParams`. `PartyThinkParams` is consumed by vanilla AI behavior selection, so an inserted score can affect which behavior vanilla chooses.

The feature is disabled by default through `ExperimentalDefenseScoreInfluenceSettings.EnableExperimentalDefenseScoreInfluence = false`.

The implementation does not directly call `MobileParty.SetMove...`, `SetPartyAiAction`, target/objective setters, army actions, settlement mutations, kingdom mutations, Harmony, or MCM.

However, this is not the same as diagnostic-only score simulation. If the experimental score wins vanilla selection, vanilla may indirectly call `SetPartyAiAction.GetActionForDefendingSettlement` and then perform real defend-settlement movement through its own AI path.

Required guardrails:

- The feature must remain disabled by default.
- A settlement name filter is required by default.
- Score boost is capped.
- Weak hypothetical scores are blocked.
- Stale diagnostic reports are blocked.
- Army members that are not army leaders are blocked.
- The safety audit must be run in strict mode and experimental mode so this boundary remains visible.

## Proposed Next Step

- Do not implement `RequestReinforcementCandidate` yet.
- Add documentation-only notes to the existing safety docs if needed.
- Continue with diagnostic-only `DefenseScoreSimulation`, which mirrors score inputs but never writes to `PartyThinkParams`.
- Continue with diagnostic-only `DefenseScoreSimulationDailySummary` for compact observation tick/day review.
- If considering a future prototype, make it blocked-only first:
  - compute the proposed `AIBehaviorData`;
  - compute the proposed score;
  - log why it would or would not be inserted;
  - keep `WasExecuted=false`;
  - do not call `AddBehaviorScore`;
  - do not call `SetBehaviorScore`;
  - do not call any `SetPartyAiAction` method.
- Only revisit execution after a separate explicit task approves changing the safety boundary.
