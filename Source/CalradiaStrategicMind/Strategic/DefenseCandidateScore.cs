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
        public float DistanceScore { get; set; }
        public float StrengthScore { get; set; }
        public float AvailabilityScore { get; set; }
        public float IntentScore { get; set; }
        public float CriticalDefenseSpeedScore { get; set; }
        public bool IsWeakSupplemental { get; set; }
        public bool IsRecoveringEmergencyDefender { get; set; }
        public bool IsReassignmentAllowed { get; set; }
        public string ReassignmentFromSettlement { get; set; }
        public string ReassignmentFromCoverageStatus { get; set; }
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
            int rejectedWrongFaction,
            int rejectedInvalid,
            int rejectedAlreadyDefendingDifferentSettlement,
            int rejectedTooWeakHard,
            int acceptedWeakSupplemental,
            int acceptedReassignment,
            string selectedCandidate,
            float selectedCandidateScore,
            float selectedCandidateDistance,
            float selectedCandidateStrength,
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
            RejectedWrongFaction = rejectedWrongFaction;
            RejectedInvalid = rejectedInvalid;
            RejectedAlreadyDefendingDifferentSettlement = rejectedAlreadyDefendingDifferentSettlement;
            RejectedTooWeakHard = rejectedTooWeakHard;
            AcceptedWeakSupplemental = acceptedWeakSupplemental;
            AcceptedReassignment = acceptedReassignment;
            SelectedCandidate = selectedCandidate;
            SelectedCandidateScore = selectedCandidateScore;
            SelectedCandidateDistance = selectedCandidateDistance;
            SelectedCandidateStrength = selectedCandidateStrength;
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

        public int RejectedWrongFaction { get; private set; }

        public int RejectedInvalid { get; private set; }

        public int RejectedAlreadyDefendingDifferentSettlement { get; private set; }

        public int RejectedTooWeakHard { get; private set; }

        public int AcceptedWeakSupplemental { get; private set; }

        public int AcceptedReassignment { get; private set; }

        public string SelectedCandidate { get; private set; }

        public float SelectedCandidateScore { get; private set; }

        public float SelectedCandidateDistance { get; private set; }

        public float SelectedCandidateStrength { get; private set; }

        public string Reason { get; private set; }
    }
}
