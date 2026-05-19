using System.Collections.Generic;
using CalradiaStrategicMind.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Strategic
{
    public class CsmArmyAttackTargetScorer
    {
        private readonly CsmArmyTargetScoringSummary _summary;

        public CsmArmyAttackTargetScorer()
        {
            _summary = new CsmArmyTargetScoringSummary();
        }

        public void ResetSummary()
        {
            _summary.EvaluatedTargets = 0;
            _summary.SelectedTargets = 0;
            _summary.HardRejectedTargets = 0;
            _summary.IrrelevantRejectedTargets = 0;
            _summary.TacticalRejectedTargets = 0;
            _summary.RejectedActiveDefenseTargets = 0;
            _summary.RejectedOverextendedTargets = 0;
            _summary.RejectedLowStrengthTargets = 0;
            _summary.RecentlyFailedTargetPenalties = 0;
        }

        public CsmArmyTargetScoringSummary SnapshotSummary()
        {
            return new CsmArmyTargetScoringSummary
            {
                EvaluatedTargets = _summary.EvaluatedTargets,
                SelectedTargets = _summary.SelectedTargets,
                HardRejectedTargets = _summary.HardRejectedTargets,
                IrrelevantRejectedTargets = _summary.IrrelevantRejectedTargets,
                TacticalRejectedTargets = _summary.TacticalRejectedTargets,
                RejectedActiveDefenseTargets = _summary.RejectedActiveDefenseTargets,
                RejectedOverextendedTargets = _summary.RejectedOverextendedTargets,
                RejectedLowStrengthTargets = _summary.RejectedLowStrengthTargets,
                RecentlyFailedTargetPenalties = _summary.RecentlyFailedTargetPenalties
            };
        }

        public CsmArmyAttackTargetScore FindBestTarget(
            Kingdom kingdom,
            MobileParty leaderParty,
            float estimatedAttackStrength,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry,
            CsmDefenseAssignmentRegistry defenseRegistry = null,
            CsmRecentlyFailedArmyTargetRegistry failedTargets = null,
            int tick = 0)
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
                var score = ScoreTarget(kingdom, leaderParty, estimatedAttackStrength, settlements[index], defenseSnapshots, registry, defenseRegistry, failedTargets, tick);
                if (score == null || !IsPassed(score))
                {
                    continue;
                }

                if (best == null || score.Score > best.Score)
                {
                    best = score;
                }
            }

            if (best != null)
            {
                _summary.SelectedTargets++;
            }

            return best;
        }

        public CsmArmyAttackTargetScore FindBestRejectedTarget(
            Kingdom kingdom,
            MobileParty leaderParty,
            float estimatedAttackStrength,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry,
            CsmDefenseAssignmentRegistry defenseRegistry = null,
            CsmRecentlyFailedArmyTargetRegistry failedTargets = null,
            int tick = 0)
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

            CsmArmyAttackTargetScore bestTacticalRejected = null;
            CsmArmyAttackTargetScore bestIrrelevantRejected = null;
            for (var index = 0; index < settlements.Count; index++)
            {
                var score = ScoreTarget(kingdom, leaderParty, estimatedAttackStrength, settlements[index], defenseSnapshots, registry, defenseRegistry, failedTargets, tick);
                if (score == null || IsPassed(score))
                {
                    continue;
                }

                if (IsTacticalReject(score))
                {
                    if (bestTacticalRejected == null || score.Score > bestTacticalRejected.Score)
                    {
                        bestTacticalRejected = score;
                    }

                    continue;
                }

                if (bestIrrelevantRejected == null || score.Score > bestIrrelevantRejected.Score)
                {
                    bestIrrelevantRejected = score;
                }
            }

            return bestTacticalRejected ?? bestIrrelevantRejected;
        }

        public CsmArmyAttackTargetScore ScoreTarget(
            Kingdom kingdom,
            MobileParty leaderParty,
            float estimatedAttackStrength,
            Settlement target,
            List<DefenseEvaluationSnapshot> defenseSnapshots,
            CsmArmyAssignmentRegistry registry,
            CsmDefenseAssignmentRegistry defenseRegistry = null,
            CsmRecentlyFailedArmyTargetRegistry failedTargets = null,
            int tick = 0)
        {
            if (kingdom == null || leaderParty == null || target == null)
            {
                return null;
            }

            if (!target.IsFortification || (!target.IsCastle && !target.IsTown))
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
            result.IsActiveDefenseTarget = IsActiveDefenseTarget(target, defenseSnapshots)
                || (defenseRegistry != null && defenseRegistry.HasActiveAssignmentForSettlement(target.StringId, result.TargetName));
            result.RecentlyFailedTargetPenalty = GetRecentlyFailedTargetPenalty(failedTargets, leaderParty, kingdom, target, result.TargetName, tick);

            CalculateScore(result);
            result.UsesExtendedFrontlineRule = UsesExtendedFrontlineRule(result);
            result.DistanceLimit = result.UsesExtendedFrontlineRule ? ArmyDirectorSettings.ExtendedFrontlineAttackTargetDistance : ArmyDirectorSettings.MaxAttackTargetDistance;
            result.HardRejectReason = BuildHardRejectReason(result, kingdom, target);
            result.Reason = BuildReason(result);
            RecordSummary(result);
            return result;
        }

        public bool IsPassed(CsmArmyAttackTargetScore score)
        {
            return score != null
                && score.Target != null
                && string.IsNullOrWhiteSpace(score.HardRejectReason)
                && score.Score >= ArmyDirectorSettings.MinimumAttackTargetScore;
        }

        private static void CalculateScore(CsmArmyAttackTargetScore score)
        {
            score.StrategicValueScore = 50f;
            score.StrategicValueScore += score.IsCastle ? ArmyDirectorSettings.CastleTargetBonus : 0f;
            score.StrategicValueScore += score.IsTown ? ArmyDirectorSettings.TownTargetBonus : 0f;
            score.StrategicValueScore += score.TargetDefenseStrength < score.EstimatedAttackStrength ? 10f : 0f;

            score.FrontlineScore = score.IsFrontlineCandidate ? ArmyDirectorSettings.FrontlineTargetBonus : -ArmyDirectorSettings.DeepEnemyTerritoryPenalty;
            score.DistanceScore = -score.Distance * ArmyDirectorSettings.DistancePenaltyPerMapUnit;
            score.StrengthRatioScore = score.StrengthRatio * ArmyDirectorSettings.StrengthRatioScoreMultiplier;
            score.NearbyFriendlySupportScore = score.NearbyFriendlySupportStrength * ArmyDirectorSettings.NearbyFriendlySupportBonusMultiplier;
            score.NearbyEnemyPenalty = score.NearbyEnemyArmyStrength * ArmyDirectorSettings.NearbyEnemyArmyPenaltyMultiplier;
            score.SiegeRiskPenalty = score.TargetDefenseStrength * ArmyDirectorSettings.TargetDefensePenaltyMultiplier;
            score.ActiveDefensePenalty = score.IsActiveDefenseTarget ? ArmyDirectorSettings.ActiveDefenseTargetPenalty : 0f;
            score.ExistingFriendlySiegePenalty = score.IsAlreadyBesieged ? ArmyDirectorSettings.ExistingFriendlySiegePenalty : 0f;
            score.OverextensionPenalty = CalculateOverextensionPenalty(score);

            if (score.IsTown && ArmyDirectorSettings.PreferCastlesForAlpha && score.StrengthRatio < ArmyDirectorSettings.GoodAttackRequiredStrengthRatio * 1.5f)
            {
                score.SiegeRiskPenalty += ArmyDirectorSettings.TownTooStrongPenalty;
            }

            if (score.IsAlreadyAssigned)
            {
                score.ExistingFriendlySiegePenalty += ArmyDirectorSettings.AlreadyBesiegedPenalty;
            }

            score.FinalScoreBeforeHardRejects = score.StrategicValueScore
                + score.FrontlineScore
                + score.DistanceScore
                + score.StrengthRatioScore
                + score.NearbyFriendlySupportScore
                - score.NearbyEnemyPenalty
                - score.SiegeRiskPenalty
                - score.ActiveDefensePenalty
                - score.ExistingFriendlySiegePenalty
                - score.OverextensionPenalty
                - score.RecentlyFailedTargetPenalty;

            if (score.StrengthRatio < ArmyDirectorSettings.GoodAttackRequiredStrengthRatio)
            {
                score.FinalScoreBeforeHardRejects -= 100f;
            }

            score.Score = score.FinalScoreBeforeHardRejects;
        }

        private static float CalculateOverextensionPenalty(CsmArmyAttackTargetScore score)
        {
            var penalty = 0f;
            if (!score.IsFrontlineCandidate)
            {
                penalty += ArmyDirectorSettings.IsolatedTargetPenalty;
            }

            if (score.Distance > ArmyDirectorSettings.MaxAttackTargetDistance)
            {
                penalty += (score.Distance - ArmyDirectorSettings.MaxAttackTargetDistance) * ArmyDirectorSettings.DistancePenaltyPerMapUnit;
            }

            if (score.NearbyFriendlySupportStrength < ArmyDirectorSettings.MinimumFriendlySupportForExtendedAttack)
            {
                penalty += ArmyDirectorSettings.IsolatedTargetPenalty;
            }

            if (score.EstimatedAttackStrength > 0f && score.NearbyEnemyArmyStrength >= score.EstimatedAttackStrength * ArmyDirectorSettings.DangerousNearbyEnemyArmyRatio)
            {
                penalty += ArmyDirectorSettings.MaxOverextensionPenalty * 0.5f;
            }

            return penalty > ArmyDirectorSettings.MaxOverextensionPenalty ? ArmyDirectorSettings.MaxOverextensionPenalty : penalty;
        }

        private static string BuildHardRejectReason(CsmArmyAttackTargetScore score, Kingdom kingdom, Settlement target)
        {
            if (score == null || target == null || kingdom == null)
            {
                return "Rejected because kingdom/army/leader/target is invalid";
            }

            if (target.MapFaction == null)
            {
                return "Rejected because target settlement faction is unreadable";
            }

            if (target.MapFaction == kingdom)
            {
                return "Rejected because target is already friendly";
            }

            if (!kingdom.IsAtWarWith(target.MapFaction))
            {
                return "Rejected because target is not enemy";
            }

            if (score.IsActiveDefenseTarget)
            {
                return "Rejected active defense target";
            }

            if (score.IsAlreadyAssigned)
            {
                return "Rejected because target already has active CSM army assignment";
            }

            if (score.IsAlreadyBesieged)
            {
                return "Rejected because target is already under friendly siege";
            }

            if (score.StrengthRatio < ArmyDirectorSettings.GoodAttackRequiredStrengthRatio)
            {
                return "Rejected because attack strength ratio is too low";
            }

            if (score.EstimatedAttackStrength > 0f
                && score.NearbyEnemyArmyStrength >= score.EstimatedAttackStrength * ArmyDirectorSettings.DangerousNearbyEnemyArmyRatio)
            {
                return "Rejected because nearby enemy army strength is too high";
            }

            if (score.Distance > ArmyDirectorSettings.MaxAttackTargetDistance && !UsesExtendedFrontlineRule(score))
            {
                return IsAlmostExtendedFrontlineTarget(score)
                    ? "Rejected because target is overextended and unsupported"
                    : "Rejected because distance is too high";
            }

            if (score.Distance > ArmyDirectorSettings.MaxAttackTargetDistance
                && score.NearbyFriendlySupportStrength < ArmyDirectorSettings.MinimumFriendlySupportForExtendedAttack)
            {
                return "Rejected because target is overextended and unsupported";
            }

            return null;
        }

        private static string BuildReason(CsmArmyAttackTargetScore score)
        {
            if (!string.IsNullOrWhiteSpace(score.HardRejectReason))
            {
                return score.HardRejectReason;
            }

            if (score.RecentlyFailedTargetPenalty > 0f)
            {
                return "Selected target with recently failed target penalty by Army Target Scoring v2";
            }

            if (UsesExtendedFrontlineRule(score))
            {
                return "Selected extended frontline attack target by Army Target Scoring v2";
            }

            return score.IsFrontlineCandidate ? "Selected frontline attack target by Army Target Scoring v2" : "Selected deep attack target by Army Target Scoring v2";
        }

        private static bool UsesExtendedFrontlineRule(CsmArmyAttackTargetScore score)
        {
            return score != null
                && ArmyDirectorSettings.AllowExtendedFrontlineTargets
                && score.IsFrontlineCandidate
                && score.Distance > ArmyDirectorSettings.MaxAttackTargetDistance
                && score.Distance <= ArmyDirectorSettings.ExtendedFrontlineAttackTargetDistance
                && score.StrengthRatio >= ArmyDirectorSettings.ExtendedFrontlineRequiredStrengthRatio
                && score.NearbyFriendlySupportStrength >= ArmyDirectorSettings.MinimumFriendlySupportForExtendedAttack
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

        private void RecordSummary(CsmArmyAttackTargetScore score)
        {
            if (score == null)
            {
                return;
            }

            _summary.EvaluatedTargets++;
            if (score.RecentlyFailedTargetPenalty > 0f)
            {
                _summary.RecentlyFailedTargetPenalties++;
            }

            if (string.IsNullOrWhiteSpace(score.HardRejectReason))
            {
                return;
            }

            _summary.HardRejectedTargets++;
            if (IsIrrelevantReject(score))
            {
                _summary.IrrelevantRejectedTargets++;
                return;
            }

            if (IsTacticalReject(score))
            {
                _summary.TacticalRejectedTargets++;
            }

            if (score.IsActiveDefenseTarget)
            {
                _summary.RejectedActiveDefenseTargets++;
            }

            if (score.StrengthRatio < ArmyDirectorSettings.GoodAttackRequiredStrengthRatio)
            {
                _summary.RejectedLowStrengthTargets++;
            }

            if (score.HardRejectReason == "Rejected because target is overextended and unsupported"
                || score.HardRejectReason == "Rejected because nearby enemy army strength is too high")
            {
                _summary.RejectedOverextendedTargets++;
            }
        }

        private static bool IsIrrelevantReject(CsmArmyAttackTargetScore score)
        {
            if (score == null)
            {
                return true;
            }

            return score.HardRejectReason == "Rejected because target is already friendly"
                || score.HardRejectReason == "Rejected because target is not enemy"
                || score.HardRejectReason == "Rejected because target settlement faction is unreadable"
                || score.HardRejectReason == "Rejected because kingdom/army/leader/target is invalid";
        }

        private static bool IsTacticalReject(CsmArmyAttackTargetScore score)
        {
            return score != null
                && !string.IsNullOrWhiteSpace(score.HardRejectReason)
                && !IsIrrelevantReject(score);
        }

        private static float GetRecentlyFailedTargetPenalty(
            CsmRecentlyFailedArmyTargetRegistry failedTargets,
            MobileParty leaderParty,
            Kingdom kingdom,
            Settlement target,
            string targetName,
            int tick)
        {
            if (failedTargets == null || target == null)
            {
                return 0f;
            }

            var state = failedTargets.GetActiveFailure(
                leaderParty?.Army?.LeaderParty?.StringId ?? leaderParty?.StringId ?? string.Empty,
                GetKingdomName(kingdom),
                target.StringId,
                targetName,
                tick);
            if (state == null)
            {
                return 0f;
            }

            return ArmyDirectorSettings.RecentlyFailedTargetCooldownTicks <= 2 ? 40f : 65f;
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

        private static string GetKingdomName(Kingdom kingdom)
        {
            return kingdom?.Name == null ? "unknown" : kingdom.Name.ToString();
        }
    }
}
