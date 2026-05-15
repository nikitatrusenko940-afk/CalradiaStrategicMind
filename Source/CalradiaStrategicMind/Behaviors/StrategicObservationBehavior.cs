using CalradiaStrategicMind.Logging;
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
            if (_nextSettlementIndex < 0 || _nextSettlementIndex >= settlements.Count)
            {
                _nextSettlementIndex = 0;
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
                if (!ShouldObserveSettlement(settlement))
                {
                    continue;
                }

                var snapshot = _defenseEvaluationSnapshotBuilder.Build(settlement, MaxDefenseCandidatesPerSettlement);
                LogSettlementThreat(snapshot.ThreatReport);
                LogSettlementValue(snapshot.ValueReport);
                LogDefensePriority(snapshot.PriorityReport);
                if (ShouldLogDefenseCandidates(snapshot.PriorityReport))
                {
                    LogDefenseCandidates(snapshot.CandidateReports);
                    LogDefenseCoverage(snapshot.CoverageReport);
                    LogDefenseNeed(snapshot.NeedReport);
                    var actionPlan = _defenseActionPlanner.CreatePlan(snapshot);
                    LogDefenseActionPlan(actionPlan);
                    _defenseActionPlanHistory.Record(actionPlan, _observationTick);
                    var stabilityReport = _defenseActionPlanHistory.EvaluateStability(actionPlan, _observationTick);
                    LogDefenseActionStability(stabilityReport);
                    var summary = _defenseDiagnosticsSummaryBuilder.Build(snapshot, actionPlan, stabilityReport);
                    LogDefenseSummary(summary);
                }
                observedCount++;
            }
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

        private void LogDefenseActionStability(DefenseActionPlanStabilityReport report)
        {
            CsmLogger.Info(
                $"Observed defense action stability: tick={_observationTick}, settlement='{report.SettlementName}', currentAction='{report.CurrentRecommendedAction}', stableAction='{report.StableRecommendedAction}', consecutiveSameActionCount={report.ConsecutiveSameActionCount}, recentNeedsDefenseActionCount={report.RecentNeedsDefenseActionCount}, recentUrgentDefenseCount={report.RecentUrgentDefenseCount}, recentReinforceCount={report.RecentReinforceCount}, isStable={report.IsStable}, shouldEscalate={report.ShouldEscalate}, shouldDeescalate={report.ShouldDeescalate}, reason='{report.Reason}'");
        }

        private void LogDefenseSummary(DefenseDiagnosticsSummary summary)
        {
            CsmLogger.Info(
                $"Observed defense summary: tick={_observationTick}, settlement='{summary.SettlementName}', owner='{summary.OwnerKingdomName}', threatType={summary.ThreatType}, action='{summary.RecommendedAction}', stableAction='{summary.StableRecommendedAction}', isStable={summary.IsStable}, shouldEscalate={summary.ShouldEscalate}, shouldDeescalate={summary.ShouldDeescalate}, priority={summary.DefensePriority:0.00}, coverageRatio={summary.DefenseCoverageRatio:0.00}, coverageStatus={summary.CoverageStatus}, primaryCandidate='{summary.PrimaryCandidateName}', primaryCandidateCategory={summary.PrimaryCandidateCategory}, confidence={summary.PlanConfidence:0.00}, reason='{summary.Reason}'");
        }

        private static string GetPartyName(MobileParty party)
        {
            if (party == null || party.Name == null)
            {
                return "unknown";
            }

            return party.Name.ToString();
        }
    }
}
