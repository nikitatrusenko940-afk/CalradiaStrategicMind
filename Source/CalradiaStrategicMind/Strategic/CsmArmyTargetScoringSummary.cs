namespace CalradiaStrategicMind.Strategic
{
    public class CsmArmyTargetScoringSummary
    {
        public int EvaluatedTargets { get; set; }
        public int SelectedTargets { get; set; }
        public int HardRejectedTargets { get; set; }
        public int IrrelevantRejectedTargets { get; set; }
        public int TacticalRejectedTargets { get; set; }
        public int RejectedActiveDefenseTargets { get; set; }
        public int RejectedOverextendedTargets { get; set; }
        public int RejectedLowStrengthTargets { get; set; }
        public int RecentlyFailedTargetPenalties { get; set; }

        public void Add(CsmArmyTargetScoringSummary other)
        {
            if (other == null)
            {
                return;
            }

            EvaluatedTargets += other.EvaluatedTargets;
            SelectedTargets += other.SelectedTargets;
            HardRejectedTargets += other.HardRejectedTargets;
            IrrelevantRejectedTargets += other.IrrelevantRejectedTargets;
            TacticalRejectedTargets += other.TacticalRejectedTargets;
            RejectedActiveDefenseTargets += other.RejectedActiveDefenseTargets;
            RejectedOverextendedTargets += other.RejectedOverextendedTargets;
            RejectedLowStrengthTargets += other.RejectedLowStrengthTargets;
            RecentlyFailedTargetPenalties += other.RecentlyFailedTargetPenalties;
        }
    }
}
