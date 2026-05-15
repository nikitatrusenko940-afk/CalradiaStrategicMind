namespace CalradiaStrategicMind.Strategic
{
    public struct SettlementValueReport
    {
        public static readonly SettlementValueReport Empty = new SettlementValueReport(
            "unknown",
            "unknown",
            "Unknown",
            false,
            false,
            0f,
            0f,
            0f,
            "No settlement data");

        public SettlementValueReport(
            string settlementName,
            string ownerKingdomName,
            string settlementType,
            bool isTown,
            bool isCastle,
            float prosperity,
            float garrisonStrength,
            float strategicValue,
            string valueReason)
        {
            SettlementName = settlementName;
            OwnerKingdomName = ownerKingdomName;
            SettlementType = settlementType;
            IsTown = isTown;
            IsCastle = isCastle;
            Prosperity = prosperity;
            GarrisonStrength = garrisonStrength;
            StrategicValue = strategicValue;
            ValueReason = valueReason;
        }

        public string SettlementName { get; private set; }

        public string OwnerKingdomName { get; private set; }

        public string SettlementType { get; private set; }

        public bool IsTown { get; private set; }

        public bool IsCastle { get; private set; }

        public float Prosperity { get; private set; }

        public float GarrisonStrength { get; private set; }

        public float StrategicValue { get; private set; }

        public string ValueReason { get; private set; }
    }
}
