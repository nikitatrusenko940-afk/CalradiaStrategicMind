namespace CalradiaStrategicMind.Settings
{
    public static class LordPartyRecoverySettings
    {
        public static bool EnableLordPartyRecoveryController = true;
        public static float MinimumHealthyPartyRatioForOperations = 0.65f;
        public static float TargetHealthyPartyRatioForRecovery = 0.95f;
        public static float CriticalDefenseOverrideRatio = 0.35f;
        public static int RecoveryAssignmentCooldownTicks = 6;
        public static int MaxRecoveryDistanceToFriendlySettlement = 120;
        public static bool RecoveryBlocksArmyDirectorSelection = true;
        public static bool RecoveryBlocksDefenseCandidateSelection = false;
        public static bool EnableRecoveryRecruitingLoop = true;
        public static int RecoveryVillagesPerLoop = 4;
        public static int MinRecoveryVillagesPerLoop = 3;
        public static int MaxRecoveryVillageSearchDistanceFromHub = 70;
        public static int MaxRecoveryVillageDistanceFromParty = 90;
        public static int RecoveryVillageCommandCooldownTicks = 3;
        public static int RecoveryHubReturnCooldownTicks = 2;
        public static int RecoveryHubVisitHoldTicks = 1;
        public static int RecoveryVillageVisitHoldTicks = 1;
        public static bool PreferTownRecoveryHub = true;
        public static bool AllowCastleRecoveryHubFallback = true;
        public static bool AvoidRecentlyVisitedRecoveryVillages = true;
    }
}
