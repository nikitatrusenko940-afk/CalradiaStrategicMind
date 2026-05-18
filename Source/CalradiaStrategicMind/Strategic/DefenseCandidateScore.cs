using TaleWorlds.CampaignSystem.Party;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseCandidateScore
    {
        public MobileParty Party { get; set; }
        public string CandidateName { get; set; }
        public PartyObservationCategory CandidateCategory { get; set; }
        public float Score { get; set; }
        public float Distance { get; set; }
        public float Strength { get; set; }
        public float HealthyStrength { get; set; }
        public bool IsRejected { get; set; }
        public string Reason { get; set; }
    }
}
