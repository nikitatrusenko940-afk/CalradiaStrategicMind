using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public struct CsmArmySnapshot
    {
        public CsmArmySnapshot(
            Army army,
            string armyId,
            string armyName,
            string kingdomName,
            MobileParty leaderParty,
            float totalStrength,
            int partyCount,
            float cohesion,
            Settlement currentTargetSettlement,
            string currentObjectiveGuess,
            bool isPlayerArmy,
            bool isValidForCsm,
            string invalidReason)
        {
            Army = army;
            ArmyId = armyId;
            ArmyName = armyName;
            KingdomName = kingdomName;
            LeaderParty = leaderParty;
            TotalStrength = totalStrength;
            PartyCount = partyCount;
            Cohesion = cohesion;
            CurrentTargetSettlement = currentTargetSettlement;
            CurrentObjectiveGuess = currentObjectiveGuess;
            IsPlayerArmy = isPlayerArmy;
            IsValidForCsm = isValidForCsm;
            InvalidReason = invalidReason;
        }

        public Army Army { get; private set; }
        public string ArmyId { get; private set; }
        public string ArmyName { get; private set; }
        public string KingdomName { get; private set; }
        public MobileParty LeaderParty { get; private set; }
        public float TotalStrength { get; private set; }
        public int PartyCount { get; private set; }
        public float Cohesion { get; private set; }
        public Settlement CurrentTargetSettlement { get; private set; }
        public string CurrentObjectiveGuess { get; private set; }
        public bool IsPlayerArmy { get; private set; }
        public bool IsValidForCsm { get; private set; }
        public string InvalidReason { get; private set; }
    }
}
