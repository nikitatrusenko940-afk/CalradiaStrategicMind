namespace CalradiaStrategicMind.Strategic
{
    public class CsmArmyMissionReport
    {
        public CsmArmyMissionReport(CsmArmyMissionState state, bool handled)
        {
            State = state;
            Handled = handled;
        }

        public CsmArmyMissionState State { get; private set; }
        public bool Handled { get; private set; }
    }
}
