namespace CalradiaStrategicMind.Strategic
{
    public struct DefenseCoverageReport
    {
        public static readonly DefenseCoverageReport Empty = new DefenseCoverageReport(
            "unknown",
            "unknown",
            "Unknown",
            0f,
            0f,
            0,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            false,
            false,
            false,
            true,
            false,
            "No settlement threat");

        public DefenseCoverageReport(
            string settlementName,
            string ownerKingdomName,
            string settlementType,
            float garrisonStrength,
            float nearbyFriendlyStrength,
            int suitableCandidateCount,
            float suitableCandidateStrength,
            float totalAvailableDefenseStrength,
            float threatScore,
            float siegeThreatScore,
            float armySiegeThreat,
            float regionalEnemyPressure,
            float defenseCoverageRatio,
            bool hasDirectSiegeThreat,
            bool hasArmyPresence,
            bool hasRegionalPressure,
            bool isDefenseEnough,
            bool needsReinforcement,
            string reason)
        {
            SettlementName = settlementName;
            OwnerKingdomName = ownerKingdomName;
            SettlementType = settlementType;
            GarrisonStrength = garrisonStrength;
            NearbyFriendlyStrength = nearbyFriendlyStrength;
            SuitableCandidateCount = suitableCandidateCount;
            SuitableCandidateStrength = suitableCandidateStrength;
            TotalAvailableDefenseStrength = totalAvailableDefenseStrength;
            ThreatScore = threatScore;
            SiegeThreatScore = siegeThreatScore;
            ArmySiegeThreat = armySiegeThreat;
            RegionalEnemyPressure = regionalEnemyPressure;
            DefenseCoverageRatio = defenseCoverageRatio;
            HasDirectSiegeThreat = hasDirectSiegeThreat;
            HasArmyPresence = hasArmyPresence;
            HasRegionalPressure = hasRegionalPressure;
            IsDefenseEnough = isDefenseEnough;
            NeedsReinforcement = needsReinforcement;
            Reason = reason;
        }

        public string SettlementName { get; private set; }

        public string OwnerKingdomName { get; private set; }

        public string SettlementType { get; private set; }

        public float GarrisonStrength { get; private set; }

        public float NearbyFriendlyStrength { get; private set; }

        public int SuitableCandidateCount { get; private set; }

        public float SuitableCandidateStrength { get; private set; }

        public float TotalAvailableDefenseStrength { get; private set; }

        public float ThreatScore { get; private set; }

        public float SiegeThreatScore { get; private set; }

        public float ArmySiegeThreat { get; private set; }

        public float RegionalEnemyPressure { get; private set; }

        public float DefenseCoverageRatio { get; private set; }

        public bool HasDirectSiegeThreat { get; private set; }

        public bool HasArmyPresence { get; private set; }

        public bool HasRegionalPressure { get; private set; }

        public bool IsDefenseEnough { get; private set; }

        public bool NeedsReinforcement { get; private set; }

        public string Reason { get; private set; }
    }
}
