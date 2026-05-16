# TaleWorlds Defense API Research

## Goal

Research TaleWorlds CampaignSystem APIs for the future first real defense action: `RequestReinforcementCandidate`.

The goal is to determine whether there is a safe vanilla-compatible way to ask a suitable lord or army to help defend a settlement without directly overriding vanilla AI, moving parties, issuing orders, changing armies, changing settlements, changing kingdoms, or changing diplomacy.

## Sources Inspected

- Reflection over local Bannerlord game DLLs in `D:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client`.
- Main inspected assembly: `TaleWorlds.CampaignSystem.dll`.
- Supporting assemblies loaded for reflection: `TaleWorlds.Core.dll`, `TaleWorlds.Library.dll`, `TaleWorlds.Localization.dll`, `TaleWorlds.ObjectSystem.dll`, `TaleWorlds.SaveSystem.dll`.
- Decompiled CampaignSystem `.cs` files were not found in the repository or local Bannerlord install during this pass.

Inspected types and members included:

- `TaleWorlds.CampaignSystem.Party.MobileParty`
- `TaleWorlds.CampaignSystem.Party.MobilePartyAi`
- `TaleWorlds.CampaignSystem.Party.AiBehavior`
- `TaleWorlds.CampaignSystem.Party.MobileParty.PartyObjective`
- `TaleWorlds.CampaignSystem.AIBehaviorData`
- `TaleWorlds.CampaignSystem.PartyThinkParams`
- `TaleWorlds.CampaignSystem.Army`
- `TaleWorlds.CampaignSystem.Army.ArmyTypes`
- `TaleWorlds.CampaignSystem.Settlements.Settlement`
- `TaleWorlds.CampaignSystem.Hero`
- `TaleWorlds.CampaignSystem.Clan`
- `TaleWorlds.CampaignSystem.Kingdom`
- `TaleWorlds.CampaignSystem.Campaign`
- `TaleWorlds.CampaignSystem.GameModels`
- `TaleWorlds.CampaignSystem.CampaignEvents`
- `TaleWorlds.CampaignSystem.CampaignBehaviorBase`
- `TaleWorlds.CampaignSystem.Actions.SetPartyAiAction`
- `TaleWorlds.CampaignSystem.Actions.GatherArmyAction`
- `TaleWorlds.CampaignSystem.Actions.DisbandArmyAction`
- `TaleWorlds.CampaignSystem.Actions.LiftSiegeAction`
- `TaleWorlds.CampaignSystem.Actions.DeclareWarAction`
- `TaleWorlds.CampaignSystem.Actions.MakePeaceAction`
- `TaleWorlds.CampaignSystem.Siege.SiegeEvent`
- `TaleWorlds.CampaignSystem.Siege.SiegeEventManager`
- `TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiMilitaryBehavior`
- `TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiArmyMemberBehavior`
- `TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiPartyThinkBehavior`
- `TaleWorlds.CampaignSystem.ComponentInterfaces.MobilePartyAIModel`
- `TaleWorlds.CampaignSystem.ComponentInterfaces.TargetScoreCalculatingModel`
- `TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyAIModel`
- `TaleWorlds.CampaignSystem.GameComponents.DefaultTargetScoreCalculatingModel`
- `TaleWorlds.CampaignSystem.CampaignBehaviors.GarrisonTroopsCampaignBehavior`
- `TaleWorlds.CampaignSystem.CampaignBehaviors.GarrisonRecruitmentCampaignBehavior`

## Candidate APIs

### MobileParty direct AI/movement members

- Type/Class: `TaleWorlds.CampaignSystem.Party.MobileParty`
- Member/Method/Property:
  - `Ai`
  - `DefaultBehavior`
  - `ShortTermBehavior`
  - `Objective`
  - `TargetParty`
  - `TargetSettlement`
  - `SetTargetSettlement(Settlement, bool)`
  - `SetPartyObjective(PartyObjective)`
  - `SetMoveDefendSettlement(Settlement, bool, NavigationType)`
  - `SetMoveGoToSettlement(Settlement, NavigationType, bool)`
  - `SetMoveBesiegeSettlement(Settlement, NavigationType)`
  - `SetMoveEscortParty(MobileParty, NavigationType, bool)`
  - `SetMoveModeHold()`
- What it seems to do: Directly changes party AI state, target, objective, or movement behavior.
- Read-only or side-effect: Side-effect.
- Risk level: High.
- Could be used for `RequestReinforcementCandidate`: No.
- Notes: These are exactly the kind of direct movement/order APIs the first real action must avoid. `SetMoveDefendSettlement` looks semantically relevant, but it is direct AI movement control, not a soft request.

### SetPartyAiAction

- Type/Class: `TaleWorlds.CampaignSystem.Actions.SetPartyAiAction`
- Member/Method/Property:
  - `GetActionForDefendingSettlement(MobileParty, Settlement, NavigationType, bool, bool)`
  - `GetActionForBesiegingSettlement(MobileParty, Settlement, NavigationType, bool)`
  - `GetActionForEngagingParty(MobileParty, MobileParty, NavigationType, bool)`
  - `GetActionForEscortingParty(MobileParty, MobileParty, NavigationType, bool, bool)`
  - `GetActionForPatrollingAroundSettlement(MobileParty, Settlement, NavigationType, bool, bool)`
  - `GetActionForVisitingSettlement(MobileParty, Settlement, NavigationType, bool, bool)`
- What it seems to do: Applies formal game actions that set party AI behavior.
- Read-only or side-effect: Side-effect.
- Risk level: High.
- Could be used for `RequestReinforcementCandidate`: No.
- Notes: These APIs are more structured than calling `MobileParty.SetMove...` directly, but they still issue concrete AI actions. They are not a vanilla-compatible soft request.

### MobilePartyAi

- Type/Class: `TaleWorlds.CampaignSystem.Party.MobilePartyAi`
- Member/Method/Property:
  - `RethinkAtNextHourlyTick`
  - `AiBehaviorInteractable`
  - `AiBehaviorPartyBase`
  - `GetNearbyPartyDataWhileDefendingSettlement(Settlement, out bool, out bool, out bool, out MobileParty, out MobileParty)`
  - `SetInitiative(float, float, float)`
  - `DisableAi()`
  - `EnableAi()`
- What it seems to do: Manages runtime party AI data and initiative.
- Read-only or side-effect: Mixed. `GetNearbyPartyDataWhileDefendingSettlement` appears query-like; setters and initiative/enable/disable methods have side effects.
- Risk level: Medium to High.
- Could be used for `RequestReinforcementCandidate`: Maybe for diagnostics only; No for execution.
- Notes: `GetNearbyPartyDataWhileDefendingSettlement` may be useful to understand vanilla defensive behavior, but it does not appear to request reinforcement. `SetInitiative`, `RethinkAtNextHourlyTick`, `DisableAi`, and `EnableAi` are not safe first-action tools.

### PartyThinkParams behavior scores

- Type/Class: `TaleWorlds.CampaignSystem.PartyThinkParams`
- Member/Method/Property:
  - `AIBehaviorScores`
  - `AddBehaviorScore(ref ValueTuple<AIBehaviorData, float>)`
  - `SetBehaviorScore(ref AIBehaviorData, float)`
  - `TryGetBehaviorScore(ref AIBehaviorData, out float)`
  - `DoNotChangeBehavior`
  - `CurrentObjectiveValue`
  - `WillGatherAnArmy`
- What it seems to do: Holds behavior score candidates during AI thinking.
- Read-only or side-effect: Side-effect on transient think parameters.
- Risk level: Unknown to High.
- Could be used for `RequestReinforcementCandidate`: Maybe, but not confirmed safe.
- Notes: This is the closest discovered API to "behavior weighting". However, using it requires hooking into `CampaignEvents.AiHourlyTickEvent` or adding a campaign behavior that mutates think params. Without decompiled confirmation of ordering and selection behavior, it is not safe to use for a real action yet.

### AIBehaviorData

- Type/Class: `TaleWorlds.CampaignSystem.AIBehaviorData`
- Member/Method/Property:
  - Public constructors accepting `IMapPoint` or `CampaignVec2`
  - Fields: `Party`, `Position`, `AiBehavior`, `NavigationType`, `WillGatherArmy`, `IsFromPort`, `IsTargetingPort`
- What it seems to do: Represents a candidate AI behavior target and score input.
- Read-only or side-effect: Data container; side effects occur when inserted into active think params.
- Risk level: Medium to Unknown.
- Could be used for `RequestReinforcementCandidate`: Maybe for a future scoring prototype only.
- Notes: Creating data is safe by itself, but feeding it into live AI thinking would influence behavior. That is not a proven-safe vanilla-compatible request yet.

### CampaignEvents.AiHourlyTickEvent

- Type/Class: `TaleWorlds.CampaignSystem.CampaignEvents`
- Member/Method/Property:
  - `AiHourlyTickEvent`
  - `TickPartialHourlyAi(MobileParty)`
  - `DailyTickPartyEvent`
  - `OnMobilePartyNavigationStateChanged`
  - `OnMobilePartyJoinedToSiegeEvent`
  - `OnMobilePartyLeftSiegeEvent`
  - `OnPartyJoinedArmy`
  - `OnPartyLeftArmy`
- What it seems to do: Exposes event hooks around campaign AI thinking and party state changes.
- Read-only or side-effect: Subscribing is side-effect on event system; event handlers can be read-only or mutating.
- Risk level: Medium.
- Could be used for `RequestReinforcementCandidate`: Maybe for diagnostics; Unknown for safe influence.
- Notes: `AiHourlyTickEvent` passes `MobileParty` and `PartyThinkParams`, which may allow behavior score changes. This is a possible research path, not a safe implementation path yet.

### TargetScoreCalculatingModel

- Type/Class: `TaleWorlds.CampaignSystem.ComponentInterfaces.TargetScoreCalculatingModel`
- Member/Method/Property:
  - `GetTargetScoreForFaction(Settlement, ArmyTypes, MobileParty, float)`
  - `CalculateDefensivePatrollingScoreForSettlement(Settlement, bool, MobileParty)`
  - `CalculateOffensivePatrollingScoreForSettlement(Settlement, bool, MobileParty)`
  - `CurrentObjectiveValue(MobileParty)`
  - `DefendingFactor`
  - `BesiegingFactor`
  - `RaidingFactor`
- What it seems to do: Calculates target scores used by military AI.
- Read-only or side-effect: Read-only calculation from the public surface.
- Risk level: Low for reading, High for overriding/replacing.
- Could be used for `RequestReinforcementCandidate`: Maybe for diagnostics only; No direct request API found.
- Notes: This model may explain why vanilla AI selects defense targets. Replacing the model to alter weights would affect global AI behavior and is too broad for the first action.

### MobilePartyAIModel

- Type/Class: `TaleWorlds.CampaignSystem.ComponentInterfaces.MobilePartyAIModel`
- Member/Method/Property:
  - `SettlementDefendingNearbyPartyCheckRadius`
  - `SettlementDefendingWaitingPositionRadius`
  - `GetSettlementNearbyThreatAndAllyCheckRadius(Settlement, bool)`
  - `GetBestInitiativeBehavior(MobileParty, ref AiBehavior, ref MobileParty, ref float, ref Vec2)`
  - `ShouldConsiderAttacking(MobileParty, MobileParty)`
  - `ShouldConsiderAvoiding(MobileParty, MobileParty)`
- What it seems to do: Provides model values and decisions for mobile party AI.
- Read-only or side-effect: Read-only calculation from the public surface.
- Risk level: Low for reading, High for overriding/replacing.
- Could be used for `RequestReinforcementCandidate`: Maybe for diagnostics only.
- Notes: Useful for understanding vanilla defensive radii and initiative. No safe command/request API found here.

### AiMilitaryBehavior

- Type/Class: `TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiMilitaryBehavior`
- Member/Method/Property:
  - `FindBestTargetAndItsValueForFaction(ArmyTypes, PartyThinkParams, float)`
  - private methods observed by reflection: `CalculateMilitaryBehaviorForSettlement`, `GetDistanceScoreForDefending`, `CheckIfSettlementIsSuitableForMilitaryAction`, `AiHourlyTick`
- What it seems to do: Vanilla military AI scoring and target selection behavior.
- Read-only or side-effect: Public method likely mutates `PartyThinkParams`; private methods are not callable without reflection/Harmony.
- Risk level: Medium to High.
- Could be used for `RequestReinforcementCandidate`: No for direct implementation; Maybe for further research.
- Notes: The relevant logic appears internal/private. Calling or patching it would be risky and outside the current safety boundary.

### AiArmyMemberBehavior

- Type/Class: `TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiArmyMemberBehavior`
- Member/Method/Property:
  - `AiHourlyTick(MobileParty, PartyThinkParams)`
- What it seems to do: Handles behavior for army members during AI hourly ticks.
- Read-only or side-effect: Likely side-effect on think params.
- Risk level: Medium to High.
- Could be used for `RequestReinforcementCandidate`: No.
- Notes: This reinforces the rule that army members should not be redirected unless they are army leaders.

### AiPartyThinkBehavior

- Type/Class: `TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiPartyThinkBehavior`
- Member/Method/Property:
  - private `PartyHourlyAiTick(MobileParty)`
  - private `CheckMobilePartyActionAccordingToSettlement(MobileParty, Settlement)`
  - private event handlers for owner changes, war, peace, and clan kingdom changes
- What it seems to do: Coordinates vanilla party AI thinking and resets behavior after strategic state changes.
- Read-only or side-effect: Side-effect, internal/private.
- Risk level: High.
- Could be used for `RequestReinforcementCandidate`: No.
- Notes: Not a public soft request surface.

### Campaign.Current.Models

- Type/Class: `TaleWorlds.CampaignSystem.Campaign` and `TaleWorlds.CampaignSystem.GameModels`
- Member/Method/Property:
  - `Campaign.Current.Models`
  - `GameModels.MobilePartyAIModel`
  - `GameModels.TargetScoreCalculatingModel`
  - `GameModels.SettlementGarrisonModel`
  - `GameModels.SiegeEventModel`
- What it seems to do: Provides global game models, many with public setters.
- Read-only or side-effect: Reading models is low risk; replacing models is a global side effect.
- Risk level: Low for reading, High for replacing.
- Could be used for `RequestReinforcementCandidate`: Maybe for diagnostics only.
- Notes: Replacing `TargetScoreCalculatingModel` or `MobilePartyAIModel` would be broad AI modification and should not be used for the first real action.

### Siege APIs

- Type/Class: `TaleWorlds.CampaignSystem.Siege.SiegeEvent` and `TaleWorlds.CampaignSystem.Siege.SiegeEventManager`
- Member/Method/Property:
  - `SiegeEventManager.StartSiegeEvent(Settlement, MobileParty)`
  - `SiegeEvent.FinalizeSiegeEvent()`
  - `SiegeEvent.DoSiegeAction(...)`
  - `SiegeEvent.CanPartyJoinSide(PartyBase, BattleSideEnum)`
  - `LiftSiegeAction.GetGameAction(MobileParty)`
- What it seems to do: Starts, ends, or manipulates siege state.
- Read-only or side-effect: Mixed; most listed methods are side-effect. `CanPartyJoinSide` is query-like.
- Risk level: High, except query methods are Low for diagnostics.
- Could be used for `RequestReinforcementCandidate`: No.
- Notes: Starting/canceling/forcing siege actions is explicitly forbidden for the first real action.

### Army APIs

- Type/Class: `TaleWorlds.CampaignSystem.Army`, `GatherArmyAction`, `DisbandArmyAction`
- Member/Method/Property:
  - `GatherArmyAction.Apply(MobileParty, IMapPoint)`
  - `DisbandArmyAction.ApplyBy...`
  - `MobileParty.Army`
  - `MobileParty.AttachedTo`
- What it seems to do: Creates, gathers, assigns, attaches, or disbands army state.
- Read-only or side-effect: Side-effect.
- Risk level: High.
- Could be used for `RequestReinforcementCandidate`: No.
- Notes: Forced army behavior is outside the safe first action.

### Kingdom/diplomacy APIs

- Type/Class: `DeclareWarAction`, `MakePeaceAction`, `ChangeKingdomAction`, `Kingdom`
- Member/Method/Property:
  - `DeclareWarAction.Apply...`
  - `MakePeaceAction.Apply...`
  - `ChangeKingdomAction`
- What it seems to do: Changes kingdom or diplomacy state.
- Read-only or side-effect: Side-effect.
- Risk level: High.
- Could be used for `RequestReinforcementCandidate`: No.
- Notes: Not relevant for a defensive reinforcement request and explicitly forbidden.

### Garrison APIs

- Type/Class: `GarrisonTroopsCampaignBehavior`, `GarrisonRecruitmentCampaignBehavior`, `SettlementGarrisonModel`
- Member/Method/Property:
  - `GarrisonRecruitmentCampaignBehavior.GetGarrisonChangeExplainedNumber(Town)`
  - `SettlementGarrisonModel`
  - campaign events such as `OnTroopGivenToSettlement`
- What it seems to do: Handles garrison recruitment, garrison transfer, and garrison model calculations.
- Read-only or side-effect: Mixed.
- Risk level: Low for reading model/calculation values, High for direct roster edits/transfers.
- Could be used for `RequestReinforcementCandidate`: No.
- Notes: Direct garrison editing is not a reinforcement request and is forbidden.

## Unsafe APIs

Do not use these methods or approaches for the first real defense action:

- `MobileParty.SetMoveDefendSettlement(...)`
- `MobileParty.SetMoveGoToSettlement(...)`
- `MobileParty.SetMoveBesiegeSettlement(...)`
- `MobileParty.SetMoveEscortParty(...)`
- `MobileParty.SetMoveEngageParty(...)`
- `MobileParty.SetMoveModeHold()`
- `MobileParty.SetTargetSettlement(...)`
- `MobileParty.SetPartyObjective(...)`
- Directly setting `MobileParty.DefaultBehavior`, `ShortTermBehavior`, `TargetParty`, `TargetPosition`, `Position`, `Army`, `AttachedTo`, or `CurrentSettlement`.
- `SetPartyAiAction.GetActionForDefendingSettlement(...)` and related `SetPartyAiAction` methods.
- `GatherArmyAction.Apply(...)`.
- Any `DisbandArmyAction.Apply...` method.
- `SiegeEventManager.StartSiegeEvent(...)`.
- `LiftSiegeAction.GetGameAction(...)`.
- `SiegeEvent.FinalizeSiegeEvent()`.
- `SiegeEvent.DoSiegeAction(...)`.
- `DeclareWarAction.Apply...`, `MakePeaceAction.Apply...`, or any kingdom/diplomacy action.
- Direct troop roster or garrison edits.
- Replacing global AI models such as `Campaign.Current.Models.TargetScoreCalculatingModel` or `MobilePartyAIModel` as part of the first action.
- Harmony patches or reflection calls into private vanilla AI methods.

## Vanilla-Compatible Options

### Soft request

No confirmed public soft-request API was found. There is no discovered method that means "ask this lord to consider defending this settlement" without directly setting movement, AI behavior, army state, or target state.

### Behavior weighting

Possible but not confirmed safe. `CampaignEvents.AiHourlyTickEvent` exposes `PartyThinkParams`, and `PartyThinkParams` exposes behavior score methods. This could theoretically add or adjust an `AIBehaviorData` score for `AiBehavior.DefendSettlement`.

Risk: this directly influences the active AI decision cycle. Without decompiled verification of event ordering, score normalization, conflict handling, and vanilla behavior priorities, it should be treated as unsafe for implementation.

### Diagnostic-only signal

Safe. The current mod can continue logging diagnostics, safety reports, and command reports. This remains the recommended path until a safe API is identified.

### Indirect influence

No safe indirect influence API was confirmed. Reading `TargetScoreCalculatingModel`, `MobilePartyAIModel`, and vanilla AI behavior outputs can help explain decisions, but changing model values or replacing models is too broad.

### No safe option found

Current research did not find a confirmed safe vanilla-compatible execution API for `RequestReinforcementCandidate`.

## Recommendation

No safe API found.

Do not implement `RequestReinforcementCandidate` yet.

The nearest candidate is behavior-score influence through `CampaignEvents.AiHourlyTickEvent` and `PartyThinkParams`, but this is not confirmed safe. It would still alter vanilla AI selection and needs deeper decompiled-code research before any prototype.

The direct APIs that look useful, such as `SetMoveDefendSettlement` or `SetPartyAiAction.GetActionForDefendingSettlement`, are high-risk because they issue concrete AI behavior changes. They should remain forbidden for the first real action.

## Proposed Next Step

- Document only for now.
- Perform deeper API research with decompiled `TaleWorlds.CampaignSystem` sources, focused on:
  - `AiPartyThinkBehavior.PartyHourlyAiTick`
  - `AiMilitaryBehavior.CalculateMilitaryBehaviorForSettlement`
  - `AiMilitaryBehavior.GetDistanceScoreForDefending`
  - `AiMilitaryBehavior.FindBestTargetAndItsValueForFaction`
  - `PartyThinkParams.SetBehaviorScore`
  - how `AIBehaviorData` scores are selected and applied.
- Keep `DefenseCommandInterface` diagnostic-only.
- Prototype only blocked commands until a safe API is confirmed.
- Only consider a very narrow implementation if a vanilla-compatible method is identified and abort behavior is fully defined.
