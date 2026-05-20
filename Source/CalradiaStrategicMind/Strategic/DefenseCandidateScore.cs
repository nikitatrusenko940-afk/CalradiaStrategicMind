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
        public string RejectionCategory { get; set; }
        public string Reason { get; set; }
    }

    public struct DefenseCandidateScoringSummary
    {
        public DefenseCandidateScoringSummary(
            string settlementName,
            int evaluatedCandidates,
            int validCandidates,
            int rejectedCandidates,
            int rejectedByArmyAssignment,
            int rejectedByDefenseAssignment,
            int rejectedTooFar,
            int rejectedTooWeak,
            string selectedCandidate,
            string reason)
        {
            SettlementName = settlementName;
            EvaluatedCandidates = evaluatedCandidates;
            ValidCandidates = validCandidates;
            RejectedCandidates = rejectedCandidates;
            RejectedByArmyAssignment = rejectedByArmyAssignment;
            RejectedByDefenseAssignment = rejectedByDefenseAssignment;
            RejectedTooFar = rejectedTooFar;
            RejectedTooWeak = rejectedTooWeak;
            SelectedCandidate = selectedCandidate;
            Reason = reason;
        }

        public string SettlementName { get; private set; }

        public int EvaluatedCandidates { get; private set; }

        public int ValidCandidates { get; private set; }

        public int RejectedCandidates { get; private set; }

        public int RejectedByArmyAssignment { get; private set; }

        public int RejectedByDefenseAssignment { get; private set; }

        public int RejectedTooFar { get; private set; }

        public int RejectedTooWeak { get; private set; }

        public string SelectedCandidate { get; private set; }

        public string Reason { get; private set; }
    }
}
