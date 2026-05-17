using System;
using System.Collections.Generic;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmArmyAssignmentRegistry
    {
        private readonly Dictionary<string, CsmArmyAssignment> _assignmentsByArmy;
        private readonly Dictionary<string, CsmArmyAssignment> _assignmentsByTarget;

        public CsmArmyAssignmentRegistry()
        {
            _assignmentsByArmy = new Dictionary<string, CsmArmyAssignment>();
            _assignmentsByTarget = new Dictionary<string, CsmArmyAssignment>();
        }

        public bool TryCreate(
            string armyId,
            string armyName,
            string leaderPartyId,
            string leaderPartyName,
            string kingdomName,
            string objectiveType,
            string targetSettlementId,
            string targetSettlementName,
            int observationTick,
            string reason,
            string assignmentKind,
            out CsmArmyAssignment assignment)
        {
            var armyKey = Normalize(armyId);
            if (_assignmentsByArmy.TryGetValue(armyKey, out assignment))
            {
                return false;
            }

            var targetKey = CreateTargetKey(objectiveType, targetSettlementId, targetSettlementName);
            if (_assignmentsByTarget.TryGetValue(targetKey, out assignment))
            {
                return false;
            }

            assignment = new CsmArmyAssignment(
                armyId,
                armyName,
                leaderPartyId,
                leaderPartyName,
                kingdomName,
                objectiveType,
                targetSettlementId,
                targetSettlementName,
                observationTick,
                observationTick,
                1,
                0,
                "Created",
                reason,
                assignmentKind);
            _assignmentsByArmy[armyKey] = assignment;
            _assignmentsByTarget[targetKey] = assignment;
            return true;
        }

        public CsmArmyAssignment GetActiveAssignmentForArmy(string armyId)
        {
            CsmArmyAssignment assignment;
            return _assignmentsByArmy.TryGetValue(Normalize(armyId), out assignment) ? assignment : null;
        }

        public CsmArmyAssignment GetActiveAssignmentForTarget(string objectiveType, string targetSettlementId, string targetSettlementName)
        {
            CsmArmyAssignment assignment;
            return _assignmentsByTarget.TryGetValue(CreateTargetKey(objectiveType, targetSettlementId, targetSettlementName), out assignment) ? assignment : null;
        }

        public bool HasActiveAssignmentForArmy(string armyId)
        {
            return _assignmentsByArmy.ContainsKey(Normalize(armyId));
        }

        public void Close(CsmArmyAssignment assignment, string status, string reason)
        {
            if (assignment == null)
            {
                return;
            }

            assignment.Status = status;
            assignment.Reason = reason;
            _assignmentsByArmy.Remove(Normalize(assignment.ArmyId));
            _assignmentsByTarget.Remove(CreateTargetKey(assignment.ObjectiveType, assignment.TargetSettlementId, assignment.TargetSettlementName));
        }

        public void MarkReasserted(CsmArmyAssignment assignment, int observationTick, string reason)
        {
            if (assignment == null)
            {
                return;
            }

            assignment.LastCommandTick = observationTick;
            assignment.CommandCount++;
            assignment.ReassertionCount++;
            assignment.Status = "Reasserted";
            assignment.Reason = reason;
        }

        private static string CreateTargetKey(string objectiveType, string targetSettlementId, string targetSettlementName)
        {
            return Normalize(objectiveType) + "|" + Normalize(string.IsNullOrWhiteSpace(targetSettlementId) ? targetSettlementName : targetSettlementId);
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim().ToLowerInvariant();
        }
    }

    public class CsmArmyAssignment
    {
        public CsmArmyAssignment(
            string armyId,
            string armyName,
            string leaderPartyId,
            string leaderPartyName,
            string kingdomName,
            string objectiveType,
            string targetSettlementId,
            string targetSettlementName,
            int startTick,
            int lastCommandTick,
            int commandCount,
            int reassertionCount,
            string status,
            string reason,
            string assignmentKind)
        {
            ArmyId = armyId;
            ArmyName = armyName;
            LeaderPartyId = leaderPartyId;
            LeaderPartyName = leaderPartyName;
            KingdomName = kingdomName;
            ObjectiveType = objectiveType;
            TargetSettlementId = targetSettlementId;
            TargetSettlementName = targetSettlementName;
            StartTick = startTick;
            LastCommandTick = lastCommandTick;
            CommandCount = commandCount;
            ReassertionCount = reassertionCount;
            Status = status;
            Reason = reason;
            AssignmentKind = assignmentKind;
        }

        public string AssignmentKind { get; set; }
        public string ArmyId { get; private set; }
        public string ArmyName { get; private set; }
        public string LeaderPartyId { get; private set; }
        public string LeaderPartyName { get; private set; }
        public string KingdomName { get; private set; }
        public string ObjectiveType { get; set; }
        public string TargetSettlementId { get; set; }
        public string TargetSettlementName { get; set; }
        public int StartTick { get; private set; }
        public int LastCommandTick { get; set; }
        public int CommandCount { get; set; }
        public int ReassertionCount { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
    }
}
