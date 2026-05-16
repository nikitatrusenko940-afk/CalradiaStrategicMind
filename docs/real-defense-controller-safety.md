# Real Defense Controller Safety

This document is the safety checkpoint before any future real defense controller work.

## Current State

- Real defense controller is disabled by default.
- `EnableRealDefenseController = false`.
- `DefenseController` currently returns `Disabled`.
- If enabled in code, it still returns `SafetyBlocked`.
- `DefenseControllerSafetyGuard` only reports `Allowed=true` or `Allowed=false`.
- `Allowed=true` does not execute any game action.
- No party movement is implemented.
- No orders are issued.
- No army behavior is changed.
- No settlement behavior is changed.
- No kingdom behavior or diplomacy is changed.

## Required Checks Before Real Execution

Real execution must not be allowed until all of these conditions are true at the same time:

- `EnableRealDefenseController == true`.
- Dry-run decision says `WouldAct == true`.
- Dry-run stability has `HasStableWouldActSignal == true`.
- Dry-run action is not `Monitor`, `Ignore`, or `Wait`.
- Action is `RequestReinforcement` or `RequestUrgentDefense`.
- Primary candidate exists.
- Primary candidate is not `none`.
- Candidate is suitable.
- Candidate is not too far.
- Candidate is not weak.
- Candidate is not busy.
- Candidate is not an army member unless it is the army leader.
- Settlement still exists.
- Settlement owner is still the same kingdom.
- Threat is still relevant at execution time.
- Defense coverage still says reinforcement is needed.
- Action has been stable for enough observations, except urgent defense.

## Forbidden Actions Without Separate Task

These actions are forbidden unless a separate explicit task approves and scopes them:

- Moving parties.
- Changing party AI.
- Forcing army targets.
- Forcing siege start.
- Forcing siege cancel.
- Changing kingdom war or diplomacy.
- Teleporting parties.
- Editing garrisons directly.
- Overriding vanilla AI with Harmony.
- Adding MCM in the same task.
- Adding save/load history in the same task.

## First Real Controller Must Be Dry-Run Compatible

- First real controller task must keep dry-run logs.
- Real execution must have a disable switch.
- Real execution must log every blocked action.
- Real execution must log every allowed action.
- Real execution must initially support only one narrow action type.
- Recommended first real action should be `request reinforcement candidate` or equivalent, not full AI replacement.

## Minimum Logs Before Real Execution

These logs must exist before real execution is considered:

- `Observed defense summary`.
- `Observed dry-run defense decision`.
- `Observed dry-run defense stability`.
- `Observed dry-run defense daily report`.
- `Observed defense controller scaffold`.
- `Observed defense controller safety`.

## Recommended Next Stages

- Stable settlement id instead of settlement name.
- Better candidate identity tracking.
- Save/load runtime history through `IDataStore` later.
- MCM later, separate task only.
- Real-controller dry-run-compatible command interface.
- First blocked execution test.
- First allowed execution test only after repeated diagnostics.
