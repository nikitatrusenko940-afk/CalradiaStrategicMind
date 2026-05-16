# First Real Defense Action Design

## Goal

Design the first very narrow and safe real-controller action before implementation.

## Recommended First Action

`RequestReinforcementCandidate`

This must not be:

- Direct party movement.
- Teleport.
- Forced siege start.
- Forced army join.
- Vanilla AI replacement.
- Harmony override.
- Diplomacy change.
- Direct garrison editing.

`RequestReinforcementCandidate` should be a soft action that tries to use a vanilla-compatible way to ask a suitable lord or army to help defend a settlement.

If there is no safe vanilla API, the action must not be implemented.

## Required Preconditions

- `EnableRealDefenseController == true`.
- `DefenseControllerSafetyGuard.Allowed == true`.
- `DefenseCommandReport.IsAllowed == true`.
- `DryRunDefenseDecision.WouldAct == true`.
- `DryRunDefenseDecisionStabilityReport.HasStableWouldActSignal == true`.
- Action is `RequestReinforcement` or `RequestUrgentDefense`.
- Primary candidate exists.
- Primary candidate is suitable.
- Candidate is not too far.
- Candidate is not weak.
- Candidate is not busy.
- Candidate is not an army member unless army leader.
- Settlement owner still matches expected kingdom.
- Threat still exists.
- Coverage still says reinforcement is needed.

## API Research Required Before Implementation

Before C# implementation, use reflection or decompiled TaleWorlds CampaignSystem code to verify:

- Whether party objective can be set safely.
- Whether vanilla behavior exists for defensive response.
- Whether there is a safe way to influence or weight target selection.
- Whether there is a campaign behavior API for a soft request.
- Whether there is risk of breaking party AI state.
- Which methods have side effects.
- Which methods must not be called.

## Execution Rules

- First implementation must still support dry-run.
- First implementation must have a hard disable switch.
- First implementation must log every blocked command.
- First implementation must log every allowed command.
- First implementation must log every executed command.
- `WasExecuted` must remain `false` until a separate implementation task explicitly changes it.
- Only one action type may be implemented first.

## Abort Conditions

- Candidate disappeared.
- Candidate faction changed.
- Settlement owner changed.
- Settlement already safe.
- Action no longer stable.
- Candidate became busy.
- Candidate became too weak.
- Candidate is now in another army and not leader.
- Target settlement is under different strategic condition.
- API call would override vanilla AI too aggressively.

## Minimum Logs

- `Observed defense summary`.
- `Observed dry-run defense decision`.
- `Observed dry-run defense stability`.
- `Observed dry-run defense daily report`.
- `Observed defense controller scaffold`.
- `Observed defense controller safety`.
- `Observed defense command`.

## Implementation Must Not Begin Until

- Blocked execution test passed.
- Safety documentation exists.
- API research is completed.
- Exact vanilla-compatible method is identified.
- Fallback or abort behavior is defined.
- Test save is prepared.
