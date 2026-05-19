namespace CalradiaStrategicMind.Strategic
{
    public class CsmAssignmentConflictReport
    {
        public bool IsBlocked { get; set; }
        public string Reason { get; set; }
        public string BlockingSystem { get; set; }
        public string BlockingAssignmentTarget { get; set; }
        public bool HasActiveArmyAssignment { get; set; }
        public bool HasActiveDefenseAssignment { get; set; }
        public bool IsArmyLeaderWithActiveMission { get; set; }
        public bool IsBesiegingAssignedArmyTarget { get; set; }
        public bool IsAssignedDefender { get; set; }
        public bool IsPlayerParty { get; set; }
        public bool IsInBattle { get; set; }
        public bool IsAlreadyBesiegingDifferentTarget { get; set; }
        public bool IsAlreadyDefendingDifferentSettlement { get; set; }

        public static CsmAssignmentConflictReport Allowed()
        {
            return new CsmAssignmentConflictReport
            {
                IsBlocked = false,
                Reason = "No assignment conflict detected",
                BlockingSystem = "None",
                BlockingAssignmentTarget = "none"
            };
        }
    }
}
