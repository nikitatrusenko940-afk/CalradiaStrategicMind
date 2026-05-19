using System.Collections.Generic;
using CalradiaStrategicMind.Settings;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmArmyDirector
    {
        private readonly CsmArmySnapshotBuilder _snapshotBuilder;
        private readonly CsmArmyAssignmentRegistry _assignmentRegistry;
        private readonly CsmArmyOperationalDirector _operationalDirector;
        private readonly CsmArmyFormationDirector _formationDirector;
        private int _lastCommandTick;

        public CsmArmyDirector()
        {
            _snapshotBuilder = new CsmArmySnapshotBuilder();
            _assignmentRegistry = new CsmArmyAssignmentRegistry();
            _operationalDirector = new CsmArmyOperationalDirector();
            _formationDirector = new CsmArmyFormationDirector();
            _lastCommandTick = -9999;
        }

        public List<CsmArmyDirectorReport> Evaluate(List<DefenseEvaluationSnapshot> defenseSnapshots, int observationTick)
        {
            var reports = new List<CsmArmyDirectorReport>();
            if (!ArmyDirectorSettings.EnableArmyDirector)
            {
                return reports;
            }

            var snapshots = _snapshotBuilder.Build();
            var isCommandCooldownActive = observationTick - _lastCommandTick < GetCooldownTicks();
            var operationalReports = _operationalDirector.Execute(snapshots, defenseSnapshots, _assignmentRegistry, observationTick, isCommandCooldownActive);
            AddReports(reports, operationalReports);

            if (isCommandCooldownActive)
            {
                reports.Add(new CsmArmyDirectorReport(observationTick, "none", "none", "AttackSettlement", "none", false, "Skipped", "Army command cooldown active"));
            }
            else
            {
                var formationReports = _formationDirector.Execute(snapshots, defenseSnapshots, _assignmentRegistry, observationTick);
                AddReports(reports, formationReports);
            }

            if (HasNewCommandApplied(reports))
            {
                _lastCommandTick = observationTick;
            }

            return reports;
        }

        private static void AddReports(List<CsmArmyDirectorReport> target, List<CsmArmyDirectorReport> source)
        {
            if (source == null)
            {
                return;
            }

            for (var index = 0; index < source.Count; index++)
            {
                target.Add(source[index]);
            }
        }

        private static bool HasNewCommandApplied(List<CsmArmyDirectorReport> reports)
        {
            for (var index = 0; index < reports.Count; index++)
            {
                if (reports[index].CommandApplied && reports[index].Status != "Reasserted")
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetCooldownTicks()
        {
            return ArmyDirectorSettings.ArmyCommandCooldownTicks < 0 ? 0 : ArmyDirectorSettings.ArmyCommandCooldownTicks;
        }
    }
}
