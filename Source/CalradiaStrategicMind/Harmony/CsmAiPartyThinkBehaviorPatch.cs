using System;
using System.Collections.Generic;
using System.Reflection;
using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Settings;
using CalradiaStrategicMind.Utils;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CalradiaStrategicMind.Harmony
{
    [HarmonyPatch]
    public static class CsmAiPartyThinkBehaviorPatch
    {
        private static readonly Dictionary<string, int> LastDefenseLockCommandTickByParty = new Dictionary<string, int>();
        private static int _diagnosticTick;

        public static string PatchedMethodName { get; private set; }

        public static MethodBase TargetMethod()
        {
            try
            {
                var behaviorType = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiPartyThinkBehavior");
                if (behaviorType == null)
                {
                    CsmLogger.Info("Observed CSM Harmony bootstrap: isEnabled=True, patched=False, patchedMethods='none', reason='AiPartyThinkBehavior type not found'");
                    return null;
                }

                var preferred = FindMethodWithMobileParty(behaviorType, "PartyHourlyAiTick");
                if (preferred != null)
                {
                    PatchedMethodName = GetMethodDisplayName(preferred);
                    return preferred;
                }

                var fallback = FindAiTickLikeMethodWithMobileParty(behaviorType);
                if (fallback != null)
                {
                    PatchedMethodName = GetMethodDisplayName(fallback);
                    return fallback;
                }

                CsmLogger.Info("Observed CSM Harmony bootstrap: isEnabled=True, patched=False, patchedMethods='none', reason='No AiPartyThinkBehavior MobileParty target method found'");
                return null;
            }
            catch (Exception exception)
            {
                CsmLogger.Error("Harmony AI diagnostics target discovery failed", exception);
                return null;
            }
        }

        public static bool Prefix(MethodBase __originalMethod, object[] __args, ref CsmAiOverwriteSnapshot __state)
        {
            try
            {
                __state = null;
                if (!DefenseControllerSettings.EnableHarmonyAiOverwriteDiagnostics)
                {
                    return true;
                }

                var party = FindPartyArgument(__args);
                if (party == null)
                {
                    return true;
                }

                var controlled = CsmControlledPartyResolver.Resolve(party);
                if (!controlled.IsControlled)
                {
                    return true;
                }

                var tick = NextDiagnosticTick();
                var methodName = GetMethodDisplayName(__originalMethod);
                if (TryApplyDefenseLock(tick, party, controlled, methodName, out var skipVanillaAiTick, out var lockDecisionLogged))
                {
                    __state = CsmAiOverwriteSnapshot.Capture(party, controlled, methodName, tick, true);
                    return !skipVanillaAiTick;
                }

                if (!lockDecisionLogged)
                {
                    LogLockNotApplied(tick, party, controlled);
                }

                __state = CsmAiOverwriteSnapshot.Capture(party, controlled, methodName, tick, false);
                return true;
            }
            catch (Exception exception)
            {
                CsmLogger.Error("Harmony AI diagnostics prefix failed", exception);
                return true;
            }
        }

        public static void Postfix(MethodBase __originalMethod, object[] __args, CsmAiOverwriteSnapshot __state)
        {
            try
            {
                if (!DefenseControllerSettings.EnableHarmonyAiOverwriteDiagnostics || __state == null)
                {
                    return;
                }

                var party = FindPartyArgument(__args);
                if (party == null)
                {
                    return;
                }

                var afterTarget = GetSettlementName(party.TargetSettlement);
                var afterBehavior = GetBehaviorName(party.DefaultBehavior);
                var changedDuringPatchedMethod = !__state.OriginalSkipped
                    && (!NamesEqual(__state.BeforeTarget, afterTarget)
                        || !NamesEqual(__state.BeforeBehavior, afterBehavior));
                var objectiveMismatchAfterTick = IsObjectiveMismatchAfterTick(__state, party);
                var overwriteDetected = changedDuringPatchedMethod || objectiveMismatchAfterTick;
                var classification = overwriteDetected ? ClassifyOverwrite(__state, party) : "None";
                CsmLogger.Info(
                    $"Observed Harmony AI overwrite diagnostic: tick={__state.Tick}, party='{__state.PartyName}', csmOwner='{__state.OwnerSystem}', expectedTarget='{__state.ExpectedTargetName}', beforeTarget='{__state.BeforeTarget}', afterTarget='{afterTarget}', beforeBehavior='{__state.BeforeBehavior}', afterBehavior='{afterBehavior}', method='{__state.MethodName}', changedDuringPatchedMethod={changedDuringPatchedMethod}, objectiveMismatchAfterTick={objectiveMismatchAfterTick}, overwriteDetected={overwriteDetected}, classification='{classification}', reason='Vanilla AI objective diagnostic'");
            }
            catch (Exception exception)
            {
                CsmLogger.Error("Harmony AI diagnostics postfix failed", exception);
            }
        }

        private static bool TryApplyDefenseLock(
            int tick,
            MobileParty party,
            CsmControlledPartyInfo controlled,
            string methodName,
            out bool skipVanillaAiTick,
            out bool lockDecisionLogged)
        {
            skipVanillaAiTick = false;
            lockDecisionLogged = false;
            if (!DefenseControllerSettings.EnableHarmonyAiControlLock
                || !DefenseControllerSettings.EnableHarmonyDefenseAiLock
                || !NamesEqual(controlled.OwnerSystem, "Defense"))
            {
                return false;
            }

            if (!CanApplyDefenseLock(party, controlled, out var reason))
            {
                LogDefenseLock(tick, party, controlled, false, reason);
                lockDecisionLogged = true;
                return false;
            }

            ApplyDefenseLockCorrectionIfNeeded(tick, party, controlled);
            skipVanillaAiTick = true;
            CsmControlledPartyResolver.MarkDefenseLockApplied(party, tick);
            LogDefenseLock(tick, party, controlled, true, "CSM Defense lock skipped vanilla party AI tick");
            lockDecisionLogged = true;
            return true;
        }

        private static bool CanApplyDefenseLock(MobileParty party, CsmControlledPartyInfo controlled, out string reason)
        {
            if (party == null)
            {
                reason = "Party missing";
                return false;
            }

            if (controlled.ExpectedTarget == null)
            {
                reason = "Defense lock skipped because expected target is missing";
                return false;
            }

            if (!party.IsActive || party.IsDisbanding)
            {
                reason = "Defense lock skipped because party is inactive or disbanding";
                return false;
            }

            if (party.MapEvent != null)
            {
                reason = "Defense lock skipped because party is in battle";
                return false;
            }

            if (party.Army != null)
            {
                reason = "Defense lock skipped because party is in army";
                return false;
            }

            if (!IsExpectedTargetStillDefensible(controlled.ExpectedTarget))
            {
                reason = "Defense lock skipped because expected settlement is no longer valid for active defense assignment";
                return false;
            }

            reason = "CSM Defense lock skipped vanilla party AI tick";
            return true;
        }

        private static void ApplyDefenseLockCorrectionIfNeeded(int tick, MobileParty party, CsmControlledPartyInfo controlled)
        {
            var beforeTarget = GetSettlementName(party.TargetSettlement);
            var beforeBehavior = GetBehaviorName(party.DefaultBehavior);
            var commandApplied = false;
            if (party.TargetSettlement == controlled.ExpectedTarget && party.DefaultBehavior == AiBehavior.DefendSettlement)
            {
                CsmLogger.Info(
                    $"Observed Harmony defense AI lock correction: tick={tick}, party='{GetPartyName(party)}', expectedTarget='{controlled.ExpectedTargetName}', beforeTarget='{beforeTarget}', beforeBehavior='{beforeBehavior}', commandApplied=False, reason='CSM Defense lock corrected party objective before skipping vanilla AI'");
                return;
            }

            if (CanRunDefenseLockCommand(tick, party))
            {
                SafeExecutor.Run("Harmony defense AI lock correction", () =>
                {
                    party.SetMoveDefendSettlement(controlled.ExpectedTarget, false, party.NavigationCapability);
                    commandApplied = true;
                });
            }

            CsmLogger.Info(
                $"Observed Harmony defense AI lock correction: tick={tick}, party='{GetPartyName(party)}', expectedTarget='{controlled.ExpectedTargetName}', beforeTarget='{beforeTarget}', beforeBehavior='{beforeBehavior}', commandApplied={commandApplied}, reason='CSM Defense lock corrected party objective before skipping vanilla AI'");
        }

        private static bool CanRunDefenseLockCommand(int tick, MobileParty party)
        {
            var key = GetPartyKey(party);
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var cooldown = Math.Max(0, DefenseControllerSettings.HarmonyDefenseLockCommandCooldownTicks);
            if (LastDefenseLockCommandTickByParty.TryGetValue(key, out var lastTick)
                && tick - lastTick < cooldown)
            {
                return false;
            }

            LastDefenseLockCommandTickByParty[key] = tick;
            return true;
        }

        private static void LogLockNotApplied(int tick, MobileParty party, CsmControlledPartyInfo controlled)
        {
            if (NamesEqual(controlled.OwnerSystem, "Army"))
            {
                CsmLogger.Info(
                    $"Observed Harmony defense AI lock: tick={tick}, party='{GetPartyName(party)}', expectedTarget='{controlled.ExpectedTargetName}', currentTarget='{GetSettlementName(party.TargetSettlement)}', currentBehavior='{GetBehaviorName(party.DefaultBehavior)}', skipVanillaAiTick=False, reason='Army AI lock disabled; diagnostics only'");
                return;
            }

            if (NamesEqual(controlled.OwnerSystem, "Defense"))
            {
                CsmLogger.Info(
                    $"Observed Harmony defense AI lock: tick={tick}, party='{GetPartyName(party)}', expectedTarget='{controlled.ExpectedTargetName}', currentTarget='{GetSettlementName(party.TargetSettlement)}', currentBehavior='{GetBehaviorName(party.DefaultBehavior)}', skipVanillaAiTick=False, reason='Defense AI lock disabled; diagnostics only'");
            }
        }

        private static void LogDefenseLock(int tick, MobileParty party, CsmControlledPartyInfo controlled, bool skipVanillaAiTick, string reason)
        {
            CsmLogger.Info(
                $"Observed Harmony defense AI lock: tick={tick}, party='{GetPartyName(party)}', expectedTarget='{controlled.ExpectedTargetName}', currentTarget='{GetSettlementName(party.TargetSettlement)}', currentBehavior='{GetBehaviorName(party.DefaultBehavior)}', skipVanillaAiTick={skipVanillaAiTick}, reason='{reason}'");
        }

        private static bool IsExpectedTargetStillDefensible(Settlement settlement)
        {
            return settlement != null && settlement.IsActive;
        }

        private static int NextDiagnosticTick()
        {
            _diagnosticTick++;
            return _diagnosticTick;
        }

        private static MethodInfo FindMethodWithMobileParty(Type behaviorType, string methodName)
        {
            var methods = behaviorType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!NamesEqual(method.Name, methodName) || !HasMobilePartyParameter(method))
                {
                    continue;
                }

                return method;
            }

            return null;
        }

        private static MethodInfo FindAiTickLikeMethodWithMobileParty(Type behaviorType)
        {
            var methods = behaviorType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!HasMobilePartyParameter(method))
                {
                    continue;
                }

                var name = method.Name ?? string.Empty;
                if (name.IndexOf("Tick", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Ai", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Think", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return method;
                }
            }

            return null;
        }

        private static bool HasMobilePartyParameter(MethodBase method)
        {
            if (method == null)
            {
                return false;
            }

            var parameters = method.GetParameters();
            for (var index = 0; index < parameters.Length; index++)
            {
                if (parameters[index].ParameterType == typeof(MobileParty))
                {
                    return true;
                }
            }

            return false;
        }

        private static MobileParty FindPartyArgument(object[] args)
        {
            if (args == null)
            {
                return null;
            }

            for (var index = 0; index < args.Length; index++)
            {
                var party = args[index] as MobileParty;
                if (party != null)
                {
                    return party;
                }
            }

            return null;
        }

        private static bool IsObjectiveMismatchAfterTick(CsmAiOverwriteSnapshot before, MobileParty party)
        {
            if (before == null || party == null)
            {
                return false;
            }

            var expectedTarget = before.ExpectedTarget;
            var afterTarget = party.TargetSettlement;
            var targetMismatch = expectedTarget != null && afterTarget != expectedTarget;
            var behaviorMismatch = NamesEqual(before.OwnerSystem, "Defense")
                && party.DefaultBehavior != AiBehavior.DefendSettlement;
            return targetMismatch || behaviorMismatch;
        }

        private static string ClassifyOverwrite(CsmAiOverwriteSnapshot before, MobileParty party)
        {
            if (party == null)
            {
                return "Unknown";
            }

            if (party.Army != before.Army)
            {
                return "JoinedArmy";
            }

            if (party.DefaultBehavior == AiBehavior.BesiegeSettlement)
            {
                return "VanillaBesiege";
            }

            if (party.DefaultBehavior == AiBehavior.RaidSettlement)
            {
                return "VanillaRaidOrVillage";
            }

            if (party.DefaultBehavior == AiBehavior.DefendSettlement && party.TargetSettlement != before.ExpectedTarget)
            {
                return "VanillaDefendOtherSettlement";
            }

            if (party.TargetSettlement != null && party.TargetSettlement != before.ExpectedTarget)
            {
                return "VanillaVisitSettlement";
            }

            return "Unknown";
        }

        private static string GetSettlementName(Settlement settlement)
        {
            return settlement == null || settlement.Name == null ? "none" : settlement.Name.ToString();
        }

        private static string GetPartyName(MobileParty party)
        {
            return party == null || party.Name == null ? "unknown" : party.Name.ToString();
        }

        private static string GetPartyKey(MobileParty party)
        {
            if (party == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(party.StringId) ? GetPartyName(party) : party.StringId;
        }

        private static string GetBehaviorName(AiBehavior behavior)
        {
            return behavior.ToString();
        }

        private static string GetMethodDisplayName(MethodBase method)
        {
            if (method == null)
            {
                return "unknown";
            }

            var declaringType = method.DeclaringType == null ? "unknown" : method.DeclaringType.FullName;
            return declaringType + "." + method.Name;
        }

        private static bool NamesEqual(string left, string right)
        {
            return string.Equals(
                left == null ? string.Empty : left.Trim(),
                right == null ? string.Empty : right.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public class CsmAiOverwriteSnapshot
    {
        public string PartyName { get; private set; }

        public int Tick { get; private set; }

        public string OwnerSystem { get; private set; }

        public string ExpectedTargetName { get; private set; }

        public Settlement ExpectedTarget { get; private set; }

        public string BeforeTarget { get; private set; }

        public string BeforeBehavior { get; private set; }

        public string BeforeShortTermBehavior { get; private set; }

        public Army Army { get; private set; }

        public string MethodName { get; private set; }

        public bool OriginalSkipped { get; private set; }

        public static CsmAiOverwriteSnapshot Capture(
            MobileParty party,
            CsmControlledPartyInfo controlled,
            string methodName,
            int tick,
            bool originalSkipped)
        {
            return new CsmAiOverwriteSnapshot
            {
                PartyName = GetPartyName(party),
                Tick = tick,
                OwnerSystem = controlled.OwnerSystem,
                ExpectedTargetName = controlled.ExpectedTargetName,
                ExpectedTarget = controlled.ExpectedTarget,
                BeforeTarget = GetSettlementName(party.TargetSettlement),
                BeforeBehavior = party.DefaultBehavior.ToString(),
                BeforeShortTermBehavior = party.ShortTermBehavior.ToString(),
                Army = party.Army,
                MethodName = string.IsNullOrWhiteSpace(methodName) ? "unknown" : methodName,
                OriginalSkipped = originalSkipped
            };
        }

        private static string GetSettlementName(Settlement settlement)
        {
            return settlement == null || settlement.Name == null ? "none" : settlement.Name.ToString();
        }

        private static string GetPartyName(MobileParty party)
        {
            return party == null || party.Name == null ? "unknown" : party.Name.ToString();
        }
    }
}
