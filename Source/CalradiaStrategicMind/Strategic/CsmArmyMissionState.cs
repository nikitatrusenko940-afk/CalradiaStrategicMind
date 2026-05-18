namespace CalradiaStrategicMind.Strategic
{
    public enum CsmArmyMissionStatus
    {
        Created,
        Gathering,
        MovingToTarget,
        NearTarget,
        BesiegingAssignedTarget,
        AssaultingAssignedTarget,
        OperatingOnAssignedTarget,
        ActiveSiegeRedirectBlocked,
        ObjectiveMismatch,
        Stalled,
        Unsafe,
        Completed,
        Invalid,
        Expired,
        ReleasedForRecovery
    }

    public class CsmArmyMissionState
    {
        public string AssignmentId { get; set; }
        public string ArmyId { get; set; }
        public string ArmyName { get; set; }
        public string KingdomName { get; set; }
        public string ObjectiveType { get; set; }
        public string TargetSettlementId { get; set; }
        public string TargetSettlementName { get; set; }
        public float LastDistanceToTarget { get; set; }
        public float CurrentDistanceToTarget { get; set; }
        public int LastProgressTick { get; set; }
        public int StalledTickCount { get; set; }
        public int SyncAttemptCount { get; set; }
        public int RepathAttemptCount { get; set; }
        public int LastRedirectTick { get; set; }
        public int RedirectCount { get; set; }
        public string PreviousTargetSettlementName { get; set; }
        public bool IsRedirectGraceActive { get; set; }
        public CsmArmyMissionStatus LastState { get; set; }
        public CsmArmyMissionStatus CurrentState { get; set; }
        public string Reason { get; set; }
    }
}
