namespace CalradiaStrategicMind.Settings
{
    public static class DefenseAssignmentSettings
    {
        public static bool EnableDefenseAssignments = true;
        public static int MaxAssignmentAgeTicks = 12;
        public static int ReassertCommandEveryTicks = 2;
        public static int MaxReassertionsPerAssignment = 4;
        public static bool EnableAssignmentLogs = true;
        public static int MaxDefenseAssignmentAgeTicks = 12;
        public static int DefenseAssignmentProgressCheckEveryTicks = 2;
        public static float DefenseAssignmentProgressMinimumDistanceDelta = 3f;
        public static int DefenseMovementReassertCooldownTicks = 1;
        public static int DefenseMovementStallTicks = 3;
        public static float DefenseMovementProgressTolerance = 2.0f;
        public static float DefenseAssignmentArrivalDistance = 8.0f;
        public static int DefenseCoverageEnoughStableTicksBeforeDeescalation = 2;
        public static float MinimumEffectiveDefenderRatioForDeescalation = 1.0f;
        public static int DefenseAssignmentIneffectiveTicksBeforeReplacement = 3;
        public static bool AllowReplacementForIneffectiveDefenseAssignments = true;
        public static float DefenseAssignmentArrivedDistance = 10f;
        public static int DefenseAssignmentGraceTicks = 4;
        public static int MinimumTicksBeforeDefenseProgressExpiry = 6;
        public static bool EnableDefenseAssignmentAuthorityHardLock = true;
        public static int DefenseAuthorityHardLockMinimumTicks = 6;
        public static int DefenseAuthorityStableThreatTicksBeforeRelease = 4;
        public static int DefenseAuthorityMaxInvalidPartyTicks = 3;
        public static int DefenseAuthorityMaxInArmyTicksBeforeRelease = 2;
        public static int DefenseAuthorityMaxInBattleTicksBeforeRelease = 4;
    }
}
