namespace CalradiaStrategicMind.Settings
{
    public static class DefenseControllerSettings
    {
        public static bool EnableRealDefenseController = true;
        public static bool EnableDefenseControllerV2 = true;
        public static bool EnableCriticalDefenseTier = true;
        public static bool EnableCoverageAwareDefenseExecution = true;
        public static bool EnableDefenseCandidateScoringV2 = true;
        public static float CriticalDefenseCoverageRatio = 0.65f;
        public static float LowDefenseCoverageRatio = 1.25f;
        public static float MinimumCandidateCoverageImprovement = 0.15f;
        public static int MinimumStableUrgentTicksForDirectCommand = 2;
    }
}
