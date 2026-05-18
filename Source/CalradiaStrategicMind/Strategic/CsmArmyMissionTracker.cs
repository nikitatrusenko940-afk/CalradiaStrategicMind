using System.Collections.Generic;
using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmArmyMissionTracker
    {
        private readonly Dictionary<string, CsmArmyMissionState> _statesByArmy;

        public CsmArmyMissionTracker()
        {
            _statesByArmy = new Dictionary<string, CsmArmyMissionState>();
        }

        public CsmArmyMissionState GetOrCreateState(CsmArmyAssignment assignment)
        {
            if (assignment == null)
            {
                return null;
            }

            var key = GetAssignmentKey(assignment);
            CsmArmyMissionState state;
            if (_statesByArmy.TryGetValue(key, out state))
            {
                return state;
            }

            state = new CsmArmyMissionState
            {
                AssignmentId = key,
                ArmyId = assignment.ArmyId,
                ArmyName = assignment.ArmyName,
                KingdomName = assignment.KingdomName,
                ObjectiveType = assignment.ObjectiveType,
                TargetSettlementId = assignment.TargetSettlementId,
                TargetSettlementName = assignment.TargetSettlementName,
                LastDistanceToTarget = -1f,
                CurrentDistanceToTarget = -1f,
                LastProgressTick = assignment.StartTick,
                StalledTickCount = 0,
                SyncAttemptCount = 0,
                RepathAttemptCount = 0,
                LastRedirectTick = -9999,
                RedirectCount = 0,
                PreviousTargetSettlementName = "none",
                IsRedirectGraceActive = false,
                LastState = CsmArmyMissionStatus.Created,
                CurrentState = CsmArmyMissionStatus.Created,
                Reason = "Mission state created"
            };
            _statesByArmy[key] = state;
            return state;
        }

        public CsmArmyMissionReport Evaluate(
            CsmArmySnapshot snapshot,
            CsmArmyObjectiveSnapshot objective,
            CsmArmyAssignment assignment,
            Settlement target,
            CsmBadSiegeEvaluator badSiegeEvaluator,
            CsmArmyAttackTargetScorer targetScorer,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry,
            int tick)
        {
            if (!ArmyDirectorSettings.EnableArmyMissionTracking)
            {
                return new CsmArmyMissionReport(null, false);
            }

            var state = GetOrCreateState(assignment);
            if (state == null)
            {
                return new CsmArmyMissionReport(null, false);
            }

            state.ArmyName = snapshot.ArmyName;
            state.KingdomName = snapshot.KingdomName;
            state.ObjectiveType = assignment.ObjectiveType;
            state.TargetSettlementId = assignment.TargetSettlementId;
            state.TargetSettlementName = assignment.TargetSettlementName;

            if (target != null && snapshot.LeaderParty != null)
            {
                state.CurrentDistanceToTarget = target.Position.Distance(snapshot.LeaderParty.Position);
            }

            var nextState = EvaluateState(snapshot, objective, assignment, target, badSiegeEvaluator, targetScorer, defenseSnapshots, registry, state, tick);
            UpdateState(state, nextState, state.Reason, tick);
            return new CsmArmyMissionReport(state, IsHandledState(state.CurrentState));
        }

        public void UpdateState(CsmArmyMissionState state, CsmArmyMissionStatus status, string reason, int tick)
        {
            if (state == null)
            {
                return;
            }

            var previousState = state.CurrentState;
            var previousReason = state.Reason;
            state.LastState = previousState;
            state.CurrentState = status;
            state.Reason = reason;

            if (previousState != status || ShouldLogRedirectGraceReasonChange(state, previousReason))
            {
                LogTransition(tick, state);
            }
        }

        public void CloseState(CsmArmyAssignment assignment, CsmArmyMissionStatus status, string reason, int tick)
        {
            var state = GetOrCreateState(assignment);
            if (state == null)
            {
                return;
            }

            UpdateState(state, status, reason, tick);
            _statesByArmy.Remove(GetAssignmentKey(assignment));
        }

        public void IncrementSyncAttempt(CsmArmyAssignment assignment)
        {
            var state = GetOrCreateState(assignment);
            if (state != null)
            {
                state.SyncAttemptCount++;
            }
        }

        public void IncrementRepathAttempt(CsmArmyAssignment assignment)
        {
            var state = GetOrCreateState(assignment);
            if (state != null)
            {
                state.RepathAttemptCount++;
            }
        }

        public bool HasExceededSyncAttempts(CsmArmyAssignment assignment)
        {
            var state = GetOrCreateState(assignment);
            return state != null && state.SyncAttemptCount >= ArmyDirectorSettings.MaxObjectiveSyncAttemptsPerAssignment;
        }

        public bool HasExceededRepathAttempts(CsmArmyAssignment assignment)
        {
            var state = GetOrCreateState(assignment);
            return state != null && state.RepathAttemptCount >= ArmyDirectorSettings.MaxMissionRepathAttemptsPerAssignment;
        }

        public int GetRedirectCount(CsmArmyAssignment assignment)
        {
            var state = GetOrCreateState(assignment);
            return state == null ? 0 : state.RedirectCount;
        }

        public int GetLastRedirectTick(CsmArmyAssignment assignment)
        {
            var state = GetOrCreateState(assignment);
            return state == null ? -9999 : state.LastRedirectTick;
        }

        public bool IsRedirectCooldownActive(CsmArmyAssignment assignment, int tick)
        {
            return tick - GetLastRedirectTick(assignment) < ArmyDirectorSettings.MinimumTicksBetweenArmyRedirects;
        }

        public bool HasExceededRedirects(CsmArmyAssignment assignment)
        {
            return GetRedirectCount(assignment) >= ArmyDirectorSettings.MaxRedirectsPerAssignment;
        }

        public void MarkRedirected(CsmArmyAssignment assignment, int tick, string previousTargetSettlementName, int inheritedRedirectCount)
        {
            var state = GetOrCreateState(assignment);
            if (state == null)
            {
                return;
            }

            state.LastRedirectTick = tick;
            state.RedirectCount = inheritedRedirectCount + 1;
            state.PreviousTargetSettlementName = string.IsNullOrWhiteSpace(previousTargetSettlementName) ? "none" : previousTargetSettlementName;
            state.IsRedirectGraceActive = true;
            state.LastState = state.CurrentState;
            state.CurrentState = CsmArmyMissionStatus.Gathering;
            state.Reason = "Redirect grace active after bad siege redirect";
        }

        private CsmArmyMissionStatus EvaluateState(
            CsmArmySnapshot snapshot,
            CsmArmyObjectiveSnapshot objective,
            CsmArmyAssignment assignment,
            Settlement target,
            CsmBadSiegeEvaluator badSiegeEvaluator,
            CsmArmyAttackTargetScorer targetScorer,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry,
            CsmArmyMissionState state,
            int tick)
        {
            if (assignment == null || target == null || snapshot.Army == null || snapshot.LeaderParty == null)
            {
                state.Reason = "Mission invalid because target, army, or leader is unavailable";
                return CsmArmyMissionStatus.Invalid;
            }

            if (!snapshot.IsValidForCsm)
            {
                state.Reason = "Mission invalid because army is no longer valid";
                return CsmArmyMissionStatus.Invalid;
            }

            var kingdom = snapshot.LeaderParty.MapFaction as Kingdom;
            if (IsAttackObjective(assignment.ObjectiveType))
            {
                if (target.MapFaction == null || kingdom == null)
                {
                    state.Reason = "Mission invalid because target faction is unreadable";
                    return CsmArmyMissionStatus.Invalid;
                }

                if (target.MapFaction == snapshot.LeaderParty.MapFaction || !kingdom.IsAtWarWith(target.MapFaction))
                {
                    state.Reason = "Mission completed because target is no longer enemy";
                    return CsmArmyMissionStatus.Completed;
                }
            }

            if (HasObjectiveMismatch(objective, assignment))
            {
                state.Reason = "Army displayed objective differs from CSM assignment target";
                return CsmArmyMissionStatus.ObjectiveMismatch;
            }

            if (IsRedirectGraceActive(assignment, state, tick))
            {
                state.IsRedirectGraceActive = true;
                state.Reason = "Redirect grace active; skipping unsafe evaluation";
                return CsmArmyMissionStatus.Gathering;
            }

            state.IsRedirectGraceActive = false;

            if (IsAssaultingAssignedTarget(snapshot, objective, assignment))
            {
                state.StalledTickCount = 0;
                state.LastDistanceToTarget = state.CurrentDistanceToTarget;
                state.LastProgressTick = tick;
                state.Reason = "Army is assaulting assigned target";
                return CsmArmyMissionStatus.AssaultingAssignedTarget;
            }

            if (IsBesiegingAssignedTarget(objective, assignment))
            {
                state.StalledTickCount = 0;
                state.LastDistanceToTarget = state.CurrentDistanceToTarget;
                state.LastProgressTick = tick;
                state.Reason = "Army is besieging assigned target";
                return CsmArmyMissionStatus.BesiegingAssignedTarget;
            }

            if (IsOperatingOnAssignedTarget(objective, assignment))
            {
                state.StalledTickCount = 0;
                state.LastDistanceToTarget = state.CurrentDistanceToTarget;
                state.LastProgressTick = tick;
                state.Reason = "Army is operating on assigned target";
                return CsmArmyMissionStatus.OperatingOnAssignedTarget;
            }

            if (IsAttackObjective(assignment.ObjectiveType) && IsCurrentTargetAssignedTarget(objective, assignment))
            {
                var badSiege = badSiegeEvaluator == null ? null : badSiegeEvaluator.Evaluate(snapshot, objective, defenseSnapshots, registry);
                if (IsUnsafeBadSiege(badSiege, assignment))
                {
                    state.Reason = badSiege.Reason;
                    return CsmArmyMissionStatus.Unsafe;
                }
            }

            if (state.CurrentDistanceToTarget <= ArmyDirectorSettings.MissionArrivedDistance)
            {
                state.StalledTickCount = 0;
                state.LastDistanceToTarget = state.CurrentDistanceToTarget;
                state.LastProgressTick = tick;
                state.Reason = "Army is near assigned target";
                return CsmArmyMissionStatus.NearTarget;
            }

            if (state.LastDistanceToTarget < 0f)
            {
                state.LastDistanceToTarget = state.CurrentDistanceToTarget;
                state.LastProgressTick = tick;
                state.Reason = "Mission distance baseline recorded";
                return CsmArmyMissionStatus.Gathering;
            }

            if (tick - state.LastProgressTick < GetProgressCheckEveryTicks())
            {
                state.Reason = "Mission progress check interval not reached";
                return state.CurrentState;
            }

            if (state.LastDistanceToTarget - state.CurrentDistanceToTarget >= ArmyDirectorSettings.MissionProgressMinimumDistanceDelta)
            {
                state.LastDistanceToTarget = state.CurrentDistanceToTarget;
                state.LastProgressTick = tick;
                state.StalledTickCount = 0;
                state.Reason = "Army is moving toward assigned target";
                return CsmArmyMissionStatus.MovingToTarget;
            }

            state.StalledTickCount++;
            if (state.StalledTickCount >= ArmyDirectorSettings.MissionStalledTicksBeforeRelease)
            {
                state.Reason = "Mission expired because army did not make progress toward assigned target";
                return CsmArmyMissionStatus.Expired;
            }

            if (state.StalledTickCount >= ArmyDirectorSettings.MissionStalledTicksBeforeReassert)
            {
                state.Reason = "Mission stalled; reasserted assigned attack target";
                return CsmArmyMissionStatus.Stalled;
            }

            state.Reason = "Army has not yet made enough progress toward assigned target";
            return CsmArmyMissionStatus.Gathering;
        }

        private static bool IsHandledState(CsmArmyMissionStatus status)
        {
            return status == CsmArmyMissionStatus.Completed
                || status == CsmArmyMissionStatus.Invalid
                || status == CsmArmyMissionStatus.Expired
                || status == CsmArmyMissionStatus.BesiegingAssignedTarget
                || status == CsmArmyMissionStatus.AssaultingAssignedTarget
                || status == CsmArmyMissionStatus.OperatingOnAssignedTarget
                || status == CsmArmyMissionStatus.ObjectiveMismatch
                || status == CsmArmyMissionStatus.Stalled
                || status == CsmArmyMissionStatus.Unsafe;
        }

        private static bool HasObjectiveMismatch(CsmArmyObjectiveSnapshot objective, CsmArmyAssignment assignment)
        {
            if (objective == null || assignment == null)
            {
                return false;
            }

            if (objective.HasObjectiveTargetMismatch)
            {
                return true;
            }

            if (objective.ArmyAiBehaviorObjectSettlement != null && !IsSameTarget(objective.ArmyAiBehaviorObjectSettlement, assignment.TargetSettlementId, assignment.TargetSettlementName))
            {
                return true;
            }

            return false;
        }

        private static bool IsBesiegingAssignedTarget(CsmArmyObjectiveSnapshot objective, CsmArmyAssignment assignment)
        {
            return objective != null
                && objective.IsBesieging
                && HasNoArmyBehaviorMismatch(objective, assignment)
                && IsSameTarget(objective.CurrentTargetSettlement, assignment.TargetSettlementId, assignment.TargetSettlementName);
        }

        private static bool IsAssaultingAssignedTarget(CsmArmySnapshot snapshot, CsmArmyObjectiveSnapshot objective, CsmArmyAssignment assignment)
        {
            return snapshot.LeaderParty != null
                && snapshot.LeaderParty.MapEvent != null
                && objective != null
                && IsSameTarget(objective.CurrentTargetSettlement, assignment.TargetSettlementId, assignment.TargetSettlementName);
        }

        private static bool IsOperatingOnAssignedTarget(CsmArmyObjectiveSnapshot objective, CsmArmyAssignment assignment)
        {
            if (objective == null || assignment == null || !IsSameTarget(objective.CurrentTargetSettlement, assignment.TargetSettlementId, assignment.TargetSettlementName))
            {
                return false;
            }

            if (!HasNoArmyBehaviorMismatch(objective, assignment))
            {
                return false;
            }

            return objective.CurrentObjective == "MovingToBesiegeSettlement"
                || objective.CurrentObjective == "MovingToSettlement";
        }

        private static bool HasNoArmyBehaviorMismatch(CsmArmyObjectiveSnapshot objective, CsmArmyAssignment assignment)
        {
            return objective == null
                || objective.ArmyAiBehaviorObjectSettlement == null
                || IsSameTarget(objective.ArmyAiBehaviorObjectSettlement, assignment.TargetSettlementId, assignment.TargetSettlementName);
        }

        private static bool IsUnsafeBadSiege(CsmBadSiegeEvaluation badSiege, CsmArmyAssignment assignment)
        {
            if (badSiege == null || !badSiege.IsBadSiege)
            {
                return false;
            }

            if (badSiege.Reason == "Siege target is already assigned to CSM attack"
                && IsSameTarget(badSiege.CurrentTarget, assignment.TargetSettlementId, assignment.TargetSettlementName))
            {
                return false;
            }

            return true;
        }

        private static bool IsRedirectGraceActive(CsmArmyAssignment assignment, CsmArmyMissionState state, int tick)
        {
            return assignment != null
                && state != null
                && assignment.ObjectiveType == "RedirectFromBadSiege"
                && tick - state.LastRedirectTick < ArmyDirectorSettings.RedirectGraceTicks;
        }

        private static bool IsCurrentTargetAssignedTarget(CsmArmyObjectiveSnapshot objective, CsmArmyAssignment assignment)
        {
            return objective != null
                && assignment != null
                && IsSameTarget(objective.CurrentTargetSettlement, assignment.TargetSettlementId, assignment.TargetSettlementName);
        }

        private static bool IsAttackObjective(string objective)
        {
            return objective == "AttackSettlement" || objective == "RedirectFromBadSiege";
        }

        private static bool IsSameTarget(Settlement settlement, string targetSettlementId, string targetSettlementName)
        {
            if (settlement == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(targetSettlementId) && settlement.StringId == targetSettlementId)
            {
                return true;
            }

            return settlement.Name != null && settlement.Name.ToString() == targetSettlementName;
        }

        private static int GetProgressCheckEveryTicks()
        {
            return ArmyDirectorSettings.MissionProgressCheckEveryTicks < 1 ? 1 : ArmyDirectorSettings.MissionProgressCheckEveryTicks;
        }

        private static string GetAssignmentKey(CsmArmyAssignment assignment)
        {
            return assignment == null || assignment.ArmyId == null ? string.Empty : assignment.ArmyId.Trim().ToLowerInvariant();
        }

        private static void LogTransition(int tick, CsmArmyMissionState state)
        {
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || state == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM army mission: tick={tick}, army='{state.ArmyName}', kingdom='{state.KingdomName}', objective='{state.ObjectiveType}', target='{state.TargetSettlementName}', state='{state.CurrentState}', distance={state.CurrentDistanceToTarget:0.00}, stalledTicks={state.StalledTickCount}, syncAttempts={state.SyncAttemptCount}, repathAttempts={state.RepathAttemptCount}, reason='{state.Reason}'");
        }

        private static bool ShouldLogRedirectGraceReasonChange(CsmArmyMissionState state, string previousReason)
        {
            return state != null
                && state.IsRedirectGraceActive
                && state.CurrentState == CsmArmyMissionStatus.Gathering
                && state.Reason == "Redirect grace active; skipping unsafe evaluation"
                && previousReason != state.Reason;
        }
    }
}
