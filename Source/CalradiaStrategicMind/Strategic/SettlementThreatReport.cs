namespace CalradiaStrategicMind.Strategic
{
    public struct SettlementThreatReport
    {
        public static readonly SettlementThreatReport Empty = new SettlementThreatReport(
            "unknown",
            "unknown",
            "Unknown",
            0f,
            0f,
            0,
            0f,
            0,
            "none",
            0f,
            "none",
            0f,
            "none",
            0f,
            0f,
            false);

        public SettlementThreatReport(
            string settlementName,
            string ownerKingdomName,
            string settlementType,
            float garrisonStrength,
            float nearbyEnemyStrength,
            int nearbyEnemyPartyCount,
            float nearbyFriendlyStrength,
            int nearbyFriendlyPartyCount,
            string nearestEnemyPartyName,
            float nearestEnemyDistance,
            string strongestEnemyPartyName,
            float strongestEnemyStrength,
            string strongestFriendlyPartyName,
            float strongestFriendlyStrength,
            float threatScore,
            bool isThreatened)
        {
            SettlementName = settlementName;
            OwnerKingdomName = ownerKingdomName;
            SettlementType = settlementType;
            GarrisonStrength = garrisonStrength;
            NearbyEnemyStrength = nearbyEnemyStrength;
            NearbyEnemyPartyCount = nearbyEnemyPartyCount;
            NearbyFriendlyStrength = nearbyFriendlyStrength;
            NearbyFriendlyPartyCount = nearbyFriendlyPartyCount;
            NearestEnemyPartyName = nearestEnemyPartyName;
            NearestEnemyDistance = nearestEnemyDistance;
            StrongestEnemyPartyName = strongestEnemyPartyName;
            StrongestEnemyStrength = strongestEnemyStrength;
            StrongestFriendlyPartyName = strongestFriendlyPartyName;
            StrongestFriendlyStrength = strongestFriendlyStrength;
            ThreatScore = threatScore;
            IsThreatened = isThreatened;
        }

        public string SettlementName { get; private set; }

        public string OwnerKingdomName { get; private set; }

        public string SettlementType { get; private set; }

        public float GarrisonStrength { get; private set; }

        public float NearbyEnemyStrength { get; private set; }

        public int NearbyEnemyPartyCount { get; private set; }

        public float NearbyFriendlyStrength { get; private set; }

        public int NearbyFriendlyPartyCount { get; private set; }

        public string NearestEnemyPartyName { get; private set; }

        public float NearestEnemyDistance { get; private set; }

        public string StrongestEnemyPartyName { get; private set; }

        public float StrongestEnemyStrength { get; private set; }

        public string StrongestFriendlyPartyName { get; private set; }

        public float StrongestFriendlyStrength { get; private set; }

        public float ThreatScore { get; private set; }

        public bool IsThreatened { get; private set; }
    }
}
