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
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            false,
            0,
            0,
            0,
            "none",
            0f,
            "none",
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
            float siegeThreatScore,
            float armySiegeThreat,
            float regionalEnemyPressure,
            float enemyArmyStrength,
            float enemyLordPressure,
            float activeSiegeThreat,
            bool hasActiveSiege,
            int nearbyEnemyArmyMemberPartyCount,
            int nearbyEnemyArmyLeaderPartyCount,
            int nearbyEnemyLordPartyCount,
            string strongestEnemyArmyName,
            float strongestEnemyArmyStrength,
            string strongestEnemyLordName,
            float strongestEnemyLordStrength,
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
            SiegeThreatScore = siegeThreatScore;
            ArmySiegeThreat = armySiegeThreat;
            RegionalEnemyPressure = regionalEnemyPressure;
            EnemyArmyStrength = enemyArmyStrength;
            EnemyLordPressure = enemyLordPressure;
            ActiveSiegeThreat = activeSiegeThreat;
            HasActiveSiege = hasActiveSiege;
            NearbyEnemyArmyMemberPartyCount = nearbyEnemyArmyMemberPartyCount;
            NearbyEnemyArmyLeaderPartyCount = nearbyEnemyArmyLeaderPartyCount;
            NearbyEnemyLordPartyCount = nearbyEnemyLordPartyCount;
            StrongestEnemyArmyName = strongestEnemyArmyName;
            StrongestEnemyArmyStrength = strongestEnemyArmyStrength;
            StrongestEnemyLordName = strongestEnemyLordName;
            StrongestEnemyLordStrength = strongestEnemyLordStrength;
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

        public float SiegeThreatScore { get; private set; }

        public float ArmySiegeThreat { get; private set; }

        public float RegionalEnemyPressure { get; private set; }

        public float AreaEnemyPressure
        {
            get { return RegionalEnemyPressure; }
        }

        public float EnemyArmyStrength { get; private set; }

        public float EnemyLordPressure { get; private set; }

        public float ActiveSiegeThreat { get; private set; }

        public bool HasActiveSiege { get; private set; }

        public int NearbyEnemyArmyMemberPartyCount { get; private set; }

        public int NearbyEnemyArmyLeaderPartyCount { get; private set; }

        public int NearbyEnemyArmyCount
        {
            get { return NearbyEnemyArmyLeaderPartyCount; }
        }

        public int NearbyEnemyLordPartyCount { get; private set; }

        public string StrongestEnemyArmyName { get; private set; }

        public float StrongestEnemyArmyStrength { get; private set; }

        public string StrongestEnemyLordName { get; private set; }

        public float StrongestEnemyLordStrength { get; private set; }

        public bool IsThreatened { get; private set; }
    }
}
