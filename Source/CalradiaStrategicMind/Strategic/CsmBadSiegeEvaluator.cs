using System.Collections.Generic;
using CalradiaStrategicMind.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmBadSiegeEvaluator
    {
        private readonly CsmArmyAttackTargetScorer _targetScorer;

        public CsmBadSiegeEvaluator(CsmArmyAttackTargetScorer targetScorer)
        {
            _targetScorer = targetScorer;
        }

        public CsmBadSiegeEvaluation Evaluate(
            CsmArmySnapshot snapshot,
            CsmArmyObjectiveSnapshot objective,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry)
        {
            var result = new CsmBadSiegeEvaluation
            {
                CurrentTarget = objective?.CurrentTargetSettlement,
                CurrentTargetName = objective?.CurrentTargetSettlementName ?? "none",
                ArmyStrength = snapshot.TotalStrength,
                Cohesion = snapshot.Cohesion,
                Reason = "No readable siege target"
            };

            if (objective == null || !objective.IsReadable || objective.CurrentTargetSettlement == null)
            {
                return result;
            }

            if (objective.CurrentObjective != "BesiegingSettlement" && objective.CurrentObjective != "MovingToBesiegeSettlement")
            {
                result.Reason = "Army is not besieging or moving to besiege";
                return result;
            }

            var target = objective.CurrentTargetSettlement;
            var kingdom = snapshot.LeaderParty?.MapFaction as Kingdom;
            if (kingdom == null || target.MapFaction == null || !kingdom.IsAtWarWith(target.MapFaction))
            {
                result.Reason = "Siege target is no longer enemy";
                return result;
            }

            result.TargetDefenseStrength = GetSettlementDefenseStrength(target);
            if (result.TargetDefenseStrength <= 0f)
            {
                result.TargetDefenseStrength = 1f;
            }

            result.StrengthRatio = result.ArmyStrength / result.TargetDefenseStrength;
            var score = _targetScorer.ScoreTarget(kingdom, snapshot.LeaderParty, snapshot.TotalStrength, target, defenseSnapshots, registry);
            result.NearbyEnemyArmyStrength = score == null ? 0f : score.NearbyEnemyArmyStrength;

            if (result.StrengthRatio < ArmyDirectorSettings.BadSiegeRequiredStrengthRatio)
            {
                result.IsBadSiege = true;
                result.Reason = "Army strength ratio is below bad siege threshold";
                return result;
            }

            if (snapshot.Cohesion < ArmyDirectorSettings.LowCohesionThreshold)
            {
                result.IsBadSiege = true;
                result.Reason = "Army cohesion is below low cohesion threshold";
                return result;
            }

            if (result.NearbyEnemyArmyStrength > snapshot.TotalStrength)
            {
                result.IsBadSiege = true;
                result.Reason = "Nearby enemy army strength is too high";
                return result;
            }

            if (target.Position.Distance(snapshot.LeaderParty.Position) > ArmyDirectorSettings.ExtendedFrontlineAttackTargetDistance)
            {
                result.IsBadSiege = true;
                result.Reason = "Siege target is too far";
                return result;
            }

            if (score != null && score.IsActiveDefenseTarget)
            {
                result.IsBadSiege = true;
                result.Reason = "Siege target conflicts with active defense target";
                return result;
            }

            if (score != null && score.IsAlreadyAssigned)
            {
                result.IsBadSiege = true;
                result.Reason = "Siege target is already assigned to CSM attack";
                return result;
            }

            if (score != null && !_targetScorer.IsPassed(score))
            {
                result.IsBadSiege = true;
                result.Reason = "Siege target no longer passes Army Target Scoring";
                return result;
            }

            result.Reason = "Current siege is acceptable";
            return result;
        }

        private static float GetSettlementDefenseStrength(Settlement settlement)
        {
            var strength = 0f;
            if (settlement?.Town?.GarrisonParty != null)
            {
                strength += settlement.Town.GarrisonParty.Party.EstimatedStrength;
            }

            if (settlement?.MilitiaPartyComponent?.MobileParty != null)
            {
                strength += settlement.MilitiaPartyComponent.MobileParty.Party.EstimatedStrength;
            }

            return strength;
        }
    }
}
