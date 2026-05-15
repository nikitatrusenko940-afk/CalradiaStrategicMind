namespace CalradiaStrategicMind.Strategic
{
    public struct DefenseNeedReport
    {
        public static readonly DefenseNeedReport Empty = new DefenseNeedReport(
            "unknown",
            "unknown",
            "Unknown",
            0f,
            999f,
            false,
            false,
            false,
            false,
            false,
            0,
            false,
            "None",
            "No settlement data");

        public DefenseNeedReport(
            string settlementName,
            string ownerKingdomName,
            string settlementType,
            float defensePriority,
            float defenseCoverageRatio,
            bool isThreatened,
            bool hasActiveSiege,
            bool hasDirectSiegeThreat,
            bool hasArmyPresence,
            bool needsReinforcement,
            int suitableCandidateCount,
            bool needsDefenseAction,
            string recommendedAction,
            string reason)
        {
            SettlementName = settlementName;
            OwnerKingdomName = ownerKingdomName;
            SettlementType = settlementType;
            DefensePriority = defensePriority;
            DefenseCoverageRatio = defenseCoverageRatio;
            IsThreatened = isThreatened;
            HasActiveSiege = hasActiveSiege;
            HasDirectSiegeThreat = hasDirectSiegeThreat;
            HasArmyPresence = hasArmyPresence;
            NeedsReinforcement = needsReinforcement;
            SuitableCandidateCount = suitableCandidateCount;
            NeedsDefenseAction = needsDefenseAction;
            RecommendedAction = recommendedAction;
            Reason = reason;
        }

        public string SettlementName { get; private set; }

        public string OwnerKingdomName { get; private set; }

        public string SettlementType { get; private set; }

        public float DefensePriority { get; private set; }

        public float DefenseCoverageRatio { get; private set; }

        public bool IsThreatened { get; private set; }

        public bool HasActiveSiege { get; private set; }

        public bool HasDirectSiegeThreat { get; private set; }

        public bool HasArmyPresence { get; private set; }

        public bool NeedsReinforcement { get; private set; }

        public int SuitableCandidateCount { get; private set; }

        public bool NeedsDefenseAction { get; private set; }

        public string RecommendedAction { get; private set; }

        public string Reason { get; private set; }
    }
}
