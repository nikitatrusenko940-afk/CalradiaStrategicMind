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
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0,
            0f,
            0,
            0f,
            0,
            "No readable defense intent found",
            0f,
            0f,
            0,
            0,
            0,
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
            float requiredThreatStrength,
            float availableDefenseStrength,
            float garrisonDefenseStrength,
            float nearbyFriendlyDefenseStrength,
            float candidateDefenseStrength,
            float explicitDefenderStrength,
            int explicitDefenderCount,
            float nearbyUncommittedStrength,
            int nearbyUncommittedCount,
            float potentialCandidateStrength,
            int potentialCandidateCount,
            string defenseIntentReason,
            float enemySiegeThreatStrength,
            float nearbyEnemyThreatStrength,
            int usedCandidateCount,
            int nearbyFriendlyPartyCount,
            int nearbyEnemyPartyCount,
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
            RequiredThreatStrength = requiredThreatStrength;
            AvailableDefenseStrength = availableDefenseStrength;
            GarrisonDefenseStrength = garrisonDefenseStrength;
            NearbyFriendlyDefenseStrength = nearbyFriendlyDefenseStrength;
            CandidateDefenseStrength = candidateDefenseStrength;
            ExplicitDefenderStrength = explicitDefenderStrength;
            ExplicitDefenderCount = explicitDefenderCount;
            NearbyUncommittedStrength = nearbyUncommittedStrength;
            NearbyUncommittedCount = nearbyUncommittedCount;
            PotentialCandidateStrength = potentialCandidateStrength;
            PotentialCandidateCount = potentialCandidateCount;
            DefenseIntentReason = defenseIntentReason;
            EnemySiegeThreatStrength = enemySiegeThreatStrength;
            NearbyEnemyThreatStrength = nearbyEnemyThreatStrength;
            UsedCandidateCount = usedCandidateCount;
            NearbyFriendlyPartyCount = nearbyFriendlyPartyCount;
            NearbyEnemyPartyCount = nearbyEnemyPartyCount;
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

        public float RequiredThreatStrength { get; private set; }

        public float AvailableDefenseStrength { get; private set; }

        public float GarrisonDefenseStrength { get; private set; }

        public float NearbyFriendlyDefenseStrength { get; private set; }

        public float CandidateDefenseStrength { get; private set; }

        public float ExplicitDefenderStrength { get; private set; }

        public int ExplicitDefenderCount { get; private set; }

        public float NearbyUncommittedStrength { get; private set; }

        public int NearbyUncommittedCount { get; private set; }

        public float PotentialCandidateStrength { get; private set; }

        public int PotentialCandidateCount { get; private set; }

        public string DefenseIntentReason { get; private set; }

        public float EnemySiegeThreatStrength { get; private set; }

        public float NearbyEnemyThreatStrength { get; private set; }

        public int UsedCandidateCount { get; private set; }

        public int NearbyFriendlyPartyCount { get; private set; }

        public int NearbyEnemyPartyCount { get; private set; }

        public bool HasDirectSiegeThreat { get; private set; }

        public bool HasArmyPresence { get; private set; }

        public bool HasRegionalPressure { get; private set; }

        public bool IsDefenseEnough { get; private set; }

        public bool NeedsReinforcement { get; private set; }

        public string Reason { get; private set; }
    }
}
