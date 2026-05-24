using System.Collections.Generic;
using CalradiaStrategicMind.Harmony;
using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Settings;
using CalradiaStrategicMind.Strategic;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Behaviors
{
    public class StrategicObservationBehavior : CampaignBehaviorBase
    {
        private const int MaxPartiesPerDailyObservation = 5;
        private const int MaxSettlementsPerDailyObservation = 3;
        private const int MaxDefenseCandidatesPerSettlement = 3;

        private readonly PartyStrengthEvaluator _partyStrengthEvaluator;
        private readonly PartyClassifier _partyClassifier;
        private readonly DefenseEvaluationSnapshotBuilder _defenseEvaluationSnapshotBuilder;
        private readonly DefenseActionPlanner _defenseActionPlanner;
        private readonly DefenseActionPlanHistory _defenseActionPlanHistory;
        private readonly DefenseDiagnosticsSummaryBuilder _defenseDiagnosticsSummaryBuilder;
        private readonly DryRunDefenseController _dryRunDefenseController;
        private readonly DryRunDefenseDecisionHistory _dryRunDefenseDecisionHistory;
        private readonly DryRunDefenseReportAggregator _dryRunDefenseReportAggregator;
        private readonly DefenseController _defenseController;
        private readonly DefenseControllerSafetyGuard _defenseControllerSafetyGuard;
        private readonly DefenseCommandInterface _defenseCommandInterface;
        private readonly DirectDefenseCommandController _directDefenseCommandController;
        private readonly CsmArmyDirector _armyDirector;
        private readonly CsmStrategicTaskDisciplineController _strategicTaskDisciplineController;
        private readonly DefenseScoreSimulator _defenseScoreSimulator;
        private readonly DefenseScoreSimulationSummaryBuilder _defenseScoreSimulationSummaryBuilder;
        private readonly ExperimentalDefenseScoreInfluenceRegistry _experimentalDefenseScoreInfluenceRegistry;
        private int _defenseCommandsExecutedThisTick;
        private int _blockedByArmyAssignmentThisTick;
        private int _blockedByDefenseAssignmentThisTick;
        private int _integrationConflictsDetectedThisTick;
        private int _nextPartyIndex;
        private int _nextSettlementIndex;
        private int _observationTick;

        public StrategicObservationBehavior()
        {
            _partyStrengthEvaluator = new PartyStrengthEvaluator();
            _partyClassifier = new PartyClassifier();
            _defenseEvaluationSnapshotBuilder = new DefenseEvaluationSnapshotBuilder();
            _defenseActionPlanner = new DefenseActionPlanner();
            _defenseActionPlanHistory = new DefenseActionPlanHistory();
            _defenseDiagnosticsSummaryBuilder = new DefenseDiagnosticsSummaryBuilder();
            _dryRunDefenseController = new DryRunDefenseController();
            _dryRunDefenseDecisionHistory = new DryRunDefenseDecisionHistory();
            _dryRunDefenseReportAggregator = new DryRunDefenseReportAggregator();
            _defenseController = new DefenseController();
            _defenseControllerSafetyGuard = new DefenseControllerSafetyGuard();
            _defenseCommandInterface = new DefenseCommandInterface();
            _directDefenseCommandController = new DirectDefenseCommandController();
            _armyDirector = new CsmArmyDirector();
            CsmControlledPartyResolver.Register(_directDefenseCommandController.AssignmentRegistry, _armyDirector.AssignmentRegistry);
            _strategicTaskDisciplineController = new CsmStrategicTaskDisciplineController();
            _defenseScoreSimulator = new DefenseScoreSimulator();
            _defenseScoreSimulationSummaryBuilder = new DefenseScoreSimulationSummaryBuilder();
            _experimentalDefenseScoreInfluenceRegistry = new ExperimentalDefenseScoreInfluenceRegistry();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_nextPartyIndex", ref _nextPartyIndex);
            dataStore.SyncData("_nextSettlementIndex", ref _nextSettlementIndex);
            dataStore.SyncData("_observationTick", ref _observationTick);
        }

        private void OnDailyTick()
        {
            SafeExecutor.Run("Strategic observation daily tick", () =>
            {
                ObserveParties();
                ObserveSettlements();
            });
        }

        private void ObserveParties()
        {
            var parties = MobileParty.All;
            if (parties == null)
            {
                CsmLogger.Warn("Strategic observation skipped: MobileParty.All is null");
                return;
            }

            _observationTick++;
            var observedCount = 0;
            var fallbackObservedCount = 0;
            var fallbackParties = new MobileParty[MaxPartiesPerDailyObservation];
            var fallbackCategories = new PartyObservationCategory[MaxPartiesPerDailyObservation];
            var checkedCount = 0;
            var startPartyIndex = _nextPartyIndex;
            if (_nextPartyIndex < 0 || _nextPartyIndex >= parties.Count)
            {
                _nextPartyIndex = 0;
                startPartyIndex = 0;
            }

            while (checkedCount < parties.Count && observedCount < MaxPartiesPerDailyObservation)
            {
                var party = parties[_nextPartyIndex];
                _nextPartyIndex++;
                if (_nextPartyIndex >= parties.Count)
                {
                    _nextPartyIndex = 0;
                }

                checkedCount++;
                if (!ShouldObserveParty(party))
                {
                    continue;
                }

                var category = _partyClassifier.GetCategory(party);
                if (_partyClassifier.ShouldObserveForStrategicAi(party))
                {
                    LogPartyObservation(party, category);
                    observedCount++;
                    continue;
                }

                if (observedCount == 0
                    && fallbackObservedCount < MaxPartiesPerDailyObservation
                    && ShouldUseFallbackCategory(category))
                {
                    fallbackParties[fallbackObservedCount] = party;
                    fallbackCategories[fallbackObservedCount] = category;
                    fallbackObservedCount++;
                }
            }

            if (checkedCount >= parties.Count)
            {
                _nextPartyIndex = startPartyIndex + 1;
                if (_nextPartyIndex >= parties.Count)
                {
                    _nextPartyIndex = 0;
                }
            }

            if (observedCount > 0)
            {
                return;
            }

            for (var index = 0; index < fallbackObservedCount; index++)
            {
                LogPartyObservation(fallbackParties[index], fallbackCategories[index]);
            }

            if (fallbackObservedCount == 0)
            {
                CsmLogger.Info($"Strategic observation completed: tick={_observationTick}, no active parties selected");
            }
        }

        private static bool ShouldObserveParty(MobileParty party)
        {
            if (party == null)
            {
                return false;
            }

            if (!party.IsActive || party.IsDisbanding)
            {
                return false;
            }

            return party.MemberRoster != null && party.MemberRoster.TotalManCount > 0;
        }

        private static bool ShouldUseFallbackCategory(PartyObservationCategory category)
        {
            return category != PartyObservationCategory.Unknown
                && category != PartyObservationCategory.Militia;
        }

        private void LogPartyObservation(MobileParty party, PartyObservationCategory category)
        {
            var report = _partyStrengthEvaluator.EvaluatePartyStrengthReport(party);
            var regulars = report.TroopCount - report.WoundedCount;
            if (regulars < 0)
            {
                regulars = 0;
            }

            var leaderName = party == null || party.LeaderHero == null || party.LeaderHero.Name == null
                ? "none"
                : party.LeaderHero.Name.ToString();

            CsmLogger.Info(
                $"Observed party strength: tick={_observationTick}, category={category}, party='{GetPartyName(party)}', leader='{leaderName}', regulars={regulars}, wounded={report.WoundedCount}, totalStrength={report.TotalStrength:0.00}, healthyStrength={report.HealthyTroopStrength:0.00}, woundedStrength={report.WoundedTroopStrength:0.00}, leaderStrength={report.LeaderStrength:0.00}");
        }

        private void ObserveSettlements()
        {
            var settlements = Settlement.All;
            if (settlements == null)
            {
                CsmLogger.Warn("Settlement threat observation skipped: Settlement.All is null");
                return;
            }

            var observedCount = 0;
            var checkedCount = 0;
            var defenseSnapshots = new List<DefenseEvaluationSnapshot>();
            _defenseCommandsExecutedThisTick = 0;
            _blockedByArmyAssignmentThisTick = 0;
            _blockedByDefenseAssignmentThisTick = 0;
            _integrationConflictsDetectedThisTick = 0;
            _dryRunDefenseReportAggregator.BeginTick(_observationTick);
            _defenseScoreSimulationSummaryBuilder.BeginTick(_observationTick);
            _experimentalDefenseScoreInfluenceRegistry.BeginTick(_observationTick);
            LogExperimentalDefenseScoreInfluenceSummaryIfAvailable();
            if (_nextSettlementIndex < 0 || _nextSettlementIndex >= settlements.Count)
            {
                _nextSettlementIndex = 0;
            }

            var observedSettlementNames = new HashSet<string>();
            for (var index = 0; index < settlements.Count; index++)
            {
                var settlement = settlements[index];
                if (!ShouldObserveSettlement(settlement) || !HasActiveSiege(settlement))
                {
                    continue;
                }

                if (ObserveDefenseSettlement(settlement, null, defenseSnapshots))
                {
                    observedSettlementNames.Add(GetSettlementName(settlement));
                }
            }

            for (var index = 0; index < settlements.Count && observedCount < MaxSettlementsPerDailyObservation; index++)
            {
                var settlement = settlements[index];
                if (!ShouldObserveSettlement(settlement) || observedSettlementNames.Contains(GetSettlementName(settlement)))
                {
                    continue;
                }

                var snapshot = _defenseEvaluationSnapshotBuilder.Build(settlement, MaxDefenseCandidatesPerSettlement);
                if (!ShouldRunFullDefensePipeline(snapshot))
                {
                    continue;
                }

                if (ObserveDefenseSettlement(settlement, snapshot, defenseSnapshots))
                {
                    observedSettlementNames.Add(GetSettlementName(settlement));
                    observedCount++;
                }
            }

            while (checkedCount < settlements.Count && observedCount < MaxSettlementsPerDailyObservation)
            {
                var settlement = settlements[_nextSettlementIndex];
                _nextSettlementIndex++;
                if (_nextSettlementIndex >= settlements.Count)
                {
                    _nextSettlementIndex = 0;
                }

                checkedCount++;
                if (!ShouldObserveSettlement(settlement) || observedSettlementNames.Contains(GetSettlementName(settlement)))
                {
                    continue;
                }

                if (ObserveDefenseSettlement(settlement, null, defenseSnapshots))
                {
                    observedSettlementNames.Add(GetSettlementName(settlement));
                    observedCount++;
                }
            }

            if (DefenseDryRunSettings.EnableDryRunDailyReport)
            {
                var dailyReport = _dryRunDefenseReportAggregator.BuildReport();
                if (dailyReport.TotalEvaluatedSettlements > 0)
                {
                    LogDryRunDefenseDailyReport(dailyReport);
                }
            }

            if (DefenseScoreSimulationSettings.EnableDefenseScoreSimulationSummary)
            {
                var scoreSimulationSummary = _defenseScoreSimulationSummaryBuilder.BuildSummary();
                if (scoreSimulationSummary.TotalScoreSimulations > 0)
                {
                    LogDefenseScoreSimulationSummary(scoreSimulationSummary);
                }
            }

            ObserveArmies(defenseSnapshots);
            var postEnforcementReport = _directDefenseCommandController.PostDirectorEnforceAssignments(defenseSnapshots, _armyDirector == null ? null : _armyDirector.AssignmentRegistry, _observationTick);
            LogDefensePostEnforcement(postEnforcementReport);
            LogDefenseAssignmentLifecycle(_directDefenseCommandController.GetAssignmentLifecycleSummary(_observationTick));
        }

        private bool ObserveDefenseSettlement(Settlement settlement, DefenseEvaluationSnapshot? existingSnapshot, List<DefenseEvaluationSnapshot> defenseSnapshots)
        {
            if (!DefenseDiagnosticsSettings.EnableDefenseDiagnostics)
            {
                return true;
            }

            var snapshot = existingSnapshot.HasValue
                ? existingSnapshot.Value
                : _defenseEvaluationSnapshotBuilder.Build(settlement, MaxDefenseCandidatesPerSettlement);
            if (defenseSnapshots != null)
            {
                defenseSnapshots.Add(snapshot);
            }

            if (DefenseDiagnosticsSettings.EnableVerboseDefenseLogs)
            {
                LogSettlementThreat(snapshot.ThreatReport);
                LogSettlementValue(snapshot.ValueReport);
                LogDefensePriority(snapshot.PriorityReport);
            }

            if (!ShouldRunFullDefensePipeline(snapshot))
            {
                if (DefenseDiagnosticsSettings.EnableVerboseDefenseLogs)
                {
                    CsmLogger.Info(
                        $"Skipping full defense pipeline because settlement has no active siege or enemy army presence: tick={_observationTick}, settlement='{snapshot.ThreatReport.SettlementName}'");
                }

                return true;
            }

            if (!ShouldLogDefenseCandidates(snapshot.PriorityReport))
            {
                return true;
            }

            if (DefenseDiagnosticsSettings.EnableDefenseCandidateLogs)
            {
                LogDefenseCandidates(snapshot.CandidateReports);
            }

            if (DefenseDiagnosticsSettings.EnableVerboseDefenseLogs)
            {
                LogDefenseCoverage(snapshot.CoverageReport);
                LogDefenseNeed(snapshot.NeedReport);
            }

            var actionPlan = _defenseActionPlanner.CreatePlan(snapshot);
            if (DefenseDiagnosticsSettings.EnableVerboseDefenseLogs)
            {
                LogDefenseActionPlan(actionPlan);
            }

            var stabilityReport = GetStabilityReport(actionPlan);
            var summary = _defenseDiagnosticsSummaryBuilder.Build(snapshot, actionPlan, stabilityReport);
            if (DefenseDiagnosticsSettings.EnableDefenseSummaryLogs)
            {
                LogDefenseSummary(summary);
                LogDefenseCoverageBreakdown(snapshot.CoverageReport);
            }

            if (DefenseDryRunSettings.EnableDryRunDefenseController)
            {
                var dryRunDecision = _dryRunDefenseController.EvaluateDryRun(summary, actionPlan, stabilityReport);
                LogDryRunDefenseDecision(dryRunDecision);
                var dryRunStabilityReport = GetDryRunStabilityReport(dryRunDecision);
                _dryRunDefenseReportAggregator.Record(dryRunDecision, dryRunStabilityReport);
                var defenseControllerDecision = _defenseController.Evaluate(summary, actionPlan, dryRunDecision, dryRunStabilityReport);
                LogDefenseControllerDecision(defenseControllerDecision);
                var defenseControllerSafetyReport = _defenseControllerSafetyGuard.Evaluate(summary, actionPlan, dryRunDecision, dryRunStabilityReport, defenseControllerDecision);
                LogDefenseControllerSafety(defenseControllerSafetyReport);
                if (DefenseAssignmentSettings.EnableDefenseAssignments)
                {
                    var assignmentReports = _directDefenseCommandController.ProcessAssignments(snapshot, _observationTick, _armyDirector == null ? null : _armyDirector.AssignmentRegistry);
                    LogDefenseAssignmentReports(assignmentReports);
                }

                if (DirectDefenseCommandSettings.EnableDirectDefenseCommand && IsUrgentDefenseAction(actionPlan.RecommendedAction))
                {
                    var directCommandReport = _directDefenseCommandController.Execute(snapshot, actionPlan, dryRunDecision, dryRunStabilityReport, defenseControllerSafetyReport, _armyDirector, _observationTick);
                    if (DirectDefenseCommandSettings.EnableDirectDefenseCommandLogs)
                    {
                        LogDirectDefenseCommand(directCommandReport);
                        LogDefenseCommand(directCommandReport.ToDefenseCommandReport());
                    }

                    RecordIntegrationBlock(directCommandReport.Reason);
                    if (directCommandReport.CommandApplied)
                    {
                        _defenseCommandsExecutedThisTick++;
                    }

                    if (DefenseAssignmentSettings.EnableDefenseAssignments)
                    {
                        var assignmentReports = _directDefenseCommandController.ConsumePendingAssignmentReports();
                        LogDefenseAssignmentReports(assignmentReports);
                    }
                }

                var commandReport = _defenseCommandInterface.RequestReinforcement(summary, actionPlan, dryRunDecision, defenseControllerSafetyReport);
                if (ShouldLogDefenseCommand(commandReport))
                {
                    LogDefenseCommand(commandReport);
                }

                if (DefenseScoreSimulationSettings.EnableDefenseScoreSimulation)
                {
                    var scoreSimulationReport = _defenseScoreSimulator.Simulate(summary, actionPlan, dryRunDecision, dryRunStabilityReport, defenseControllerSafetyReport);
                    LogDefenseScoreSimulation(scoreSimulationReport);
                    _defenseScoreSimulationSummaryBuilder.Record(scoreSimulationReport);
                    _experimentalDefenseScoreInfluenceRegistry.Record(scoreSimulationReport, _observationTick);
                }
            }

            return true;
        }

        private void ObserveArmies(List<DefenseEvaluationSnapshot> defenseSnapshots)
        {
            if (!ArmyDirectorSettings.EnableArmyDirector)
            {
                return;
            }

            var reports = _armyDirector.Evaluate(defenseSnapshots, _directDefenseCommandController.AssignmentRegistry, _observationTick);
            var armyCommandsCreated = 0;
            var armyCommandsReasserted = 0;
            if (reports != null)
            {
                for (var index = 0; index < reports.Count; index++)
                {
                    var report = reports[index];
                    if (report.CommandApplied && report.Status == "Created")
                    {
                        armyCommandsCreated++;
                    }

                    if (report.CommandApplied && report.Status == "Reasserted")
                    {
                        armyCommandsReasserted++;
                    }

                    RecordIntegrationBlock(report.Reason);
                    if (IsIntegrationConflictReason(report.Reason))
                    {
                        _integrationConflictsDetectedThisTick++;
                        LogIntegrationConflict(report.ArmyName, report.TargetName, "AttackDefenseAssignmentConflict", report.Reason);
                    }
                }
            }

            _strategicTaskDisciplineController.Execute(_armyDirector, _directDefenseCommandController, _observationTick);
            LogIntegrationHealth(armyCommandsCreated, armyCommandsReasserted);
            if (!ArmyDirectorSettings.EnableArmyDirectorLogs || reports == null)
            {
                return;
            }

            for (var index = 0; index < reports.Count; index++)
            {
                var report = reports[index];
                if (!ShouldLogArmyDirectorReport(report))
                {
                    continue;
                }

                CsmLogger.Info(
                    $"Observed CSM army director: tick={report.ObservationTick}, army='{report.ArmyName}', kingdom='{report.KingdomName}', objective='{report.Objective}', target='{report.TargetName}', commandApplied={report.CommandApplied}, status='{report.Status}', reason='{report.Reason}'");
            }
        }

        private static bool ShouldLogArmyDirectorReport(CsmArmyDirectorReport report)
        {
            return report.CommandApplied
                || report.Status == "Created"
                || report.Status == "Reasserted"
                || report.Status == "Completed"
                || report.Status == "Invalid"
                || report.Status == "Expired"
                || report.Status == "Skipped";
        }

        private void RecordIntegrationBlock(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            if (reason.IndexOf("active CSM army assignment", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _blockedByArmyAssignmentThisTick++;
                return;
            }

            if (reason.IndexOf("active CSM defense assignment", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _blockedByDefenseAssignmentThisTick++;
            }
        }

        private static bool IsIntegrationConflictReason(string reason)
        {
            return !string.IsNullOrWhiteSpace(reason)
                && reason.IndexOf("active CSM defense assignment", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LogIntegrationConflict(string partyOrArmyName, string settlementName, string conflictType, string reason)
        {
            CsmLogger.Info(
                $"Observed CSM integration conflict: tick={_observationTick}, party='{partyOrArmyName}', army='{partyOrArmyName}', settlement='{settlementName}', conflictType='{conflictType}', reason='{reason}'");
        }

        private void LogIntegrationHealth(int armyCommandsCreated, int armyCommandsReasserted)
        {
            CsmLogger.Info(
                $"Observed CSM integration health: tick={_observationTick}, activeArmyAssignments={_armyDirector.CountActiveAssignments()}, activeDefenseAssignments={_directDefenseCommandController.CountActiveAssignments()}, armyCommandsCreated={armyCommandsCreated}, armyCommandsReasserted={armyCommandsReasserted}, defenseCommandsExecuted={_defenseCommandsExecutedThisTick}, blockedByArmyAssignment={_blockedByArmyAssignmentThisTick}, blockedByDefenseAssignment={_blockedByDefenseAssignmentThisTick}, conflictsDetected={_integrationConflictsDetectedThisTick}, reason='Integration health snapshot'");
        }

        private static bool IsUrgentDefenseAction(string action)
        {
            return string.Equals(action, "UrgentDefense", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "RequestUrgentDefense", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldObserveSettlement(Settlement settlement)
        {
            if (settlement == null)
            {
                return false;
            }

            if (!settlement.IsActive)
            {
                return false;
            }

            return settlement.IsTown || settlement.IsCastle;
        }

        private static bool HasActiveSiege(Settlement settlement)
        {
            return settlement != null && settlement.SiegeEvent != null;
        }

        private static bool ShouldRunFullDefensePipeline(DefenseEvaluationSnapshot snapshot)
        {
            return snapshot.ThreatReport.HasActiveSiege
                || snapshot.CoverageReport.HasDirectSiegeThreat
                || snapshot.CoverageReport.HasArmyPresence;
        }

        private void LogSettlementThreat(SettlementThreatReport report)
        {
            CsmLogger.Info(
                $"Observed settlement threat: tick={_observationTick}, settlement='{report.SettlementName}', owner='{report.OwnerKingdomName}', type={report.SettlementType}, garrisonStrength={report.GarrisonStrength:0.00}, nearbyEnemyStrength={report.NearbyEnemyStrength:0.00}, nearbyEnemyPartyCount={report.NearbyEnemyPartyCount}, nearbyFriendlyStrength={report.NearbyFriendlyStrength:0.00}, nearbyFriendlyPartyCount={report.NearbyFriendlyPartyCount}, strongestEnemyPartyName='{report.StrongestEnemyPartyName}', strongestEnemyStrength={report.StrongestEnemyStrength:0.00}, nearestEnemyPartyName='{report.NearestEnemyPartyName}', nearestEnemyDistance={report.NearestEnemyDistance:0.00}, hasActiveSiege={report.HasActiveSiege}, threatScore={report.ThreatScore:0.00}, siegeThreatScore={report.SiegeThreatScore:0.00}, armySiegeThreat={report.ArmySiegeThreat:0.00}, regionalEnemyPressure={report.RegionalEnemyPressure:0.00}, enemyLordPressure={report.EnemyLordPressure:0.00}, activeSiegeThreat={report.ActiveSiegeThreat:0.00}, nearbyEnemyLordPartyCount={report.NearbyEnemyLordPartyCount}, nearbyEnemyArmyMemberPartyCount={report.NearbyEnemyArmyMemberPartyCount}, nearbyEnemyArmyLeaderPartyCount={report.NearbyEnemyArmyLeaderPartyCount}, strongestEnemyArmyName='{report.StrongestEnemyArmyName}', strongestEnemyArmyStrength={report.StrongestEnemyArmyStrength:0.00}, strongestEnemyLordName='{report.StrongestEnemyLordName}', strongestEnemyLordStrength={report.StrongestEnemyLordStrength:0.00}, isThreatened={report.IsThreatened}");
        }

        private void LogSettlementValue(SettlementValueReport report)
        {
            CsmLogger.Info(
                $"Observed settlement value: tick={_observationTick}, settlement='{report.SettlementName}', owner='{report.OwnerKingdomName}', type={report.SettlementType}, prosperity={report.Prosperity:0.00}, garrisonStrength={report.GarrisonStrength:0.00}, strategicValue={report.StrategicValue:0.00}, reason='{report.ValueReason}'");
        }

        private void LogDefensePriority(DefensePriorityReport report)
        {
            CsmLogger.Info(
                $"Observed defense priority: tick={_observationTick}, settlement='{report.SettlementName}', owner='{report.OwnerKingdomName}', type={report.SettlementType}, threatScore={report.ThreatScore:0.00}, siegeThreatScore={report.SiegeThreatScore:0.00}, armySiegeThreat={report.ArmySiegeThreat:0.00}, regionalEnemyPressure={report.RegionalEnemyPressure:0.00}, strategicValue={report.StrategicValue:0.00}, threatComponent={report.ThreatComponent:0.00}, valueComponent={report.ValueComponent:0.00}, threatenedBonus={report.ThreatenedBonus:0.00}, defensePriority={report.DefensePriority:0.00}, hasActiveSiege={report.HasActiveSiege}, isThreatened={report.IsThreatened}, shouldRequestDefense={report.ShouldRequestDefense}, enemyPartyCount={report.EnemyPartyCount}, enemyArmyMemberPartyCount={report.EnemyArmyMemberPartyCount}, enemyArmyLeaderPartyCount={report.EnemyArmyLeaderPartyCount}, enemyLordPartyCount={report.EnemyLordPartyCount}, strongestEnemy='{report.StrongestEnemyPartyName}', strongestEnemyStrength={report.StrongestEnemyStrength:0.00}, nearestEnemy='{report.NearestEnemyPartyName}', nearestEnemyDistance={report.NearestEnemyDistance:0.00}, reason='{report.Reason}'");
        }

        private static bool ShouldLogDefenseCandidates(DefensePriorityReport report)
        {
            return report.ShouldRequestDefense
                || report.HasActiveSiege
                || report.EnemyArmyLeaderPartyCount > 0
                || report.DefensePriority >= 35f
                || report.IsThreatened;
        }

        private void LogDefenseCandidates(System.Collections.Generic.List<DefenseCandidateReport> reports)
        {
            for (var index = 0; index < reports.Count; index++)
            {
                var report = reports[index];
                CsmLogger.Info(
                    $"Observed defense candidate: tick={_observationTick}, settlement='{report.SettlementName}', candidate='{report.CandidatePartyName}', leader='{report.CandidateLeaderName}', category={report.CandidateCategory}, strength={report.CandidateStrength:0.00}, distance={report.DistanceToSettlement:0.00}, healthyTroops={report.HealthyTroopCount}, woundedTroops={report.WoundedTroopCount}, woundedRatio={report.WoundedRatio:0.00}, isArmyLeader={report.IsArmyLeader}, isArmyMember={report.IsArmyMember}, isWeak={report.IsWeak}, isTooFar={report.IsTooFar}, isBusy={report.IsBusy}, availabilityScore={report.AvailabilityScore:0.00}, suitabilityScore={report.SuitabilityScore:0.00}, isSuitable={report.IsSuitable}, reason='{report.Reason}'");
            }
        }

        private void LogDefenseCoverage(DefenseCoverageReport report)
        {
            CsmLogger.Info(
                $"Observed defense coverage: tick={_observationTick}, settlement='{report.SettlementName}', owner='{report.OwnerKingdomName}', type={report.SettlementType}, garrisonStrength={report.GarrisonStrength:0.00}, nearbyFriendlyStrength={report.NearbyFriendlyStrength:0.00}, suitableCandidateCount={report.SuitableCandidateCount}, suitableCandidateStrength={report.SuitableCandidateStrength:0.00}, totalAvailableDefenseStrength={report.TotalAvailableDefenseStrength:0.00}, threatScore={report.ThreatScore:0.00}, siegeThreatScore={report.SiegeThreatScore:0.00}, armySiegeThreat={report.ArmySiegeThreat:0.00}, regionalEnemyPressure={report.RegionalEnemyPressure:0.00}, defenseCoverageRatio={report.DefenseCoverageRatio:0.00}, hasDirectSiegeThreat={report.HasDirectSiegeThreat}, hasArmyPresence={report.HasArmyPresence}, hasRegionalPressure={report.HasRegionalPressure}, isDefenseEnough={report.IsDefenseEnough}, needsReinforcement={report.NeedsReinforcement}, reason='{report.Reason}'");
        }

        private void LogDefenseCoverageBreakdown(DefenseCoverageReport report)
        {
            CsmLogger.Info(
                $"Observed defense coverage breakdown: tick={_observationTick}, settlement='{report.SettlementName}', requiredThreatStrength={report.RequiredThreatStrength:0.00}, availableDefenseStrength={report.AvailableDefenseStrength:0.00}, garrisonDefenseStrength={report.GarrisonDefenseStrength:0.00}, nearbyFriendlyDefenseStrength={report.NearbyFriendlyDefenseStrength:0.00}, candidateDefenseStrength={report.CandidateDefenseStrength:0.00}, explicitDefenderStrength={report.ExplicitDefenderStrength:0.00}, explicitDefenderCount={report.ExplicitDefenderCount}, nearbyUncommittedStrength={report.NearbyUncommittedStrength:0.00}, nearbyUncommittedCount={report.NearbyUncommittedCount}, potentialCandidateStrength={report.PotentialCandidateStrength:0.00}, potentialCandidateCount={report.PotentialCandidateCount}, defenseIntentReason='{report.DefenseIntentReason}', enemySiegeThreatStrength={report.EnemySiegeThreatStrength:0.00}, nearbyEnemyThreatStrength={report.NearbyEnemyThreatStrength:0.00}, usedCandidateCount={report.UsedCandidateCount}, nearbyFriendlyPartyCount={report.NearbyFriendlyPartyCount}, nearbyEnemyPartyCount={report.NearbyEnemyPartyCount}, coverageRatio={report.DefenseCoverageRatio:0.00}, coverageStatus={GetCoverageStatus(report)}, reason='{report.Reason}'");
        }

        private static string GetCoverageStatus(DefenseCoverageReport report)
        {
            if (report.DefenseCoverageRatio <= DefenseActionThresholdSettings.UrgentDefenseCoverageRatioThreshold)
            {
                return "Critical";
            }

            if (report.DefenseCoverageRatio < DefenseActionThresholdSettings.ReinforcementCoverageRatioThreshold)
            {
                return "Low";
            }

            return report.IsDefenseEnough ? "Enough" : "Unknown";
        }

        private void LogDefenseNeed(DefenseNeedReport report)
        {
            CsmLogger.Info(
                $"Observed defense need: tick={_observationTick}, settlement='{report.SettlementName}', owner='{report.OwnerKingdomName}', type={report.SettlementType}, defensePriority={report.DefensePriority:0.00}, defenseCoverageRatio={report.DefenseCoverageRatio:0.00}, isThreatened={report.IsThreatened}, hasActiveSiege={report.HasActiveSiege}, hasDirectSiegeThreat={report.HasDirectSiegeThreat}, hasArmyPresence={report.HasArmyPresence}, needsReinforcement={report.NeedsReinforcement}, suitableCandidateCount={report.SuitableCandidateCount}, needsDefenseAction={report.NeedsDefenseAction}, recommendedAction='{report.RecommendedAction}', reason='{report.Reason}'");
        }

        private void LogDefenseActionPlan(DefenseActionPlan plan)
        {
            CsmLogger.Info(
                $"Observed defense action plan: tick={_observationTick}, settlement='{plan.SettlementName}', owner='{plan.OwnerKingdomName}', recommendedAction='{plan.RecommendedAction}', needsDefenseAction={plan.NeedsDefenseAction}, defensePriority={plan.DefensePriority:0.00}, defenseCoverageRatio={plan.DefenseCoverageRatio:0.00}, selectedCandidateCount={plan.SelectedCandidateCount}, selectedCandidateStrength={plan.SelectedCandidateStrength:0.00}, primaryCandidate='{plan.PrimaryCandidateName}', primaryCandidateCategory={plan.PrimaryCandidateCategory}, primaryCandidateStrength={plan.PrimaryCandidateStrength:0.00}, primaryCandidateDistance={plan.PrimaryCandidateDistance:0.00}, planConfidence={plan.PlanConfidence:0.00}, reason='{plan.Reason}'");
        }

        private DefenseActionPlanStabilityReport GetStabilityReport(DefenseActionPlan actionPlan)
        {
            if (!DefenseDryRunSettings.EnableDefenseActionHistory)
            {
                return new DefenseActionPlanStabilityReport(
                    actionPlan.SettlementName,
                    actionPlan.RecommendedAction,
                    "None",
                    0,
                    0,
                    0,
                    0,
                    false,
                    false,
                    false,
                    "Defense action history disabled");
            }

            _defenseActionPlanHistory.Record(actionPlan, _observationTick);
            var stabilityReport = _defenseActionPlanHistory.EvaluateStability(actionPlan, _observationTick);
            if (DefenseDiagnosticsSettings.EnableVerboseDefenseLogs)
            {
                LogDefenseActionStability(stabilityReport);
            }

            return stabilityReport;
        }

        private void LogDefenseActionStability(DefenseActionPlanStabilityReport report)
        {
            CsmLogger.Info(
                $"Observed defense action stability: tick={_observationTick}, settlement='{report.SettlementName}', currentAction='{report.CurrentRecommendedAction}', stableAction='{report.StableRecommendedAction}', consecutiveSameActionCount={report.ConsecutiveSameActionCount}, recentNeedsDefenseActionCount={report.RecentNeedsDefenseActionCount}, recentUrgentDefenseCount={report.RecentUrgentDefenseCount}, recentReinforceCount={report.RecentReinforceCount}, isStable={report.IsStable}, shouldEscalate={report.ShouldEscalate}, shouldDeescalate={report.ShouldDeescalate}, reason='{report.Reason}'");
        }

        private void LogDefenseSummary(DefenseDiagnosticsSummary summary)
        {
            CsmLogger.Info(
                $"Observed defense summary: tick={_observationTick}, settlement='{summary.SettlementName}', settlementType={summary.SettlementType}, owner='{summary.OwnerKingdomName}', threatType={summary.ThreatType}, action='{summary.RecommendedAction}', stableAction='{summary.StableRecommendedAction}', isStable={summary.IsStable}, shouldEscalate={summary.ShouldEscalate}, shouldDeescalate={summary.ShouldDeescalate}, priority={summary.DefensePriority:0.00}, coverageRatio={summary.DefenseCoverageRatio:0.00}, coverageStatus={summary.CoverageStatus}, primaryCandidate='{summary.PrimaryCandidateName}', primaryCandidateCategory={summary.PrimaryCandidateCategory}, confidence={summary.PlanConfidence:0.00}, reason='{summary.Reason}'");
        }

        private void LogDryRunDefenseDecision(DryRunDefenseDecision decision)
        {
            CsmLogger.Info(
                $"Observed dry-run defense decision: tick={_observationTick}, settlement='{decision.SettlementName}', owner='{decision.OwnerKingdomName}', wouldAct={decision.WouldAct}, wouldMonitor={decision.WouldMonitor}, wouldRequestReinforcement={decision.WouldRequestReinforcement}, wouldRequestUrgentDefense={decision.WouldRequestUrgentDefense}, action='{decision.Action}', stableAction='{decision.StableAction}', primaryCandidate='{decision.PrimaryCandidateName}', primaryCandidateCategory={decision.PrimaryCandidateCategory}, planConfidence={decision.PlanConfidence:0.00}, reason='{decision.Reason}'");
        }

        private void LogDryRunDefenseStability(DryRunDefenseDecisionStabilityReport report)
        {
            CsmLogger.Info(
                $"Observed dry-run defense stability: tick={_observationTick}, settlement='{report.SettlementName}', currentAction='{report.CurrentAction}', stableAction='{report.StableAction}', consecutiveSameActionCount={report.ConsecutiveSameActionCount}, recentWouldActCount={report.RecentWouldActCount}, recentMonitorCount={report.RecentMonitorCount}, recentReinforcementRequestCount={report.RecentReinforcementRequestCount}, recentUrgentDefenseRequestCount={report.RecentUrgentDefenseRequestCount}, isStable={report.IsStable}, hasStableWouldActSignal={report.HasStableWouldActSignal}, hasStableMonitorSignal={report.HasStableMonitorSignal}, reason='{report.Reason}'");
        }

        private void LogDefenseControllerDecision(DefenseControllerDecision decision)
        {
            CsmLogger.Info(
                $"Observed defense controller scaffold: tick={_observationTick}, settlement='{decision.SettlementName}', owner='{decision.OwnerKingdomName}', isEnabled={decision.IsEnabled}, wouldExecute={decision.WouldExecute}, action='{decision.Action}', dryRunAction='{decision.DryRunAction}', primaryCandidate='{decision.PrimaryCandidateName}', primaryCandidateCategory={decision.PrimaryCandidateCategory}, reason='{decision.Reason}'");
        }

        private void LogDefenseControllerSafety(DefenseControllerSafetyReport report)
        {
            CsmLogger.Info(
                $"Observed defense controller safety: tick={_observationTick}, settlement='{report.SettlementName}', owner='{report.OwnerKingdomName}', allowed={report.Allowed}, realControllerEnabled={report.RealControllerEnabled}, dryRunWouldAct={report.DryRunWouldAct}, hasStableWouldActSignal={report.HasStableWouldActSignal}, action='{report.Action}', dryRunAction='{report.DryRunAction}', primaryCandidate='{report.PrimaryCandidateName}', primaryCandidateCategory={report.PrimaryCandidateCategory}, reason='{report.Reason}'");
        }

        private void LogDefenseCommand(DefenseCommandReport report)
        {
            if (ShouldLogDefenseCommandGate(report))
            {
                LogDefenseCommandGate(report);
                return;
            }

            if (!ShouldLogFullDefenseCommand(report))
            {
                return;
            }

            CsmLogger.Info(
                $"Observed defense command: tick={_observationTick}, settlement='{report.SettlementName}', owner='{report.OwnerKingdomName}', commandType='{report.CommandType}', candidate='{report.CandidateName}', candidateCategory={report.CandidateCategory}, isAllowed={report.IsAllowed}, wasExecuted={report.WasExecuted}, reason='{report.Reason}'");
        }

        private static bool ShouldLogDefenseCommand(DefenseCommandReport report)
        {
            if (report.WasExecuted)
            {
                return true;
            }

            if (ShouldLogDefenseCommandGate(report))
            {
                return true;
            }

            return report.CommandType != "RequestReinforcement"
                && ShouldLogFullDefenseCommand(report);
        }

        private void LogDefenseCommandGate(DefenseCommandReport report)
        {
            CsmLogger.Info(
                $"Observed defense command gate: tick={_observationTick}, settlement='{report.SettlementName}', action='{report.CommandType}', candidate='{report.CandidateName}', allowed={report.IsAllowed}, reason='{report.Reason}'");
        }

        private static bool ShouldLogDefenseCommandGate(DefenseCommandReport report)
        {
            return !report.WasExecuted
                && !HasValidOwner(report.OwnerKingdomName)
                && report.CandidateCategory == PartyObservationCategory.Unknown
                && IsDefenseCommandGateReason(report.Reason);
        }

        private static bool ShouldLogFullDefenseCommand(DefenseCommandReport report)
        {
            return report.WasExecuted
                || (HasValidOwner(report.OwnerKingdomName)
                    && report.CandidateCategory != PartyObservationCategory.Unknown);
        }

        private static bool HasValidOwner(string ownerKingdomName)
        {
            return !string.IsNullOrWhiteSpace(ownerKingdomName)
                && !string.Equals(ownerKingdomName, "unknown", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDefenseCommandGateReason(string reason)
        {
            return string.Equals(reason, "Stable urgent defense signal required", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason, "Controller execution blocked", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason, "No stable urgent defense dry-run signal", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason, "Safety guard blocked execution", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason, "Coverage already sufficient", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason, "Existing defenders already cover threat", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason, "Candidate does not improve coverage", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason, "Candidate rejected by active CSM army assignment", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason, "Candidate rejected by active CSM defense assignment", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason, "Candidate too far for critical defense", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason, "Candidate too weak for urgent defense", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void LogDirectDefenseCommand(DirectDefenseCommandReport report)
        {
            CsmLogger.Info(
                $"Observed direct defense command: tick={report.ObservationTick}, settlement='{report.SettlementName}', candidate='{report.CandidateName}', commandApplied={report.CommandApplied}, reason='{report.Reason}'");
        }

        private static void LogDefenseAssignmentReports(System.Collections.Generic.List<CsmDefenseAssignmentReport> reports)
        {
            if (!DefenseAssignmentSettings.EnableAssignmentLogs || reports == null)
            {
                return;
            }

            for (var index = 0; index < reports.Count; index++)
            {
                var report = reports[index];
                CsmLogger.Info(
                    $"Observed defense assignment: tick={report.ObservationTick}, settlement='{report.SettlementName}', candidate='{report.PartyName}', status='{report.Status}', reason='{report.Reason}'");
            }
        }

        private void LogDefenseScoreSimulation(DefenseScoreSimulationReport report)
        {
            CsmLogger.Info(
                $"Observed defense score simulation: tick={_observationTick}, settlement='{report.SettlementName}', owner='{report.OwnerKingdomName}', candidate='{report.CandidateName}', candidateCategory={report.CandidateCategory}, threatType={report.ThreatType}, recommendedAction='{report.RecommendedAction}', coverageStatus={report.CoverageStatus}, defensePriority={report.DefensePriority:0.00}, planConfidence={report.PlanConfidence:0.00}, hypotheticalScore={report.HypotheticalScore:0.00}, wouldAddScore={report.WouldAddScore}, isBlockedBySafety={report.IsBlockedBySafety}, reason='{report.Reason}'");
        }

        private void LogDefenseScoreSimulationSummary(DefenseScoreSimulationDailySummary summary)
        {
            CsmLogger.Info(
                $"Observed defense score simulation summary: tick={summary.ObservationTick}, totalScoreSimulations={summary.TotalScoreSimulations}, blockedBySafetyCount={summary.BlockedBySafetyCount}, wouldAddScoreCount={summary.WouldAddScoreCount}, maxHypotheticalScore={summary.MaxHypotheticalScore:0.00}, averageHypotheticalScore={summary.AverageHypotheticalScore:0.00}, topScoreSettlement='{summary.TopScoreSettlementName}', topScoreCandidate='{summary.TopScoreCandidateName}', topScoreCandidateCategory={summary.TopScoreCandidateCategory}, topScoreRecommendedAction='{summary.TopScoreRecommendedAction}', topScoreReason='{summary.TopScoreReason}', reason='{summary.Reason}'");
        }

        private void LogExperimentalDefenseScoreInfluenceSummaryIfAvailable()
        {
            ExperimentalDefenseScoreInfluenceSummary summary;
            if (!_experimentalDefenseScoreInfluenceRegistry.TryConsumeCompletedSummary(out summary))
            {
                return;
            }

            CsmLogger.Info(
                $"Observed experimental defense score influence summary: tick={summary.ObservationTick}, attempted={summary.Attempted}, applied={summary.Applied}, suppressedDuplicateApplies={summary.SuppressedDuplicateApplies}, suppressedDuplicateLogs={summary.SuppressedDuplicateLogs}, reason='{summary.Reason}'");
        }

        private static void LogDefenseAssignmentLifecycle(CsmDefenseAssignmentLifecycleSummary summary)
        {
            CsmLogger.Info(
                $"Observed defense assignment lifecycle: tick={summary.ObservationTick}, activeAssignments={summary.ActiveAssignments}, created={summary.Created}, completed={summary.Completed}, deescalated={summary.Deescalated}, expired={summary.Expired}, invalid={summary.Invalid}, progressExpired={summary.ProgressExpired}, duplicateAssignmentBlocked={summary.DuplicateAssignmentBlocked}, reinforcementAssignmentsCreated={summary.ReinforcementAssignmentsCreated}, reinforcementBlocked={summary.ReinforcementBlocked}, movementReasserted={summary.MovementReasserted}, movementStalled={summary.MovementStalled}, movementInvalid={summary.MovementInvalid}, effectiveAssignments={summary.EffectiveAssignments}, ineffectiveAssignments={summary.IneffectiveAssignments}, replacementAssignmentsCreated={summary.ReplacementAssignmentsCreated}, deescalationDelayed={summary.DeescalationDelayed}, postEnforcementReasserted={summary.PostEnforcementReasserted}, reassertedAssignments={summary.ReassertedAssignments}, activeSiegeReasserted={summary.ActiveSiegeReasserted}, movementFailed={summary.MovementFailed}, adaptiveGlobalCapBlocked={summary.AdaptiveGlobalCapBlocked}, kingdomCapBlocked={summary.KingdomCapBlocked}, reason='{summary.Reason}'");
        }

        private static void LogDefensePostEnforcement(CsmDefensePostEnforcementReport report)
        {
            CsmLogger.Info(
                $"Observed defense assignment post-enforcement: tick={report.Tick}, reasserted={report.Reasserted}, stalled={report.Stalled}, reason='{report.Reason}'");
        }

        private DryRunDefenseDecisionStabilityReport GetDryRunStabilityReport(DryRunDefenseDecision decision)
        {
            if (!DefenseDryRunSettings.EnableDryRunDecisionHistory)
            {
                return new DryRunDefenseDecisionStabilityReport(
                    decision.SettlementName,
                    decision.Action,
                    "None",
                    0,
                    0,
                    0,
                    0,
                    0,
                    false,
                    false,
                    false,
                    "Dry-run decision history disabled");
            }

            _dryRunDefenseDecisionHistory.Record(decision, _observationTick);
            var stabilityReport = _dryRunDefenseDecisionHistory.EvaluateStability(decision, _observationTick);
            LogDryRunDefenseStability(stabilityReport);
            return stabilityReport;
        }

        private void LogDryRunDefenseDailyReport(DryRunDefenseDailyReport report)
        {
            CsmLogger.Info(
                $"Observed dry-run defense daily report: tick={report.ObservationTick}, totalEvaluatedSettlements={report.TotalEvaluatedSettlements}, ignoreCount={report.IgnoreCount}, monitorCount={report.MonitorCount}, waitCount={report.WaitCount}, requestReinforcementCount={report.RequestReinforcementCount}, requestUrgentDefenseCount={report.RequestUrgentDefenseCount}, wouldActCount={report.WouldActCount}, wouldMonitorCount={report.WouldMonitorCount}, stableWouldActSignalCount={report.StableWouldActSignalCount}, stableMonitorSignalCount={report.StableMonitorSignalCount}, topActionSettlement='{report.TopActionSettlementName}', topAction='{report.TopAction}', topActionConfidence={report.TopActionConfidence:0.00}, reason='{report.Reason}'");
        }

        private static string GetPartyName(MobileParty party)
        {
            if (party == null || party.Name == null)
            {
                return "unknown";
            }

            return party.Name.ToString();
        }

        private static string GetSettlementName(Settlement settlement)
        {
            if (settlement == null || settlement.Name == null)
            {
                return "unknown";
            }

            return settlement.Name.ToString();
        }
    }
}
