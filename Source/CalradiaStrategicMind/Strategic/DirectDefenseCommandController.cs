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
        private readonly Dictionary<string, int> _settlementCommandCounts;
        private readonly HashSet<string> _commandedParties;
        private readonly List<CsmDefenseAssignmentReport> _pendingAssignmentReports;
        private int _currentTick;
        private int _commandsToday;
        private int _assignmentCreatedThisTick;
        private int _assignmentCompletedThisTick;
        private int _assignmentExpiredThisTick;
        private int _assignmentInvalidThisTick;
        private int _assignmentProgressExpiredThisTick;

        public DirectDefenseCommandController()
        {
            _assignmentRegistry = new CsmDefenseAssignmentRegistry();
            _candidateScorer = new DefenseCandidateScorer();
            _settlementCommandCounts = new Dictionary<string, int>();
            _commandedParties = new HashSet<string>();
            _pendingAssignmentReports = new List<CsmDefenseAssignmentReport>();
            _currentTick = -1;
        }

        public List<CsmDefenseAssignmentReport> ProcessAssignments(DefenseEvaluationSnapshot snapshot, int observationTick)
        {
            return SafeExecutor.Run(
                "Process CSM defense assignments",
                () => ProcessAssignmentsCore(snapshot, observationTick),
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
                    return new CsmDefenseAssignmentLifecycleSummary(
                        observationTick,
                        _assignmentRegistry.CountActiveAssignments(),
                        _assignmentCreatedThisTick,
                        _assignmentCompletedThisTick,
                        _assignmentExpiredThisTick,
                        _assignmentInvalidThisTick,
                        _assignmentProgressExpiredThisTick,
                        "Defense assignment lifecycle snapshot");
                },
                new CsmDefenseAssignmentLifecycleSummary(
                    observationTick,
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

        private List<CsmDefenseAssignmentReport> ProcessAssignmentsCore(DefenseEvaluationSnapshot snapshot, int observationTick)
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
                var invalidReason = GetInvalidAssignmentReason(snapshot, assignment, party, settlement, observationTick);
                if (!string.IsNullOrWhiteSpace(invalidReason))
                {
                    var status = GetClosedAssignmentStatus(assignment, observationTick, invalidReason);
                    _assignmentRegistry.Close(assignment, status, invalidReason);
                    RecordClosedAssignment(status, false);
                    reports.Add(CreateAssignmentReport(observationTick, assignment, status, false, invalidReason));
                    continue;
                }

                var completionReason = GetCompletedAssignmentReason(snapshot, assignment, party, settlement);
                if (!string.IsNullOrWhiteSpace(completionReason))
                {
                    _assignmentRegistry.Close(assignment, "Completed", completionReason);
                    RecordClosedAssignment("Completed", false);
                    reports.Add(CreateAssignmentReport(observationTick, assignment, "Completed", false, completionReason));
                    continue;
                }

                var expiredReason = GetProgressExpiredReason(assignment, party, settlement, observationTick);
                if (!string.IsNullOrWhiteSpace(expiredReason))
                {
                    _assignmentRegistry.Close(assignment, "Expired", expiredReason);
                    RecordClosedAssignment("Expired", true);
                    reports.Add(CreateAssignmentReport(observationTick, assignment, "Expired", false, expiredReason));
                    continue;
                }

                if (!ShouldReassertAssignment(assignment, party, settlement, observationTick))
                {
                    continue;
                }

                party.SetMoveDefendSettlement(settlement, false, party.NavigationCapability);
                _assignmentRegistry.MarkReasserted(assignment, observationTick, "Defense assignment reasserted because candidate target changed");
                reports.Add(CreateAssignmentReport(observationTick, assignment, "Reasserted", true, "Defense assignment reasserted because candidate target changed"));
            }

            return reports;
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

            if (DirectDefenseCommandSettings.OnlyUrgentDefense && !IsUrgentDefenseAction(actionPlan.RecommendedAction))
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Action is not urgent defense");
            }

            if (!DefenseActionThresholdSettings.AllowUrgentDefenseCommands)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Urgent defense commands disabled");
            }

            if (!IsUrgentDefenseAction(actionPlan.RecommendedAction))
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Recommended action is not UrgentDefense");
            }

            if (!IsCriticalCoverage(snapshot.CoverageReport))
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Coverage is not critical");
            }

            if (actionPlan.DefensePriority < DefenseActionThresholdSettings.MinimumDefensePriorityToAct)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Defense priority is below activation threshold");
            }

            if (actionPlan.PlanConfidence < DefenseActionThresholdSettings.MinimumDefenseConfidenceToAct)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Defense confidence is below activation threshold");
            }

            if (!dryRunDecision.WouldAct || !safetyReport.DryRunWouldAct)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Dry-run would-act signal required");
            }

            if (DefenseActionThresholdSettings.RequireStableDefenseSignal
                && (!IsUrgentDefenseAction(stabilityReport.StableAction)
                    || stabilityReport.ConsecutiveSameActionCount < DefenseActionThresholdSettings.RequiredStableDefenseTicks))
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Stable urgent defense signal required");
            }

            if (!IsUrgentDefenseAction(dryRunDecision.Action))
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Dry-run action is not RequestUrgentDefense");
            }

            if (_commandsToday >= GetMaxCommandsPerDay())
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Daily direct command limit reached");
            }

            if (GetSettlementCommandCount(settlementName) >= GetMaxCommandsPerSettlementPerDay())
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Daily settlement direct command limit reached");
            }

            var settlement = FindSettlementByName(settlementName);
            if (settlement == null)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Settlement not found");
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

            if (_assignmentRegistry.CountActiveAssignments() >= DirectDefenseCommandSettings.MaxActiveDefenseAssignmentsGlobal)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Global active defense assignment limit reached");
            }

            if (_assignmentRegistry.CountActiveAssignmentsForKingdom(snapshot.ThreatReport.OwnerKingdomName) >= DirectDefenseCommandSettings.MaxActiveDefenseAssignmentsPerKingdom)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Kingdom active defense assignment limit reached");
            }

            var activeAssignment = GetValidActiveAssignmentForSettlement(snapshot, settlement, observationTick);
            if (activeAssignment != null)
            {
                return CreateReport(observationTick, settlementName, activeAssignment.PartyName, false, "Active CSM defense assignment already exists");
            }

            DefenseCandidateScore topRejected;
            DefenseCandidateScoringSummary candidateScoringSummary;
            var selectedCandidate = _candidateScorer.SelectBest(settlement, snapshot.CandidateReports, candidateName, _assignmentRegistry, armyDirector == null ? null : armyDirector.AssignmentRegistry, out topRejected, out candidateScoringSummary);
            LogCandidateScore(observationTick, settlementName, selectedCandidate, topRejected, candidateScoringSummary);
            if (selectedCandidate == null)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, topRejected == null ? "Primary candidate not found" : topRejected.Reason);
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

            var partyBlockReason = GetCommandPartyBlockReason(commandParty, settlement, selectedCandidate.Distance, _assignmentRegistry, armyDirector == null ? null : armyDirector.AssignmentRegistry);
            if (!string.IsNullOrWhiteSpace(partyBlockReason))
            {
                return CreateReport(observationTick, settlementName, commandPartyName, false, partyBlockReason);
            }

            var settlementId = GetSettlementId(settlement);
            var commandPartyId = GetPartyId(commandParty);
            if (DefenseAssignmentSettings.EnableDefenseAssignments
                && _assignmentRegistry.HasActiveAssignment(settlementId, settlementName, commandPartyId, commandPartyName))
            {
                return CreateReport(observationTick, settlementName, commandPartyName, false, "Active CSM defense assignment already exists");
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
                }

                _pendingAssignmentReports.Add(CreateAssignmentReport(observationTick, assignment, assignment.Status, true, "Direct defense command created CSM assignment"));
            }

            return CreateReport(observationTick, settlementName, snapshot.ThreatReport.OwnerKingdomName, "UrgentDefense", commandPartyName, commandPartyCategory, true, true, "Direct urgent defense command applied");
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

            if (NamesEqual(status, "Invalid"))
            {
                _assignmentInvalidThisTick++;
            }
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
            _assignmentExpiredThisTick = 0;
            _assignmentInvalidThisTick = 0;
            _assignmentProgressExpiredThisTick = 0;
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
            CsmArmyAssignmentRegistry armyAssignments)
        {
            if (party == null)
            {
                return "Command party not found";
            }

            var conflict = new CsmAssignmentConflictChecker(armyAssignments, defenseAssignments).CheckPartyForNewDefenseCommand(party, settlement);
            if (conflict.IsBlocked)
            {
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

        private static void LogCandidateScore(int tick, string settlementName, DefenseCandidateScore selected, DefenseCandidateScore rejected, DefenseCandidateScoringSummary summary)
        {
            if (selected != null)
            {
                CsmLogger.Info(
                    $"Observed defense candidate score: tick={tick}, settlement='{settlementName}', selectedCandidate='{selected.CandidateName}', score={selected.Score:0.00}, distance={selected.Distance:0.00}, strength={selected.Strength:0.00}, category={selected.CandidateCategory}, reason='{selected.Reason}'");
            }

            if (rejected != null)
            {
                CsmLogger.Info(
                    $"Observed defense candidate rejection: tick={tick}, settlement='{settlementName}', rejectedCandidate='{rejected.CandidateName}', score={rejected.Score:0.00}, distance={rejected.Distance:0.00}, strength={rejected.Strength:0.00}, reason='{rejected.Reason}'");
            }

            CsmLogger.Info(
                $"Observed defense candidate scoring summary: tick={tick}, settlement='{summary.SettlementName}', evaluatedCandidates={summary.EvaluatedCandidates}, validCandidates={summary.ValidCandidates}, rejectedCandidates={summary.RejectedCandidates}, rejectedByArmyAssignment={summary.RejectedByArmyAssignment}, rejectedByDefenseAssignment={summary.RejectedByDefenseAssignment}, rejectedTooFar={summary.RejectedTooFar}, rejectedTooWeak={summary.RejectedTooWeak}, selectedCandidate='{summary.SelectedCandidate}', reason='{summary.Reason}'");
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

        private static string GetCompletedAssignmentReason(
            DefenseEvaluationSnapshot snapshot,
            CsmDefenseAssignment assignment,
            MobileParty party,
            Settlement settlement)
        {
            if (settlement != null && settlement.SiegeEvent == null)
            {
                return "Settlement no longer under siege";
            }

            if (snapshot.CoverageReport.IsDefenseEnough)
            {
                return "Settlement defense coverage is enough";
            }

            if (!IsAssignmentThreatStillValid(snapshot))
            {
                return "Settlement no longer has urgent defense threat";
            }

            if (party != null && settlement != null)
            {
                if (party.CurrentSettlement == settlement || party.Position.Distance(settlement.Position) <= DefenseAssignmentSettings.DefenseAssignmentArrivedDistance)
                {
                    return "Assigned party arrived at settlement";
                }

                if (party.DefaultBehavior == AiBehavior.DefendSettlement && party.TargetSettlement == settlement)
                {
                    return "Assigned party is defending settlement";
                }
            }

            return null;
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
            return coverageReport.DefenseCoverageRatio <= DefenseActionThresholdSettings.CriticalCoverageRatio;
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
