using System;
using System.Collections.Generic;
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
        private readonly Dictionary<string, int> _settlementCommandCounts;
        private readonly HashSet<string> _commandedParties;
        private readonly List<CsmDefenseAssignmentReport> _pendingAssignmentReports;
        private int _currentTick;
        private int _commandsToday;

        public DirectDefenseCommandController()
        {
            _assignmentRegistry = new CsmDefenseAssignmentRegistry();
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

        public DirectDefenseCommandReport Execute(
            DefenseEvaluationSnapshot snapshot,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecision dryRunDecision,
            DryRunDefenseDecisionStabilityReport stabilityReport,
            DefenseControllerSafetyReport safetyReport,
            int observationTick)
        {
            return SafeExecutor.Run(
                "Execute direct defense command",
                () => ExecuteCore(snapshot, actionPlan, dryRunDecision, stabilityReport, safetyReport, observationTick),
                new DirectDefenseCommandReport(observationTick, "unknown", "none", false, "Direct defense command failed"));
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
                var invalidReason = GetInvalidAssignmentReason(snapshot, assignment, party, observationTick);
                if (!string.IsNullOrWhiteSpace(invalidReason))
                {
                    var status = GetClosedAssignmentStatus(assignment, observationTick, invalidReason);
                    _assignmentRegistry.Close(assignment, status, invalidReason);
                    reports.Add(CreateAssignmentReport(observationTick, assignment, status, false, invalidReason));
                    continue;
                }

                if (!ShouldReassertAssignment(assignment, observationTick))
                {
                    continue;
                }

                party.SetMoveDefendSettlement(settlement, false, party.NavigationCapability);
                _assignmentRegistry.MarkReasserted(assignment, observationTick, "CSM defense assignment reasserted");
                reports.Add(CreateAssignmentReport(observationTick, assignment, "Reasserted", true, "CSM defense assignment reasserted"));
            }

            return reports;
        }

        private DirectDefenseCommandReport ExecuteCore(
            DefenseEvaluationSnapshot snapshot,
            DefenseActionPlan actionPlan,
            DryRunDefenseDecision dryRunDecision,
            DryRunDefenseDecisionStabilityReport stabilityReport,
            DefenseControllerSafetyReport safetyReport,
            int observationTick)
        {
            ResetDailyStateIfNeeded(observationTick);

            var settlementName = snapshot.ThreatReport.SettlementName;
            var candidateName = actionPlan.PrimaryCandidateName;
            if (!DirectDefenseCommandSettings.EnableDirectDefenseCommand)
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

            if (!IsLowOrCriticalCoverage(snapshot.CoverageReport))
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Coverage status is not low or critical");
            }

            if (!dryRunDecision.WouldAct || !safetyReport.DryRunWouldAct)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Dry-run would-act signal required");
            }

            if (DirectDefenseCommandSettings.RequireStableWouldActSignal
                && (!stabilityReport.HasStableWouldActSignal || !safetyReport.HasStableWouldActSignal))
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Stable would-act signal required");
            }

            if (!NamesEqual(dryRunDecision.Action, "RequestUrgentDefense"))
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

            var activeAssignment = GetValidActiveAssignmentForSettlement(snapshot, settlement, observationTick);
            if (activeAssignment != null)
            {
                return CreateReport(observationTick, settlementName, activeAssignment.PartyName, false, "Active CSM defense assignment already exists");
            }

            var candidate = FindPartyByName(candidateName);
            if (candidate == null)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Primary candidate not found");
            }

            var commandParty = GetCommandParty(candidate);
            if (commandParty == null)
            {
                return CreateReport(observationTick, settlementName, candidateName, false, "Command party not found");
            }

            var commandPartyName = GetPartyName(commandParty);
            if (_commandedParties.Contains(Normalize(commandPartyName)))
            {
                return CreateReport(observationTick, settlementName, commandPartyName, false, "Party already received direct command today");
            }

            if (!CanCommandParty(commandParty))
            {
                return CreateReport(observationTick, settlementName, commandPartyName, false, "Party cannot receive direct defense command");
            }

            commandParty.SetMoveDefendSettlement(settlement, false, commandParty.NavigationCapability);

            _commandsToday++;
            _commandedParties.Add(Normalize(commandPartyName));
            IncrementSettlementCommandCount(settlementName);
            if (DefenseAssignmentSettings.EnableDefenseAssignments)
            {
                var assignment = _assignmentRegistry.CreateOrUpdate(
                    GetSettlementId(settlement),
                    settlementName,
                    GetPartyId(commandParty),
                    commandPartyName,
                    observationTick,
                    "Direct defense command created CSM assignment");
                _pendingAssignmentReports.Add(CreateAssignmentReport(observationTick, assignment, assignment.Status, true, "Direct defense command created CSM assignment"));
            }

            return CreateReport(observationTick, settlementName, commandPartyName, true, "Direct defense command applied");
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
                var invalidReason = GetInvalidAssignmentReason(snapshot, assignment, party, observationTick);
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

            if (!IsAssignmentThreatStillValid(snapshot))
            {
                return "Settlement no longer has urgent defense threat";
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

        private static bool ShouldReassertAssignment(CsmDefenseAssignment assignment, int observationTick)
        {
            if (assignment.ReassertionCount >= GetMaxReassertionsPerAssignment())
            {
                return false;
            }

            return observationTick - assignment.LastCommandTick >= GetReassertCommandEveryTicks();
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
            return DefenseAssignmentSettings.MaxAssignmentAgeTicks < 0
                ? 0
                : DefenseAssignmentSettings.MaxAssignmentAgeTicks;
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
            return new DirectDefenseCommandReport(
                observationTick,
                string.IsNullOrWhiteSpace(settlementName) ? "unknown" : settlementName,
                string.IsNullOrWhiteSpace(candidateName) ? "none" : candidateName,
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
            string candidateName,
            bool commandApplied,
            string reason)
        {
            ObservationTick = observationTick;
            SettlementName = settlementName;
            CandidateName = candidateName;
            CommandApplied = commandApplied;
            Reason = reason;
        }

        public int ObservationTick { get; private set; }

        public string SettlementName { get; private set; }

        public string CandidateName { get; private set; }

        public bool CommandApplied { get; private set; }

        public string Reason { get; private set; }
    }
}
