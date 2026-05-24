# Vanilla Lord AI Control Research v1

## Scope

Research-only pass. No production Defense Controller logic changed.

Runtime issue: CSM can create defense assignments, but vanilla campaign AI or other CSM systems can later overwrite `MobileParty.TargetSettlement` / `DefaultBehavior`, causing target oscillation and failed movement.

## Local Sources Checked

- `docs/taleworlds-defense-api-research.md`
- `docs/decompiled-ai-behavior-research.md`
- CSM objective/movement readers and trackers:
  - `CsmArmyObjectiveReader`
  - `CsmArmyMissionTracker`
  - `CsmTaskDistractionEvaluator`
  - `DirectDefenseCommandController`
- Current repository file list: no decompiled TaleWorlds `.cs` source tree was found in repo/docs/archive.

## Vanilla Classes That Control Lord Party Behavior

From the existing API/decompiled research:

- `TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiPartyThinkBehavior`
  - Coordinates hourly party AI thinking.
  - Consumes `PartyThinkParams.AIBehaviorScores`.
  - Applies the selected behavior through `SetPartyAiAction`.
  - Contains private/internal logic such as `PartyHourlyAiTick` and settlement-action checks.

- `TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiMilitaryBehavior`
  - Adds military behavior scores into `PartyThinkParams`.
  - Includes defend/besiege/raid target scoring.
  - Decompiled notes identify private methods such as `CalculateMilitaryBehaviorForSettlement`, `GetDistanceScoreForDefending`, and `CheckIfSettlementIsSuitableForMilitaryAction`.

- `TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiArmyMemberBehavior`
  - Handles army member behavior during AI hourly ticks.
  - Relevant because non-leader army parties should not be independently redirected.

- `TaleWorlds.CampaignSystem.Party.MobilePartyAi`
  - Runtime AI container for parties.
  - Public members include `RethinkAtNextHourlyTick`, `SetInitiative`, `DisableAi`, `EnableAi`, and query helpers.

- `TaleWorlds.CampaignSystem.ComponentInterfaces.MobilePartyAIModel`
  - Public model for initiative, threat, nearby party checks, and behavior decisions.
  - Useful for diagnostics, broad and risky to replace.

- `TaleWorlds.CampaignSystem.ComponentInterfaces.TargetScoreCalculatingModel`
  - Public model for target scores, including defensive settlement scoring.
  - Useful for diagnostics, broad and risky to replace.

## Methods That Change Target Or Behavior

Direct side-effect APIs:

- `MobileParty.SetMoveDefendSettlement(Settlement, bool, NavigationType)`
- `MobileParty.SetMoveGoToSettlement(Settlement, NavigationType, bool)`
- `MobileParty.SetMoveBesiegeSettlement(Settlement, NavigationType)`
- `MobileParty.SetMoveEscortParty(MobileParty, NavigationType, bool)`
- `MobileParty.SetMoveEngageParty(MobileParty, NavigationType)`
- `MobileParty.SetMoveModeHold()`
- `MobileParty.SetTargetSettlement(Settlement, bool)`
- `MobileParty.SetPartyObjective(PartyObjective)`
- Direct state writes to `DefaultBehavior`, `ShortTermBehavior`, `TargetParty`, `TargetPosition`, `Army`, `AttachedTo`, `CurrentSettlement`, or `Position`.

Structured but still direct side-effect APIs:

- `SetPartyAiAction.GetActionForDefendingSettlement(...)`
- `SetPartyAiAction.GetActionForBesiegingSettlement(...)`
- `SetPartyAiAction.GetActionForEngagingParty(...)`
- `SetPartyAiAction.GetActionForEscortingParty(...)`
- `SetPartyAiAction.GetActionForPatrollingAroundSettlement(...)`
- `SetPartyAiAction.GetActionForVisitingSettlement(...)`

Important decompiled finding: inserting or boosting `AiBehavior.DefendSettlement` in live `PartyThinkParams` is not passive. If that score wins, vanilla applies `SetPartyAiAction.GetActionForDefendingSettlement(...)`, which ultimately issues real defend movement.

## Public API Availability

Available public API, but direct control:

- `MobileParty.SetMoveDefendSettlement`
- `MobileParty.SetTargetSettlement`
- `SetPartyAiAction.GetActionForDefendingSettlement`
- `MobilePartyAi.DisableAi` / `EnableAi`
- `MobilePartyAi.RethinkAtNextHourlyTick`
- `MobilePartyAi.SetInitiative`
- `PartyThinkParams.AddBehaviorScore` / `SetBehaviorScore`

Available public API, safer for diagnostics:

- Read `MobileParty.TargetSettlement`
- Read `MobileParty.DefaultBehavior`
- Read `MobileParty.ShortTermBehavior`
- Read `MobileParty.BesiegedSettlement`
- Read `Army.AiBehaviorObject`
- Read `MobilePartyAIModel` values
- Read `TargetScoreCalculatingModel` scores
- Subscribe to campaign events for diagnostics

No confirmed public API was found for:

- A durable "lock this lord to CSM objective" flag.
- A vanilla-approved "do not change this party target" request.
- A scoped exclusion from vanilla lord AI decision making.
- A soft defend request that cannot become a direct movement command.

## What Can Be Controlled Without Harmony Or Reflection

Realistically safe:

- Maintain a CSM-controlled party registry.
- Detect and classify overwrites by comparing expected target, current target, behavior, army target, and task owner.
- Run a final CSM arbitration pass after all CSM subsystems in `StrategicObservationBehavior`.
- Avoid issuing conflicting commands from Army Director and Defense Controller by checking one shared authority registry.
- Prefer candidate selection that avoids parties likely to be overwritten:
  - army members,
  - parties with strong vanilla target intent,
  - parties already on active siege defense,
  - parties recently overwritten,
  - parties with repeated objective oscillation.
- Use direct `SetMoveDefendSettlement` only where the current direct-defense feature already explicitly allows it.

Possible but high risk:

- `PartyThinkParams` influence via `CampaignEvents.AiHourlyTickEvent`.
- `MobilePartyAi.SetInitiative` / `RethinkAtNextHourlyTick`.
- `MobilePartyAi.DisableAi` / `EnableAi`.

These are public, but they alter live AI decision making and are not safe as a narrow fix without a separate explicit task.

## What Requires Harmony, Reflection, Or Private API

- Preventing `AiPartyThinkBehavior` from overwriting CSM-controlled parties.
- Patching vanilla target scoring for selected settlements only.
- Intercepting `SetPartyAiAction.GetActionFor...` application.
- Calling private `AiMilitaryBehavior` scoring internals.
- Altering private `AiPartyThinkBehavior.PartyHourlyAiTick` flow.
- Adding a true vanilla AI "lock" if no public hook exists.

## Recommended CSM Strategic Authority Layer

Add a new coordination layer, not stronger reassert loops:

1. `CsmStrategicAuthorityRegistry`
   - Owns party-level CSM control state.
   - Records owner system: `Defense`, `Army`, `Recovery`, `Released`, `None`.
   - Records expected objective, expected target, priority, expiry, last command tick, and overwrite history.

2. `CsmStrategicAuthorityArbiter`
   - Runs before any CSM command is issued.
   - Decides whether Defense or Army has authority over a party.
   - Prevents Defense and Army from fighting each other.
   - Refuses parties with high overwrite risk unless active siege priority is critical.

3. `CsmObjectiveObservation`
   - Read-only observation pass for every CSM-controlled party.
   - Captures:
     - `TargetSettlement`
     - `DefaultBehavior`
     - `ShortTermBehavior`
     - `BesiegedSettlement`
     - `Army.AiBehaviorObject`
     - distance/progress
     - likely overwriter classification.

4. `CsmFinalCommandPass`
   - Runs once after Defense Controller, Army Director, and Task Discipline have all produced desired commands.
   - Applies at most one final command per party.
   - This reduces CSM-vs-CSM oscillation and avoids multiple reasserts in the same tick.

5. `CsmOverwriteClassifier`
   - Classifies overwrite source:
     - `ArmyDirector`
     - `DefenseController`
     - `TaskDiscipline`
     - `VanillaAiDefend`
     - `VanillaAiBesiege`
     - `VanillaAiRaidOrPatrol`
     - `JoinedArmy`
     - `Unknown`.

## How To Reduce Conflict Without Constant Reassert

- Stop issuing commands immediately in subsystem controllers.
- Let controllers publish desired objectives to the authority layer.
- Apply final commands once per tick in deterministic priority order.
- Add cooldowns per party and per target.
- If vanilla overwrites the same party repeatedly, quarantine it from defense candidate pools for a short time.
- Replace failed defenders instead of repeatedly fighting vanilla for the same lord.
- Treat vanilla-aligned defenders as valid if they are already moving to the threatened settlement, even without CSM ownership.
- Prefer closer, idle, non-army lord parties with no recent target churn.

## Risks

- Direct `SetMoveDefendSettlement` remains a hard command and can be overwritten by vanilla on the next AI tick.
- `PartyThinkParams` score injection can indirectly invoke the same direct movement action through vanilla.
- Disabling AI may create stuck parties or break campaign simulation.
- Replacing global AI models would affect all kingdoms and may destabilize the campaign.
- A final command pass can still oscillate if it runs before vanilla AI rather than after it.
- Without Harmony/reflection, there may be no true post-vanilla-AI hook for the exact moment after `AiPartyThinkBehavior` applies behavior.

## Next Implementation Plan

1. Implement read-only `CsmStrategicAuthorityRegistry`.
2. Convert Defense Controller and Army Director to reserve authority before issuing commands.
3. Add read-only overwrite diagnostics with source classification.
4. Add a final CSM command pass inside `StrategicObservationBehavior` after all CSM systems run.
5. Move direct `SetMove...` application behind that final pass.
6. Add candidate quarantine for repeated vanilla overwrites.
7. Only after diagnostics prove ordering, consider a separate explicit task for `PartyThinkParams` influence or stronger vanilla integration.

## Build

No build required. This task only adds research documentation.
