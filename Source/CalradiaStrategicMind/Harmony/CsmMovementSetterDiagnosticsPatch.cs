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
    public static class CsmMovementSetterDiagnosticsPatch
    {
        private static readonly Dictionary<string, int> LastGuardCorrectionTickByParty = new Dictionary<string, int>();
        private static int _diagnosticTick;

        public static string PatchedMethodNames { get; private set; }

        public static IEnumerable<MethodBase> TargetMethods()
        {
            var methods = new List<MethodBase>();
            try
            {
                AddMethodsIfFound(methods, "SetMoveGoToSettlement");
                AddMethodsIfFound(methods, "SetMoveRaidSettlement");
                AddMethodsIfFound(methods, "SetMoveDefendSettlement");
                AddMethodsIfFound(methods, "SetMovePatrolAroundSettlement");
                AddMethodsIfFound(methods, "SetMoveModeHold");
                AddMethodsIfFound(methods, "SetMoveBesiegeSettlement");
                PatchedMethodNames = JoinMethodNames(methods);
            }
            catch (Exception exception)
            {
                CsmLogger.Error("Harmony movement setter diagnostics target discovery failed", exception);
                PatchedMethodNames = "none";
            }

            return methods;
        }

        public static bool Prefix(MethodBase __originalMethod, MobileParty __instance, object[] __args, ref CsmMovementSetterSnapshot __state)
        {
            try
            {
                __state = null;
                if (!DefenseControllerSettings.EnableHarmonyAiOverwriteDiagnostics || __instance == null)
                {
                    return true;
                }

                var controlled = CsmControlledPartyResolver.Resolve(__instance);
                if (!controlled.IsControlled || !NamesEqual(controlled.OwnerSystem, "Defense"))
                {
                    return true;
                }

                var methodName = GetMethodDisplayName(__originalMethod);
                var simpleMethodName = __originalMethod == null ? "unknown" : __originalMethod.Name;
                var tick = NextDiagnosticTick();
                var attemptedTarget = FindAttemptedTarget(__args);
                __state = CsmMovementSetterSnapshot.Capture(
                    __instance,
                    controlled,
                    methodName,
                    tick,
                    attemptedTarget.Name);

                var guardReason = "Movement setter guard disabled";
                if (!DefenseControllerSettings.EnableHarmonyDefenseMovementSetterGuard
                    || !CanGuardDefenseMovementSetter(__instance, controlled, out guardReason))
                {
                    LogGuardIfEnabled(__state, false, false, guardReason);
                    return true;
                }

                var hostileSetter = IsHostileSetter(simpleMethodName, controlled.ExpectedTarget, attemptedTarget.Settlement);
                if (!hostileSetter)
                {
                    LogGuardIfEnabled(__state, false, false, "Allowed safe movement setter for CSM Defense party");
                    return true;
                }

                var correctionApplied = CorrectDefenseObjectiveIfNeeded(tick, __instance, controlled);
                __state.GuardBlocked = true;
                __state.CorrectionApplied = correctionApplied;
                LogGuardIfEnabled(__state, true, correctionApplied, "Blocked hostile movement setter for CSM Defense party");
                return false;
            }
            catch (Exception exception)
            {
                CsmLogger.Error("Harmony movement setter guard prefix failed", exception);
                return true;
            }
        }

        public static void Postfix(MobileParty __instance, CsmMovementSetterSnapshot __state)
        {
            try
            {
                if (__instance == null || __state == null || !DefenseControllerSettings.EnableHarmonyDefenseMovementSetterGuardDiagnostics)
                {
                    return;
                }

                var afterTarget = GetSettlementName(__instance.TargetSettlement);
                var afterBehavior = __instance.DefaultBehavior.ToString();
                if (NamesEqual(__state.BeforeTarget, afterTarget) && NamesEqual(__state.BeforeBehavior, afterBehavior))
                {
                    return;
                }

                CsmLogger.Info(
                    $"Observed Harmony movement setter diagnostic: tick={__state.Tick}, method='{__state.MethodName}', party='{__state.PartyName}', csmOwner='Defense', expectedTarget='{__state.ExpectedTargetName}', beforeTarget='{__state.BeforeTarget}', afterTarget='{afterTarget}', beforeBehavior='{__state.BeforeBehavior}', afterBehavior='{afterBehavior}', guardBlocked={__state.GuardBlocked}, reason='Movement setter changed CSM defense party objective'");
            }
            catch (Exception exception)
            {
                CsmLogger.Error("Harmony movement setter diagnostics postfix failed", exception);
            }
        }

        private static bool CanGuardDefenseMovementSetter(MobileParty party, CsmControlledPartyInfo controlled, out string reason)
        {
            if (party == null)
            {
                reason = "Party missing";
                return false;
            }

            if (controlled.ExpectedTarget == null)
            {
                reason = "Movement setter guard skipped because expected target is missing";
                return false;
            }

            if (!party.IsActive || party.IsDisbanding)
            {
                reason = "Movement setter guard skipped because party is inactive or disbanding";
                return false;
            }

            if (party.MapEvent != null)
            {
                reason = "Movement setter guard skipped because party is in battle";
                return false;
            }

            if (party.Army != null)
            {
                reason = "Movement setter guard skipped because party is in army";
                return false;
            }

            if (!controlled.ExpectedTarget.IsActive)
            {
                reason = "Movement setter guard skipped because expected settlement is inactive";
                return false;
            }

            reason = "Movement setter guard active";
            return true;
        }

        private static bool IsHostileSetter(string methodName, Settlement expectedTarget, Settlement attemptedTarget)
        {
            if (NamesEqual(methodName, "SetMoveDefendSettlement"))
            {
                return attemptedTarget != expectedTarget;
            }

            if (NamesEqual(methodName, "SetMoveGoToSettlement"))
            {
                return attemptedTarget != expectedTarget;
            }

            if (NamesEqual(methodName, "SetMoveModeHold"))
            {
                return true;
            }

            if (NamesEqual(methodName, "SetMoveRaidSettlement")
                || NamesEqual(methodName, "SetMovePatrolAroundSettlement")
                || NamesEqual(methodName, "SetMoveBesiegeSettlement"))
            {
                return true;
            }

            return attemptedTarget != null && attemptedTarget != expectedTarget;
        }

        private static bool CorrectDefenseObjectiveIfNeeded(int tick, MobileParty party, CsmControlledPartyInfo controlled)
        {
            if (party == null || controlled.ExpectedTarget == null || party.MapEvent != null || party.Army != null || !controlled.ExpectedTarget.IsActive)
            {
                return false;
            }

            if (party.TargetSettlement == controlled.ExpectedTarget && party.DefaultBehavior == AiBehavior.DefendSettlement)
            {
                return false;
            }

            if (!CanRunCorrection(tick, party))
            {
                return false;
            }

            var applied = false;
            SafeExecutor.Run("Harmony defense movement setter guard correction", () =>
            {
                party.SetMoveDefendSettlement(controlled.ExpectedTarget, false, party.NavigationCapability);
                applied = true;
            });

            return applied;
        }

        private static bool CanRunCorrection(int tick, MobileParty party)
        {
            var key = GetPartyKey(party);
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var cooldown = DefenseControllerSettings.HarmonyDefenseMovementSetterCorrectionCooldownTicks < 0
                ? 0
                : DefenseControllerSettings.HarmonyDefenseMovementSetterCorrectionCooldownTicks;
            if (LastGuardCorrectionTickByParty.TryGetValue(key, out var lastTick)
                && tick - lastTick < cooldown)
            {
                return false;
            }

            LastGuardCorrectionTickByParty[key] = tick;
            return true;
        }

        private static MovementSetterTarget FindAttemptedTarget(object[] args)
        {
            if (args == null)
            {
                return new MovementSetterTarget(null, "none");
            }

            for (var index = 0; index < args.Length; index++)
            {
                var settlement = args[index] as Settlement;
                if (settlement != null)
                {
                    return new MovementSetterTarget(settlement, GetSettlementName(settlement));
                }

                var village = args[index] as Village;
                if (village != null)
                {
                    var villageSettlement = village.Settlement;
                    return new MovementSetterTarget(villageSettlement, villageSettlement == null ? GetVillageName(village) : GetSettlementName(villageSettlement));
                }
            }

            return new MovementSetterTarget(null, "none");
        }

        private static void LogGuardIfEnabled(CsmMovementSetterSnapshot state, bool blocked, bool correctionApplied, string reason)
        {
            if (!DefenseControllerSettings.EnableHarmonyDefenseMovementSetterGuardDiagnostics || state == null)
            {
                return;
            }

            CsmLogger.Info(
                $"Observed Harmony movement setter guard: tick={state.Tick}, method='{state.MethodName}', party='{state.PartyName}', csmOwner='Defense', expectedTarget='{state.ExpectedTargetName}', attemptedTarget='{state.AttemptedTarget}', beforeTarget='{state.BeforeTarget}', beforeBehavior='{state.BeforeBehavior}', blocked={blocked}, correctionApplied={correctionApplied}, reason='{reason}'");
        }

        private static void AddMethodsIfFound(List<MethodBase> methods, string methodName)
        {
            var mobilePartyMethods = typeof(MobileParty).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (var index = 0; index < mobilePartyMethods.Length; index++)
            {
                var method = mobilePartyMethods[index];
                if (NamesEqual(method.Name, methodName))
                {
                    methods.Add(method);
                }
            }
        }

        private static int NextDiagnosticTick()
        {
            _diagnosticTick++;
            return _diagnosticTick;
        }

        private static string JoinMethodNames(List<MethodBase> methods)
        {
            if (methods == null || methods.Count == 0)
            {
                return "none";
            }

            var names = string.Empty;
            for (var index = 0; index < methods.Count; index++)
            {
                if (index > 0)
                {
                    names += ",";
                }

                names += GetMethodDisplayName(methods[index]);
            }

            return names;
        }

        private static string GetSettlementName(Settlement settlement)
        {
            return settlement == null || settlement.Name == null ? "none" : settlement.Name.ToString();
        }

        private static string GetVillageName(Village village)
        {
            return village == null || village.Name == null ? "none" : village.Name.ToString();
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

        private struct MovementSetterTarget
        {
            public MovementSetterTarget(Settlement settlement, string name)
            {
                Settlement = settlement;
                Name = string.IsNullOrWhiteSpace(name) ? "none" : name;
            }

            public Settlement Settlement { get; private set; }

            public string Name { get; private set; }
        }
    }

    public class CsmMovementSetterSnapshot
    {
        public int Tick { get; private set; }

        public string MethodName { get; private set; }

        public string PartyName { get; private set; }

        public string ExpectedTargetName { get; private set; }

        public string AttemptedTarget { get; private set; }

        public string BeforeTarget { get; private set; }

        public string BeforeBehavior { get; private set; }

        public bool GuardBlocked { get; set; }

        public bool CorrectionApplied { get; set; }

        public static CsmMovementSetterSnapshot Capture(
            MobileParty party,
            CsmControlledPartyInfo controlled,
            string methodName,
            int tick,
            string attemptedTarget)
        {
            return new CsmMovementSetterSnapshot
            {
                Tick = tick,
                MethodName = string.IsNullOrWhiteSpace(methodName) ? "unknown" : methodName,
                PartyName = GetPartyName(party),
                ExpectedTargetName = controlled.ExpectedTargetName,
                AttemptedTarget = string.IsNullOrWhiteSpace(attemptedTarget) ? "none" : attemptedTarget,
                BeforeTarget = GetSettlementName(party.TargetSettlement),
                BeforeBehavior = party.DefaultBehavior.ToString(),
                GuardBlocked = false,
                CorrectionApplied = false
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
