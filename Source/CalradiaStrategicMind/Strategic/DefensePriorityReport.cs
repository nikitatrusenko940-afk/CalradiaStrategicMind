namespace CalradiaStrategicMind.Strategic
{
    public struct DefensePriorityReport
    {
        public static readonly DefensePriorityReport Empty = new DefensePriorityReport(
            "unknown",
            "unknown",
            "Unknown",
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0,
            "none",
            0f,
            "none",
            0f,
            false,
            false,
            "No settlement data");

        public DefensePriorityReport(
            string settlementName,
            string ownerKingdomName,
            string settlementType,
            float threatScore,
            float strategicValue,
            float threatComponent,
            float valueComponent,
            float threatenedBonus,
            float defensePriority,
            int enemyPartyCount,
            string strongestEnemyPartyName,
            float strongestEnemyStrength,
            string nearestEnemyPartyName,
            float nearestEnemyDistance,
            bool isThreatened,
            bool shouldRequestDefense,
            string reason)
        {
            SettlementName = settlementName;
            OwnerKingdomName = ownerKingdomName;
            SettlementType = settlementType;
            ThreatScore = threatScore;
            StrategicValue = strategicValue;
            ThreatComponent = threatComponent;
            ValueComponent = valueComponent;
            ThreatenedBonus = threatenedBonus;
            DefensePriority = defensePriority;
            EnemyPartyCount = enemyPartyCount;
            StrongestEnemyPartyName = strongestEnemyPartyName;
            StrongestEnemyStrength = strongestEnemyStrength;
            NearestEnemyPartyName = nearestEnemyPartyName;
            NearestEnemyDistance = nearestEnemyDistance;
            IsThreatened = isThreatened;
            ShouldRequestDefense = shouldRequestDefense;
            Reason = reason;
        }

        public string SettlementName { get; private set; }

        public string OwnerKingdomName { get; private set; }

        public string SettlementType { get; private set; }

        public float ThreatScore { get; private set; }

        public float StrategicValue { get; private set; }

        public float ThreatComponent { get; private set; }

        public float ValueComponent { get; private set; }

        public float ThreatenedBonus { get; private set; }

        public float DefensePriority { get; private set; }

        public int EnemyPartyCount { get; private set; }

        public string StrongestEnemyPartyName { get; private set; }

        public float StrongestEnemyStrength { get; private set; }

        public string NearestEnemyPartyName { get; private set; }

        public float NearestEnemyDistance { get; private set; }

        public bool IsThreatened { get; private set; }

        public bool ShouldRequestDefense { get; private set; }

        public string Reason { get; private set; }
    }
}
