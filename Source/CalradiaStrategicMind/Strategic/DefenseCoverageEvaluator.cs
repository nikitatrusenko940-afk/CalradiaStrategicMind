using System.Collections.Generic;
using CalradiaStrategicMind.Settings;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class DefenseCoverageEvaluator
    {
        private const float NearbyPartySearchRadius = 80f;
        private const float RegionalPressureWeight = 0.25f;

        private readonly SettlementThreatEvaluator _settlementThreatEvaluator;
        private readonly DefenseCandidateSelector _defenseCandidateSelector;
        private readonly PartyStrengthEvaluator _partyStrengthEvaluator;
        private readonly PartyClassifier _partyClassifier;
        private readonly DefenseIntentEvaluator _defenseIntentEvaluator;

        public DefenseCoverageEvaluator()
        {
            _settlementThreatEvaluator = new SettlementThreatEvaluator();
            _defenseCandidateSelector = new DefenseCandidateSelector();
            _partyStrengthEvaluator = new PartyStrengthEvaluator();
            _partyClassifier = new PartyClassifier();
            _defenseIntentEvaluator = new DefenseIntentEvaluator();
        }

        public DefenseCoverageReport EvaluateDefenseCoverage(Settlement settlement)
        {
            return SafeExecutor.Run("Evaluate defense coverage", () => EvaluateDefenseCoverageCore(settlement), DefenseCoverageReport.Empty);
        }

        public DefenseCoverageReport EvaluateDefenseCoverage(
            Settlement settlement,
            SettlementThreatReport threatReport,
            List<DefenseCandidateReport> candidates)
        {
            return SafeExecutor.Run(
                "Evaluate defense coverage from reports",
                () => EvaluateDefenseCoverageCore(settlement, threatReport, candidates),
                DefenseCoverageReport.Empty);
        }

        private DefenseCoverageReport EvaluateDefenseCoverageCore(Settlement settlement)
        {
            if (settlement == null)
            {
                return DefenseCoverageReport.Empty;
            }

            var threatReport = _settlementThreatEvaluator.EvaluateSettlementThreat(settlement);
            var candidates = _defenseCandidateSelector.FindDefenseCandidates(settlement, 0);
            return EvaluateDefenseCoverageCore(settlement, threatReport, candidates);
        }

        private DefenseCoverageReport EvaluateDefenseCoverageCore(
            Settlement settlement,
            SettlementThreatReport threatReport,
            List<DefenseCandidateReport> candidates)
        {
            if (settlement == null)
            {
                return DefenseCoverageReport.Empty;
            }

            var suitableCandidateCount = 0;
            var suitableCandidateStrength = 0f;
            if (candidates != null)
            {
                for (var index = 0; index < candidates.Count; index++)
                {
                    var candidate = candidates[index];
                    if (!candidate.IsSuitable)
                    {
                        continue;
                    }

                    suitableCandidateCount++;
                    suitableCandidateStrength += candidate.CandidateStrength;
                }
            }

            var garrisonDefenseStrength = GetSettlementDefenseStrength(settlement);
            var explicitDefenseReport = EvaluateExplicitDefenders(settlement);
            var currentDefenseStrength = garrisonDefenseStrength + explicitDefenseReport.ExplicitDefenderStrength;
            var enemySiegeThreatStrength = GetDirectThreat(threatReport);
            var nearbyEnemyThreatStrength = threatReport.RegionalEnemyPressure * RegionalPressureWeight;
            var relevantThreat = enemySiegeThreatStrength + nearbyEnemyThreatStrength;
            var defenseCoverageRatio = relevantThreat > 0f
                ? currentDefenseStrength / relevantThreat
                : 999f;
            var hasDirectSiegeThreat = threatReport.HasActiveSiege || threatReport.SiegeThreatScore > 0f;
            var hasArmyPresence = threatReport.ArmySiegeThreat > 0f;
            var hasRegionalPressure = threatReport.RegionalEnemyPressure > 0f;
            var isDefenseEnough = !hasDirectSiegeThreat || defenseCoverageRatio >= DefenseActionThresholdSettings.ReinforcementCoverageRatioThreshold;
            var needsReinforcement = hasDirectSiegeThreat && defenseCoverageRatio < DefenseActionThresholdSettings.ReinforcementCoverageRatioThreshold;

            return new DefenseCoverageReport(
                threatReport.SettlementName,
                threatReport.OwnerKingdomName,
                threatReport.SettlementType,
                threatReport.GarrisonStrength,
                threatReport.NearbyFriendlyStrength,
                suitableCandidateCount,
                suitableCandidateStrength,
                currentDefenseStrength,
                threatReport.ThreatScore,
                threatReport.SiegeThreatScore,
                threatReport.ArmySiegeThreat,
                threatReport.RegionalEnemyPressure,
                defenseCoverageRatio,
                relevantThreat,
                currentDefenseStrength,
                garrisonDefenseStrength,
                threatReport.NearbyFriendlyStrength,
                suitableCandidateStrength,
                explicitDefenseReport.ExplicitDefenderStrength,
                explicitDefenseReport.ExplicitDefenderCount,
                explicitDefenseReport.NearbyUncommittedStrength,
                explicitDefenseReport.NearbyUncommittedCount,
                suitableCandidateStrength,
                suitableCandidateCount,
                explicitDefenseReport.DefenseIntentReason,
                enemySiegeThreatStrength,
                nearbyEnemyThreatStrength,
                suitableCandidateCount,
                threatReport.NearbyFriendlyPartyCount,
                threatReport.NearbyEnemyPartyCount,
                hasDirectSiegeThreat,
                hasArmyPresence,
                hasRegionalPressure,
                isDefenseEnough,
                needsReinforcement,
                GetReason(threatReport, hasDirectSiegeThreat, hasArmyPresence, hasRegionalPressure, defenseCoverageRatio, suitableCandidateCount));
        }

        private ExplicitDefenseReport EvaluateExplicitDefenders(Settlement settlement)
        {
            var parties = MobileParty.All;
            if (settlement == null || parties == null)
            {
                return new ExplicitDefenseReport(0f, 0, 0f, 0, "No readable defense intent found");
            }

            var explicitDefenderStrength = 0f;
            var explicitDefenderCount = 0;
            var nearbyUncommittedStrength = 0f;
            var nearbyUncommittedCount = 0;
            var defenseIntentReason = "No readable defense intent found";
            var hasExplicitDefender = false;
            var ownerFaction = settlement.MapFaction;
            var settlementPosition = settlement.GetPosition2D;

            for (var index = 0; index < parties.Count; index++)
            {
                var party = parties[index];
                var category = _partyClassifier.GetCategory(party);
                if (!ShouldCountFriendlyParty(party, category))
                {
                    continue;
                }

                if (!IsFriendly(ownerFaction, party.MapFaction))
                {
                    continue;
                }

                var distance = settlementPosition.Distance(party.GetPosition2D);
                var isInsideSettlement = party.CurrentSettlement == settlement;
                if (distance > NearbyPartySearchRadius && !isInsideSettlement)
                {
                    continue;
                }

                var partyStrength = _partyStrengthEvaluator.EvaluatePartyStrength(party);
                var weightedStrength = partyStrength * GetDistanceWeight(isInsideSettlement ? 0f : distance);
                var intentReport = _defenseIntentEvaluator.EvaluateDefenseIntent(party, settlement);
                if (intentReport.IsExplicitDefender)
                {
                    explicitDefenderStrength += weightedStrength;
                    explicitDefenderCount++;
                    hasExplicitDefender = true;
                    defenseIntentReason = intentReport.Reason;
                    continue;
                }

                nearbyUncommittedStrength += weightedStrength;
                nearbyUncommittedCount++;
                if (!hasExplicitDefender)
                {
                    defenseIntentReason = intentReport.Reason;
                }
            }

            // TODO: Later CSM-owned defense assignments should be tracked separately in a runtime assignment registry.
            // Vanilla defense intent must not be the only long-term source of truth.
            if (hasExplicitDefender && nearbyUncommittedCount > 0)
            {
                defenseIntentReason = "Explicit defenders found; nearby uncommitted parties ignored";
            }

            return new ExplicitDefenseReport(
                explicitDefenderStrength,
                explicitDefenderCount,
                nearbyUncommittedStrength,
                nearbyUncommittedCount,
                defenseIntentReason);
        }

        private float GetSettlementDefenseStrength(Settlement settlement)
        {
            if (settlement == null)
            {
                return 0f;
            }

            var strength = 0f;
            if (settlement.Town != null && settlement.Town.GarrisonParty != null)
            {
                strength += _partyStrengthEvaluator.EvaluatePartyStrength(settlement.Town.GarrisonParty);
            }

            var militiaParty = settlement.MilitiaPartyComponent?.MobileParty;
            if (militiaParty != null)
            {
                strength += _partyStrengthEvaluator.EvaluatePartyStrength(militiaParty);
            }

            return strength;
        }

        private static string GetReason(
            SettlementThreatReport threatReport,
            bool hasDirectSiegeThreat,
            bool hasArmyPresence,
            bool hasRegionalPressure,
            float defenseCoverageRatio,
            int suitableCandidateCount)
        {
            if (!hasDirectSiegeThreat)
            {
                if (hasArmyPresence)
                {
                    return "Army presence covered by local defense";
                }

                if (hasRegionalPressure)
                {
                    return "Regional pressure covered by available defense";
                }

                return suitableCandidateCount > 0
                    ? "No direct siege threat; candidates available"
                    : "No direct siege threat";
            }

            if (defenseCoverageRatio <= DefenseActionThresholdSettings.UrgentDefenseCoverageRatioThreshold)
            {
                return threatReport.HasActiveSiege
                    ? "Coverage critical against active siege"
                    : "Coverage critical against army siege threat";
            }

            if (defenseCoverageRatio < DefenseActionThresholdSettings.ReinforcementCoverageRatioThreshold)
            {
                return threatReport.HasActiveSiege
                    ? "Coverage low against active siege"
                    : "Coverage low against army siege threat";
            }

            return threatReport.HasActiveSiege
                ? "Coverage sufficient against active siege"
                : "Coverage sufficient against army siege threat";
        }

        private static float GetDirectThreat(SettlementThreatReport threatReport)
        {
            return threatReport.SiegeThreatScore > 0f
                ? threatReport.SiegeThreatScore
                : threatReport.ArmySiegeThreat;
        }

        private static bool ShouldCountFriendlyParty(MobileParty party, PartyObservationCategory category)
        {
            if (party == null)
            {
                return false;
            }

            if (!party.IsActive || party.IsDisbanding || party.IsGarrison || party.IsMilitia)
            {
                return false;
            }

            if (party.MemberRoster == null || party.MemberRoster.TotalManCount <= 0)
            {
                return false;
            }

            return category == PartyObservationCategory.LordParty
                || category == PartyObservationCategory.ArmyParty;
        }

        private static bool IsFriendly(IFaction ownerFaction, IFaction partyFaction)
        {
            if (ownerFaction == null || partyFaction == null)
            {
                return false;
            }

            return ownerFaction == partyFaction || ownerFaction == partyFaction.MapFaction;
        }

        private static float GetDistanceWeight(float distance)
        {
            if (distance < 0f)
            {
                distance = 0f;
            }

            var weight = 1f - distance / NearbyPartySearchRadius;
            return weight < 0.25f ? 0.25f : weight;
        }

        private struct ExplicitDefenseReport
        {
            public ExplicitDefenseReport(
                float explicitDefenderStrength,
                int explicitDefenderCount,
                float nearbyUncommittedStrength,
                int nearbyUncommittedCount,
                string defenseIntentReason)
            {
                ExplicitDefenderStrength = explicitDefenderStrength;
                ExplicitDefenderCount = explicitDefenderCount;
                NearbyUncommittedStrength = nearbyUncommittedStrength;
                NearbyUncommittedCount = nearbyUncommittedCount;
                DefenseIntentReason = defenseIntentReason;
            }

            public float ExplicitDefenderStrength { get; private set; }

            public int ExplicitDefenderCount { get; private set; }

            public float NearbyUncommittedStrength { get; private set; }

            public int NearbyUncommittedCount { get; private set; }

            public string DefenseIntentReason { get; private set; }
        }
    }
}
