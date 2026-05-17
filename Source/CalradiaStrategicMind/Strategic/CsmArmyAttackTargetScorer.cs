using System.Collections.Generic;
using CalradiaStrategicMind.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmArmyAttackTargetScorer
    {
        public CsmArmyAttackTargetScore FindBestTarget(
            Kingdom kingdom,
            MobileParty leaderParty,
            float estimatedAttackStrength,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry)
        {
            if (!ArmyDirectorSettings.EnableArmyTargetScoring || kingdom == null || leaderParty == null)
            {
                return null;
            }

            var settlements = Settlement.All;
            if (settlements == null)
            {
                return null;
            }

            CsmArmyAttackTargetScore best = null;
            for (var index = 0; index < settlements.Count; index++)
            {
                var score = ScoreTarget(kingdom, leaderParty, estimatedAttackStrength, settlements[index], defenseSnapshots, registry);
                if (score == null || !IsPassed(score))
                {
                    continue;
                }

                if (best == null || score.Score > best.Score)
                {
                    best = score;
                }
            }

            return best;
        }

        public CsmArmyAttackTargetScore FindBestRejectedTarget(
            Kingdom kingdom,
            MobileParty leaderParty,
            float estimatedAttackStrength,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry)
        {
            if (!ArmyDirectorSettings.EnableArmyTargetScoring || kingdom == null || leaderParty == null)
            {
                return null;
            }

            var settlements = Settlement.All;
            if (settlements == null)
            {
                return null;
            }

            CsmArmyAttackTargetScore bestRejected = null;
            for (var index = 0; index < settlements.Count; index++)
            {
                var score = ScoreTarget(kingdom, leaderParty, estimatedAttackStrength, settlements[index], defenseSnapshots, registry);
                if (score == null || IsPassed(score))
                {
                    continue;
                }

                if (bestRejected == null || score.Score > bestRejected.Score)
                {
                    bestRejected = score;
                }
            }

            return bestRejected;
        }

        public CsmArmyAttackTargetScore ScoreTarget(
            Kingdom kingdom,
            MobileParty leaderParty,
            float estimatedAttackStrength,
            Settlement target,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry)
        {
            if (kingdom == null || leaderParty == null || target == null)
            {
                return null;
            }

            if (!target.IsFortification || (!target.IsCastle && !target.IsTown))
            {
                return null;
            }

            if (target.MapFaction == null || target.MapFaction == kingdom || !kingdom.IsAtWarWith(target.MapFaction))
            {
                return null;
            }

            var result = new CsmArmyAttackTargetScore();
            result.Target = target;
            result.TargetName = GetSettlementName(target);
            result.Distance = target.Position.Distance(leaderParty.Position);
            result.TargetDefenseStrength = GetSettlementDefenseStrength(target);
            if (result.TargetDefenseStrength <= 0f)
            {
                result.TargetDefenseStrength = 1f;
            }

            result.EstimatedAttackStrength = estimatedAttackStrength;
            result.StrengthRatio = estimatedAttackStrength / result.TargetDefenseStrength;
            result.NearbyEnemyArmyStrength = GetNearbyArmyStrength(target, kingdom, true);
            result.NearbyFriendlySupportStrength = GetNearbyFriendlySupportStrength(target, kingdom, leaderParty);
            result.IsCastle = target.IsCastle;
            result.IsTown = target.IsTown;
            result.IsFrontlineCandidate = IsFrontlineCandidate(target, kingdom);
            result.IsAlreadyAssigned = registry != null && registry.GetActiveAssignmentForTarget("AttackSettlement", target.StringId, result.TargetName) != null;
            result.IsAlreadyBesieged = IsAlreadyBesiegedByFriendly(target, kingdom);
            result.IsActiveDefenseTarget = IsActiveDefenseTarget(target, defenseSnapshots);
            result.Score = CalculateScore(result);
            result.UsesExtendedFrontlineRule = UsesExtendedFrontlineRule(result);
            result.DistanceLimit = result.UsesExtendedFrontlineRule ? ArmyDirectorSettings.ExtendedFrontlineAttackTargetDistance : ArmyDirectorSettings.MaxAttackTargetDistance;
            result.Reason = BuildReason(result);
            return result;
        }

        public bool IsPassed(CsmArmyAttackTargetScore score)
        {
            return score != null
                && score.Target != null
                && (score.Distance <= ArmyDirectorSettings.MaxAttackTargetDistance || UsesExtendedFrontlineRule(score))
                && score.Score >= ArmyDirectorSettings.MinimumAttackTargetScore
                && score.StrengthRatio >= ArmyDirectorSettings.GoodAttackRequiredStrengthRatio
                && !score.IsAlreadyAssigned
                && !score.IsAlreadyBesieged
                && !score.IsActiveDefenseTarget;
        }

        private static float CalculateScore(CsmArmyAttackTargetScore score)
        {
            var value = 50f;
            value += score.IsCastle ? ArmyDirectorSettings.CastleTargetBonus : 0f;
            value += score.IsTown ? ArmyDirectorSettings.TownTargetBonus : 0f;
            value += score.IsFrontlineCandidate ? ArmyDirectorSettings.FrontlineTargetBonus : -ArmyDirectorSettings.DeepEnemyTerritoryPenalty;
            value += score.StrengthRatio * ArmyDirectorSettings.StrengthRatioScoreMultiplier;
            value += score.NearbyFriendlySupportStrength * ArmyDirectorSettings.NearbyFriendlySupportBonusMultiplier;
            value -= score.Distance * ArmyDirectorSettings.DistancePenaltyPerMapUnit;
            value -= score.TargetDefenseStrength * ArmyDirectorSettings.TargetDefensePenaltyMultiplier;
            value -= score.NearbyEnemyArmyStrength * ArmyDirectorSettings.NearbyEnemyArmyPenaltyMultiplier;

            if (score.IsTown && ArmyDirectorSettings.PreferCastlesForAlpha && score.StrengthRatio < ArmyDirectorSettings.GoodAttackRequiredStrengthRatio * 1.5f)
            {
                value -= ArmyDirectorSettings.TownTooStrongPenalty;
            }

            if (score.IsAlreadyBesieged)
            {
                value -= ArmyDirectorSettings.AlreadyBesiegedPenalty;
            }

            if (score.IsActiveDefenseTarget)
            {
                value -= ArmyDirectorSettings.ActiveDefenseTargetPenalty;
            }

            if (score.IsAlreadyAssigned)
            {
                value -= ArmyDirectorSettings.AlreadyBesiegedPenalty;
            }

            if (score.StrengthRatio < ArmyDirectorSettings.GoodAttackRequiredStrengthRatio)
            {
                value -= 100f;
            }

            return value;
        }

        private static string BuildReason(CsmArmyAttackTargetScore score)
        {
            if (score.IsActiveDefenseTarget)
            {
                return "Rejected active defense target";
            }

            if (score.IsAlreadyAssigned)
            {
                return "Rejected active CSM attack assignment target";
            }

            if (score.IsAlreadyBesieged)
            {
                return "Rejected already besieged target";
            }

            if (score.StrengthRatio < ArmyDirectorSettings.GoodAttackRequiredStrengthRatio)
            {
                return "Rejected target because attack strength ratio is too low";
            }

            if (UsesExtendedFrontlineRule(score))
            {
                return "Selected extended frontline attack target by Army Target Scoring";
            }

            if (score.Distance > ArmyDirectorSettings.MaxAttackTargetDistance)
            {
                return IsAlmostExtendedFrontlineTarget(score)
                    ? "Rejected target because extended frontline requirements were not met"
                    : "Rejected target because distance is too high";
            }

            return score.IsFrontlineCandidate ? "Selected frontline attack target by Army Target Scoring" : "Selected deep attack target by Army Target Scoring";
        }

        private static bool UsesExtendedFrontlineRule(CsmArmyAttackTargetScore score)
        {
            return score != null
                && ArmyDirectorSettings.AllowExtendedFrontlineTargets
                && score.IsFrontlineCandidate
                && score.Distance > ArmyDirectorSettings.MaxAttackTargetDistance
                && score.Distance <= ArmyDirectorSettings.ExtendedFrontlineAttackTargetDistance
                && score.StrengthRatio >= ArmyDirectorSettings.ExtendedFrontlineRequiredStrengthRatio
                && score.Score >= ArmyDirectorSettings.ExtendedFrontlineMinimumScore;
        }

        private static bool IsAlmostExtendedFrontlineTarget(CsmArmyAttackTargetScore score)
        {
            return score != null
                && ArmyDirectorSettings.AllowExtendedFrontlineTargets
                && score.IsFrontlineCandidate
                && score.Distance > ArmyDirectorSettings.MaxAttackTargetDistance
                && score.Distance <= ArmyDirectorSettings.ExtendedFrontlineAttackTargetDistance;
        }

        private static bool IsAlreadyBesiegedByFriendly(Settlement settlement, Kingdom kingdom)
        {
            if (settlement?.SiegeEvent == null || settlement.SiegeEvent.BesiegerCamp == null)
            {
                return false;
            }

            return settlement.SiegeEvent.BesiegerCamp.MapFaction == kingdom;
        }

        private static bool IsActiveDefenseTarget(Settlement settlement, List<DefenseEvaluationSnapshot> defenseSnapshots)
        {
            if (settlement == null || defenseSnapshots == null)
            {
                return false;
            }

            var settlementName = GetSettlementName(settlement);
            for (var index = 0; index < defenseSnapshots.Count; index++)
            {
                var defense = defenseSnapshots[index];
                if (defense.ThreatReport.SettlementName == settlementName
                    && (defense.ThreatReport.HasActiveSiege || defense.CoverageReport.HasDirectSiegeThreat || defense.CoverageReport.HasArmyPresence))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFrontlineCandidate(Settlement target, Kingdom kingdom)
        {
            var settlements = Settlement.All;
            if (target == null || kingdom == null || settlements == null)
            {
                return false;
            }

            for (var index = 0; index < settlements.Count; index++)
            {
                var settlement = settlements[index];
                if (settlement == null || !settlement.IsFortification || settlement.MapFaction != kingdom)
                {
                    continue;
                }

                if (settlement.Position.Distance(target.Position) <= ArmyDirectorSettings.MaxFrontlineDistanceFromFriendlyFortification)
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetNearbyArmyStrength(Settlement target, Kingdom kingdom, bool enemyOnly)
        {
            var strength = 0f;
            var kingdoms = Kingdom.All;
            if (target == null || kingdom == null || kingdoms == null)
            {
                return strength;
            }

            for (var kingdomIndex = 0; kingdomIndex < kingdoms.Count; kingdomIndex++)
            {
                var currentKingdom = kingdoms[kingdomIndex];
                if (currentKingdom == null || currentKingdom.Armies == null)
                {
                    continue;
                }

                for (var armyIndex = 0; armyIndex < currentKingdom.Armies.Count; armyIndex++)
                {
                    var army = currentKingdom.Armies[armyIndex];
                    var leader = army?.LeaderParty;
                    if (leader == null || leader.MapEvent != null)
                    {
                        continue;
                    }

                    if (leader.Position.Distance(target.Position) > ArmyDirectorSettings.MaxNearbyEnemyArmyScanDistance)
                    {
                        continue;
                    }

                    var isEnemy = leader.MapFaction != null && kingdom.IsAtWarWith(leader.MapFaction);
                    if (enemyOnly != isEnemy)
                    {
                        continue;
                    }

                    strength += army.EstimatedStrength;
                }
            }

            return strength;
        }

        private static float GetNearbyFriendlySupportStrength(Settlement target, Kingdom kingdom, MobileParty leaderParty)
        {
            var strength = 0f;
            var parties = MobileParty.All;
            if (target == null || kingdom == null || parties == null)
            {
                return strength;
            }

            for (var index = 0; index < parties.Count; index++)
            {
                var party = parties[index];
                if (party == null || party == leaderParty || !party.IsActive || party.IsDisbanding || party.MapEvent != null || party.MapFaction != kingdom)
                {
                    continue;
                }

                if (party.Position.Distance(target.Position) <= ArmyDirectorSettings.MaxNearbyFriendlySupportScanDistance)
                {
                    strength += party.Party.EstimatedStrength;
                }
            }

            return strength;
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

        private static string GetSettlementName(Settlement settlement)
        {
            return settlement?.Name == null ? "unknown" : settlement.Name.ToString();
        }
    }
}
