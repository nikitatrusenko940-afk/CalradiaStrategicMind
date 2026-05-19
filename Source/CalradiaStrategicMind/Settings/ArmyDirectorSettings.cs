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
    }
}
