using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmArmyAttackTargetScore
    {
        public Settlement Target { get; set; }
        public string TargetName { get; set; }
        public float Score { get; set; }
        public float Distance { get; set; }
        public float TargetDefenseStrength { get; set; }
        public float EstimatedAttackStrength { get; set; }
        public float StrengthRatio { get; set; }
        public float NearbyEnemyArmyStrength { get; set; }
        public float NearbyFriendlySupportStrength { get; set; }
        public bool IsCastle { get; set; }
        public bool IsTown { get; set; }
        public bool IsFrontlineCandidate { get; set; }
        public bool UsesExtendedFrontlineRule { get; set; }
        public float DistanceLimit { get; set; }
        public bool IsAlreadyAssigned { get; set; }
        public bool IsAlreadyBesieged { get; set; }
        public bool IsActiveDefenseTarget { get; set; }
        public string Reason { get; set; }
    }
}
