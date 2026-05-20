namespace CalradiaStrategicMind.Settings
{
    public static class ExperimentalDefenseScoreInfluenceSettings
    {
        public static bool EnableExperimentalDefenseScoreInfluence = true;
        public static bool EnableExperimentalDefenseScoreInfluenceDebugLogs = true;
        public static bool LogNoRecentScoreSimulationReport = false;
        public static bool LogArmyMemberSkip = false;
        public static int MaxExperimentalDebugLogsPerTick = 5;
        public static bool EnableExperimentalDefenseInfluenceApplyThrottle = true;
        public static int ExperimentalDefenseInfluenceLogCooldownTicks = 1;
        public static bool RequireSettlementNameFilter = false;
        public static string SettlementNameFilter = "";
        public static float MaxScoreBoost = 5f;
        public static float MinimumHypotheticalScore = 80f;
        public static int MaxInfluenceAgeTicks = 3;
    }
}
