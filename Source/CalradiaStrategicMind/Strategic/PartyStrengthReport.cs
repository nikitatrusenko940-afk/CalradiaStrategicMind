namespace CalradiaStrategicMind.Strategic
{
    public struct PartyStrengthReport
    {
        public static readonly PartyStrengthReport Empty = new PartyStrengthReport(0f, 0f, 0f, 0f, 0, 0);

        public PartyStrengthReport(
            float totalStrength,
            float healthyTroopStrength,
            float woundedTroopStrength,
            float leaderStrength,
            int troopCount,
            int woundedCount)
        {
            TotalStrength = totalStrength;
            HealthyTroopStrength = healthyTroopStrength;
            WoundedTroopStrength = woundedTroopStrength;
            LeaderStrength = leaderStrength;
            TroopCount = troopCount;
            WoundedCount = woundedCount;
        }

        public float TotalStrength { get; private set; }

        public float HealthyTroopStrength { get; private set; }

        public float WoundedTroopStrength { get; private set; }

        public float LeaderStrength { get; private set; }

        public int TroopCount { get; private set; }

        public int WoundedCount { get; private set; }
    }
}
