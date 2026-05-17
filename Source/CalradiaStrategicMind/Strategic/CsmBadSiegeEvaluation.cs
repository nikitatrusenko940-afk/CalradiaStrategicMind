using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmBadSiegeEvaluation
    {
        public bool IsBadSiege { get; set; }
        public Settlement CurrentTarget { get; set; }
        public string CurrentTargetName { get; set; }
        public float ArmyStrength { get; set; }
        public float TargetDefenseStrength { get; set; }
        public float StrengthRatio { get; set; }
        public float Cohesion { get; set; }
        public float NearbyEnemyArmyStrength { get; set; }
        public string Reason { get; set; }
    }
}
