using System;
using System.Collections.Generic;
using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Settings;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class DirectDefenseCommandController
    {
        private readonly CsmDefenseAssignmentRegistry _assignmentRegistry;
        private readonly DefenseCandidateScorer _candidateScorer;
        private readonly DefenseCandidateSelector _candidateSelector;
        private readonly Dictionary<string, int> _settlementCommandCounts;
        private readonly Dictionary<string, int> _coverageEnoughStableTicks;
        private readonly Dictionary<string, DefenseReplacementCandidate> _pendingReplacementBySettlement;
        private readonly HashSet<string> _commandedParties;
        private readonly List<CsmDefenseAssignmentReport> _pendingAssignmentReports;
        private int _currentTick;
        private int _commandsToday;
        private int _assignmentCreatedThisTick;
        private int _assignmentCompletedThisTick;
        private int _assignmentDeescalatedThisTick;
        private int _assignmentExpiredThisTick;
        private int _assignmentInvalidThisTick;
        private int _assignmentProgressExpiredThisTick;
        private int _duplicateAssignmentBlockedThisTick;
        private int _reinforcementAssignmentsCreatedThisTick;
        private int _reinforcementBlockedThisTick;
        private int _movementReassertedThisTick;
        private int _movementStalledThisTick;
        private int _movementInvalidThisTick;
        private int _effectiveAssignmentsThisTick;
        private int _ineffectiveAssignmentsThisTick;
        private int _replacementAssignmentsCreatedThisTick;
        private int _deescalationDelayedThisTick;
        private int _postEnforcementReassertedThisTick;
        private int _reassertedAssignmentsThisTick;
        private int _activeSiegeReassertedThisTick;
        private int _movementFailedThisTick;
        private int _adaptiveGlobalCapBlockedThisTick;
        private int _kingdomCapBlockedThisTick;
        private int _assignmentLifecycleUnprocessedTicks;
        private int _lastAssignmentCreatedTick = -1;

        public DirectDefenseCommandController()
        {
            _assignmentRegistry = new CsmDefenseAssignmentRegistry();
            _candidateScorer = new DefenseCandidateScorer();
            _candidateSelector = new DefenseCandidateSelector();
            _settlementCommandCounts = new Dictionary<string, int>();
            _coverageEnoughStableTicks = new Dictionary<string, int>();
            _pendingReplacementBySettlement = new Dictionary<string, DefenseReplacementCandidate>();
            _commandedParties = new HashSet<string>();
            _pendingAssignmentReports = new List<CsmDefenseAssignmentReport>();
            _currentTick = -1;
        }

        public List<CsmDefenseAssignmentReport> ProcessAssignments(DefenseEvaluationSnapshot snapshot, int observationTick, CsmArmyAssignmentRegistry armyAssignments = null)
        {
            return SafeExecutor.Run(
                "Process CSM defense assignments",
                () => ProcessAssignmentsCore(snapshot, observationTick, armyAssignments),
                new List<CsmDefenseAssignmentReport>());
        }

        public List<CsmDefenseAssignmentReport> ConsumePendingAssignmentReports()
        {
            var reports = new List<CsmDefenseAssignmentReport>(_pendingAssignmentReports);
            _pendingAssignmentReports.Clear();
            return reports;
        }

        public CsmDefenseAssignmentRegistry AssignmentRegistry
        {
            get { return _assignmentRegistry; }
        }

        public int CountActiveAssignments()
        {
            return _assignmentRegistry.CountActiveAssignments();
        }

        public CsmDefenseAssignmentLifecycleSummary GetAssignmentLifecycleSummary(int observationTick)
        {
            return SafeExecutor.Run(
                "Build CSM defense assignment lifecycle summary",
                () =>
                {
                    ResetDailyStateIfNeeded(observationTick);
                    var activeAssignments = _assignmentRegistry.CountActiveAssignments();
                    LogLifecycleWarningIfNeeded(observationTick, activeAssignments);
                    return new CsmDefenseAssignmentLifecycleSummary(
                        observationTick,
                        activeAssignments,
                        _assignmentCreatedThisTick,
                        _assignmentCompletedThisTick,
                        _assignmentDeescalatedThisTick,
                        _assignmentExpiredThisTick,
                        _assignmentInvalidThisTick,
                        _assignmentProgressExpiredThisTick,
                        _duplicateAssignmentBlockedThisTick,
                        _reinforcementAssignmentsCreatedThisTick,
                        _reinforcementBlockedThisTick,
                        _movementReassertedThisTick,
                        _movementStalledThisTick,
                        _movementInvalidThisTick,
                        _effectiveAssignmentsThisTick,
                        _ineffectiveAssignmentsThisTick,
                        _replacementAssignmentsCreatedThisTick,
                        _deescalationDelayedThisTick,
                        _postEnforcementReassertedThisTick,
                        _reassertedAssignmentsThisTick,
                        _activeSiegeReassertedThisTick,
                        _movementFailedThisTick,
                        _adaptiveGlobalCapBlockedThisTick,
                        _kingdomCapBlockedThisTick,
                        DefenseControllerSettings.EnableDefenseControllerV2
                            ? "Defense assignment lifecycle v2 snapshot"
                            : "Defense assignment lifecycle snapshot");
                },
                new CsmDefenseAssignmentLifecycleSummary(
                    observationTick,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    "Defense assignment lifecycle snapshot"));
        }

        public bool ReassertDefenseAssignment(MobileParty party, CsmDefenseAssignment assignment, int observationTick, out string reason)
        {
            var localReason = string.Empty;
            var result = SafeExecutor.Run(
                "Reassert CSM defense task discipline",
                () => ReassertDefenseAssignmentCore(party, assignment, observationTick, out localReason),
                false);
            reason = localReason;
            return result;
        }

        private bool ReassertDefenseAssignmentCore(MobileParty party, CsmDefenseAssignment assignment, int observationTick, out string reason)
        {
            reason = "Defense assignment could not be reasserted";
            if (party == null || assignment == null)
            {
                reason = "Party or defense assignment missing";
                return false;
            }

            if (party.IsMainParty || party.MapEvent != null || party.IsDisbanding || !party.IsActive)
            {
                reason = "Party cannot safely receive task discipline defense reassert";
                return false;
            }

            var settlement = FindSettlementByIdOrName(assignment.SettlementId, assignment.SettlementName);
            if (settlement == null || settlement.MapFaction == null || party.MapFaction != settlement.MapFaction)
            {
                reason = "Defense assignment target is missing or no longer friendly";
                return false;
            }

            party.SetMoveDefendSettlement(settlement, false, party.NavigationCapability);
            _assignmentRegistry.MarkReasserted(assignment, observationTick, "Strategic task discipline reasserted CSM defense assignment");
            reason = "Strategic task discipline reasserted CSM defense assignment";
            return true;
        }

        public DirectDefenseCommandReport Execute(
            DefenseEvaluationSnapshot snapshot,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecision dryRunDecision,
            DryRunDefenseDecisionStabilityReport stabilityReport,
            DefenseControllerSafetyReport safetyReport,
            CsmArmyDirector armyDirector,
            int observationTick)
        {
            return SafeExecutor.Run(
                "Execute direct defense command",
                () => ExecuteCore(snapshot, actionPlan, dryRunDecision, stabilityReport, safetyReport, armyDirector, observationTick),
                CreateReport(observationTick, "unknown", "unknown", "UrgentDefense", "none", PartyObservationCategory.Unknown, false, false, "Direct defense command failed"));
        }

        private List<CsmDefenseAssignmentReport> ProcessAssignmentsCore(DefenseEvaluationSnapshot snapshot, int observationTick, CsmArmyAssignmentRegistry armyAssignments)
        {
            ResetDailyStateIfNeeded(observationTick);
            var reports = new List<CsmDefenseAssignmentReport>();
            if (!DefenseAssignmentSettings.EnableDefenseAssignments)
            {
                return reports;
            }

            var settlementName = snapshot.ThreatReport.SettlementName;
            var settlement = FindSettlementByName(settlementName);
            var settlementId = GetSettlementId(settlement);
            var assignments = _assignmentRegistry.GetActiveAssignmentsForSettlement(settlementId, settlementName);
            for (var index = 0; index < assignments.Count; index++)
            {
                var assignment = assignments[index];
                var party = FindPartyByIdOrName(assignment.PartyId, assignment.PartyName);
                UpdateDefenseAuthorityCounters(assignment, party, armyAssignments, observationTick);
                var invalidReason = GetInvalidAssignmentReason(snapshot, assignment, party, settlement, observationTick);
                if (string.IsNullOrWhiteSpace(invalidReason) && HasActiveCsmArmyAssignment(party, armyAssignments))
                {
                    invalidReason = "Assigned party has active CSM army assignment";
                }

                if (!string.IsNullOrWhiteSpace(invalidReason))
                {
                    _movementInvalidThisTick++;
                    var invalidDistance = party == null || settlement == null ? 0f : party.Position.Distance(settlement.Position);
                    LogDefenseAssignmentMovementIssue(observationTick, assignment, settlement, party, GetInvalidMovementIssue(invalidReason), invalidDistance, assignment.StalledTicks, false, invalidReason);
                    var status = GetClosedAssignmentStatus(assignment, observationTick, invalidReason);
                    if (TryCloseDefenseAssignment(snapshot, assignment, party, settlement, armyAssignments, observationTick, status, invalidReason, invalidReason, false))
                    {
                        reports.Add(CreateAssignmentReport(observationTick, assignment, status, false, invalidReason));
                    }

                    continue;
                }

                var movement = TrackDefenseAssignmentMovement(assignment, party, settlement, observationTick);
                LogDefenseAssignmentMovement(movement);
                var effectiveness = EvaluateDefenseAssignmentEffectiveness(assignment, party, settlement, movement, observationTick);
                LogDefenseAssignmentEffectiveness(effectiveness);
                EvaluateAndLogDefenseCommitment(snapshot, assignment, party, settlement, movement, observationTick);
                LogDefenseAuthorityHardLockDiagnostics(snapshot, assignment, party, settlement, movement, observationTick);

                var deescalatedReason = GetDeescalatedAssignmentReason(snapshot, settlement, assignment, effectiveness, observationTick);
                if (!string.IsNullOrWhiteSpace(deescalatedReason))
                {
                    if (NamesEqual(deescalatedReason, "Deescalation delayed because defense assignments are not committed yet"))
                    {
                        _deescalationDelayedThisTick++;
                        LogDefenseDeescalationDelayed(observationTick, assignment, effectiveness, deescalatedReason);
                    }
                    else
                    {
                        if (TryCloseDefenseAssignment(snapshot, assignment, party, settlement, armyAssignments, observationTick, "Deescalated", deescalatedReason, deescalatedReason, false))
                        {
                            reports.Add(CreateAssignmentReport(observationTick, assignment, "Deescalated", false, deescalatedReason));
                        }

                        continue;
                    }
                }

                var completionReason = GetCompletedAssignmentReason(snapshot, assignment, party, settlement, movement, observationTick);
                if (!string.IsNullOrWhiteSpace(completionReason))
                {
                    if (TryCloseDefenseAssignment(snapshot, assignment, party, settlement, armyAssignments, observationTick, "Completed", completionReason, completionReason, false))
                    {
                        reports.Add(CreateAssignmentReport(observationTick, assignment, "Completed", false, completionReason));
                    }

                    continue;
                }

                var issue = GetMovementIssue(movement);
                var reasserted = false;
                var activeSiegeAssignment = IsActiveSiegeAssignment(snapshot, settlement);
                if (ShouldFailActiveSiegeDefenseMovement(snapshot, assignment, party, settlement, movement))
                {
                    var failedReason = "Active siege defender failed movement";
                    if (TryCloseDefenseAssignment(snapshot, assignment, party, settlement, armyAssignments, observationTick, "Expired", failedReason, failedReason, false))
                    {
                        _pendingReplacementBySettlement[Normalize(assignment.SettlementName)] = new DefenseReplacementCandidate
                        {
                            OldPartyName = assignment.PartyName,
                            IneffectiveTicks = assignment.IneffectiveTicks,
                            FailedPartyName = assignment.PartyName,
                            Reason = failedReason
                        };
                        _movementFailedThisTick++;
                        LogActiveSiegeDefensePersistence(observationTick, assignment, movement, false, false, "MovementFailed", "Active siege defense persistence evaluation");
                        reports.Add(CreateAssignmentReport(observationTick, assignment, "Expired", false, failedReason));
                    }

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(issue))
                {
                    _movementStalledThisTick++;
                    reasserted = TryReassertDefenseMovement(snapshot, assignment, party, settlement, movement, observationTick);
                    LogDefenseAssignmentMovementIssue(observationTick, assignment, settlement, party, issue, movement.DistanceToTarget, assignment.StalledTicks, reasserted, "Defense assignment movement issue detected");
                    LogActiveSiegeDefensePersistence(observationTick, assignment, movement, true, activeSiegeAssignment && reasserted, reasserted ? "Reasserted" : "Kept", "Active siege defense persistence evaluation");
                    if (reasserted)
                    {
                        movement.Reasserted = true;
                        LogDefenseAssignmentMovement(movement);
                        reports.Add(CreateAssignmentReport(observationTick, assignment, "Reasserted", true, "Defense target overwritten; reasserted movement"));
                        continue;
                    }
                }
                else
                {
                    LogActiveSiegeDefensePersistence(observationTick, assignment, movement, false, false, "Kept", "Active siege defense persistence evaluation");
                }

                var replacementReason = GetIneffectiveReplacementReason(assignment, effectiveness);
                if (!string.IsNullOrWhiteSpace(replacementReason))
                {
                    if (TryCloseDefenseAssignment(snapshot, assignment, party, settlement, armyAssignments, observationTick, "Expired", replacementReason, replacementReason, false))
                    {
                        _pendingReplacementBySettlement[Normalize(assignment.SettlementName)] = new DefenseReplacementCandidate
                        {
                            OldPartyName = assignment.PartyName,
                            IneffectiveTicks = assignment.IneffectiveTicks,
                            FailedPartyName = assignment.PartyName,
                            Reason = replacementReason
                        };
                        reports.Add(CreateAssignmentReport(observationTick, assignment, "Expired", false, replacementReason));
                    }

                    continue;
                }

                var expiredReason = GetProgressExpiredReason(assignment, party, settlement, observationTick);
                if (!string.IsNullOrWhiteSpace(expiredReason))
                {
                    if (TryCloseDefenseAssignment(snapshot, assignment, party, settlement, armyAssignments, observationTick, "Expired", expiredReason, expiredReason, true))
                    {
                        reports.Add(CreateAssignmentReport(observationTick, assignment, "Expired", false, expiredReason));
                    }
                }
            }

            return reports;
        }

        public CsmDefensePostEnforcementReport PostDirectorEnforceAssignments(
            List<DefenseEvaluationSnapshot> snapshots,
            CsmArmyAssignmentRegistry armyAssignments,
            int observationTick)
        {
            return SafeExecutor.Run(
                "Post-director CSM defense movement enforcement",
                () => PostDirectorEnforceAssignmentsCore(snapshots, armyAssignments, observationTick),
                new CsmDefensePostEnforcementReport(observationTick, 0, 0, "Post-director defense movement enforcement pass"));
        }

        private CsmDefensePostEnforcementReport PostDirectorEnforceAssignmentsCore(
            List<DefenseEvaluationSnapshot> snapshots,
            CsmArmyAssignmentRegistry armyAssignments,
            int observationTick)
        {
            ResetDailyStateIfNeeded(observationTick);
            if (!DefenseAssignmentSettings.EnableDefenseAssignments)
            {
                return new CsmDefensePostEnforcementReport(observationTick, 0, 0, "Post-director defense movement enforcement pass");
            }

            var reasserted = 0;
            var stalled = 0;
            var assignments = _assignmentRegistry.GetActiveAssignments();
            for (var index = 0; index < assignments.Count; index++)
            {
                var assignment = assignments[index];
                var settlement = FindSettlementByIdOrName(assignment.SettlementId, assignment.SettlementName);
                var party = FindPartyByIdOrName(assignment.PartyId, assignment.PartyName);
                var snapshot = FindSnapshotForSettlement(snapshots, assignment.SettlementId, assignment.SettlementName);
                UpdateDefenseAuthorityCounters(assignment, party, armyAssignments, observationTick);
                LogDefenseAuthorityHardLockDiagnostics(snapshot, assignment, party, settlement, null, observationTick);
                var staleReason = GetStaleAssignmentCleanupReason(snapshot, assignment, party, settlement, armyAssignments, observationTick);
                if (!string.IsNullOrWhiteSpace(staleReason))
                {
                    var status = GetStaleAssignmentCleanupStatus(staleReason);
                    var closeReason = "Stale defense assignment cleanup: " + staleReason;
                    if (TryCloseDefenseAssignment(snapshot, assignment, party, settlement, armyAssignments, observationTick, status, staleReason, closeReason, NamesEqual(status, "Expired")))
                    {
                        _pendingAssignmentReports.Add(CreateAssignmentReport(observationTick, assignment, status, false, closeReason));
                        LogDefenseAssignmentCleanup(observationTick, assignment, status);
                    }
                    else
                    {
                        LogDefenseAssignmentCleanupBlocked(observationTick, assignment, staleReason);
                    }

                    continue;
                }

                if (snapshot == null || party == null || settlement == null)
                {
                    continue;
                }

                var movement = TrackDefenseAssignmentMovement(assignment, party, settlement, observationTick);
                var issue = GetMovementIssue(movement);
                if (string.IsNullOrWhiteSpace(issue))
                {
                    continue;
                }

                stalled++;
                if (TryReassertDefenseMovement(snapshot.Value, assignment, party, settlement, movement, observationTick))
                {
                    reasserted++;
                    _postEnforcementReassertedThisTick++;
                    movement.Reasserted = true;
                    LogDefenseAssignmentMovement(movement);
                }
            }

            return new CsmDefensePostEnforcementReport(observationTick, reasserted, stalled, "Post-director defense movement enforcement pass");
        }

        private static DefenseEvaluationSnapshot? FindSnapshotForSettlement(
            List<DefenseEvaluationSnapshot> snapshots,
            string settlementId,
            string settlementName)
        {
            if (snapshots == null)
            {
                return null;
            }

            for (var index = 0; index < snapshots.Count; index++)
            {
                var snapshot = snapshots[index];
                if (NamesEqual(snapshot.ThreatReport.SettlementName, settlementName))
                {
                    return snapshot;
                }
            }

            return null;
        }

        private DirectDefenseCommandReport ExecuteCore(
            DefenseEvaluationSnapshot snapshot,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecision dryRunDecision,
            DryRunDefenseDecisionStabilityReport stabilityReport,
            DefenseControllerSafetyReport safetyReport,
            CsmArmyDirector armyDirector,
            int observationTick)
        {
            ResetDailyStateIfNeeded(observationTick);

            var settlementName = snapshot.ThreatReport.SettlementName;
            var candidateName = actionPlan.PrimaryCandidateName;
            var actionTier = GetActionTier(snapshot, actionPlan, stabilityReport);
            if (!DefenseControllerSettings.EnableRealDefenseController || !DefenseActionThresholdSettings.EnableRealDefenseController)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Real defense controller disabled");
            }

            if (!DirectDefenseCommandSettings.EnableDirectDefenseCommand || !DirectDefenseCommandSettings.EnableDirectDefenseCommands)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Direct defense command disabled");
            }

            var settlementFilter = DirectDefenseCommandSettings.SettlementNameFilter;
            if (DirectDefenseCommandSettings.RequireSettlementNameFilter && string.IsNullOrWhiteSpace(settlementFilter))
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Settlement name filter required");
            }

            if (!string.IsNullOrWhiteSpace(settlementFilter) && !NamesEqual(settlementName, settlementFilter))
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Settlement filter mismatch");
            }

            var settlement = FindSettlementByName(settlementName);
            var adaptiveActiveSiegeDefense = ShouldUseAdaptiveActiveSiegeDefense(snapshot, settlement);
            if (adaptiveActiveSiegeDefense)
            {
                actionTier = "AdaptiveActiveSiegeDefense";
            }

            if (DirectDefenseCommandSettings.OnlyUrgentDefense && !IsUrgentDefenseAction(actionPlan.RecommendedAction) && !adaptiveActiveSiegeDefense)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Action is not urgent defense");
            }

            if (DefenseControllerSettings.EnableDefenseControllerV2
                && (NamesEqual(actionTier, "Monitor") || NamesEqual(actionTier, "ReinforcementNeeded"))
                && !adaptiveActiveSiegeDefense)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, actionTier + " does not execute direct defense command");
            }

            if (!DefenseActionThresholdSettings.AllowUrgentDefenseCommands)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Urgent defense commands disabled");
            }

            if (!IsUrgentDefenseAction(actionPlan.RecommendedAction) && !adaptiveActiveSiegeDefense)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Recommended action is not UrgentDefense");
            }

            if (!DefenseControllerSettings.EnableDefenseControllerV2 && !IsCriticalCoverage(snapshot.CoverageReport) && !adaptiveActiveSiegeDefense)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Legacy controller requires critical coverage");
            }

            if (actionPlan.DefensePriority < DefenseActionThresholdSettings.MinimumDefensePriorityToAct && !adaptiveActiveSiegeDefense)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Defense priority is below activation threshold");
            }

            if (actionPlan.PlanConfidence < DefenseActionThresholdSettings.MinimumDefenseConfidenceToAct && !adaptiveActiveSiegeDefense)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Defense confidence is below activation threshold");
            }

            var immediateCriticalReaction = EvaluateImmediateCriticalSiegeDefense(snapshot, settlement, safetyReport);
            var lowSiegeEarlyReinforcement = EvaluateLowSiegeEarlyReinforcement(snapshot, settlement, safetyReport);

            if ((!dryRunDecision.WouldAct || !safetyReport.DryRunWouldAct) && !adaptiveActiveSiegeDefense)
            {
                LogCriticalDefenseReaction(immediateCriticalReaction, snapshot, observationTick, 0);
                return CreateReport(observationTick, settlementName, candidateName, false, "Dry-run would-act signal required");
            }

            if (!safetyReport.Allowed
                && !CanBypassStableSignalForImmediateCriticalSiege(immediateCriticalReaction, safetyReport)
                && !CanBypassStableSignalForLowSiegeEarlyReinforcement(lowSiegeEarlyReinforcement, safetyReport)
                && !CanBypassDryRunSafetyForAdaptiveActiveSiege(adaptiveActiveSiegeDefense, safetyReport))
            {
                LogCriticalDefenseReaction(immediateCriticalReaction, snapshot, observationTick, 0);
                return CreateReport(observationTick, settlementName, candidateName, false, "Safety guard blocked execution");
            }

            if (DefenseActionThresholdSettings.RequireStableDefenseSignal
                && !immediateCriticalReaction.ImmediateReactionAllowed
                && !lowSiegeEarlyReinforcement.Allowed
                && !adaptiveActiveSiegeDefense
                && (!IsUrgentDefenseAction(stabilityReport.StableAction)
                    || stabilityReport.ConsecutiveSameActionCount < GetRequiredStableUrgentTicks()))
            {
                LogCriticalDefenseReaction(immediateCriticalReaction, snapshot, observationTick, 0);
                return CreateReport(observationTick, settlementName, candidateName, false, "Stable urgent defense signal required");
            }

            if (!IsUrgentDefenseAction(dryRunDecision.Action) && !adaptiveActiveSiegeDefense)
            {
                LogCriticalDefenseReaction(immediateCriticalReaction, snapshot, observationTick, 0);
                return CreateReport(observationTick, settlementName, candidateName, false, "Dry-run action is not RequestUrgentDefense");
            }

            var dailyCommandBudgetOverrideEligible = IsActiveSiegeDefenseBudgetOverrideEligible(snapshot, settlement, observationTick);
            var dailyCommandLimitReached = _commandsToday >= GetMaxCommandsPerDay();
            var dailyCommandBudgetOverrideApplied = dailyCommandLimitReached && dailyCommandBudgetOverrideEligible;
            if (dailyCommandLimitReached && !dailyCommandBudgetOverrideApplied)
            {
                LogDefenseCommandBudgetOverride(snapshot, settlement, observationTick, false);
                return CreateReport(observationTick, settlementName, candidateName, false, "Daily direct command limit reached");
            }

            if (GetSettlementCommandCount(settlementName) >= GetMaxCommandsPerSettlementPerDay()
                && !dailyCommandBudgetOverrideEligible)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Daily settlement direct command limit reached");
            }

            if (settlement == null)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Settlement not found");
            }

            var coverageBlockReason = adaptiveActiveSiegeDefense
                ? null
                : GetCoverageExecutionBlockReason(snapshot, actionPlan, null);
            if (!string.IsNullOrWhiteSpace(coverageBlockReason))
            {
                return CreateReport(observationTick, settlementName, candidateName, false, coverageBlockReason);
            }

            if (!settlement.IsCastle && !settlement.IsTown)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Settlement is not a castle or town");
            }

            if (DefenseActionThresholdSettings.RequireTargetSettlementUnderSiegeForUrgentDefense && settlement.SiegeEvent == null)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Target settlement is not under active siege");
            }

            if (!DefenseActionThresholdSettings.AllowArmyPresenceDefenseWithoutSiege && !snapshot.ThreatReport.HasActiveSiege)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Army presence without siege is monitor-only");
            }

            if (ShouldUseCoverageBasedDefenseReinforcement(snapshot, settlement))
            {
                return ExecuteCoverageBasedReinforcement(
                    snapshot,
                    settlement,
                    candidateName,
                    actionTier,
                    armyDirector == null ? null : armyDirector.AssignmentRegistry,
                    immediateCriticalReaction.ImmediateReactionAllowed,
                    lowSiegeEarlyReinforcement.Allowed,
                    dailyCommandBudgetOverrideEligible,
                    dailyCommandBudgetOverrideApplied,
                    observationTick);
            }

            var capEvaluation = EvaluateDefenseAssignmentCaps(snapshot, settlement, observationTick, 0);
            if (capEvaluation.Blocked)
            {
                LogDefenseAssignmentCapEvaluation(capEvaluation);
                RecordCapBlocked(capEvaluation);
                return CreateReport(observationTick, settlementName, candidateName, false, capEvaluation.Reason);
            }

            var activeAssignment = GetValidActiveAssignmentForSettlement(snapshot, settlement, observationTick);
            if (activeAssignment != null)
            {
                _duplicateAssignmentBlockedThisTick++;
                return CreateReport(observationTick, settlementName, activeAssignment.PartyName, false, "Active CSM defense assignment already exists");
            }

            DefenseCandidateScore topRejected;
            DefenseCandidateScoringSummary candidateScoringSummary;
            var selectedCandidate = _candidateScorer.SelectBest(settlement, snapshot.CandidateReports, candidateName, _assignmentRegistry, armyDirector == null ? null : armyDirector.AssignmentRegistry, actionTier, out topRejected, out candidateScoringSummary);
            LogCandidateScore(observationTick, actionTier, selectedCandidate, topRejected, candidateScoringSummary);
            if (selectedCandidate == null)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, GetCandidateRejectedReason(topRejected));
            }

            coverageBlockReason = adaptiveActiveSiegeDefense
                ? null
                : GetCoverageExecutionBlockReason(snapshot, actionPlan, selectedCandidate);
            if (!string.IsNullOrWhiteSpace(coverageBlockReason))
            {
                return CreateReport(observationTick, settlementName, selectedCandidate.CandidateName, false, coverageBlockReason);
            }

            var candidate = selectedCandidate.Party;
            var commandParty = GetCommandParty(candidate);
            if (commandParty == null)
            {
                return CreateReport(observationTick, settlementName, selectedCandidate.CandidateName, false, "Command party not found");
            }

            var commandPartyName = GetPartyName(commandParty);
            var commandPartyCategory = GetCommandPartyCategory(commandParty);
            if (_commandedParties.Contains(Normalize(commandPartyName)))
            {
                return CreateReport(observationTick, settlementName, commandPartyName, false, "Party already received direct command today");
            }

            var partyBlockReason = GetCommandPartyBlockReason(commandParty, settlement, selectedCandidate.Distance, _assignmentRegistry, armyDirector == null ? null : armyDirector.AssignmentRegistry, actionTier);
            if (!string.IsNullOrWhiteSpace(partyBlockReason))
            {
                return CreateReport(observationTick, settlementName, commandPartyName, false, partyBlockReason);
            }

            var settlementId = GetSettlementId(settlement);
            var commandPartyId = GetPartyId(commandParty);
            if (DefenseAssignmentSettings.EnableDefenseAssignments
                && _assignmentRegistry.HasActiveAssignment(settlementId, settlementName, commandPartyId, commandPartyName))
            {
                _duplicateAssignmentBlockedThisTick++;
                return CreateReport(observationTick, settlementName, commandPartyName, false, "Active CSM defense assignment already exists");
            }

            if (dailyCommandBudgetOverrideApplied)
            {
                LogDefenseCommandBudgetOverride(snapshot, settlement, observationTick, true);
            }

            commandParty.SetMoveDefendSettlement(settlement, false, commandParty.NavigationCapability);

            _commandsToday++;
            _commandedParties.Add(Normalize(commandPartyName));
            IncrementSettlementCommandCount(settlementName);
            if (DefenseAssignmentSettings.EnableDefenseAssignments)
            {
                var assignment = _assignmentRegistry.CreateOrUpdate(
                    settlementId,
                    settlementName,
                    commandPartyId,
                    commandPartyName,
                    observationTick,
                    "Direct defense command created CSM assignment");
                assignment.OwnerKingdomName = snapshot.ThreatReport.OwnerKingdomName;
                assignment.LastDistanceToSettlement = settlement.Position.Distance(commandParty.Position);
                assignment.LastProgressTick = observationTick;
                if (assignment.Status == "Created")
                {
                    _assignmentCreatedThisTick++;
                    _lastAssignmentCreatedTick = observationTick;
                    RecordReplacementAssignmentIfNeeded(observationTick, settlementName, commandPartyName);
                }

                _pendingAssignmentReports.Add(CreateAssignmentReport(observationTick, assignment, assignment.Status, true, "Direct defense command created CSM assignment"));
            }

            return CreateReport(
                observationTick,
                settlementName,
                snapshot.ThreatReport.OwnerKingdomName,
                actionTier,
                commandPartyName,
                commandPartyCategory,
                true,
                true,
                dailyCommandBudgetOverrideApplied
                    ? "Active siege defense bypassed daily direct command limit"
                    : "Direct urgent defense command applied");
        }

        private DirectDefenseCommandReport ExecuteCoverageBasedReinforcement(
            DefenseEvaluationSnapshot snapshot,
            Settlement settlement,
            string fallbackCandidateName,
            string actionTier,
            CsmArmyAssignmentRegistry armyAssignments,
            bool immediateCriticalSiegeDefense,
            bool lowSiegeEarlyReinforcement,
            bool dailyCommandBudgetOverrideEligible,
            bool dailyCommandBudgetOverrideApplied,
            int observationTick)
        {
            var settlementName = snapshot.ThreatReport.SettlementName;
            var context = BuildReinforcementContext(snapshot, settlement, observationTick);
            LogReinforcementNeed(context);
            var adaptiveActiveSiegeDefense = ShouldUseAdaptiveActiveSiegeDefense(snapshot, settlement);

            if (!context.AdditionalDefenseNeeded)
            {
                RecordReinforcementBlocked(context, "Coverage target already met");
                if (adaptiveActiveSiegeDefense)
                {
                    LogAdaptiveActiveSiegeDefenseSatisfied(context);
                }

                LogDefenseCommandBudgetOverrideNotAppliedIfNeeded(snapshot, settlement, observationTick, dailyCommandBudgetOverrideApplied);
                return CreateReport(observationTick, settlementName, fallbackCandidateName, false, "Coverage target already met");
            }

            if (lowSiegeEarlyReinforcement && context.ActiveAssignedDefenderStrength >= context.RequiredThreatStrength)
            {
                RecordReinforcementBlocked(context, "Effective assigned defenders already cover threat");
                LogDefenseCommandBudgetOverrideNotAppliedIfNeeded(snapshot, settlement, observationTick, dailyCommandBudgetOverrideApplied);
                return CreateReport(observationTick, settlementName, fallbackCandidateName, false, "Effective assigned defenders already cover threat");
            }

            if (context.ActiveDefenseAssignments >= GetMaxDefenseAssignmentsPerSettlementSafetyCap())
            {
                var safetyCapReason = immediateCriticalSiegeDefense ? "Settlement safety cap reached" : "safety cap reached";
                RecordReinforcementBlocked(context, safetyCapReason);
                LogDefenseCommandBudgetOverrideNotAppliedIfNeeded(snapshot, settlement, observationTick, dailyCommandBudgetOverrideApplied);
                return CreateReport(observationTick, settlementName, fallbackCandidateName, false, safetyCapReason);
            }

            var maxReinforcements = adaptiveActiveSiegeDefense
                ? NamesEqual(context.CoverageStatus, "Critical")
                    ? GetMaxAdaptiveSiegeDefenseAssignmentsPerTickCritical()
                    : GetMaxAdaptiveSiegeDefenseAssignmentsPerTick()
                : immediateCriticalSiegeDefense
                ? GetMaxInitialCriticalDefenseReinforcements()
                : lowSiegeEarlyReinforcement
                ? GetMaxInitialLowSiegeReinforcements()
                : GetMaxDefenseReinforcementsPerTick();
            if (!immediateCriticalSiegeDefense && !dailyCommandBudgetOverrideEligible)
            {
                var availableDirectCommands = GetMaxCommandsPerDay() - _commandsToday;
                if (availableDirectCommands < maxReinforcements)
                {
                    maxReinforcements = availableDirectCommands;
                }
            }

            if (!DefenseAssignmentSettings.EnableDefenseAssignments && maxReinforcements > 1)
            {
                maxReinforcements = 1;
            }

            if (maxReinforcements <= 0)
            {
                RecordReinforcementBlocked(context, "safety cap reached");
                LogDefenseCommandBudgetOverrideNotAppliedIfNeeded(snapshot, settlement, observationTick, dailyCommandBudgetOverrideApplied);
                return CreateReport(observationTick, settlementName, fallbackCandidateName, false, "safety cap reached");
            }

            var settlementId = GetSettlementId(settlement);
            var estimatedCoverage = context.EstimatedCoverageAfterAssignments;
            var assignmentsCreated = 0;
            DirectDefenseCommandReport appliedReport = default(DirectDefenseCommandReport);
            var blockedReason = string.Empty;
            var blockedCandidateName = fallbackCandidateName;
            var candidatePool = GetReinforcementCandidatePool(snapshot, settlement, immediateCriticalSiegeDefense, lowSiegeEarlyReinforcement, adaptiveActiveSiegeDefense, context, estimatedCoverage, context.TargetCoverageRatio);
            RemovePendingFailedReplacementCandidate(settlementName, candidatePool);
            var rejectedCandidateNames = new HashSet<string>();
            DefenseCandidateScoringSummary lastScoringSummary = default(DefenseCandidateScoringSummary);
            var lowSiegeReassignmentsCreated = 0;

            while (assignmentsCreated < maxReinforcements && ShouldContinueAdaptiveSiegeReinforcement(context, adaptiveActiveSiegeDefense, estimatedCoverage))
            {
                var capEvaluation = EvaluateDefenseAssignmentCaps(snapshot, settlement, observationTick, assignmentsCreated);
                if (adaptiveActiveSiegeDefense)
                {
                    LogDefenseAssignmentCapEvaluation(capEvaluation);
                }

                if (capEvaluation.Blocked)
                {
                    if (!adaptiveActiveSiegeDefense)
                    {
                        LogDefenseAssignmentCapEvaluation(capEvaluation);
                    }

                    RecordCapBlocked(capEvaluation);
                    blockedReason = capEvaluation.Reason;
                    break;
                }

                var activeAssignmentsBefore = context.ActiveDefenseAssignments + assignmentsCreated;
                if (activeAssignmentsBefore >= GetMaxDefenseAssignmentsPerSettlementSafetyCap())
                {
                    blockedReason = immediateCriticalSiegeDefense ? "Settlement safety cap reached" : "safety cap reached";
                    break;
                }

                DefenseCandidateScore topRejected;
                DefenseCandidateScoringSummary candidateScoringSummary;
                var availableCandidatePool = FilterCandidatePool(candidatePool, rejectedCandidateNames);
                if ((immediateCriticalSiegeDefense || lowSiegeEarlyReinforcement || adaptiveActiveSiegeDefense) && availableCandidatePool.Count <= 0)
                {
                    blockedReason = immediateCriticalSiegeDefense ? "No valid immediate candidates" : "No valid low siege candidates";
                    break;
                }

                var scoringActionTier = context.ScarceLocalRelief
                    ? "ScarceLocalSiegeRelief"
                    : adaptiveActiveSiegeDefense
                    ? "AdaptiveActiveSiegeDefense"
                    : immediateCriticalSiegeDefense
                    ? "ImmediateCriticalDefense"
                    : lowSiegeEarlyReinforcement
                    ? "LowActiveSiegeDefense"
                    : actionTier;
                var selectedCandidate = _candidateScorer.SelectBest(
                    settlement,
                    availableCandidatePool,
                    fallbackCandidateName,
                    _assignmentRegistry,
                    armyAssignments,
                    scoringActionTier,
                    out topRejected,
                    out candidateScoringSummary);
                lastScoringSummary = candidateScoringSummary;
                LogCandidateScore(observationTick, scoringActionTier, selectedCandidate, topRejected, candidateScoringSummary);
                if (IsActiveSiegeCandidateDiagnosticEnabled(snapshot, settlement, immediateCriticalSiegeDefense, lowSiegeEarlyReinforcement))
                {
                    LogActiveSiegeCandidatePoolDiagnostic(context, candidatePool.Count, candidateScoringSummary);
                    LogBlockedReassignmentIfTopRejected(observationTick, topRejected, settlementName);
                }

                if (immediateCriticalSiegeDefense)
                {
                    LogImmediateCriticalCandidatePool(context, candidatePool.Count, candidateScoringSummary);
                }

                if (selectedCandidate == null)
                {
                    blockedReason = immediateCriticalSiegeDefense ? "No valid immediate candidates" : "no valid additional candidate";
                    blockedCandidateName = topRejected == null ? fallbackCandidateName : topRejected.CandidateName;
                    if (lowSiegeEarlyReinforcement
                        && IsReassignmentRejection(topRejected)
                        && RejectCandidateAndContinue(rejectedCandidateNames, topRejected))
                    {
                        continue;
                    }

                    break;
                }

                var commandParty = GetCommandParty(selectedCandidate.Party);
                if (commandParty == null)
                {
                    blockedReason = immediateCriticalSiegeDefense ? "No valid immediate candidates" : "no valid additional candidate";
                    blockedCandidateName = selectedCandidate.CandidateName;
                    if ((immediateCriticalSiegeDefense || lowSiegeEarlyReinforcement || adaptiveActiveSiegeDefense) && RejectCandidateAndContinue(rejectedCandidateNames, selectedCandidate))
                    {
                        continue;
                    }

                    break;
                }

                var commandPartyName = GetPartyName(commandParty);
                var commandPartyCategory = GetCommandPartyCategory(commandParty);
                var commandPartyId = GetPartyId(commandParty);
                blockedCandidateName = commandPartyName;
                if (_commandedParties.Contains(Normalize(commandPartyName)))
                {
                    blockedReason = "candidate already assigned";
                    if ((immediateCriticalSiegeDefense || lowSiegeEarlyReinforcement || adaptiveActiveSiegeDefense) && RejectCandidateAndContinue(rejectedCandidateNames, selectedCandidate))
                    {
                        continue;
                    }

                    break;
                }

                if (DefenseAssignmentSettings.EnableDefenseAssignments
                    && _assignmentRegistry.HasActiveAssignment(settlementId, settlementName, commandPartyId, commandPartyName))
                {
                    _duplicateAssignmentBlockedThisTick++;
                    blockedReason = "candidate already assigned";
                    if ((immediateCriticalSiegeDefense || lowSiegeEarlyReinforcement || adaptiveActiveSiegeDefense) && RejectCandidateAndContinue(rejectedCandidateNames, selectedCandidate))
                    {
                        continue;
                    }

                    break;
                }

                var partyBlockReason = GetCommandPartyBlockReason(commandParty, settlement, selectedCandidate.Distance, _assignmentRegistry, armyAssignments, scoringActionTier);
                if (!string.IsNullOrWhiteSpace(partyBlockReason))
                {
                    blockedReason = IsDefenseAssignmentBlockReason(partyBlockReason)
                        ? "candidate already assigned"
                        : immediateCriticalSiegeDefense ? "No valid immediate candidates" : "no valid additional candidate";
                    if ((immediateCriticalSiegeDefense || lowSiegeEarlyReinforcement || adaptiveActiveSiegeDefense) && RejectCandidateAndContinue(rejectedCandidateNames, selectedCandidate))
                    {
                        continue;
                    }

                    break;
                }

                var estimatedCoverageBefore = estimatedCoverage;
                var estimatedCoverageAfter = EstimateCoverageAfterCandidate(context.RequiredThreatStrength, estimatedCoverageBefore, selectedCandidate);
                if (dailyCommandBudgetOverrideEligible && _commandsToday >= GetMaxCommandsPerDay())
                {
                    dailyCommandBudgetOverrideApplied = true;
                    LogDefenseCommandBudgetOverride(snapshot, settlement, observationTick, true);
                }

                if (lowSiegeEarlyReinforcement && selectedCandidate.IsReassignmentAllowed && lowSiegeReassignmentsCreated >= 1)
                {
                    blockedReason = "Low active siege reassignment limit reached";
                    if (RejectCandidateAndContinue(rejectedCandidateNames, selectedCandidate))
                    {
                        continue;
                    }

                    break;
                }

                var baselineOrAdaptiveNeed = adaptiveActiveSiegeDefense
                    && (context.EffectiveFieldDefenders < context.RequiredBaselineFieldDefenders
                        || context.EffectiveDefenseStrength < context.DesiredEffectiveDefenseStrength);
                if (NamesEqual(context.CoverageStatus, "Low")
                    && !baselineOrAdaptiveNeed
                    && estimatedCoverageAfter - estimatedCoverageBefore < DefenseControllerSettings.MinimumCandidateCoverageImprovement)
                {
                    blockedReason = "Candidate does not improve coverage";
                    break;
                }

                commandParty.SetMoveDefendSettlement(settlement, false, commandParty.NavigationCapability);
                CloseReassignedDefenseAssignmentIfNeeded(selectedCandidate, commandParty, settlementId, settlementName, observationTick);

                _commandsToday++;
                _commandedParties.Add(Normalize(commandPartyName));
                IncrementSettlementCommandCount(settlementName);
                assignmentsCreated++;
                estimatedCoverage = estimatedCoverageAfter;
                context.EffectiveDefenseStrength += GetProjectedCandidateAdaptiveContribution(selectedCandidate);
                context.DefenseStrengthDeficit = context.DesiredEffectiveDefenseStrength - context.EffectiveDefenseStrength;
                if (context.DefenseStrengthDeficit < 0f)
                {
                    context.DefenseStrengthDeficit = 0f;
                }

                if (selectedCandidate.Distance <= GetFarDefenderDistanceThreshold())
                {
                    context.EffectiveFieldDefenders++;
                }

                blockedReason = string.Empty;

                if (DefenseAssignmentSettings.EnableDefenseAssignments)
                {
                    var assignment = _assignmentRegistry.CreateOrUpdate(
                        settlementId,
                        settlementName,
                        commandPartyId,
                        commandPartyName,
                        observationTick,
                        "Coverage-based reinforcement created CSM defense assignment");
                    assignment.OwnerKingdomName = snapshot.ThreatReport.OwnerKingdomName;
                    assignment.LastDistanceToSettlement = settlement.Position.Distance(commandParty.Position);
                    assignment.LastProgressTick = observationTick;
                    if (assignment.Status == "Created")
                    {
                        _assignmentCreatedThisTick++;
                        _lastAssignmentCreatedTick = observationTick;
                        _reinforcementAssignmentsCreatedThisTick++;
                        RecordReplacementAssignmentIfNeeded(observationTick, settlementName, commandPartyName);
                    }

                    _pendingAssignmentReports.Add(CreateAssignmentReport(observationTick, assignment, assignment.Status, true, "Coverage-based reinforcement created CSM defense assignment"));
                }
                else
                {
                    _reinforcementAssignmentsCreatedThisTick++;
                }

                LogReinforcementAssignment(
                    context,
                    commandPartyName,
                    selectedCandidate.Strength,
                    selectedCandidate.Distance,
                    activeAssignmentsBefore,
                    estimatedCoverageBefore,
                    estimatedCoverageAfter);
                if (selectedCandidate.IsWeakSupplemental)
                {
                    LogWeakSupplementalDefenderAccepted(observationTick, settlementName, commandPartyName, selectedCandidate.Strength);
                }

                if (baselineOrAdaptiveNeed
                    && estimatedCoverageAfter - estimatedCoverageBefore < DefenseControllerSettings.MinimumCandidateCoverageImprovement)
                {
                    CsmLogger.Info(
                        $"Observed defense reinforcement assignment: tick={context.Tick}, settlement='{context.SettlementName}', candidate='{commandPartyName}', reason='Adaptive active siege defense accepted candidate despite low individual coverage improvement'");
                }

                if (selectedCandidate.IsReassignmentAllowed)
                {
                    if (lowSiegeEarlyReinforcement)
                    {
                        lowSiegeReassignmentsCreated++;
                    }

                    LogDefenseCandidateReassignmentAllowed(observationTick, selectedCandidate, settlementName, context.CoverageStatus);
                }

                appliedReport = CreateReport(
                    observationTick,
                    settlementName,
                    snapshot.ThreatReport.OwnerKingdomName,
                    actionTier,
                    commandPartyName,
                    commandPartyCategory,
                    true,
                    true,
                    dailyCommandBudgetOverrideApplied
                        ? "Active siege defense bypassed daily direct command limit"
                        : assignmentsCreated > 1
                        ? "Coverage-based defense reinforcement command applied"
                        : "Direct urgent defense command applied");
            }

            if (!string.IsNullOrWhiteSpace(blockedReason))
            {
                context.ActiveDefenseAssignments += assignmentsCreated;
                context.EstimatedCoverageAfterAssignments = estimatedCoverage;
                RecordReinforcementBlocked(context, blockedReason);
                if (adaptiveActiveSiegeDefense)
                {
                    LogAdaptiveActiveSiegeDefenseBlocked(context, candidatePool.Count, lastScoringSummary.ValidCandidates, blockedReason);
                }
            }

            if (immediateCriticalSiegeDefense)
            {
                var reaction = new CriticalDefenseReactionEvaluation
                {
                    ImmediateReactionAllowed = true,
                    StableSignalRequired = false,
                    Reason = string.IsNullOrWhiteSpace(blockedReason)
                        ? "Immediate critical siege defense applied"
                        : blockedReason
                };
                LogCriticalDefenseReaction(reaction, snapshot, observationTick, assignmentsCreated);
            }

            if (lowSiegeEarlyReinforcement)
            {
                LogLowSiegeEarlyReinforcement(context, candidatePool.Count, lastScoringSummary.ValidCandidates, assignmentsCreated);
            }

            if (adaptiveActiveSiegeDefense)
            {
                LogAdaptiveActiveSiegeDefense(context, candidatePool.Count, lastScoringSummary.ValidCandidates, assignmentsCreated);
            }

            if (assignmentsCreated > 0)
            {
                if (adaptiveActiveSiegeDefense
                    && !ShouldContinueAdaptiveSiegeReinforcement(context, true, estimatedCoverage))
                {
                    LogAdaptiveActiveSiegeDefenseSatisfied(context);
                }

                return appliedReport;
            }

            LogDefenseCommandBudgetOverrideNotAppliedIfNeeded(snapshot, settlement, observationTick, dailyCommandBudgetOverrideApplied);

            if (string.IsNullOrWhiteSpace(blockedReason))
            {
                blockedReason = "Coverage target already met";
                context.EstimatedCoverageAfterAssignments = estimatedCoverage;
                RecordReinforcementBlocked(context, blockedReason);
            }

            return CreateReport(observationTick, settlementName, blockedCandidateName, false, blockedReason);
        }

        private List<DefenseCandidateReport> GetReinforcementCandidatePool(
            DefenseEvaluationSnapshot snapshot,
            Settlement settlement,
            bool immediateCriticalSiegeDefense,
            bool lowSiegeEarlyReinforcement,
            bool adaptiveActiveSiegeDefense,
            DefenseReinforcementContext context,
            float estimatedCoverage,
            float targetCoverageRatio)
        {
            if (adaptiveActiveSiegeDefense
                && settlement != null
                && (settlement.SiegeEvent != null || snapshot.ThreatReport.HasActiveSiege)
                && context != null
                && ShouldContinueAdaptiveSiegeReinforcement(context, true, estimatedCoverage))
            {
                var poolSize = context.ScarceLocalRelief
                    ? GetScarceLocalReliefCandidatePool()
                    : NamesEqual(context.CoverageStatus, "Critical")
                    ? GetMaxImmediateCriticalCandidatePool()
                    : GetMaxLowSiegeCandidatePool();
                var distanceAllowance = context.ScarceLocalRelief
                    ? GetScarceLocalReliefExtraDistanceAllowance()
                    : 1f;
                return _candidateSelector.FindDefenseCandidates(settlement, poolSize, distanceAllowance);
            }

            if (immediateCriticalSiegeDefense
                && settlement != null
                && (settlement.SiegeEvent != null || snapshot.ThreatReport.HasActiveSiege)
                && NamesEqual(GetCoverageStatus(snapshot.CoverageReport), "Critical")
                && estimatedCoverage < targetCoverageRatio)
            {
                return _candidateSelector.FindDefenseCandidates(settlement, GetMaxImmediateCriticalCandidatePool());
            }

            if (lowSiegeEarlyReinforcement
                && settlement != null
                && (settlement.SiegeEvent != null || snapshot.ThreatReport.HasActiveSiege)
                && NamesEqual(GetCoverageStatus(snapshot.CoverageReport), "Low")
                && estimatedCoverage < targetCoverageRatio)
            {
                return _candidateSelector.FindDefenseCandidates(settlement, GetMaxLowSiegeCandidatePool());
            }

            return snapshot.CandidateReports == null
                ? new List<DefenseCandidateReport>()
                : new List<DefenseCandidateReport>(snapshot.CandidateReports);
        }

        private static List<DefenseCandidateReport> FilterCandidatePool(
            List<DefenseCandidateReport> candidatePool,
            HashSet<string> rejectedCandidateNames)
        {
            var filtered = new List<DefenseCandidateReport>();
            if (candidatePool == null)
            {
                return filtered;
            }

            for (var index = 0; index < candidatePool.Count; index++)
            {
                var candidate = candidatePool[index];
                if (rejectedCandidateNames != null
                    && rejectedCandidateNames.Contains(Normalize(candidate.CandidatePartyName)))
                {
                    continue;
                }

                filtered.Add(candidate);
            }

            return filtered;
        }

        private void RemovePendingFailedReplacementCandidate(string settlementName, List<DefenseCandidateReport> candidatePool)
        {
            if (candidatePool == null)
            {
                return;
            }

            DefenseReplacementCandidate replacement;
            if (!_pendingReplacementBySettlement.TryGetValue(Normalize(settlementName), out replacement)
                || string.IsNullOrWhiteSpace(replacement.FailedPartyName))
            {
                return;
            }

            for (var index = candidatePool.Count - 1; index >= 0; index--)
            {
                if (NamesEqual(candidatePool[index].CandidatePartyName, replacement.FailedPartyName))
                {
                    candidatePool.RemoveAt(index);
                }
            }
        }

        private static bool RejectCandidateAndContinue(HashSet<string> rejectedCandidateNames, DefenseCandidateScore selectedCandidate)
        {
            if (rejectedCandidateNames == null || selectedCandidate == null || string.IsNullOrWhiteSpace(selectedCandidate.CandidateName))
            {
                return false;
            }

            rejectedCandidateNames.Add(Normalize(selectedCandidate.CandidateName));
            return true;
        }

        private static bool ShouldContinueAdaptiveSiegeReinforcement(
            DefenseReinforcementContext context,
            bool adaptiveActiveSiegeDefense,
            float estimatedCoverage)
        {
            if (context == null)
            {
                return false;
            }

            if (!adaptiveActiveSiegeDefense)
            {
                return estimatedCoverage < context.TargetCoverageRatio;
            }

            return context.EffectiveFieldDefenders < context.RequiredBaselineFieldDefenders
                || context.EffectiveDefenseStrength < context.DesiredEffectiveDefenseStrength
                || estimatedCoverage < context.TargetCoverageRatio;
        }

        private static bool IsReassignmentRejection(DefenseCandidateScore rejectedCandidate)
        {
            return rejectedCandidate != null
                && (NamesEqual(rejectedCandidate.RejectionCategory, "AlreadyDefendingDifferentSettlement")
                    || NamesEqual(rejectedCandidate.RejectionCategory, "DefenseAssignment"));
        }

        private void RecordClosedAssignment(string status, bool progressExpired)
        {
            if (NamesEqual(status, "Completed"))
            {
                _assignmentCompletedThisTick++;
                return;
            }

            if (NamesEqual(status, "Expired"))
            {
                _assignmentExpiredThisTick++;
                if (progressExpired)
                {
                    _assignmentProgressExpiredThisTick++;
                }

                return;
            }

            if (NamesEqual(status, "Deescalated"))
            {
                _assignmentDeescalatedThisTick++;
                return;
            }

            if (NamesEqual(status, "Invalid"))
            {
                _assignmentInvalidThisTick++;
            }
        }

        private static string GetActionTier(
            DefenseEvaluationSnapshot snapshot,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecisionStabilityReport stabilityReport)
        {
            if (!DefenseControllerSettings.EnableDefenseControllerV2)
            {
                return IsUrgentDefenseAction(actionPlan.RecommendedAction) ? "UrgentDefense" : "Monitor";
            }

            if (!actionPlan.NeedsDefenseAction || NamesEqual(actionPlan.RecommendedAction, "Monitor") || NamesEqual(actionPlan.RecommendedAction, "None"))
            {
                return "Monitor";
            }

            if (!IsUrgentDefenseAction(actionPlan.RecommendedAction))
            {
                return "ReinforcementNeeded";
            }

            if (DefenseControllerSettings.EnableCriticalDefenseTier
                && snapshot.ThreatReport.HasActiveSiege
                && IsCriticalCoverage(snapshot.CoverageReport)
                && IsThreatClearlyHigherThanDefense(snapshot.CoverageReport)
                && IsStableUrgentSignal(stabilityReport))
            {
                return "CriticalDefense";
            }

            return "UrgentDefense";
        }

        private static string GetCoverageExecutionBlockReason(
            DefenseEvaluationSnapshot snapshot,
            DefenseActionPlan actionPlan,
            DefenseCandidateScore selectedCandidate)
        {
            if (!DefenseControllerSettings.EnableCoverageAwareDefenseExecution)
            {
                return null;
            }

            if (snapshot.CoverageReport.IsDefenseEnough || NamesEqual(GetCoverageStatus(snapshot.CoverageReport), "Enough"))
            {
                return "Coverage already sufficient";
            }

            if (snapshot.CoverageReport.ExplicitDefenderCount > 0
                && snapshot.CoverageReport.DefenseCoverageRatio >= DefenseControllerSettings.LowDefenseCoverageRatio)
            {
                return "Existing defenders already cover threat";
            }

            if (selectedCandidate == null)
            {
                return null;
            }

            var estimatedCoverage = EstimateCoverageAfterCandidate(snapshot.CoverageReport, selectedCandidate);
            var improvesCoverage = estimatedCoverage - snapshot.CoverageReport.DefenseCoverageRatio >= DefenseControllerSettings.MinimumCandidateCoverageImprovement;
            if (NamesEqual(GetCoverageStatus(snapshot.CoverageReport), "Low") && !improvesCoverage)
            {
                return "Candidate does not improve coverage";
            }

            return null;
        }

        private bool ShouldUseCoverageBasedDefenseReinforcement(DefenseEvaluationSnapshot snapshot, Settlement settlement)
        {
            if (!DefenseControllerSettings.EnableCoverageBasedDefenseReinforcement || settlement == null)
            {
                return false;
            }

            if (settlement.SiegeEvent == null && !snapshot.ThreatReport.HasActiveSiege)
            {
                return false;
            }

            if (ShouldUseAdaptiveActiveSiegeDefense(snapshot, settlement))
            {
                return true;
            }

            var coverageStatus = GetCoverageStatus(snapshot.CoverageReport);
            return NamesEqual(coverageStatus, "Critical") || NamesEqual(coverageStatus, "Low");
        }

        private static bool ShouldUseAdaptiveActiveSiegeDefense(DefenseEvaluationSnapshot snapshot, Settlement settlement)
        {
            return DefenseControllerSettings.EnableAdaptiveActiveSiegeDefense
                && settlement != null
                && (settlement.SiegeEvent != null || snapshot.ThreatReport.HasActiveSiege);
        }

        private CriticalDefenseReactionEvaluation EvaluateImmediateCriticalSiegeDefense(
            DefenseEvaluationSnapshot snapshot,
            Settlement settlement,
            DefenseControllerSafetyReport safetyReport)
        {
            var result = new CriticalDefenseReactionEvaluation
            {
                ImmediateReactionAllowed = false,
                StableSignalRequired = true,
                Reason = "Immediate critical siege defense disabled"
            };

            if (!DefenseControllerSettings.EnableImmediateCriticalSiegeDefense)
            {
                return result;
            }

            if (!DefenseControllerSettings.EnableRealDefenseController || !DefenseActionThresholdSettings.EnableRealDefenseController)
            {
                result.Reason = "Real defense controller disabled";
                return result;
            }

            if (settlement == null)
            {
                result.Reason = "Settlement not found";
                return result;
            }

            if (settlement.SiegeEvent == null && !snapshot.ThreatReport.HasActiveSiege)
            {
                result.Reason = "Not active siege";
                return result;
            }

            if (!NamesEqual(GetCoverageStatus(snapshot.CoverageReport), "Critical"))
            {
                result.Reason = "Not critical coverage";
                return result;
            }

            if (snapshot.CoverageReport.DefenseCoverageRatio > DefenseControllerSettings.ImmediateCriticalSiegeCoverageRatio)
            {
                result.Reason = "Coverage above immediate threshold";
                return result;
            }

            if (snapshot.CoverageReport.PotentialCandidateCount <= 0)
            {
                result.Reason = "No valid immediate candidates";
                return result;
            }

            var capEvaluation = EvaluateDefenseAssignmentCaps(snapshot, settlement, _currentTick, 0);
            if (capEvaluation.Blocked)
            {
                result.Reason = capEvaluation.Reason;
                return result;
            }

            var activeSettlementAssignments = GetValidActiveAssignmentsForSettlement(snapshot, settlement, _currentTick).Count;
            if (activeSettlementAssignments >= GetMaxDefenseAssignmentsPerSettlementSafetyCap())
            {
                result.Reason = "Settlement safety cap reached";
                return result;
            }

            if (!safetyReport.Allowed
                && !NamesEqual(safetyReport.Reason, "No stable urgent defense dry-run signal"))
            {
                result.Reason = safetyReport.Reason;
                return result;
            }

            result.ImmediateReactionAllowed = true;
            result.StableSignalRequired = false;
            result.Reason = "Immediate critical siege defense allowed";
            return result;
        }

        private LowSiegeEarlyReinforcementEvaluation EvaluateLowSiegeEarlyReinforcement(
            DefenseEvaluationSnapshot snapshot,
            Settlement settlement,
            DefenseControllerSafetyReport safetyReport)
        {
            var result = new LowSiegeEarlyReinforcementEvaluation
            {
                Allowed = false,
                Reason = "Low siege early reinforcement disabled"
            };

            if (!DefenseControllerSettings.EnableLowSiegeEarlyReinforcement)
            {
                return result;
            }

            if (!DefenseControllerSettings.EnableRealDefenseController || !DefenseActionThresholdSettings.EnableRealDefenseController)
            {
                result.Reason = "Real defense controller disabled";
                return result;
            }

            if (settlement == null)
            {
                result.Reason = "Settlement not found";
                return result;
            }

            if (settlement.SiegeEvent == null && !snapshot.ThreatReport.HasActiveSiege)
            {
                result.Reason = "Not active siege";
                return result;
            }

            if (!NamesEqual(GetCoverageStatus(snapshot.CoverageReport), "Low"))
            {
                result.Reason = "Not low coverage";
                return result;
            }

            var targetCoverageRatio = DefenseControllerSettings.LowDefenseTargetCoverageRatio;
            if (snapshot.CoverageReport.DefenseCoverageRatio >= targetCoverageRatio
                || snapshot.CoverageReport.DefenseCoverageRatio >= DefenseControllerSettings.LowSiegeEarlyReinforcementCoverageRatio)
            {
                result.Reason = "Coverage above low siege early threshold";
                return result;
            }

            if (snapshot.CoverageReport.NearbyUncommittedCount <= 0)
            {
                result.Reason = "No nearby uncommitted candidates";
                return result;
            }

            var capEvaluation = EvaluateDefenseAssignmentCaps(snapshot, settlement, _currentTick, 0);
            if (capEvaluation.Blocked)
            {
                result.Reason = capEvaluation.Reason;
                return result;
            }

            if (!safetyReport.Allowed
                && !NamesEqual(safetyReport.Reason, "No stable urgent defense dry-run signal"))
            {
                result.Reason = safetyReport.Reason;
                return result;
            }

            result.Allowed = true;
            result.Reason = "Low active siege early reinforcement allowed";
            return result;
        }

        private static bool CanBypassStableSignalForImmediateCriticalSiege(
            CriticalDefenseReactionEvaluation reaction,
            DefenseControllerSafetyReport safetyReport)
        {
            return reaction != null
                && reaction.ImmediateReactionAllowed
                && NamesEqual(safetyReport.Reason, "No stable urgent defense dry-run signal");
        }

        private static bool CanBypassStableSignalForLowSiegeEarlyReinforcement(
            LowSiegeEarlyReinforcementEvaluation reaction,
            DefenseControllerSafetyReport safetyReport)
        {
            return reaction != null
                && reaction.Allowed
                && NamesEqual(safetyReport.Reason, "No stable urgent defense dry-run signal");
        }

        private static bool CanBypassDryRunSafetyForAdaptiveActiveSiege(
            bool adaptiveActiveSiegeDefense,
            DefenseControllerSafetyReport safetyReport)
        {
            if (!adaptiveActiveSiegeDefense)
            {
                return false;
            }

            return NamesEqual(safetyReport.Reason, "Dry-run does not request action")
                || NamesEqual(safetyReport.Reason, "No stable urgent defense dry-run signal")
                || NamesEqual(safetyReport.Reason, "Dry-run action is not urgent defense")
                || NamesEqual(safetyReport.Reason, "Controller action is not urgent defense")
                || NamesEqual(safetyReport.Reason, "Non-executable dry-run action")
                || NamesEqual(safetyReport.Reason, "No primary candidate");
        }

        private DefenseReinforcementContext BuildReinforcementContext(
            DefenseEvaluationSnapshot snapshot,
            Settlement settlement,
            int observationTick)
        {
            var context = new DefenseReinforcementContext
            {
                Tick = observationTick,
                SettlementName = snapshot.ThreatReport.SettlementName,
                CoverageStatus = GetCoverageStatus(snapshot.CoverageReport),
                CoverageRatio = snapshot.CoverageReport.DefenseCoverageRatio,
                RequiredThreatStrength = snapshot.CoverageReport.RequiredThreatStrength,
                EnemySiegeThreatStrength = GetEnemySiegeThreatStrength(snapshot),
                GarrisonDefenseStrength = snapshot.CoverageReport.GarrisonDefenseStrength,
                ExplicitDefenderCount = snapshot.CoverageReport.ExplicitDefenderCount,
                CurrentDefenseStrength = snapshot.CoverageReport.AvailableDefenseStrength,
                TargetCoverageRatio = GetTargetCoverageRatio(snapshot.CoverageReport),
                NearbyUncommittedCandidates = snapshot.CoverageReport.NearbyUncommittedCount
            };

            var assignments = GetValidActiveAssignmentsForSettlement(snapshot, settlement, observationTick);
            context.ActiveDefenseAssignments = assignments.Count;
            context.ActiveAssignedDefenderStrength = GetAssignedDefenderStrength(assignments, settlement);
            context.RequiredBaselineFieldDefenders = GetRequiredBaselineFieldDefenders(snapshot, settlement);
            context.DesiredEffectiveDefenseStrength = GetDesiredAdaptiveDefenseStrength(snapshot, settlement, context.EnemySiegeThreatStrength);
            context.EffectiveAssignedDefenderStrength = GetEffectiveAssignedAdaptiveDefenderStrength(assignments, settlement, true);
            context.EffectiveFieldDefenders = snapshot.CoverageReport.ExplicitDefenderCount + CountEffectiveFieldDefenders(assignments, settlement);
            context.EffectiveDefenseStrength = snapshot.CoverageReport.GarrisonDefenseStrength
                + snapshot.CoverageReport.ExplicitDefenderStrength
                + GetEffectiveAssignedAdaptiveDefenderStrength(assignments, settlement, false);
            context.DefenseStrengthDeficit = context.DesiredEffectiveDefenseStrength - context.EffectiveDefenseStrength;
            if (context.DefenseStrengthDeficit < 0f)
            {
                context.DefenseStrengthDeficit = 0f;
            }

            context.ScarceLocalRelief = ShouldUseScarceLocalRelief(snapshot, context);
            var uncountedAssignedStrength = GetUncountedAssignedDefenderStrength(assignments, settlement);
            context.EstimatedCoverageAfterAssignments = EstimateCoverage(
                snapshot.CoverageReport,
                snapshot.CoverageReport.AvailableDefenseStrength + uncountedAssignedStrength);
            if (context.EstimatedCoverageAfterAssignments < snapshot.CoverageReport.DefenseCoverageRatio)
            {
                context.EstimatedCoverageAfterAssignments = snapshot.CoverageReport.DefenseCoverageRatio;
            }

            context.AdditionalDefenseNeeded = context.EstimatedCoverageAfterAssignments < context.TargetCoverageRatio
                || context.EffectiveFieldDefenders < context.RequiredBaselineFieldDefenders
                || context.EffectiveDefenseStrength < context.DesiredEffectiveDefenseStrength;
            return context;
        }

        private List<CsmDefenseAssignment> GetValidActiveAssignmentsForSettlement(
            DefenseEvaluationSnapshot snapshot,
            Settlement settlement,
            int observationTick)
        {
            var validAssignments = new List<CsmDefenseAssignment>();
            if (!DefenseAssignmentSettings.EnableDefenseAssignments || settlement == null)
            {
                return validAssignments;
            }

            var settlementId = GetSettlementId(settlement);
            var settlementName = snapshot.ThreatReport.SettlementName;
            var assignments = _assignmentRegistry.GetActiveAssignmentsForSettlement(settlementId, settlementName);
            for (var index = 0; index < assignments.Count; index++)
            {
                var assignment = assignments[index];
                var party = FindPartyByIdOrName(assignment.PartyId, assignment.PartyName);
                var invalidReason = GetInvalidAssignmentReason(snapshot, assignment, party, settlement, observationTick);
                if (string.IsNullOrWhiteSpace(invalidReason))
                {
                    validAssignments.Add(assignment);
                }
            }

            return validAssignments;
        }

        private static float GetAssignedDefenderStrength(List<CsmDefenseAssignment> assignments, Settlement settlement)
        {
            var strength = 0f;
            if (assignments == null)
            {
                return strength;
            }

            for (var index = 0; index < assignments.Count; index++)
            {
                var party = FindPartyByIdOrName(assignments[index].PartyId, assignments[index].PartyName);
                strength += GetEffectiveAssignedDefenderStrength(assignments[index], party, settlement);
            }

            return strength;
        }

        private static float GetUncountedAssignedDefenderStrength(List<CsmDefenseAssignment> assignments, Settlement settlement)
        {
            var strength = 0f;
            if (assignments == null)
            {
                return strength;
            }

            for (var index = 0; index < assignments.Count; index++)
            {
                var party = FindPartyByIdOrName(assignments[index].PartyId, assignments[index].PartyName);
                if (!IsAlreadyCountedAsExplicitDefender(party, settlement))
                {
                    strength += GetEffectiveAssignedDefenderStrength(assignments[index], party, settlement);
                }
            }

            return strength;
        }

        private static bool IsAlreadyCountedAsExplicitDefender(MobileParty party, Settlement settlement)
        {
            if (party == null || settlement == null)
            {
                return false;
            }

            if (party.CurrentSettlement == settlement)
            {
                return true;
            }

            return party.DefaultBehavior == AiBehavior.DefendSettlement
                && party.TargetSettlement == settlement
                && party.Position.Distance(settlement.Position) <= 80f;
        }

        private static int CountEffectiveFieldDefenders(List<CsmDefenseAssignment> assignments, Settlement settlement)
        {
            var count = 0;
            if (assignments == null)
            {
                return count;
            }

            for (var index = 0; index < assignments.Count; index++)
            {
                var party = FindPartyByIdOrName(assignments[index].PartyId, assignments[index].PartyName);
                if (IsEffectiveFieldDefender(assignments[index], party, settlement))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsEffectiveFieldDefender(CsmDefenseAssignment assignment, MobileParty party, Settlement settlement)
        {
            if (assignment == null || party == null || settlement == null || party.MapEvent != null)
            {
                return false;
            }

            var distance = party.Position.Distance(settlement.Position);
            var targetExpected = party.TargetSettlement == settlement
                && party.DefaultBehavior == AiBehavior.DefendSettlement;
            if (!targetExpected && assignment.StalledTicks >= GetTargetOverwrittenTicksBeforeIneffective())
            {
                return false;
            }

            return party.CurrentSettlement == settlement
                || targetExpected
                || distance <= GetFarDefenderDistanceThreshold();
        }

        private static float GetEffectiveAssignedAdaptiveDefenderStrength(
            List<CsmDefenseAssignment> assignments,
            Settlement settlement,
            bool includeExplicitlyCounted)
        {
            var strength = 0f;
            if (assignments == null)
            {
                return strength;
            }

            for (var index = 0; index < assignments.Count; index++)
            {
                var party = FindPartyByIdOrName(assignments[index].PartyId, assignments[index].PartyName);
                if (!includeExplicitlyCounted && IsAlreadyCountedAsExplicitDefender(party, settlement))
                {
                    continue;
                }

                strength += GetAdaptiveDefenderContribution(assignments[index], party, settlement);
            }

            return strength;
        }

        private static float GetAdaptiveDefenderContribution(CsmDefenseAssignment assignment, MobileParty party, Settlement settlement)
        {
            var strength = GetPartyStrength(party);
            if (assignment == null || party == null || settlement == null || strength <= 0f)
            {
                return 0f;
            }

            if (party.MapEvent != null)
            {
                return strength * 0.25f;
            }

            var distance = party.Position.Distance(settlement.Position);
            var targetExpected = party.TargetSettlement == settlement
                && party.DefaultBehavior == AiBehavior.DefendSettlement;
            if (!targetExpected)
            {
                if (assignment.StalledTicks >= GetTargetOverwrittenTicksBeforeIneffective())
                {
                    return 0f;
                }

                return strength * GetTargetOverwrittenEffectiveContributionMultiplier();
            }

            if (party.CurrentSettlement == settlement || distance <= GetDefenseAssignmentArrivalDistance())
            {
                return strength;
            }

            return distance > GetFarDefenderDistanceThreshold()
                ? strength * GetFarDefenderContributionMultiplier()
                : strength;
        }

        private static float GetProjectedCandidateAdaptiveContribution(DefenseCandidateScore selectedCandidate)
        {
            if (selectedCandidate == null)
            {
                return 0f;
            }

            return selectedCandidate.Distance > GetFarDefenderDistanceThreshold()
                ? selectedCandidate.Strength * GetFarDefenderContributionMultiplier()
                : selectedCandidate.Strength;
        }

        private static int GetRequiredBaselineFieldDefenders(DefenseEvaluationSnapshot snapshot, Settlement settlement)
        {
            var baseline = DefenseControllerSettings.BaselineFieldDefendersForActiveSiege;
            if (settlement != null && settlement.IsTown)
            {
                baseline = Max(baseline, DefenseControllerSettings.BaselineFieldDefendersForTownSiege);
            }

            if (NamesEqual(GetCoverageStatus(snapshot.CoverageReport), "Critical"))
            {
                baseline = Max(baseline, DefenseControllerSettings.BaselineFieldDefendersForCriticalSiege);
            }

            if (snapshot.CoverageReport.ExplicitDefenderCount <= 0)
            {
                baseline = Max(baseline, DefenseControllerSettings.BaselineFieldDefendersWhenNoExplicitDefenders);
            }

            return baseline < 0 ? 0 : baseline;
        }

        private static float GetEnemySiegeThreatStrength(DefenseEvaluationSnapshot snapshot)
        {
            return snapshot.CoverageReport.EnemySiegeThreatStrength > 0f
                ? snapshot.CoverageReport.EnemySiegeThreatStrength
                : snapshot.CoverageReport.RequiredThreatStrength;
        }

        private static float GetDesiredAdaptiveDefenseStrength(DefenseEvaluationSnapshot snapshot, Settlement settlement, float enemySiegeThreatStrength)
        {
            var ratio = DefenseControllerSettings.AdaptiveSiegeDefenseTargetRatio;
            if (NamesEqual(GetCoverageStatus(snapshot.CoverageReport), "Critical"))
            {
                ratio = Max(ratio, DefenseControllerSettings.CriticalSiegeDefenseTargetRatio);
            }

            if (enemySiegeThreatStrength >= DefenseControllerSettings.HighThreatSiegeStrengthThreshold)
            {
                ratio = Max(ratio, DefenseControllerSettings.HighThreatSiegeDefenseTargetRatio);
            }

            if (settlement != null && settlement.IsTown)
            {
                ratio = Max(ratio, DefenseControllerSettings.CriticalSiegeDefenseTargetRatio);
            }

            return enemySiegeThreatStrength * ratio;
        }

        private static bool ShouldUseScarceLocalRelief(DefenseEvaluationSnapshot snapshot, DefenseReinforcementContext context)
        {
            return DefenseControllerSettings.EnableScarceLocalSiegeRelief
                && (snapshot.CoverageReport.ExplicitDefenderCount <= 0
                    || snapshot.CoverageReport.NearbyUncommittedCount <= context.RequiredBaselineFieldDefenders
                    || context.EffectiveFieldDefenders < context.RequiredBaselineFieldDefenders
                    || context.EnemySiegeThreatStrength >= 800f);
        }

        private static int CountActiveSieges()
        {
            var settlements = Settlement.All;
            if (settlements == null)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < settlements.Count; index++)
            {
                var settlement = settlements[index];
                if (settlement != null && settlement.SiegeEvent != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static float GetEffectiveAssignedDefenderStrength(CsmDefenseAssignment assignment, MobileParty party, Settlement settlement)
        {
            var strength = GetPartyStrength(party);
            if (strength <= 0f)
            {
                return 0f;
            }

            if (party == null || settlement == null || assignment == null)
            {
                return 0f;
            }

            var distance = party.Position.Distance(settlement.Position);
            var distanceDelta = assignment.LastDistanceToSettlement < 0f ? 0f : assignment.LastDistanceToSettlement - distance;
            var movingTowardTarget = distanceDelta >= DefenseAssignmentSettings.DefenseMovementProgressTolerance;
            var targetOverwritten = IsDefenseTargetOverwritten(party, settlement);
            var effective = !targetOverwritten
                || party.TargetSettlement == settlement
                || (party.DefaultBehavior == AiBehavior.DefendSettlement && party.TargetSettlement == settlement)
                || distance <= GetDefenseAssignmentArrivalDistance()
                || movingTowardTarget;
            if (targetOverwritten && assignment.StalledTicks >= DefenseAssignmentSettings.DefenseMovementStallTicks)
            {
                effective = false;
            }

            return effective ? strength : strength * 0.25f;
        }

        private static float GetPartyStrength(MobileParty party)
        {
            return party?.Party == null ? 0f : party.Party.EstimatedStrength;
        }

        private static float EstimateCoverage(DefenseCoverageReport coverageReport, float defenseStrength)
        {
            if (coverageReport.RequiredThreatStrength <= 0f)
            {
                return coverageReport.DefenseCoverageRatio;
            }

            return defenseStrength / coverageReport.RequiredThreatStrength;
        }

        private static float EstimateCoverageAfterCandidate(float requiredThreatStrength, float currentCoverage, DefenseCandidateScore selectedCandidate)
        {
            if (requiredThreatStrength <= 0f || selectedCandidate == null)
            {
                return currentCoverage;
            }

            var candidateStrength = selectedCandidate.HealthyStrength > 0f
                ? selectedCandidate.HealthyStrength
                : selectedCandidate.Strength;
            return currentCoverage + candidateStrength / requiredThreatStrength;
        }

        private static float GetTargetCoverageRatio(DefenseCoverageReport coverageReport)
        {
            return NamesEqual(GetCoverageStatus(coverageReport), "Critical")
                ? DefenseControllerSettings.CriticalDefenseTargetCoverageRatio
                : DefenseControllerSettings.LowDefenseTargetCoverageRatio;
        }

        private static int GetMaxDefenseReinforcementsPerTick()
        {
            return DefenseControllerSettings.MaxDefenseReinforcementsPerTick < 0
                ? 0
                : DefenseControllerSettings.MaxDefenseReinforcementsPerTick;
        }

        private static int GetMaxInitialCriticalDefenseReinforcements()
        {
            return DefenseControllerSettings.MaxInitialCriticalDefenseReinforcements < 0
                ? 0
                : DefenseControllerSettings.MaxInitialCriticalDefenseReinforcements;
        }

        private static int GetMaxAdaptiveSiegeDefenseAssignmentsPerTick()
        {
            return DefenseControllerSettings.MaxAdaptiveSiegeDefenseAssignmentsPerTick < 1
                ? 1
                : DefenseControllerSettings.MaxAdaptiveSiegeDefenseAssignmentsPerTick;
        }

        private static int GetMaxAdaptiveSiegeDefenseAssignmentsPerTickCritical()
        {
            return DefenseControllerSettings.MaxAdaptiveSiegeDefenseAssignmentsPerTickCritical < 1
                ? 1
                : DefenseControllerSettings.MaxAdaptiveSiegeDefenseAssignmentsPerTickCritical;
        }

        private static int GetMaxImmediateCriticalCandidatePool()
        {
            return DefenseControllerSettings.MaxImmediateCriticalCandidatePool < 1
                ? 1
                : DefenseControllerSettings.MaxImmediateCriticalCandidatePool;
        }

        private static int GetMaxInitialLowSiegeReinforcements()
        {
            return DefenseControllerSettings.MaxInitialLowSiegeReinforcements < 0
                ? 0
                : DefenseControllerSettings.MaxInitialLowSiegeReinforcements;
        }

        private static int GetMaxLowSiegeCandidatePool()
        {
            return DefenseControllerSettings.MaxLowSiegeCandidatePool < 1
                ? 1
                : DefenseControllerSettings.MaxLowSiegeCandidatePool;
        }

        private static int GetScarceLocalReliefCandidatePool()
        {
            return DefenseControllerSettings.ScarceLocalReliefCandidatePool < 1
                ? 1
                : DefenseControllerSettings.ScarceLocalReliefCandidatePool;
        }

        private static float GetScarceLocalReliefExtraDistanceAllowance()
        {
            return DefenseControllerSettings.ScarceLocalReliefExtraDistanceAllowance < 1f
                ? 1f
                : DefenseControllerSettings.ScarceLocalReliefExtraDistanceAllowance;
        }

        private static int GetMaxDefenseAssignmentsPerSettlementSafetyCap()
        {
            return DefenseControllerSettings.MaxDefenseAssignmentsPerSettlementSafetyCap < 1
                ? 1
                : DefenseControllerSettings.MaxDefenseAssignmentsPerSettlementSafetyCap;
        }

        private static int GetBaseMaxActiveDefenseAssignmentsGlobal()
        {
            return DefenseControllerSettings.BaseMaxActiveDefenseAssignmentsGlobal < 1
                ? 1
                : DefenseControllerSettings.BaseMaxActiveDefenseAssignmentsGlobal;
        }

        private static int GetExtraActiveDefenseAssignmentsPerActiveSiege()
        {
            return DefenseControllerSettings.ExtraActiveDefenseAssignmentsPerActiveSiege < 0
                ? 0
                : DefenseControllerSettings.ExtraActiveDefenseAssignmentsPerActiveSiege;
        }

        private static int GetMaxActiveDefenseAssignmentsPerKingdom()
        {
            return DefenseControllerSettings.MaxActiveDefenseAssignmentsPerKingdom < 1
                ? 1
                : DefenseControllerSettings.MaxActiveDefenseAssignmentsPerKingdom;
        }

        private void RecordReinforcementBlocked(DefenseReinforcementContext context, string reason)
        {
            _reinforcementBlockedThisTick++;
            LogReinforcementBlocked(context, reason);
        }

        private DefenseAssignmentCapEvaluation EvaluateDefenseAssignmentCaps(
            DefenseEvaluationSnapshot snapshot,
            Settlement settlement,
            int observationTick,
            int pendingAssignments)
        {
            var activeGlobalAssignments = _assignmentRegistry.CountActiveAssignments() + pendingAssignments;
            var activeKingdomAssignments = _assignmentRegistry.CountActiveAssignmentsForKingdom(snapshot.ThreatReport.OwnerKingdomName) + pendingAssignments;
            var activeSiegeCount = CountActiveSieges();
            var effectiveGlobalCap = GetBaseMaxActiveDefenseAssignmentsGlobal()
                + activeSiegeCount * GetExtraActiveDefenseAssignmentsPerActiveSiege();
            var kingdomCap = GetMaxActiveDefenseAssignmentsPerKingdom();
            var settlementCap = GetMaxDefenseAssignmentsPerSettlementSafetyCap();
            var activeSiegeWithDeficit = settlement != null
                && (settlement.SiegeEvent != null || snapshot.ThreatReport.HasActiveSiege)
                && BuildReinforcementContext(snapshot, settlement, observationTick).DefenseStrengthDeficit > 0f;
            var blocked = false;
            var reason = "Adaptive active siege defense cap evaluation";
            if (activeGlobalAssignments >= effectiveGlobalCap)
            {
                blocked = true;
                reason = "Adaptive global active defense assignment limit reached";
            }
            else if (activeKingdomAssignments >= kingdomCap)
            {
                blocked = true;
                reason = "Kingdom active defense assignment limit reached";
            }
            else if (!activeSiegeWithDeficit && activeGlobalAssignments >= DirectDefenseCommandSettings.MaxActiveDefenseAssignmentsGlobal)
            {
                blocked = true;
                reason = "Global active defense assignment limit reached";
            }

            return new DefenseAssignmentCapEvaluation
            {
                Tick = observationTick,
                SettlementName = snapshot.ThreatReport.SettlementName,
                KingdomName = snapshot.ThreatReport.OwnerKingdomName,
                ActiveGlobalAssignments = activeGlobalAssignments,
                ActiveKingdomAssignments = activeKingdomAssignments,
                ActiveSiegeCount = activeSiegeCount,
                EffectiveGlobalCap = effectiveGlobalCap,
                KingdomCap = kingdomCap,
                SettlementCap = settlementCap,
                Blocked = blocked,
                Reason = reason
            };
        }

        private void RecordCapBlocked(DefenseAssignmentCapEvaluation evaluation)
        {
            if (evaluation == null || !evaluation.Blocked)
            {
                return;
            }

            if (NamesEqual(evaluation.Reason, "Adaptive global active defense assignment limit reached")
                || NamesEqual(evaluation.Reason, "Global active defense assignment limit reached"))
            {
                _adaptiveGlobalCapBlockedThisTick++;
            }
            else if (NamesEqual(evaluation.Reason, "Kingdom active defense assignment limit reached"))
            {
                _kingdomCapBlockedThisTick++;
            }
        }

        private void RecordReplacementAssignmentIfNeeded(int observationTick, string settlementName, string newCandidate)
        {
            DefenseReplacementCandidate replacement;
            var key = Normalize(settlementName);
            if (!_pendingReplacementBySettlement.TryGetValue(key, out replacement))
            {
                return;
            }

            _pendingReplacementBySettlement.Remove(key);
            _replacementAssignmentsCreatedThisTick++;
            LogDefenseAssignmentReplacement(
                observationTick,
                settlementName,
                replacement.OldPartyName,
                newCandidate,
                replacement.IneffectiveTicks,
                string.IsNullOrWhiteSpace(replacement.Reason)
                    ? "Ineffective defense assignment replaced or supplemented"
                    : replacement.Reason == "Active siege defender failed movement"
                    ? "Active siege defender failed movement and was replaced"
                    : replacement.Reason);
        }

        private static bool IsDefenseAssignmentBlockReason(string reason)
        {
            return reason != null
                && reason.IndexOf("active CSM defense assignment", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsActiveSiegeDefenseActionTier(string actionTier)
        {
            return NamesEqual(actionTier, "ImmediateCriticalDefense")
                || NamesEqual(actionTier, "CriticalDefense")
                || NamesEqual(actionTier, "LowActiveSiegeDefense");
        }

        private static bool IsActiveSiegeCandidateDiagnosticEnabled(
            DefenseEvaluationSnapshot snapshot,
            Settlement settlement,
            bool immediateCriticalSiegeDefense,
            bool lowSiegeEarlyReinforcement)
        {
            return (immediateCriticalSiegeDefense || lowSiegeEarlyReinforcement)
                && settlement != null
                && (settlement.SiegeEvent != null || snapshot.ThreatReport.HasActiveSiege);
        }

        private static bool CanAllowCommandReassignment(
            MobileParty party,
            Settlement newTarget,
            CsmDefenseAssignmentRegistry defenseAssignments,
            string actionTier)
        {
            if (!IsActiveSiegeDefenseActionTier(actionTier) || party == null || newTarget == null)
            {
                return false;
            }

            var assignment = defenseAssignments == null ? null : defenseAssignments.GetActiveAssignmentForParty(GetPartyId(party), GetPartyName(party));
            if (assignment != null && !NamesEqual(assignment.SettlementName, GetSettlementName(newTarget)))
            {
                return IsPreviousDefenseTargetCoveredOrStale(FindSettlementByIdOrName(assignment.SettlementId, assignment.SettlementName), assignment);
            }

            if (party.TargetSettlement != null && party.TargetSettlement != newTarget)
            {
                return IsPreviousDefenseTargetCoveredOrStale(party.TargetSettlement, null);
            }

            return false;
        }

        private static bool IsPreviousDefenseTargetCoveredOrStale(Settlement oldTarget, CsmDefenseAssignment assignment)
        {
            if (oldTarget == null)
            {
                return true;
            }

            if (assignment != null && assignment.IneffectiveTicks >= GetDefenseAssignmentIneffectiveTicksBeforeReplacement())
            {
                return true;
            }

            return false;
        }

        private void CloseReassignedDefenseAssignmentIfNeeded(
            DefenseCandidateScore selectedCandidate,
            MobileParty commandParty,
            string settlementId,
            string settlementName,
            int observationTick)
        {
            if (selectedCandidate == null || !selectedCandidate.IsReassignmentAllowed || commandParty == null)
            {
                return;
            }

            var assignment = _assignmentRegistry.GetActiveAssignmentForParty(GetPartyId(commandParty), GetPartyName(commandParty));
            if (assignment == null || NamesEqual(assignment.SettlementId, settlementId) || NamesEqual(assignment.SettlementName, settlementName))
            {
                return;
            }

            var oldSettlement = FindSettlementByIdOrName(assignment.SettlementId, assignment.SettlementName);
            var reason = "Candidate reassignment allowed because previous defense target is covered";
            UpdateDefenseAuthorityCounters(assignment, commandParty, null, observationTick);
            if (!TryCloseDefenseAssignment(null, assignment, commandParty, oldSettlement, null, observationTick, "Deescalated", reason, reason, false))
            {
                return;
            }

            _pendingAssignmentReports.Add(CreateAssignmentReport(observationTick, assignment, "Deescalated", false, reason));
        }

        private static void LogReinforcementNeed(DefenseReinforcementContext context)
        {
            CsmLogger.Info(
                $"Observed defense reinforcement need: tick={context.Tick}, settlement='{context.SettlementName}', coverageStatus='{context.CoverageStatus}', requiredThreatStrength={context.RequiredThreatStrength:0.00}, currentDefenseStrength={context.CurrentDefenseStrength:0.00}, activeAssignedDefenderStrength={context.ActiveAssignedDefenderStrength:0.00}, estimatedCoverageAfterAssignments={context.EstimatedCoverageAfterAssignments:0.00}, targetCoverageRatio={context.TargetCoverageRatio:0.00}, additionalDefenseNeeded={context.AdditionalDefenseNeeded}, activeDefenseAssignments={context.ActiveDefenseAssignments}, nearbyUncommittedCandidates={context.NearbyUncommittedCandidates}, reason='Coverage-based defense reinforcement evaluation'");
        }

        private static void LogReinforcementAssignment(
            DefenseReinforcementContext context,
            string candidateName,
            float candidateStrength,
            float candidateDistance,
            int activeAssignmentsBefore,
            float estimatedCoverageBefore,
            float estimatedCoverageAfter)
        {
            CsmLogger.Info(
                $"Observed defense reinforcement assignment: tick={context.Tick}, settlement='{context.SettlementName}', candidate='{candidateName}', candidateStrength={candidateStrength:0.00}, candidateDistance={candidateDistance:0.00}, activeAssignmentsBefore={activeAssignmentsBefore}, estimatedCoverageBefore={estimatedCoverageBefore:0.00}, estimatedCoverageAfter={estimatedCoverageAfter:0.00}, reason='Additional defender assigned because coverage remains below target'");
        }

        private static void LogReinforcementBlocked(DefenseReinforcementContext context, string reason)
        {
            CsmLogger.Info(
                $"Observed defense reinforcement blocked: tick={context.Tick}, settlement='{context.SettlementName}', activeAssignments={context.ActiveDefenseAssignments}, estimatedCoverageAfterAssignments={context.EstimatedCoverageAfterAssignments:0.00}, targetCoverageRatio={context.TargetCoverageRatio:0.00}, reason='{reason}'");
        }

        private static void LogAdaptiveActiveSiegeDefense(
            DefenseReinforcementContext context,
            int candidatePoolSize,
            int validCandidates,
            int assignmentsCreated)
        {
            CsmLogger.Info(
                $"Observed adaptive active siege defense: tick={context.Tick}, settlement='{context.SettlementName}', coverageStatus='{context.CoverageStatus}', coverageRatio={context.CoverageRatio:0.00}, enemySiegeThreatStrength={context.EnemySiegeThreatStrength:0.00}, garrisonDefenseStrength={context.GarrisonDefenseStrength:0.00}, explicitDefenderCount={context.ExplicitDefenderCount}, effectiveFieldDefenders={context.EffectiveFieldDefenders}, requiredBaselineFieldDefenders={context.RequiredBaselineFieldDefenders}, effectiveDefenseStrength={context.EffectiveDefenseStrength:0.00}, desiredEffectiveDefenseStrength={context.DesiredEffectiveDefenseStrength:0.00}, defenseStrengthDeficit={context.DefenseStrengthDeficit:0.00}, scarceLocalRelief={context.ScarceLocalRelief}, candidatePoolSize={candidatePoolSize}, validCandidates={validCandidates}, assignmentsCreated={assignmentsCreated}, reason='Adaptive active siege defense evaluation'");
        }

        private static void LogAdaptiveActiveSiegeDefenseSatisfied(DefenseReinforcementContext context)
        {
            CsmLogger.Info(
                $"Observed adaptive active siege defense satisfied: tick={context.Tick}, settlement='{context.SettlementName}', effectiveFieldDefenders={context.EffectiveFieldDefenders}, requiredBaselineFieldDefenders={context.RequiredBaselineFieldDefenders}, effectiveDefenseStrength={context.EffectiveDefenseStrength:0.00}, desiredEffectiveDefenseStrength={context.DesiredEffectiveDefenseStrength:0.00}, reason='Baseline and adaptive defense target satisfied'");
        }

        private static void LogAdaptiveActiveSiegeDefenseBlocked(
            DefenseReinforcementContext context,
            int candidatePoolSize,
            int validCandidates,
            string reason)
        {
            CsmLogger.Info(
                $"Observed adaptive active siege defense blocked: tick={context.Tick}, settlement='{context.SettlementName}', candidatePoolSize={candidatePoolSize}, validCandidates={validCandidates}, reason='{reason}'");
        }

        private static void LogDefenseAssignmentCapEvaluation(DefenseAssignmentCapEvaluation evaluation)
        {
            if (evaluation == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed defense assignment cap evaluation: tick={evaluation.Tick}, settlement='{evaluation.SettlementName}', kingdom='{evaluation.KingdomName}', activeGlobalAssignments={evaluation.ActiveGlobalAssignments}, activeKingdomAssignments={evaluation.ActiveKingdomAssignments}, activeSiegeCount={evaluation.ActiveSiegeCount}, effectiveGlobalCap={evaluation.EffectiveGlobalCap}, kingdomCap={evaluation.KingdomCap}, settlementCap={evaluation.SettlementCap}, blocked={evaluation.Blocked}, reason='Adaptive active siege defense cap evaluation'");
        }

        private bool IsActiveSiegeDefenseBudgetOverrideEligible(
            DefenseEvaluationSnapshot snapshot,
            Settlement settlement,
            int observationTick)
        {
            if (settlement == null || (settlement.SiegeEvent == null && !snapshot.ThreatReport.HasActiveSiege))
            {
                return false;
            }

            var context = BuildReinforcementContext(snapshot, settlement, observationTick);
            return context.AdditionalDefenseNeeded;
        }

        private static void LogDefenseCommandBudgetOverride(
            DefenseEvaluationSnapshot snapshot,
            Settlement settlement,
            int observationTick,
            bool overrideApplied)
        {
            var coverageStatus = GetCoverageStatus(snapshot.CoverageReport);
            var hasActiveSiege = settlement != null && settlement.SiegeEvent != null
                || snapshot.ThreatReport.HasActiveSiege;
            CsmLogger.Info(
                $"Observed defense command budget override: tick={observationTick}, settlement='{snapshot.ThreatReport.SettlementName}', coverageStatus='{coverageStatus}', hasActiveSiege={hasActiveSiege}, dailyLimitReached=True, overrideApplied={overrideApplied}, reason='{(overrideApplied ? "Active siege defense bypassed daily direct command limit" : "Daily command limit kept for non-siege / non-critical / invalid defense command")}'");
        }

        private static void LogDefenseCommandBudgetOverrideNotAppliedIfNeeded(
            DefenseEvaluationSnapshot snapshot,
            Settlement settlement,
            int observationTick,
            bool dailyCommandBudgetOverrideApplied)
        {
            if (dailyCommandBudgetOverrideApplied)
            {
                LogDefenseCommandBudgetOverride(snapshot, settlement, observationTick, false);
            }
        }

        private static void LogImmediateCriticalCandidatePool(
            DefenseReinforcementContext context,
            int candidatePoolSize,
            DefenseCandidateScoringSummary summary)
        {
            CsmLogger.Info(
                $"Observed immediate critical defense candidate pool: tick={context.Tick}, settlement='{context.SettlementName}', candidatePoolSize={candidatePoolSize}, evaluatedCandidates={summary.EvaluatedCandidates}, validCandidates={summary.ValidCandidates}, rejectedInvalid={summary.RejectedInvalid}, rejectedTooFar={summary.RejectedTooFar}, rejectedDefenseAssignment={summary.RejectedByDefenseAssignment}, rejectedArmyAssignment={summary.RejectedByArmyAssignment}, estimatedCoverageAfterAssignments={context.EstimatedCoverageAfterAssignments:0.00}, targetCoverageRatio={context.TargetCoverageRatio:0.00}, reason='Expanded immediate critical defense candidate pool evaluation'");
        }

        private static void LogLowSiegeEarlyReinforcement(
            DefenseReinforcementContext context,
            int candidatePoolSize,
            int validCandidates,
            int assignmentsCreated)
        {
            CsmLogger.Info(
                $"Observed low siege early reinforcement: tick={context.Tick}, settlement='{context.SettlementName}', coverageRatio={context.EstimatedCoverageAfterAssignments:0.00}, targetCoverageRatio={context.TargetCoverageRatio:0.00}, candidatePoolSize={candidatePoolSize}, validCandidates={validCandidates}, assignmentsCreated={assignmentsCreated}, reason='Low active siege early reinforcement evaluation'");
        }

        private static void LogActiveSiegeCandidatePoolDiagnostic(
            DefenseReinforcementContext context,
            int candidatePoolSize,
            DefenseCandidateScoringSummary summary)
        {
            CsmLogger.Info(
                $"Observed defense candidate pool diagnostic: tick={context.Tick}, settlement='{context.SettlementName}', coverageStatus='{context.CoverageStatus}', candidatePoolSize={candidatePoolSize}, evaluatedCandidates={summary.EvaluatedCandidates}, validCandidates={summary.ValidCandidates}, rejectedInvalid={summary.RejectedInvalid}, rejectedTooWeakHard={summary.RejectedTooWeakHard}, acceptedWeakSupplemental={summary.AcceptedWeakSupplemental}, rejectedDefenseAssignment={summary.RejectedByDefenseAssignment}, rejectedArmyAssignment={summary.RejectedByArmyAssignment}, rejectedAlreadyDefendingDifferentSettlement={summary.RejectedAlreadyDefendingDifferentSettlement}, acceptedReassignment={summary.AcceptedReassignment}, reason='Active siege defense candidate pool diagnostic'");
        }

        private static void LogWeakSupplementalDefenderAccepted(
            int observationTick,
            string settlementName,
            string candidateName,
            float candidateStrength)
        {
            CsmLogger.Info(
                $"Observed defense weak candidate accepted: tick={observationTick}, settlement='{settlementName}', candidate='{candidateName}', candidateStrength={candidateStrength:0.00}, reason='Weak but useful supplemental defender accepted for active siege'");
        }

        private static void LogDefenseCandidateReassignmentAllowed(
            int observationTick,
            DefenseCandidateScore selectedCandidate,
            string toSettlement,
            string toCoverageStatus)
        {
            if (selectedCandidate == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed defense candidate reassignment allowed: tick={observationTick}, candidate='{selectedCandidate.CandidateName}', fromSettlement='{selectedCandidate.ReassignmentFromSettlement}', toSettlement='{toSettlement}', fromCoverageStatus='{selectedCandidate.ReassignmentFromCoverageStatus}', toCoverageStatus='{toCoverageStatus}', reason='Candidate reassignment allowed because previous defense target is covered'");
        }

        private static void LogDefenseCandidateReassignmentBlocked(
            int observationTick,
            string candidateName,
            string fromSettlement,
            string toSettlement)
        {
            CsmLogger.Info(
                $"Observed defense candidate reassignment blocked: tick={observationTick}, candidate='{candidateName}', fromSettlement='{fromSettlement}', toSettlement='{toSettlement}', reason='Previous defense target still needs defender'");
        }

        private static void LogBlockedReassignmentIfTopRejected(
            int observationTick,
            DefenseCandidateScore topRejected,
            string toSettlement)
        {
            if (topRejected == null
                || (!NamesEqual(topRejected.RejectionCategory, "AlreadyDefendingDifferentSettlement")
                    && !NamesEqual(topRejected.RejectionCategory, "DefenseAssignment")))
            {
                return;
            }

            LogDefenseCandidateReassignmentBlocked(
                observationTick,
                topRejected.CandidateName,
                string.IsNullOrWhiteSpace(topRejected.ReassignmentFromSettlement) ? "unknown" : topRejected.ReassignmentFromSettlement,
                toSettlement);
        }

        private static void LogDefenseAssignmentCleanup(
            int observationTick,
            CsmDefenseAssignment assignment,
            string status)
        {
            CsmLogger.Info(
                $"Observed defense assignment cleanup: tick={observationTick}, settlement='{(assignment == null ? "unknown" : assignment.SettlementName)}', party='{(assignment == null ? "unknown" : assignment.PartyName)}', status='{status}', reason='Stale/orphaned defense assignment cleanup'");
        }

        private static void LogDefenseAssignmentCleanupBlocked(
            int observationTick,
            CsmDefenseAssignment assignment,
            string staleReason)
        {
            CsmLogger.Info(
                $"Observed defense assignment cleanup: tick={observationTick}, settlement='{(assignment == null ? "unknown" : assignment.SettlementName)}', party='{(assignment == null ? "unknown" : assignment.PartyName)}', status='ActiveCommitted', requestedCloseReason='{staleReason}', reason='Stale/orphaned defense assignment cleanup blocked by authority hard lock'");
        }

        private void LogLifecycleWarningIfNeeded(int observationTick, int activeAssignments)
        {
            var noLifecycleProcessing = activeAssignments > 0
                && _assignmentCreatedThisTick == 0
                && _effectiveAssignmentsThisTick == 0
                && _ineffectiveAssignmentsThisTick == 0
                && _assignmentCompletedThisTick == 0
                && _assignmentDeescalatedThisTick == 0
                && _assignmentExpiredThisTick == 0
                && _assignmentInvalidThisTick == 0
                && _movementReassertedThisTick == 0
                && _movementStalledThisTick == 0
                && _movementInvalidThisTick == 0
                && _postEnforcementReassertedThisTick == 0;

            if (_lastAssignmentCreatedTick >= 0 && observationTick - _lastAssignmentCreatedTick <= 1)
            {
                _assignmentLifecycleUnprocessedTicks = 0;
                return;
            }

            _assignmentLifecycleUnprocessedTicks = noLifecycleProcessing
                ? _assignmentLifecycleUnprocessedTicks + 1
                : 0;
            if (!noLifecycleProcessing || _assignmentLifecycleUnprocessedTicks < 3)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed defense assignment lifecycle warning: tick={observationTick}, activeAssignments={activeAssignments}, effectiveAssignments=0, ineffectiveAssignments=0, completed=0, deescalated=0, expired=0, invalid=0, consecutiveTicks={_assignmentLifecycleUnprocessedTicks}, reason='Active assignments were not processed this tick'");
        }

        private static void LogCriticalDefenseReaction(
            CriticalDefenseReactionEvaluation reaction,
            DefenseEvaluationSnapshot snapshot,
            int observationTick,
            int firstWaveAssignments)
        {
            if (reaction == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed critical defense reaction: tick={observationTick}, settlement='{snapshot.ThreatReport.SettlementName}', coverageStatus='{GetCoverageStatus(snapshot.CoverageReport)}', coverageRatio={snapshot.CoverageReport.DefenseCoverageRatio:0.00}, hasActiveSiege={snapshot.ThreatReport.HasActiveSiege}, stableSignalRequired={reaction.StableSignalRequired}, immediateReactionAllowed={reaction.ImmediateReactionAllowed}, firstWaveAssignments={firstWaveAssignments}, targetCoverageRatio={GetTargetCoverageRatio(snapshot.CoverageReport):0.00}, reason='{reaction.Reason}'");
        }

        private static void LogDefenseAssignmentMovement(DefenseAssignmentMovementSnapshot movement)
        {
            if (movement == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed defense assignment movement: tick={movement.Tick}, settlement='{movement.SettlementName}', party='{movement.PartyName}', currentTarget='{movement.CurrentTarget}', expectedTarget='{movement.ExpectedTarget}', currentBehavior='{movement.CurrentBehavior}', distanceToTarget={movement.DistanceToTarget:0.00}, previousDistanceToTarget={movement.PreviousDistanceToTarget:0.00}, distanceDelta={movement.DistanceDelta:0.00}, targetOverwritten={movement.TargetOverwritten}, movingTowardTarget={movement.MovingTowardTarget}, reasserted={movement.Reasserted}, reason='Defense assignment movement tracking'");
        }

        private static void LogDefenseAssignmentMovementIssue(
            int observationTick,
            CsmDefenseAssignment assignment,
            Settlement settlement,
            MobileParty party,
            string issue,
            float distanceToTarget,
            int stalledTicks,
            bool reassertAttempted,
            string reason)
        {
            CsmLogger.Info(
                $"Observed defense assignment movement issue: tick={observationTick}, settlement='{(assignment == null ? GetSettlementName(settlement) : assignment.SettlementName)}', party='{(assignment == null ? GetPartyName(party) : assignment.PartyName)}', issue='{issue}', distanceToTarget={distanceToTarget:0.00}, stalledTicks={stalledTicks}, reassertAttempted={reassertAttempted}, reason='{reason}'");
        }

        private static void LogDefenseAssignmentEffectiveness(DefenseAssignmentEffectivenessSnapshot effectiveness)
        {
            if (effectiveness == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed defense assignment effectiveness: tick={effectiveness.Tick}, settlement='{effectiveness.SettlementName}', party='{effectiveness.PartyName}', isEffective={effectiveness.IsEffective}, targetOverwritten={effectiveness.TargetOverwritten}, movingTowardTarget={effectiveness.MovingTowardTarget}, stalledTicks={effectiveness.StalledTicks}, distanceToTarget={effectiveness.DistanceToTarget:0.00}, effectiveStrengthContribution={effectiveness.EffectiveStrengthContribution:0.00}, reason='Defense assignment effectiveness evaluation'");
        }

        private static void LogDefenseAssignmentReplacement(
            int observationTick,
            string settlementName,
            string oldParty,
            string newCandidate,
            int oldPartyIneffectiveTicks,
            string reason)
        {
            CsmLogger.Info(
                $"Observed defense assignment replacement: tick={observationTick}, settlement='{settlementName}', failedParty='{oldParty}', newCandidate='{newCandidate}', oldPartyIneffectiveTicks={oldPartyIneffectiveTicks}, reason='{reason}'");
        }

        private static void LogActiveSiegeDefensePersistence(
            int observationTick,
            CsmDefenseAssignment assignment,
            DefenseAssignmentMovementSnapshot movement,
            bool reassertAttempted,
            bool reassertForced,
            string status,
            string reason)
        {
            if (assignment == null || movement == null)
            {
                return;
            }

            var settlement = FindSettlementByIdOrName(assignment.SettlementId, assignment.SettlementName);
            if (settlement == null || settlement.SiegeEvent == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed active siege defense persistence: tick={observationTick}, settlement='{assignment.SettlementName}', party='{assignment.PartyName}', currentTarget='{movement.CurrentTarget}', expectedTarget='{movement.ExpectedTarget}', targetOverwritten={movement.TargetOverwritten}, movingTowardTarget={movement.MovingTowardTarget}, distanceToTarget={movement.DistanceToTarget:0.00}, previousDistanceToTarget={movement.PreviousDistanceToTarget:0.00}, stalledTicks={movement.StalledTicks}, reassertAttempted={reassertAttempted}, reassertForced={reassertForced}, status='{status}', reason='{reason}'");
        }

        private static void LogDefenseDeescalationDelayed(
            int observationTick,
            CsmDefenseAssignment assignment,
            DefenseAssignmentEffectivenessSnapshot effectiveness,
            string reason)
        {
            CsmLogger.Info(
                $"Observed defense assignment deescalation delayed: tick={observationTick}, settlement='{assignment.SettlementName}', party='{assignment.PartyName}', isEffective={(effectiveness != null && effectiveness.IsEffective)}, stalledTicks={(effectiveness == null ? 0 : effectiveness.StalledTicks)}, distanceToTarget={(effectiveness == null ? 0f : effectiveness.DistanceToTarget):0.00}, reason='{reason}'");
        }

        private static float EstimateCoverageAfterCandidate(DefenseCoverageReport coverageReport, DefenseCandidateScore selectedCandidate)
        {
            if (coverageReport.RequiredThreatStrength <= 0f || selectedCandidate == null)
            {
                return coverageReport.DefenseCoverageRatio;
            }

            return (coverageReport.AvailableDefenseStrength + selectedCandidate.HealthyStrength) / coverageReport.RequiredThreatStrength;
        }

        private static bool IsThreatClearlyHigherThanDefense(DefenseCoverageReport coverageReport)
        {
            if (coverageReport.RequiredThreatStrength <= 0f)
            {
                return coverageReport.DefenseCoverageRatio <= DefenseControllerSettings.CriticalDefenseCoverageRatio;
            }

            return coverageReport.RequiredThreatStrength > coverageReport.AvailableDefenseStrength * 1.25f;
        }

        private static bool IsStableUrgentSignal(DryRunDefenseDecisionStabilityReport stabilityReport)
        {
            return IsUrgentDefenseAction(stabilityReport.StableAction)
                && stabilityReport.ConsecutiveSameActionCount >= GetRequiredStableUrgentTicks();
        }

        private static string GetCoverageStatus(DefenseCoverageReport coverageReport)
        {
            if (coverageReport.DefenseCoverageRatio <= DefenseControllerSettings.CriticalDefenseCoverageRatio)
            {
                return "Critical";
            }

            if (coverageReport.DefenseCoverageRatio < DefenseControllerSettings.LowDefenseCoverageRatio)
            {
                return "Low";
            }

            return coverageReport.IsDefenseEnough ? "Enough" : "Unknown";
        }

        private static string GetCandidateRejectedReason(DefenseCandidateScore topRejected)
        {
            if (topRejected == null)
            {
                return "Primary candidate not found";
            }

            if (topRejected.RejectionCategory == "ArmyAssignment")
            {
                return "Candidate rejected by active CSM army assignment";
            }

            if (topRejected.RejectionCategory == "DefenseAssignment")
            {
                return "Candidate rejected by active CSM defense assignment";
            }

            return topRejected.Reason;
        }

        private void ResetDailyStateIfNeeded(int observationTick)
        {
            if (_currentTick == observationTick)
            {
                return;
            }

            _currentTick = observationTick;
            _commandsToday = 0;
            _settlementCommandCounts.Clear();
            _commandedParties.Clear();
            _assignmentCreatedThisTick = 0;
            _assignmentCompletedThisTick = 0;
            _assignmentDeescalatedThisTick = 0;
            _assignmentExpiredThisTick = 0;
            _assignmentInvalidThisTick = 0;
            _assignmentProgressExpiredThisTick = 0;
            _duplicateAssignmentBlockedThisTick = 0;
            _reinforcementAssignmentsCreatedThisTick = 0;
            _reinforcementBlockedThisTick = 0;
            _movementReassertedThisTick = 0;
            _movementStalledThisTick = 0;
            _movementInvalidThisTick = 0;
            _effectiveAssignmentsThisTick = 0;
            _ineffectiveAssignmentsThisTick = 0;
            _replacementAssignmentsCreatedThisTick = 0;
            _deescalationDelayedThisTick = 0;
            _postEnforcementReassertedThisTick = 0;
            _reassertedAssignmentsThisTick = 0;
            _activeSiegeReassertedThisTick = 0;
            _movementFailedThisTick = 0;
            _adaptiveGlobalCapBlockedThisTick = 0;
            _kingdomCapBlockedThisTick = 0;
        }

        private static MobileParty GetCommandParty(MobileParty candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            if (candidate.Army != null && candidate.Army.LeaderParty != null)
            {
                return candidate.Army.LeaderParty;
            }

            return candidate;
        }

        private static bool CanCommandParty(MobileParty party)
        {
            if (party == null)
            {
                return false;
            }

            if (!party.IsActive || party.IsDisbanding || party.IsMainParty)
            {
                return false;
            }

            if (party.LeaderHero == null || party.MapEvent != null || party.BesiegedSettlement != null)
            {
                return false;
            }

            return party.MemberRoster != null && party.MemberRoster.TotalManCount > 0;
        }

        private static string GetCommandPartyBlockReason(
            MobileParty party,
            Settlement settlement,
            float distanceToSettlement,
            CsmDefenseAssignmentRegistry defenseAssignments,
            CsmArmyAssignmentRegistry armyAssignments,
            string actionTier)
        {
            if (party == null)
            {
                return "Command party not found";
            }

            var conflict = new CsmAssignmentConflictChecker(armyAssignments, defenseAssignments).CheckPartyForNewDefenseCommand(party, settlement);
            if (conflict.IsBlocked)
            {
                if ((conflict.HasActiveDefenseAssignment || conflict.IsAlreadyDefendingDifferentSettlement)
                    && CanAllowCommandReassignment(party, settlement, defenseAssignments, actionTier))
                {
                    return null;
                }

                return conflict.Reason;
            }

            if (settlement == null || settlement.MapFaction == null || party.MapFaction != settlement.MapFaction)
            {
                return "Candidate faction does not match settlement owner faction";
            }

            if (party.Army != null && party.Army.LeaderParty == party)
            {
                if (!DirectDefenseCommandSettings.AllowArmyPartyDefenseCommands)
                {
                    return "Army party defense commands disabled";
                }
            }
            else if (party.Army == null)
            {
                if (!DirectDefenseCommandSettings.AllowLordPartyDefenseCommands)
                {
                    return "Lord party defense commands disabled";
                }
            }
            else
            {
                return "Candidate is army member but not army leader";
            }

            if (distanceToSettlement > DirectDefenseCommandSettings.MaxUrgentDefenseCommandDistance)
            {
                return "Candidate is too far for urgent defense command";
            }

            if (!CanCommandParty(party))
            {
                return "Party cannot receive direct defense command";
            }

            return null;
        }

        private static void LogCandidateScore(int tick, string actionTier, DefenseCandidateScore selected, DefenseCandidateScore rejected, DefenseCandidateScoringSummary summary)
        {
            if (selected != null)
            {
                CsmLogger.Info(
                    $"Observed defense candidate score: tick={tick}, settlement='{summary.SettlementName}', actionTier='{actionTier}', selectedCandidate='{selected.CandidateName}', score={selected.Score:0.00}, distance={selected.Distance:0.00}, strength={selected.Strength:0.00}, distanceScore={selected.DistanceScore:0.00}, strengthScore={selected.StrengthScore:0.00}, availabilityScore={selected.AvailabilityScore:0.00}, intentScore={selected.IntentScore:0.00}, criticalDefenseSpeedScore={selected.CriticalDefenseSpeedScore:0.00}, category={selected.CandidateCategory}, reason='{selected.Reason}'");
            }

            if (rejected != null)
            {
                CsmLogger.Info(
                    $"Observed defense candidate rejection: tick={tick}, settlement='{summary.SettlementName}', actionTier='{actionTier}', rejectedCandidate='{rejected.CandidateName}', score={rejected.Score:0.00}, distance={rejected.Distance:0.00}, strength={rejected.Strength:0.00}, rejectionCategory='{rejected.RejectionCategory}', reason='{rejected.Reason}'");
            }

            CsmLogger.Info(
                $"Observed defense candidate scoring summary: tick={tick}, settlement='{summary.SettlementName}', actionTier='{actionTier}', evaluatedCandidates={summary.EvaluatedCandidates}, validCandidates={summary.ValidCandidates}, rejectedCandidates={summary.RejectedCandidates}, rejectedByArmyAssignment={summary.RejectedByArmyAssignment}, rejectedByDefenseAssignment={summary.RejectedByDefenseAssignment}, rejectedTooFar={summary.RejectedTooFar}, rejectedTooWeak={summary.RejectedTooWeak}, rejectedTooWeakHard={summary.RejectedTooWeakHard}, acceptedWeakSupplemental={summary.AcceptedWeakSupplemental}, rejectedWrongFaction={summary.RejectedWrongFaction}, rejectedInvalid={summary.RejectedInvalid}, rejectedAlreadyDefendingDifferentSettlement={summary.RejectedAlreadyDefendingDifferentSettlement}, acceptedReassignment={summary.AcceptedReassignment}, selectedCandidate='{summary.SelectedCandidate}', selectedCandidateScore={summary.SelectedCandidateScore:0.00}, selectedCandidateDistance={summary.SelectedCandidateDistance:0.00}, selectedCandidateStrength={summary.SelectedCandidateStrength:0.00}, reason='{summary.Reason}'");
        }

        private static PartyObservationCategory GetCommandPartyCategory(MobileParty party)
        {
            if (party == null)
            {
                return PartyObservationCategory.Unknown;
            }

            return party.Army != null && party.Army.LeaderParty == party
                ? PartyObservationCategory.ArmyParty
                : PartyObservationCategory.LordParty;
        }

        private CsmDefenseAssignment GetValidActiveAssignmentForSettlement(
            DefenseEvaluationSnapshot snapshot,
            Settlement settlement,
            int observationTick)
        {
            if (!DefenseAssignmentSettings.EnableDefenseAssignments || settlement == null)
            {
                return null;
            }

            var settlementId = GetSettlementId(settlement);
            var settlementName = snapshot.ThreatReport.SettlementName;
            if (!_assignmentRegistry.HasActiveAssignmentForSettlement(settlementId, settlementName))
            {
                return null;
            }

            var assignments = _assignmentRegistry.GetActiveAssignmentsForSettlement(settlementId, settlementName);
            for (var index = 0; index < assignments.Count; index++)
            {
                var assignment = assignments[index];
                var party = FindPartyByIdOrName(assignment.PartyId, assignment.PartyName);
                var invalidReason = GetInvalidAssignmentReason(snapshot, assignment, party, settlement, observationTick);
                if (string.IsNullOrWhiteSpace(invalidReason))
                {
                    return assignment;
                }
            }

            return null;
        }

        private bool TryCloseDefenseAssignment(
            DefenseEvaluationSnapshot? snapshot,
            CsmDefenseAssignment assignment,
            MobileParty party,
            Settlement settlement,
            CsmArmyAssignmentRegistry armyAssignments,
            int observationTick,
            string status,
            string requestedCloseReason,
            string closeReason,
            bool progressExpired)
        {
            var decision = BuildDefenseAuthorityReleaseDecision(snapshot, assignment, party, settlement, armyAssignments, observationTick, requestedCloseReason);
            LogDefenseAuthorityReleaseDecision(decision);
            if (!decision.ReleaseAllowed)
            {
                if (assignment != null)
                {
                    assignment.LastReleaseBlockedReason = decision.FinalReason;
                    assignment.Status = assignment.ArrivedOrNear ? "ActiveCommitted" : "Active";
                }

                return false;
            }

            _assignmentRegistry.Close(assignment, status, closeReason);
            RecordClosedAssignment(status, progressExpired);
            return true;
        }

        private static DefenseAuthorityReleaseDecision BuildDefenseAuthorityReleaseDecision(
            DefenseEvaluationSnapshot? snapshot,
            CsmDefenseAssignment assignment,
            MobileParty party,
            Settlement settlement,
            CsmArmyAssignmentRegistry armyAssignments,
            int observationTick,
            string requestedCloseReason)
        {
            var hardLockEnabled = DefenseAssignmentSettings.EnableDefenseAssignmentAuthorityHardLock;
            var partyValid = !IsDefenseAuthorityPartyInvalid(party);
            var partyInArmy = IsDefenseAuthorityPartyInArmy(party, armyAssignments);
            var partyInBattle = party != null && party.MapEvent != null;
            var settlementStillUnderSiege = IsDefenseAuthoritySettlementStillUnderSiege(snapshot, settlement);
            var currentTarget = GetDefenseAuthorityCurrentTarget(party);
            var currentBehavior = GetDefenseAuthorityCurrentBehavior(party);
            var releaseAllowed = true;
            var finalReason = "Release allowed";

            if (hardLockEnabled
                && assignment != null
                && !IsExpiredAssignment(assignment, observationTick)
                && IsDefenseAuthorityThreatActive(snapshot, settlement)
                && IsDefenseAuthoritySettlementActive(settlement)
                && IsDefenseAuthorityPartyStillFriendlyOrTransient(party, settlement))
            {
                var invalidExceeded = assignment.InvalidPartyTicks > GetDefenseAuthorityMaxInvalidPartyTicks();
                var armyExceeded = assignment.InArmyTicks > GetDefenseAuthorityMaxInArmyTicksBeforeRelease();
                var battleExceeded = assignment.InBattleTicks > GetDefenseAuthorityMaxInBattleTicksBeforeRelease();
                var age = observationTick - assignment.AuthorityHardLockStartTick;
                if (assignment.AuthorityHardLockStartTick < 0)
                {
                    age = observationTick - assignment.CommandStartTick;
                }

                var minimumAgeReached = age >= GetDefenseAuthorityHardLockMinimumTicks();
                var stableThreatReached = assignment.StableThreatTicks >= GetDefenseAuthorityStableThreatTicksBeforeRelease();
                if (!invalidExceeded && !armyExceeded && !battleExceeded && (!minimumAgeReached || !stableThreatReached))
                {
                    releaseAllowed = false;
                    finalReason = "Authority hard lock kept assignment active";
                }
            }

            return new DefenseAuthorityReleaseDecision
            {
                Tick = observationTick,
                PartyName = assignment == null ? GetPartyName(party) : assignment.PartyName,
                SettlementName = assignment == null ? GetSettlementName(settlement) : assignment.SettlementName,
                AssignmentStatus = assignment == null ? "unknown" : assignment.Status,
                RequestedCloseReason = requestedCloseReason,
                SettlementStillUnderSiege = settlementStillUnderSiege,
                PartyValid = partyValid,
                PartyInArmy = partyInArmy,
                PartyInBattle = partyInBattle,
                CurrentTarget = currentTarget,
                CurrentBehavior = currentBehavior,
                HardLockEnabled = hardLockEnabled,
                ReleaseAllowed = releaseAllowed,
                FinalReason = finalReason
            };
        }

        private static void UpdateDefenseAuthorityCounters(
            CsmDefenseAssignment assignment,
            MobileParty party,
            CsmArmyAssignmentRegistry armyAssignments,
            int observationTick)
        {
            if (assignment == null || assignment.LastAuthorityCounterTick == observationTick)
            {
                return;
            }

            assignment.LastAuthorityCounterTick = observationTick;
            if (assignment.AuthorityHardLockStartTick < 0)
            {
                assignment.AuthorityHardLockStartTick = observationTick;
            }

            assignment.InvalidPartyTicks = IsDefenseAuthorityPartyInvalid(party)
                ? assignment.InvalidPartyTicks + 1
                : 0;
            assignment.InArmyTicks = IsDefenseAuthorityPartyInArmy(party, armyAssignments)
                ? assignment.InArmyTicks + 1
                : 0;
            assignment.InBattleTicks = party != null && party.MapEvent != null
                ? assignment.InBattleTicks + 1
                : 0;
        }

        private static bool IsDefenseAuthorityPartyInvalid(MobileParty party)
        {
            if (party == null)
            {
                return true;
            }

            if (!party.IsActive || party.IsDisbanding || party.IsMainParty)
            {
                return true;
            }

            return party.MemberRoster == null || party.MemberRoster.TotalManCount <= 0;
        }

        private static bool IsDefenseAuthorityPartyInArmy(MobileParty party, CsmArmyAssignmentRegistry armyAssignments)
        {
            if (party == null)
            {
                return false;
            }

            return party.Army != null || HasActiveCsmArmyAssignment(party, armyAssignments);
        }

        private static bool IsDefenseAuthorityPartyStillFriendlyOrTransient(MobileParty party, Settlement settlement)
        {
            if (party == null)
            {
                return true;
            }

            if (settlement == null || settlement.MapFaction == null || party.MapFaction == null)
            {
                return false;
            }

            return party.MapFaction == settlement.MapFaction;
        }

        private static bool IsDefenseAuthoritySettlementActive(Settlement settlement)
        {
            return settlement != null && settlement.IsActive;
        }

        private static bool IsDefenseAuthoritySettlementStillUnderSiege(DefenseEvaluationSnapshot? snapshot, Settlement settlement)
        {
            if (settlement != null && settlement.SiegeEvent != null)
            {
                return true;
            }

            return snapshot.HasValue && snapshot.Value.ThreatReport.HasActiveSiege;
        }

        private static bool IsDefenseAuthorityThreatActive(DefenseEvaluationSnapshot? snapshot, Settlement settlement)
        {
            if (IsDefenseAuthoritySettlementStillUnderSiege(snapshot, settlement))
            {
                return true;
            }

            return snapshot.HasValue && IsAssignmentThreatStillValid(snapshot.Value);
        }

        private static string GetDefenseAuthorityCurrentTarget(MobileParty party)
        {
            return party == null || party.TargetSettlement == null
                ? "none"
                : GetSettlementName(party.TargetSettlement);
        }

        private static string GetDefenseAuthorityCurrentBehavior(MobileParty party)
        {
            return party == null ? "none" : party.DefaultBehavior.ToString();
        }

        private static void LogDefenseAuthorityReleaseDecision(DefenseAuthorityReleaseDecision decision)
        {
            if (decision == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed CSM defense authority release decision: tick={decision.Tick}, party='{decision.PartyName}', settlement='{decision.SettlementName}', assignmentStatus='{decision.AssignmentStatus}', requestedCloseReason='{decision.RequestedCloseReason}', settlementStillUnderSiege={decision.SettlementStillUnderSiege}, partyValid={decision.PartyValid}, partyInArmy={decision.PartyInArmy}, partyInBattle={decision.PartyInBattle}, currentTarget='{decision.CurrentTarget}', currentBehavior='{decision.CurrentBehavior}', hardLockEnabled={decision.HardLockEnabled}, releaseAllowed={decision.ReleaseAllowed}, finalReason='{decision.FinalReason}'");
        }

        private static void LogDefenseAuthorityHardLockDiagnostics(
            DefenseEvaluationSnapshot? snapshot,
            CsmDefenseAssignment assignment,
            MobileParty party,
            Settlement settlement,
            DefenseAssignmentMovementSnapshot movement,
            int observationTick)
        {
            if (!DefenseAssignmentSettings.EnableDefenseAssignmentAuthorityHardLock
                || assignment == null
                || !IsDefenseAuthorityThreatActive(snapshot, settlement)
                || IsExpiredAssignment(assignment, observationTick))
            {
                return;
            }

            var decision = BuildDefenseAuthorityReleaseDecision(snapshot, assignment, party, settlement, null, observationTick, "Authority hard lock diagnostic");
            var currentTarget = movement == null ? decision.CurrentTarget : movement.CurrentTarget;
            if (NamesEqual(currentTarget, "unknown"))
            {
                currentTarget = "none";
            }

            var currentBehavior = movement == null ? decision.CurrentBehavior : movement.CurrentBehavior;
            CsmLogger.Info(
                $"Observed CSM defense authority hard lock: tick={observationTick}, party='{assignment.PartyName}', settlement='{assignment.SettlementName}', active=True, settlementStillUnderSiege={decision.SettlementStillUnderSiege}, currentTarget='{currentTarget}', currentBehavior='{currentBehavior}', arrivedOrNear={assignment.ArrivedOrNear}, invalidPartyTicks={assignment.InvalidPartyTicks}, inArmyTicks={assignment.InArmyTicks}, inBattleTicks={assignment.InBattleTicks}, releaseBlocked={!decision.ReleaseAllowed}, reason='CSM Defense assignment authority hard lock evaluation'");
        }

        private static string GetStaleAssignmentCleanupReason(
            DefenseEvaluationSnapshot? snapshot,
            CsmDefenseAssignment assignment,
            MobileParty party,
            Settlement settlement,
            CsmArmyAssignmentRegistry armyAssignments,
            int observationTick)
        {
            if (assignment == null)
            {
                return "Assignment missing";
            }

            if (IsExpiredAssignment(assignment, observationTick))
            {
                return "Assignment age limit exceeded";
            }

            if (settlement == null)
            {
                return "Assigned settlement not found";
            }

            if (!string.IsNullOrWhiteSpace(assignment.OwnerKingdomName)
                && !NamesEqual(assignment.OwnerKingdomName, "unknown")
                && !NamesEqual(GetFactionName(settlement.MapFaction), assignment.OwnerKingdomName))
            {
                return "Assigned settlement owner changed";
            }

            if (party == null)
            {
                return "Assigned party not found";
            }

            if (!party.IsActive)
            {
                return "Assigned party is inactive";
            }

            if (party.IsDisbanding)
            {
                return "Assigned party is disbanding";
            }

            if (party.IsMainParty)
            {
                return "Assigned party is main party";
            }

            if (party.MapEvent != null)
            {
                return "Assigned party is in battle";
            }

            if (party.Army != null && party.Army.LeaderParty != party)
            {
                return "Assigned party joined army as non-leader";
            }

            if (HasActiveCsmArmyAssignment(party, armyAssignments))
            {
                return "Assigned party has active CSM army assignment";
            }

            if (settlement.MapFaction == null || party.MapFaction != settlement.MapFaction)
            {
                return "Assigned party faction no longer matches settlement owner";
            }

            if (party.MemberRoster == null || party.MemberRoster.TotalManCount <= 0)
            {
                return "Assigned party has no troops";
            }

            if (settlement.SiegeEvent == null && (snapshot == null || !snapshot.Value.ThreatReport.HasActiveSiege))
            {
                return "Settlement no longer under siege";
            }

            if (snapshot != null && !IsAssignmentThreatStillValid(snapshot.Value))
            {
                return "Settlement no longer has urgent defense threat";
            }

            return null;
        }

        private static string GetStaleAssignmentCleanupStatus(string staleReason)
        {
            if (NamesEqual(staleReason, "Assignment age limit exceeded"))
            {
                return "Expired";
            }

            if (NamesEqual(staleReason, "Settlement no longer under siege")
                || NamesEqual(staleReason, "Settlement no longer has urgent defense threat"))
            {
                return "Deescalated";
            }

            return "Invalid";
        }

        private static string GetInvalidAssignmentReason(
            DefenseEvaluationSnapshot snapshot,
            CsmDefenseAssignment assignment,
            MobileParty party,
            Settlement settlement,
            int observationTick)
        {
            if (assignment == null)
            {
                return "Assignment missing";
            }

            if (IsExpiredAssignment(assignment, observationTick))
            {
                return "Assignment age limit exceeded";
            }

            if (party == null)
            {
                return "Assigned party not found";
            }

            if (settlement == null)
            {
                return "Assigned settlement not found";
            }

            if (!string.IsNullOrWhiteSpace(assignment.OwnerKingdomName)
                && !NamesEqual(assignment.OwnerKingdomName, "unknown")
                && !NamesEqual(GetFactionName(settlement.MapFaction), assignment.OwnerKingdomName))
            {
                return "Assigned settlement owner changed";
            }

            if (!party.IsActive)
            {
                return "Assigned party is inactive";
            }

            if (party.IsDisbanding)
            {
                return "Assigned party is disbanding";
            }

            if (party.IsMainParty)
            {
                return "Assigned party is main party";
            }

            if (party.MapEvent != null)
            {
                return "Assigned party is in battle";
            }

            if (party.Army != null && party.Army.LeaderParty != party)
            {
                return "Assigned party joined army as non-leader";
            }

            if (settlement.MapFaction == null || party.MapFaction != settlement.MapFaction)
            {
                return "Assigned party faction no longer matches settlement owner";
            }

            if (party.BesiegedSettlement != null && !NamesEqual(GetSettlementName(party.BesiegedSettlement), assignment.SettlementName))
            {
                return "Assigned party is besieging another settlement";
            }

            if (party.MemberRoster == null || party.MemberRoster.TotalManCount <= 0)
            {
                return "Assigned party has no troops";
            }

            return null;
        }

        private void EvaluateAndLogDefenseCommitment(
            DefenseEvaluationSnapshot snapshot,
            CsmDefenseAssignment assignment,
            MobileParty party,
            Settlement settlement,
            DefenseAssignmentMovementSnapshot movement,
            int observationTick)
        {
            if (!DefenseControllerSettings.EnableDefensePostArrivalCommitment || assignment == null)
            {
                return;
            }

            var distanceToTarget = movement == null ? 0f : movement.DistanceToTarget;
            var arrivedOrNear = IsDefenseCommitmentArrivedOrNear(party, settlement, movement, distanceToTarget);
            if (arrivedOrNear)
            {
                assignment.ArrivedOrNear = true;
                if (assignment.PostArrivalCommitmentStartTick < 0)
                {
                    assignment.PostArrivalCommitmentStartTick = observationTick;
                }
            }

            var settlementStillUnderSiege = settlement != null && settlement.SiegeEvent != null;
            if (settlementStillUnderSiege && assignment.ArrivedOrNear)
            {
                assignment.Status = "ActiveCommitted";
            }

            var coverageStatus = GetCoverageStatus(snapshot.CoverageReport);
            if (settlementStillUnderSiege && snapshot.CoverageReport.IsDefenseEnough && !IsLowOrCriticalCoverage(snapshot.CoverageReport))
            {
                assignment.StableThreatTicks++;
            }
            else
            {
                assignment.StableThreatTicks = 0;
            }

            var holdTicksRemaining = GetPostArrivalHoldTicksRemaining(assignment, observationTick);
            var wouldRelease = IsPostArrivalReleaseAllowed(snapshot, settlement, assignment, observationTick);
            CsmLogger.Info(
                $"Observed CSM defense commitment: tick={observationTick}, party='{assignment.PartyName}', settlement='{assignment.SettlementName}', distanceToTarget={distanceToTarget:0.00}, arrivedOrNear={arrivedOrNear}, assignmentActive=True, settlementStillUnderSiege={settlementStillUnderSiege}, coverageStatus='{coverageStatus}', coverageRatio={snapshot.CoverageReport.DefenseCoverageRatio:0.00}, holdTicksRemaining={holdTicksRemaining}, wouldRelease={wouldRelease}, reason='Defense post-arrival commitment evaluation'");

            if (assignment.ArrivedOrNear
                && movement != null
                && (movement.TargetOverwritten || !NamesEqual(movement.CurrentBehavior, AiBehavior.DefendSettlement.ToString())))
            {
                CsmLogger.Info(
                    $"Observed CSM defense post-arrival target loss: tick={observationTick}, party='{assignment.PartyName}', settlement='{assignment.SettlementName}', previouslyArrived=True, currentTarget='{movement.CurrentTarget}', expectedTarget='{movement.ExpectedTarget}', currentBehavior='{movement.CurrentBehavior}', assignmentActive=True, harmonyLockAppliedRecently={assignment.LastHarmonyDefenseLockTick >= 0}, reason='Defender left after arriving'");
            }
        }

        private static bool IsDefenseCommitmentArrivedOrNear(
            MobileParty party,
            Settlement settlement,
            DefenseAssignmentMovementSnapshot movement,
            float distanceToTarget)
        {
            if (party == null || settlement == null)
            {
                return false;
            }

            return party.CurrentSettlement == settlement
                || distanceToTarget <= GetDefenseArrivalDistanceThreshold()
                || (party.TargetSettlement == settlement && party.DefaultBehavior == AiBehavior.DefendSettlement);
        }

        private static bool IsPostArrivalReleaseAllowed(
            DefenseEvaluationSnapshot snapshot,
            Settlement settlement,
            CsmDefenseAssignment assignment,
            int observationTick)
        {
            if (!DefenseControllerSettings.EnableDefensePostArrivalCommitment || assignment == null)
            {
                return true;
            }

            var settlementStillUnderSiege = settlement != null && settlement.SiegeEvent != null;
            if (!settlementStillUnderSiege)
            {
                return true;
            }

            if (!assignment.ArrivedOrNear)
            {
                return false;
            }

            if (GetPostArrivalHoldTicksRemaining(assignment, observationTick) > 0)
            {
                return false;
            }

            if (!snapshot.CoverageReport.IsDefenseEnough || IsLowOrCriticalCoverage(snapshot.CoverageReport))
            {
                return false;
            }

            return assignment.StableThreatTicks >= GetDefenseStableThreatTicksBeforeRelease();
        }

        private static int GetPostArrivalHoldTicksRemaining(CsmDefenseAssignment assignment, int observationTick)
        {
            if (assignment == null || assignment.PostArrivalCommitmentStartTick < 0)
            {
                return GetDefensePostArrivalMinimumHoldTicks();
            }

            var elapsed = observationTick - assignment.PostArrivalCommitmentStartTick;
            var remaining = GetDefensePostArrivalMinimumHoldTicks() - elapsed;
            return remaining < 0 ? 0 : remaining;
        }

        private string GetDeescalatedAssignmentReason(
            DefenseEvaluationSnapshot snapshot,
            Settlement settlement,
            CsmDefenseAssignment assignment,
            DefenseAssignmentEffectivenessSnapshot effectiveness,
            int observationTick)
        {
            if (settlement != null && settlement.SiegeEvent == null)
            {
                _coverageEnoughStableTicks.Remove(Normalize(snapshot.ThreatReport.SettlementName));
                return "Settlement no longer under siege";
            }

            var stableEnoughTicks = UpdateCoverageEnoughStableTicks(snapshot);
            if (snapshot.CoverageReport.IsDefenseEnough)
            {
                if (!IsPostArrivalReleaseAllowed(snapshot, settlement, assignment, observationTick))
                {
                    return "Deescalation delayed because defense assignments are not committed yet";
                }

                if (settlement != null && settlement.SiegeEvent != null
                    && (stableEnoughTicks < GetCoverageEnoughStableTicksBeforeDeescalation()
                        || !HasCommittedEffectiveDefendersForDeescalation(snapshot, settlement)
                        || effectiveness == null
                        || !effectiveness.IsEffective))
                {
                    return "Deescalation delayed because defense assignments are not committed yet";
                }

                return "Settlement defense coverage is enough";
            }

            _coverageEnoughStableTicks.Remove(Normalize(snapshot.ThreatReport.SettlementName));
            return !IsAssignmentThreatStillValid(snapshot)
                ? "Settlement no longer has urgent defense threat"
                : null;
        }

        private static string GetCompletedAssignmentReason(
            DefenseEvaluationSnapshot snapshot,
            CsmDefenseAssignment assignment,
            MobileParty party,
            Settlement settlement,
            DefenseAssignmentMovementSnapshot movement,
            int observationTick)
        {
            if (party != null && settlement != null)
            {
                var distance = party.Position.Distance(settlement.Position);
                if (party.CurrentSettlement == settlement || distance <= GetDefenseAssignmentArrivalDistance())
                {
                    if (!IsPostArrivalReleaseAllowed(snapshot, settlement, assignment, observationTick))
                    {
                        return DefenseAssignmentSettings.EnableDefenseAssignmentAuthorityHardLock
                            && IsDefenseAuthorityThreatActive(snapshot, settlement)
                            ? "Assigned party arrived at settlement"
                            : null;
                    }

                    return "Assigned party arrived at settlement";
                }

                if (party.DefaultBehavior == AiBehavior.DefendSettlement
                    && party.TargetSettlement == settlement
                    && distance <= GetDefenseAssignmentArrivalDistance())
                {
                    if (!IsPostArrivalReleaseAllowed(snapshot, settlement, assignment, observationTick))
                    {
                        return DefenseAssignmentSettings.EnableDefenseAssignmentAuthorityHardLock
                            && IsDefenseAuthorityThreatActive(snapshot, settlement)
                            ? "Assigned party is defending settlement"
                            : null;
                    }

                    return "Assigned party is defending settlement";
                }
            }

            return null;
        }

        private DefenseAssignmentMovementSnapshot TrackDefenseAssignmentMovement(
            CsmDefenseAssignment assignment,
            MobileParty party,
            Settlement settlement,
            int observationTick)
        {
            var distance = party.Position.Distance(settlement.Position);
            var previousDistance = assignment.LastDistanceToSettlement;
            var distanceDelta = previousDistance < 0f ? 0f : previousDistance - distance;
            var movingTowardTarget = previousDistance >= 0f && distanceDelta >= DefenseAssignmentSettings.DefenseMovementProgressTolerance;
            var targetOverwritten = IsDefenseTargetOverwritten(party, settlement);
            var reached = IsPartyAtDefenseTarget(party, settlement, distance);

            if (previousDistance < 0f || movingTowardTarget || reached)
            {
                assignment.LastProgressTick = observationTick;
                assignment.StalledTicks = 0;
            }
            else if (!reached)
            {
                assignment.StalledTicks++;
            }

            assignment.LastDistanceToSettlement = distance;

            return new DefenseAssignmentMovementSnapshot
            {
                Tick = observationTick,
                SettlementName = assignment.SettlementName,
                PartyName = assignment.PartyName,
                CurrentTarget = GetSettlementName(party.TargetSettlement),
                ExpectedTarget = GetSettlementName(settlement),
                CurrentBehavior = party.DefaultBehavior.ToString(),
                DistanceToTarget = distance,
                PreviousDistanceToTarget = previousDistance,
                DistanceDelta = distanceDelta,
                TargetOverwritten = targetOverwritten,
                MovingTowardTarget = movingTowardTarget,
                StalledTicks = assignment.StalledTicks,
                Reasserted = false
            };
        }

        private bool TryReassertDefenseMovement(
            DefenseEvaluationSnapshot snapshot,
            CsmDefenseAssignment assignment,
            MobileParty party,
            Settlement settlement,
            DefenseAssignmentMovementSnapshot movement,
            int observationTick)
        {
            if (!ShouldReassertDefenseMovement(snapshot, assignment, party, settlement, movement, observationTick))
            {
                return false;
            }

            party.SetMoveDefendSettlement(settlement, false, party.NavigationCapability);
            _assignmentRegistry.MarkReasserted(assignment, observationTick, "Defense target overwritten; reasserted movement");
            assignment.LastProgressTick = observationTick;
            assignment.LastDistanceToSettlement = movement.DistanceToTarget;
            _movementReassertedThisTick++;
            _reassertedAssignmentsThisTick++;
            if (IsActiveSiegeAssignment(snapshot, settlement))
            {
                _activeSiegeReassertedThisTick++;
            }

            return true;
        }

        private DefenseAssignmentEffectivenessSnapshot EvaluateDefenseAssignmentEffectiveness(
            CsmDefenseAssignment assignment,
            MobileParty party,
            Settlement settlement,
            DefenseAssignmentMovementSnapshot movement,
            int observationTick)
        {
            var strength = GetPartyStrength(party);
            var closeOrArrived = IsPartyAtDefenseTarget(party, settlement, movement.DistanceToTarget);
            var hasExpectedTarget = party.TargetSettlement == settlement;
            var defendingExpectedSettlement = party.DefaultBehavior == AiBehavior.DefendSettlement && party.TargetSettlement == settlement;
            var isEffective = !movement.TargetOverwritten
                || hasExpectedTarget
                || defendingExpectedSettlement
                || closeOrArrived
                || movement.MovingTowardTarget;
            if (movement.TargetOverwritten && assignment.StalledTicks >= DefenseAssignmentSettings.DefenseMovementStallTicks)
            {
                isEffective = false;
            }

            assignment.IneffectiveTicks = isEffective ? 0 : assignment.IneffectiveTicks + 1;
            var contribution = isEffective
                ? strength
                : strength * 0.25f;
            assignment.LastEffectiveStrengthContribution = contribution;
            if (isEffective)
            {
                _effectiveAssignmentsThisTick++;
            }
            else
            {
                _ineffectiveAssignmentsThisTick++;
            }

            return new DefenseAssignmentEffectivenessSnapshot
            {
                Tick = observationTick,
                SettlementName = assignment.SettlementName,
                PartyName = assignment.PartyName,
                IsEffective = isEffective,
                TargetOverwritten = movement.TargetOverwritten,
                MovingTowardTarget = movement.MovingTowardTarget,
                StalledTicks = assignment.StalledTicks,
                IneffectiveTicks = assignment.IneffectiveTicks,
                DistanceToTarget = movement.DistanceToTarget,
                EffectiveStrengthContribution = contribution
            };
        }

        private static bool ShouldReassertDefenseMovement(
            DefenseEvaluationSnapshot snapshot,
            CsmDefenseAssignment assignment,
            MobileParty party,
            Settlement settlement,
            DefenseAssignmentMovementSnapshot movement,
            int observationTick)
        {
            if (assignment == null || party == null || settlement == null || movement == null)
            {
                return false;
            }

            if (party.MapEvent != null || party.CurrentSettlement == settlement || IsPartyAtDefenseTarget(party, settlement, movement.DistanceToTarget))
            {
                return false;
            }

            var activeSiegeAssignment = IsActiveSiegeAssignment(snapshot, settlement);
            if (activeSiegeAssignment && !DefenseControllerSettings.EnableStrongActiveSiegeDefenseReassert)
            {
                activeSiegeAssignment = false;
            }

            if (activeSiegeAssignment && HasActiveCsmArmyAssignment(party, null))
            {
                return false;
            }

            if (activeSiegeAssignment && IsPartyDefendingAnotherActiveSiege(party, settlement))
            {
                return false;
            }

            var maxReassertions = activeSiegeAssignment ? GetMaxActiveSiegeDefenseReassertions() : GetMaxReassertionsPerAssignment();
            if (assignment.ReassertionCount >= maxReassertions)
            {
                return false;
            }

            var stalledThreshold = activeSiegeAssignment
                ? GetActiveSiegeReassertStalledTickThreshold()
                : DefenseAssignmentSettings.DefenseMovementStallTicks;
            if (!movement.TargetOverwritten && movement.MovingTowardTarget && assignment.StalledTicks < stalledThreshold)
            {
                return false;
            }

            var cooldown = activeSiegeAssignment
                ? GetActiveSiegeDefenseReassertCooldownTicks()
                : GetDefenseMovementReassertCooldownTicks(snapshot);
            return observationTick - assignment.LastCommandTick >= cooldown;
        }

        private static string GetMovementIssue(DefenseAssignmentMovementSnapshot movement)
        {
            if (movement == null)
            {
                return "Invalid";
            }

            if (movement.TargetOverwritten)
            {
                return "TargetOverwritten";
            }

            if (movement.MovingTowardTarget)
            {
                return null;
            }

            return movement.StalledTicks >= DefenseAssignmentSettings.DefenseMovementStallTicks
                ? "Stalled"
                : null;
        }

        private static bool ShouldFailActiveSiegeDefenseMovement(
            DefenseEvaluationSnapshot snapshot,
            CsmDefenseAssignment assignment,
            MobileParty party,
            Settlement settlement,
            DefenseAssignmentMovementSnapshot movement)
        {
            if (!DefenseControllerSettings.EnableActiveSiegeDefenseReplacement
                || !IsActiveSiegeAssignment(snapshot, settlement)
                || assignment == null
                || party == null
                || settlement == null
                || movement == null
                || party.MapEvent != null)
            {
                return false;
            }

            if (!movement.TargetOverwritten || movement.MovingTowardTarget)
            {
                return false;
            }

            if (movement.StalledTicks < GetActiveSiegeDefenseMovementFailedTicks())
            {
                return false;
            }

            if (assignment.ReassertionCount <= 0)
            {
                return false;
            }

            return party.TargetSettlement != settlement
                || party.DefaultBehavior != AiBehavior.DefendSettlement;
        }

        private static bool IsActiveSiegeAssignment(DefenseEvaluationSnapshot snapshot, Settlement settlement)
        {
            return settlement != null
                && (settlement.SiegeEvent != null || snapshot.ThreatReport.HasActiveSiege);
        }

        private static bool IsPartyDefendingAnotherActiveSiege(MobileParty party, Settlement expectedSettlement)
        {
            if (party == null || expectedSettlement == null || party.TargetSettlement == null || party.TargetSettlement == expectedSettlement)
            {
                return false;
            }

            return party.DefaultBehavior == AiBehavior.DefendSettlement
                && party.TargetSettlement.SiegeEvent != null;
        }

        private static string GetIneffectiveReplacementReason(
            CsmDefenseAssignment assignment,
            DefenseAssignmentEffectivenessSnapshot effectiveness)
        {
            if (!DefenseAssignmentSettings.AllowReplacementForIneffectiveDefenseAssignments
                || assignment == null
                || effectiveness == null
                || effectiveness.IsEffective)
            {
                return null;
            }

            return assignment.IneffectiveTicks >= GetDefenseAssignmentIneffectiveTicksBeforeReplacement()
                ? "Ineffective defense assignment replaced or supplemented"
                : null;
        }

        private int UpdateCoverageEnoughStableTicks(DefenseEvaluationSnapshot snapshot)
        {
            var key = Normalize(snapshot.ThreatReport.SettlementName);
            if (!snapshot.CoverageReport.IsDefenseEnough)
            {
                _coverageEnoughStableTicks.Remove(key);
                return 0;
            }

            int current;
            _coverageEnoughStableTicks.TryGetValue(key, out current);
            current++;
            _coverageEnoughStableTicks[key] = current;
            return current;
        }

        private bool HasCommittedEffectiveDefendersForDeescalation(DefenseEvaluationSnapshot snapshot, Settlement settlement)
        {
            if (settlement == null)
            {
                return false;
            }

            var assignments = GetValidActiveAssignmentsForSettlement(snapshot, settlement, _currentTick);
            var effectiveStrength = 0f;
            var hasCloseOrDefending = false;
            for (var index = 0; index < assignments.Count; index++)
            {
                var assignment = assignments[index];
                var party = FindPartyByIdOrName(assignment.PartyId, assignment.PartyName);
                if (party == null)
                {
                    continue;
                }

                var distance = party.Position.Distance(settlement.Position);
                if (assignment.LastEffectiveStrengthContribution > 0f)
                {
                    effectiveStrength += assignment.LastEffectiveStrengthContribution;
                }

                if (IsPartyAtDefenseTarget(party, settlement, distance))
                {
                    hasCloseOrDefending = true;
                }
            }

            var requiredEffectiveStrength = snapshot.CoverageReport.RequiredThreatStrength
                * DefenseAssignmentSettings.MinimumEffectiveDefenderRatioForDeescalation;
            return hasCloseOrDefending && effectiveStrength >= requiredEffectiveStrength;
        }

        private static string GetInvalidMovementIssue(string invalidReason)
        {
            if (invalidReason != null
                && invalidReason.IndexOf("active CSM army assignment", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "JoinedArmy";
            }

            return invalidReason != null
                && invalidReason.IndexOf("joined army", StringComparison.OrdinalIgnoreCase) >= 0
                ? "JoinedArmy"
                : "Invalid";
        }

        private static bool HasActiveCsmArmyAssignment(MobileParty party, CsmArmyAssignmentRegistry armyAssignments)
        {
            if (party == null || armyAssignments == null || party.Army == null || party.Army.LeaderParty == null)
            {
                return false;
            }

            return armyAssignments.GetActiveAssignmentForArmy(GetPartyId(party.Army.LeaderParty)) != null;
        }

        private static bool IsDefenseTargetOverwritten(MobileParty party, Settlement settlement)
        {
            if (party == null || settlement == null)
            {
                return true;
            }

            return party.TargetSettlement != settlement
                || party.DefaultBehavior != AiBehavior.DefendSettlement;
        }

        private static bool IsPartyAtDefenseTarget(MobileParty party, Settlement settlement, float distance)
        {
            if (party == null || settlement == null)
            {
                return false;
            }

            return party.CurrentSettlement == settlement
                || distance <= GetDefenseAssignmentArrivalDistance()
                || (party.DefaultBehavior == AiBehavior.DefendSettlement
                    && party.TargetSettlement == settlement
                    && distance <= GetDefenseAssignmentArrivalDistance());
        }

        private static string GetProgressExpiredReason(CsmDefenseAssignment assignment, MobileParty party, Settlement settlement, int observationTick)
        {
            if (assignment == null || party == null || settlement == null)
            {
                return null;
            }

            var assignmentAge = observationTick - assignment.CommandStartTick;
            if (assignmentAge < DefenseAssignmentSettings.DefenseAssignmentGraceTicks
                || assignmentAge < DefenseAssignmentSettings.MinimumTicksBeforeDefenseProgressExpiry)
            {
                return null;
            }

            var distance = party.Position.Distance(settlement.Position);
            if (assignment.LastDistanceToSettlement < 0f)
            {
                assignment.LastDistanceToSettlement = distance;
                assignment.LastProgressTick = observationTick;
                return null;
            }

            if (observationTick - assignment.LastProgressTick < GetProgressCheckEveryTicks())
            {
                return null;
            }

            if (assignment.LastDistanceToSettlement - distance >= DefenseAssignmentSettings.DefenseAssignmentProgressMinimumDistanceDelta)
            {
                assignment.LastDistanceToSettlement = distance;
                assignment.LastProgressTick = observationTick;
                return null;
            }

            if (party.TargetSettlement != null && party.TargetSettlement != settlement)
            {
                return "Assigned party moved to another target";
            }

            return "Assigned party is not making progress toward settlement";
        }

        private static bool IsExpiredAssignment(CsmDefenseAssignment assignment, int observationTick)
        {
            return observationTick - assignment.CommandStartTick > GetMaxAssignmentAgeTicks();
        }

        private static bool IsAssignmentThreatStillValid(DefenseEvaluationSnapshot snapshot)
        {
            if (!IsLowOrCriticalCoverage(snapshot.CoverageReport))
            {
                return false;
            }

            return snapshot.ThreatReport.HasActiveSiege
                || snapshot.CoverageReport.HasDirectSiegeThreat
                || snapshot.CoverageReport.HasArmyPresence;
        }

        private static bool ShouldReassertAssignment(CsmDefenseAssignment assignment, MobileParty party, Settlement settlement, int observationTick)
        {
            if (assignment.ReassertionCount >= GetMaxReassertionsPerAssignment())
            {
                return false;
            }

            if (party == null || settlement == null || party.TargetSettlement == settlement)
            {
                return false;
            }

            return observationTick - assignment.LastCommandTick >= DirectDefenseCommandSettings.DefenseCommandCooldownTicks;
        }

        private static string GetClosedAssignmentStatus(CsmDefenseAssignment assignment, int observationTick, string reason)
        {
            if (IsExpiredAssignment(assignment, observationTick))
            {
                return "Expired";
            }

            return NamesEqual(reason, "Settlement no longer has urgent defense threat")
                ? "Completed"
                : "Invalid";
        }

        private static bool IsUrgentDefenseAction(string actionPlanAction)
        {
            return NamesEqual(actionPlanAction, "UrgentDefense")
                || NamesEqual(actionPlanAction, "RequestUrgentDefense");
        }

        private static bool IsLowOrCriticalCoverage(DefenseCoverageReport coverageReport)
        {
            if (coverageReport.DefenseCoverageRatio <= DefenseActionThresholdSettings.UrgentDefenseCoverageRatioThreshold)
            {
                return true;
            }

            return coverageReport.DefenseCoverageRatio < DefenseActionThresholdSettings.ReinforcementCoverageRatioThreshold;
        }

        private static bool IsCriticalCoverage(DefenseCoverageReport coverageReport)
        {
            if (DefenseControllerSettings.EnableDefenseControllerV2)
            {
                return coverageReport.DefenseCoverageRatio <= DefenseControllerSettings.CriticalDefenseCoverageRatio;
            }

            return coverageReport.DefenseCoverageRatio <= DefenseActionThresholdSettings.CriticalCoverageRatio;
        }

        private static int GetRequiredStableUrgentTicks()
        {
            if (!DefenseControllerSettings.EnableDefenseControllerV2)
            {
                return DefenseActionThresholdSettings.RequiredStableDefenseTicks;
            }

            return DefenseControllerSettings.MinimumStableUrgentTicksForDirectCommand < 1
                ? 1
                : DefenseControllerSettings.MinimumStableUrgentTicksForDirectCommand;
        }

        private int GetSettlementCommandCount(string settlementName)
        {
            int count;
            return _settlementCommandCounts.TryGetValue(Normalize(settlementName), out count) ? count : 0;
        }

        private void IncrementSettlementCommandCount(string settlementName)
        {
            var key = Normalize(settlementName);
            _settlementCommandCounts[key] = GetSettlementCommandCount(settlementName) + 1;
        }

        private static int GetMaxCommandsPerDay()
        {
            return DirectDefenseCommandSettings.MaxDirectCommandsPerDay < 0
                ? 0
                : DirectDefenseCommandSettings.MaxDirectCommandsPerDay;
        }

        private static int GetMaxCommandsPerSettlementPerDay()
        {
            return DirectDefenseCommandSettings.MaxDirectCommandsPerSettlementPerDay < 0
                ? 0
                : DirectDefenseCommandSettings.MaxDirectCommandsPerSettlementPerDay;
        }

        private static int GetMaxAssignmentAgeTicks()
        {
            return DefenseAssignmentSettings.MaxDefenseAssignmentAgeTicks < 0
                ? 0
                : DefenseAssignmentSettings.MaxDefenseAssignmentAgeTicks;
        }

        private static int GetProgressCheckEveryTicks()
        {
            return DefenseAssignmentSettings.DefenseAssignmentProgressCheckEveryTicks < 1
                ? 1
                : DefenseAssignmentSettings.DefenseAssignmentProgressCheckEveryTicks;
        }

        private static int GetReassertCommandEveryTicks()
        {
            return DefenseAssignmentSettings.ReassertCommandEveryTicks < 1
                ? 1
                : DefenseAssignmentSettings.ReassertCommandEveryTicks;
        }

        private static int GetDefenseMovementReassertCooldownTicks(DefenseEvaluationSnapshot snapshot)
        {
            var configured = DefenseAssignmentSettings.DefenseMovementReassertCooldownTicks < 1
                ? 1
                : DefenseAssignmentSettings.DefenseMovementReassertCooldownTicks;
            if (snapshot.ThreatReport.HasActiveSiege && NamesEqual(GetCoverageStatus(snapshot.CoverageReport), "Critical"))
            {
                return configured;
            }

            return configured > DirectDefenseCommandSettings.DefenseCommandCooldownTicks
                ? configured
                : DirectDefenseCommandSettings.DefenseCommandCooldownTicks;
        }

        private static int GetCoverageEnoughStableTicksBeforeDeescalation()
        {
            return DefenseAssignmentSettings.DefenseCoverageEnoughStableTicksBeforeDeescalation < 1
                ? 1
                : DefenseAssignmentSettings.DefenseCoverageEnoughStableTicksBeforeDeescalation;
        }

        private static int GetDefenseAssignmentIneffectiveTicksBeforeReplacement()
        {
            return DefenseAssignmentSettings.DefenseAssignmentIneffectiveTicksBeforeReplacement < 1
                ? 1
                : DefenseAssignmentSettings.DefenseAssignmentIneffectiveTicksBeforeReplacement;
        }

        private static int GetActiveSiegeDefenseMovementFailedTicks()
        {
            return DefenseControllerSettings.ActiveSiegeDefenseMovementFailedTicks < 1
                ? 1
                : DefenseControllerSettings.ActiveSiegeDefenseMovementFailedTicks;
        }

        private static int GetActiveSiegeDefenseReassertCooldownTicks()
        {
            return DefenseControllerSettings.ActiveSiegeDefenseReassertCooldownTicks < 0
                ? 0
                : DefenseControllerSettings.ActiveSiegeDefenseReassertCooldownTicks;
        }

        private static int GetMaxActiveSiegeDefenseReassertions()
        {
            return DefenseControllerSettings.MaxActiveSiegeDefenseReassertions < 0
                ? 0
                : DefenseControllerSettings.MaxActiveSiegeDefenseReassertions;
        }

        private static int GetActiveSiegeReassertStalledTickThreshold()
        {
            return DefenseControllerSettings.ActiveSiegeReassertStalledTickThreshold < 0
                ? 0
                : DefenseControllerSettings.ActiveSiegeReassertStalledTickThreshold;
        }

        private static int GetDefensePostArrivalMinimumHoldTicks()
        {
            return DefenseControllerSettings.DefensePostArrivalMinimumHoldTicks < 0
                ? 0
                : DefenseControllerSettings.DefensePostArrivalMinimumHoldTicks;
        }

        private static int GetDefenseStableThreatTicksBeforeRelease()
        {
            return DefenseControllerSettings.DefenseStableThreatTicksBeforeRelease < 1
                ? 1
                : DefenseControllerSettings.DefenseStableThreatTicksBeforeRelease;
        }

        private static int GetDefenseAuthorityHardLockMinimumTicks()
        {
            return DefenseAssignmentSettings.DefenseAuthorityHardLockMinimumTicks < 0
                ? 0
                : DefenseAssignmentSettings.DefenseAuthorityHardLockMinimumTicks;
        }

        private static int GetDefenseAuthorityStableThreatTicksBeforeRelease()
        {
            return DefenseAssignmentSettings.DefenseAuthorityStableThreatTicksBeforeRelease < 1
                ? 1
                : DefenseAssignmentSettings.DefenseAuthorityStableThreatTicksBeforeRelease;
        }

        private static int GetDefenseAuthorityMaxInvalidPartyTicks()
        {
            return DefenseAssignmentSettings.DefenseAuthorityMaxInvalidPartyTicks < 0
                ? 0
                : DefenseAssignmentSettings.DefenseAuthorityMaxInvalidPartyTicks;
        }

        private static int GetDefenseAuthorityMaxInArmyTicksBeforeRelease()
        {
            return DefenseAssignmentSettings.DefenseAuthorityMaxInArmyTicksBeforeRelease < 0
                ? 0
                : DefenseAssignmentSettings.DefenseAuthorityMaxInArmyTicksBeforeRelease;
        }

        private static int GetDefenseAuthorityMaxInBattleTicksBeforeRelease()
        {
            return DefenseAssignmentSettings.DefenseAuthorityMaxInBattleTicksBeforeRelease < 0
                ? 0
                : DefenseAssignmentSettings.DefenseAuthorityMaxInBattleTicksBeforeRelease;
        }

        private static float GetDefenseArrivalDistanceThreshold()
        {
            return DefenseControllerSettings.DefenseArrivalDistanceThreshold <= 0f
                ? GetDefenseAssignmentArrivalDistance()
                : DefenseControllerSettings.DefenseArrivalDistanceThreshold;
        }

        private static float GetDefenseAssignmentArrivalDistance()
        {
            return DefenseAssignmentSettings.DefenseAssignmentArrivalDistance <= 0f
                ? DefenseAssignmentSettings.DefenseAssignmentArrivedDistance
                : DefenseAssignmentSettings.DefenseAssignmentArrivalDistance;
        }

        private static float GetTargetOverwrittenEffectiveContributionMultiplier()
        {
            return DefenseControllerSettings.TargetOverwrittenEffectiveContributionMultiplier < 0f
                ? 0f
                : DefenseControllerSettings.TargetOverwrittenEffectiveContributionMultiplier;
        }

        private static int GetTargetOverwrittenTicksBeforeIneffective()
        {
            return DefenseControllerSettings.TargetOverwrittenTicksBeforeIneffective < 1
                ? 1
                : DefenseControllerSettings.TargetOverwrittenTicksBeforeIneffective;
        }

        private static float GetFarDefenderContributionMultiplier()
        {
            return DefenseControllerSettings.FarDefenderContributionMultiplier < 0f
                ? 0f
                : DefenseControllerSettings.FarDefenderContributionMultiplier;
        }

        private static float GetFarDefenderDistanceThreshold()
        {
            return DefenseControllerSettings.FarDefenderDistanceThreshold <= 0f
                ? 120f
                : DefenseControllerSettings.FarDefenderDistanceThreshold;
        }

        private static int GetMaxReassertionsPerAssignment()
        {
            return DefenseAssignmentSettings.MaxReassertionsPerAssignment < 0
                ? 0
                : DefenseAssignmentSettings.MaxReassertionsPerAssignment;
        }

        private static Settlement FindSettlementByName(string settlementName)
        {
            var settlements = Settlement.All;
            if (settlements == null || string.IsNullOrWhiteSpace(settlementName))
            {
                return null;
            }

            for (var index = 0; index < settlements.Count; index++)
            {
                var settlement = settlements[index];
                if (settlement == null || settlement.Name == null)
                {
                    continue;
                }

                if (NamesEqual(settlement.Name.ToString(), settlementName))
                {
                    return settlement;
                }
            }

            return null;
        }

        private static Settlement FindSettlementByIdOrName(string settlementId, string settlementName)
        {
            var settlements = Settlement.All;
            if (settlements == null)
            {
                return null;
            }

            for (var index = 0; index < settlements.Count; index++)
            {
                var settlement = settlements[index];
                if (settlement == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(settlementId) && NamesEqual(settlement.StringId, settlementId))
                {
                    return settlement;
                }

                if (settlement.Name != null && NamesEqual(settlement.Name.ToString(), settlementName))
                {
                    return settlement;
                }
            }

            return null;
        }

        private static MobileParty FindPartyByName(string partyName)
        {
            var parties = MobileParty.All;
            if (parties == null || string.IsNullOrWhiteSpace(partyName))
            {
                return null;
            }

            for (var index = 0; index < parties.Count; index++)
            {
                var party = parties[index];
                if (party == null || party.Name == null)
                {
                    continue;
                }

                if (NamesEqual(party.Name.ToString(), partyName))
                {
                    return party;
                }
            }

            return null;
        }

        private static MobileParty FindPartyByIdOrName(string partyId, string partyName)
        {
            var parties = MobileParty.All;
            if (parties == null)
            {
                return null;
            }

            for (var index = 0; index < parties.Count; index++)
            {
                var party = parties[index];
                if (party == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(partyId) && NamesEqual(GetPartyId(party), partyId))
                {
                    return party;
                }

                if (!string.IsNullOrWhiteSpace(partyName) && party.Name != null && NamesEqual(party.Name.ToString(), partyName))
                {
                    return party;
                }
            }

            return null;
        }

        private static CsmDefenseAssignmentReport CreateAssignmentReport(
            int observationTick,
            CsmDefenseAssignment assignment,
            string status,
            bool commandApplied,
            string reason)
        {
            return new CsmDefenseAssignmentReport(
                observationTick,
                assignment == null ? "unknown" : assignment.SettlementName,
                assignment == null ? "none" : assignment.PartyName,
                status,
                commandApplied,
                reason);
        }

        private static DirectDefenseCommandReport CreateReport(
            int observationTick,
            string settlementName,
            string candidateName,
            bool commandApplied,
            string reason)
        {
            return CreateReport(
                observationTick,
                settlementName,
                "unknown",
                "UrgentDefense",
                candidateName,
                PartyObservationCategory.Unknown,
                false,
                commandApplied,
                reason);
        }

        private static DirectDefenseCommandReport CreateReport(
            int observationTick,
            string settlementName,
            string ownerKingdomName,
            string commandType,
            string candidateName,
            PartyObservationCategory candidateCategory,
            bool isAllowed,
            bool commandApplied,
            string reason)
        {
            return new DirectDefenseCommandReport(
                observationTick,
                string.IsNullOrWhiteSpace(settlementName) ? "unknown" : settlementName,
                string.IsNullOrWhiteSpace(ownerKingdomName) ? "unknown" : ownerKingdomName,
                string.IsNullOrWhiteSpace(commandType) ? "UrgentDefense" : commandType,
                string.IsNullOrWhiteSpace(candidateName) ? "none" : candidateName,
                candidateCategory,
                isAllowed,
                commandApplied,
                reason);
        }

        private static string GetPartyName(MobileParty party)
        {
            if (party == null || party.Name == null)
            {
                return "unknown";
            }

            return party.Name.ToString();
        }

        private static string GetPartyId(MobileParty party)
        {
            return party == null || party.StringId == null ? string.Empty : party.StringId;
        }

        private static string GetSettlementId(Settlement settlement)
        {
            return settlement == null || settlement.StringId == null ? string.Empty : settlement.StringId;
        }

        private static string GetSettlementName(Settlement settlement)
        {
            if (settlement == null || settlement.Name == null)
            {
                return "unknown";
            }

            return settlement.Name.ToString();
        }

        private static string GetFactionName(IFaction faction)
        {
            return faction?.Name == null ? "unknown" : faction.Name.ToString();
        }

        private static bool NamesEqual(string left, string right)
        {
            return string.Equals(
                left == null ? string.Empty : left.Trim(),
                right == null ? string.Empty : right.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private static int Max(int left, int right)
        {
            return left > right ? left : right;
        }

        private static float Max(float left, float right)
        {
            return left > right ? left : right;
        }

        private class DefenseReinforcementContext
        {
            public int Tick { get; set; }

            public string SettlementName { get; set; }

            public string CoverageStatus { get; set; }

            public float CoverageRatio { get; set; }

            public float RequiredThreatStrength { get; set; }

            public float EnemySiegeThreatStrength { get; set; }

            public float GarrisonDefenseStrength { get; set; }

            public int ExplicitDefenderCount { get; set; }

            public float CurrentDefenseStrength { get; set; }

            public float ActiveAssignedDefenderStrength { get; set; }

            public float EffectiveAssignedDefenderStrength { get; set; }

            public int EffectiveFieldDefenders { get; set; }

            public int RequiredBaselineFieldDefenders { get; set; }

            public float EffectiveDefenseStrength { get; set; }

            public float DesiredEffectiveDefenseStrength { get; set; }

            public float DefenseStrengthDeficit { get; set; }

            public float EstimatedCoverageAfterAssignments { get; set; }

            public float TargetCoverageRatio { get; set; }

            public bool AdditionalDefenseNeeded { get; set; }

            public bool ScarceLocalRelief { get; set; }

            public int ActiveDefenseAssignments { get; set; }

            public int NearbyUncommittedCandidates { get; set; }
        }

        private class CriticalDefenseReactionEvaluation
        {
            public bool StableSignalRequired { get; set; }

            public bool ImmediateReactionAllowed { get; set; }

            public string Reason { get; set; }
        }

        private class LowSiegeEarlyReinforcementEvaluation
        {
            public bool Allowed { get; set; }

            public string Reason { get; set; }
        }

        private class DefenseAuthorityReleaseDecision
        {
            public int Tick { get; set; }

            public string PartyName { get; set; }

            public string SettlementName { get; set; }

            public string AssignmentStatus { get; set; }

            public string RequestedCloseReason { get; set; }

            public bool SettlementStillUnderSiege { get; set; }

            public bool PartyValid { get; set; }

            public bool PartyInArmy { get; set; }

            public bool PartyInBattle { get; set; }

            public string CurrentTarget { get; set; }

            public string CurrentBehavior { get; set; }

            public bool HardLockEnabled { get; set; }

            public bool ReleaseAllowed { get; set; }

            public string FinalReason { get; set; }
        }

        private class DefenseAssignmentMovementSnapshot
        {
            public int Tick { get; set; }

            public string SettlementName { get; set; }

            public string PartyName { get; set; }

            public string CurrentTarget { get; set; }

            public string ExpectedTarget { get; set; }

            public string CurrentBehavior { get; set; }

            public float DistanceToTarget { get; set; }

            public float PreviousDistanceToTarget { get; set; }

            public float DistanceDelta { get; set; }

            public bool TargetOverwritten { get; set; }

            public bool MovingTowardTarget { get; set; }

            public bool Reasserted { get; set; }

            public int StalledTicks { get; set; }
        }

        private class DefenseAssignmentEffectivenessSnapshot
        {
            public int Tick { get; set; }

            public string SettlementName { get; set; }

            public string PartyName { get; set; }

            public bool IsEffective { get; set; }

            public bool TargetOverwritten { get; set; }

            public bool MovingTowardTarget { get; set; }

            public int StalledTicks { get; set; }

            public int IneffectiveTicks { get; set; }

            public float DistanceToTarget { get; set; }

            public float EffectiveStrengthContribution { get; set; }
        }

        private class DefenseAssignmentCapEvaluation
        {
            public int Tick { get; set; }

            public string SettlementName { get; set; }

            public string KingdomName { get; set; }

            public int ActiveGlobalAssignments { get; set; }

            public int ActiveKingdomAssignments { get; set; }

            public int ActiveSiegeCount { get; set; }

            public int EffectiveGlobalCap { get; set; }

            public int KingdomCap { get; set; }

            public int SettlementCap { get; set; }

            public bool Blocked { get; set; }

            public string Reason { get; set; }
        }

        private class DefenseReplacementCandidate
        {
            public string OldPartyName { get; set; }

            public string FailedPartyName { get; set; }

            public int IneffectiveTicks { get; set; }

            public string Reason { get; set; }
        }
    }

    public struct DirectDefenseCommandReport
    {
        public DirectDefenseCommandReport(
            int observationTick,
            string settlementName,
            string ownerKingdomName,
            string commandType,
            string candidateName,
            PartyObservationCategory candidateCategory,
            bool isAllowed,
            bool commandApplied,
            string reason)
        {
            ObservationTick = observationTick;
            SettlementName = settlementName;
            OwnerKingdomName = ownerKingdomName;
            CommandType = commandType;
            CandidateName = candidateName;
            CandidateCategory = candidateCategory;
            IsAllowed = isAllowed;
            CommandApplied = commandApplied;
            Reason = reason;
        }

        public int ObservationTick { get; private set; }

        public string SettlementName { get; private set; }

        public string OwnerKingdomName { get; private set; }

        public string CommandType { get; private set; }

        public string CandidateName { get; private set; }

        public PartyObservationCategory CandidateCategory { get; private set; }

        public bool IsAllowed { get; private set; }

        public bool CommandApplied { get; private set; }

        public string Reason { get; private set; }

        public DefenseCommandReport ToDefenseCommandReport()
        {
            return new DefenseCommandReport(
                SettlementName,
                OwnerKingdomName,
                CommandType,
                CandidateName,
                CandidateCategory,
                IsAllowed,
                CommandApplied,
                CommandApplied ? "Urgent defense command executed" : Reason);
        }
    }
}
