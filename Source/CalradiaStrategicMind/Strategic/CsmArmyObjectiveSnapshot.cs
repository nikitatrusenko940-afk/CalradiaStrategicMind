using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmArmyObjectiveSnapshot
    {
        public string ArmyId { get; set; }
        public string ArmyName { get; set; }
        public string KingdomName { get; set; }
        public MobileParty LeaderParty { get; set; }
        public Settlement CurrentTargetSettlement { get; set; }
        public string CurrentTargetSettlementName { get; set; }
        public Settlement LeaderTargetSettlement { get; set; }
        public string LeaderTargetSettlementName { get; set; }
        public Settlement LeaderBesiegedSettlement { get; set; }
        public string LeaderBesiegedSettlementName { get; set; }
        public Settlement LeaderCurrentSettlement { get; set; }
        public string LeaderCurrentSettlementName { get; set; }
        public Settlement ArmyAiBehaviorObjectSettlement { get; set; }
        public string ArmyAiBehaviorObjectSettlementName { get; set; }
        public string CurrentObjective { get; set; }
        public bool IsBesieging { get; set; }
        public bool IsMovingToTarget { get; set; }
        public bool IsDefending { get; set; }
        public bool IsReadable { get; set; }
        public bool HasObjectiveTargetMismatch { get; set; }
        public string ObjectiveMismatchReason { get; set; }
        public string Reason { get; set; }
    }
}
