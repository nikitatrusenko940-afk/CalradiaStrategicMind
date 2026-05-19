namespace CalradiaStrategicMind.Settings
{
    public static class ArmyDirectorSettings
    {
        public static bool EnableArmyDirector = true;
        public static bool EnableArmyDirectorLogs = true;

        public static bool AllowExistingArmyControl = true;
        public static bool AllowOffensiveArmyFormation = true;
        public static bool AllowBadSiegeRedirect = true;
        public static bool AllowExistingArmyDefenseRedirect = true;
        public static bool AllowExistingArmySiegeSupport = true;
        public static bool AllowWeakArmyRelease = true;

        public static bool AllowDefensiveArmyFormation = false;
        public static bool AllowInterceptionArmyFormation = false;
        public static bool AllowFrontGuardArmyFormation = false;
        public static bool AllowSiegeSupportArmyFormation = false;
        public static bool AllowArmyRecruitingBehavior = false;

        public static int MaxNewArmyCommandsPerTick = 2;
        public static int MaxNewArmyCommandsPerKingdomPerTick = 1;
        public static int ArmyCommandCooldownTicks = 4;

        public static int MaxArmyAssignmentAgeTicks = 16;
        public static int ReassertArmyCommandEveryTicks = 2;
        public static int MaxArmyReassertionsPerAssignment = 5;

        public static bool EnableArmyMissionTracking = true;

        public static int MissionProgressCheckEveryTicks = 2;
        public static int MissionStalledTicksBeforeReassert = 4;
        public static int MissionStalledTicksBeforeRelease = 8;

        public static float MissionProgressMinimumDistanceDelta = 3f;
        public static float MissionArrivedDistance = 8f;
        public static float MissionWrongTargetToleranceDistance = 20f;

        public static int MaxObjectiveSyncAttemptsPerAssignment = 4;
        public static int MaxMissionRepathAttemptsPerAssignment = 4;
        public static int RedirectGraceTicks = 4;
        public static int MinimumTicksBetweenArmyRedirects = 8;
        public static int MaxRedirectsPerAssignment = 2;

        public static float MinimumArmyStrengthForAttack = 700f;
        public static float MinimumArmyStrengthForDefenseRedirect = 500f;
        public static float MinimumArmyStrengthForSiegeSupport = 500f;

        public static float MaxAttackTargetDistance = 120f;
        public static float MaxDefenseRedirectDistance = 160f;
        public static float MaxSiegeSupportDistance = 100f;

        public static float BadSiegeRequiredStrengthRatio = 0.75f;
        public static float GoodAttackRequiredStrengthRatio = 1.35f;
        public static float WeakArmyStrengthThreshold = 350f;
        public static float LowCohesionThreshold = 25f;

        public static int MaxOffensiveFormationParties = 6;
        public static int MinOffensiveFormationParties = 3;

        public static bool EnableArmyTargetScoring = true;
        public static bool PreferCastlesForAlpha = true;

        public static float MinimumAttackTargetScore = 50f;

        public static float CastleTargetBonus = 20f;
        public static float TownTargetBonus = 5f;
        public static float TownTooStrongPenalty = 40f;

        public static float DistancePenaltyPerMapUnit = 0.35f;
        public static float TargetDefensePenaltyMultiplier = 0.04f;
        public static float StrengthRatioScoreMultiplier = 45f;
        public static float NearbyEnemyArmyPenaltyMultiplier = 0.06f;
        public static float NearbyFriendlySupportBonusMultiplier = 0.03f;
        public static float FrontlineTargetBonus = 25f;
        public static float AlreadyBesiegedPenalty = 100f;
        public static float ActiveDefenseTargetPenalty = 100f;
        public static float DeepEnemyTerritoryPenalty = 35f;

        public static float MaxNearbyEnemyArmyScanDistance = 80f;
        public static float MaxNearbyFriendlySupportScanDistance = 60f;
        public static float MaxFrontlineDistanceFromFriendlyFortification = 90f;

        public static float ExtendedFrontlineAttackTargetDistance = 150f;
        public static float ExtendedFrontlineRequiredStrengthRatio = 1.70f;
        public static float ExtendedFrontlineMinimumScore = 120f;

        public static bool AllowExtendedFrontlineTargets = true;
    }
}
