using System;
using System.Collections.Generic;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmDefenseAssignmentRegistry
    {
        private readonly Dictionary<string, CsmDefenseAssignment> _activeAssignments;

        public CsmDefenseAssignmentRegistry()
        {
            _activeAssignments = new Dictionary<string, CsmDefenseAssignment>();
        }

        public CsmDefenseAssignment CreateOrUpdate(
            string settlementId,
            string settlementName,
            string partyId,
            string partyName,
            int observationTick,
            string reason)
        {
            var key = CreateKey(settlementId, settlementName, partyId, partyName);
            CsmDefenseAssignment assignment;
            if (_activeAssignments.TryGetValue(key, out assignment))
            {
                assignment.LastCommandTick = observationTick;
                assignment.CommandCount++;
                assignment.Reason = reason;
                assignment.Status = "Reasserted";
                return assignment;
            }

            assignment = new CsmDefenseAssignment(
                settlementId,
                settlementName,
                partyId,
                partyName,
                observationTick,
                observationTick,
                1,
                0,
                reason,
                "Created");
            _activeAssignments[key] = assignment;
            return assignment;
        }

        public List<CsmDefenseAssignment> GetActiveAssignmentsForSettlement(string settlementId, string settlementName)
        {
            var assignments = new List<CsmDefenseAssignment>();
            foreach (var assignment in _activeAssignments.Values)
            {
                if (MatchesSettlement(assignment, settlementId, settlementName))
                {
                    assignments.Add(assignment);
                }
            }

            return assignments;
        }

        public bool HasActiveAssignmentForSettlement(string settlementId, string settlementName)
        {
            foreach (var assignment in _activeAssignments.Values)
            {
                if (MatchesSettlement(assignment, settlementId, settlementName))
                {
                    return true;
                }
            }

            return false;
        }

        public void MarkReasserted(CsmDefenseAssignment assignment, int observationTick, string reason)
        {
            if (assignment == null)
            {
                return;
            }

            assignment.LastCommandTick = observationTick;
            assignment.CommandCount++;
            assignment.ReassertionCount++;
            assignment.Reason = reason;
            assignment.Status = "Reasserted";
        }

        public void Close(CsmDefenseAssignment assignment, string status, string reason)
        {
            if (assignment == null)
            {
                return;
            }

            assignment.Status = status;
            assignment.Reason = reason;
            _activeAssignments.Remove(CreateKey(assignment.SettlementId, assignment.SettlementName, assignment.PartyId, assignment.PartyName));
        }

        private static bool MatchesSettlement(CsmDefenseAssignment assignment, string settlementId, string settlementName)
        {
            if (assignment == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(assignment.SettlementId) && !string.IsNullOrWhiteSpace(settlementId))
            {
                return NamesEqual(assignment.SettlementId, settlementId);
            }

            return NamesEqual(assignment.SettlementName, settlementName);
        }

        private static string CreateKey(string settlementId, string settlementName, string partyId, string partyName)
        {
            return Normalize(string.IsNullOrWhiteSpace(settlementId) ? settlementName : settlementId)
                + "|"
                + Normalize(string.IsNullOrWhiteSpace(partyId) ? partyName : partyId);
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

    public class CsmDefenseAssignment
    {
        public CsmDefenseAssignment(
            string settlementId,
            string settlementName,
            string partyId,
            string partyName,
            int commandStartTick,
            int lastCommandTick,
            int commandCount,
            int reassertionCount,
            string reason,
            string status)
        {
            SettlementId = settlementId;
            SettlementName = settlementName;
            PartyId = partyId;
            PartyName = partyName;
            CommandStartTick = commandStartTick;
            LastCommandTick = lastCommandTick;
            CommandCount = commandCount;
            ReassertionCount = reassertionCount;
            Reason = reason;
            Status = status;
        }

        public string SettlementId { get; private set; }

        public string SettlementName { get; private set; }

        public string PartyId { get; private set; }

        public string PartyName { get; private set; }

        public int CommandStartTick { get; private set; }

        public int LastCommandTick { get; set; }

        public int CommandCount { get; set; }

        public int ReassertionCount { get; set; }

        public string Reason { get; set; }

        public string Status { get; set; }
    }

    public struct CsmDefenseAssignmentReport
    {
        public CsmDefenseAssignmentReport(
            int observationTick,
            string settlementName,
            string partyName,
            string status,
            bool commandApplied,
            string reason)
        {
            ObservationTick = observationTick;
            SettlementName = settlementName;
            PartyName = partyName;
            Status = status;
            CommandApplied = commandApplied;
            Reason = reason;
        }

        public int ObservationTick { get; private set; }

        public string SettlementName { get; private set; }

        public string PartyName { get; private set; }

        public string Status { get; private set; }

        public bool CommandApplied { get; private set; }

        public string Reason { get; private set; }
    }
}
