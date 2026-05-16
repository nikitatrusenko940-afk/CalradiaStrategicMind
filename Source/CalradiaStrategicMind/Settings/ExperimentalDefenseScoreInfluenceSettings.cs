namespace CalradiaStrategicMind.Settings
{
    public static class ExperimentalDefenseScoreInfluenceSettings
    {
        public static bool EnableExperimentalDefenseScoreInfluence = false;
        public static bool RequireSettlementNameFilter = true;
        public static string SettlementNameFilter = "";
        public static float MaxScoreBoost = 10f;
        public static float MinimumHypotheticalScore = 70f;
        public static int MaxInfluenceAgeTicks = 3;
    }
}
