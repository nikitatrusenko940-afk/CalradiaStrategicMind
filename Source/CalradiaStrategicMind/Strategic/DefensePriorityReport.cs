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

        public bool IsThreatened { get; private set; }

        public bool ShouldRequestDefense { get; private set; }

        public string Reason { get; private set; }
    }
}
