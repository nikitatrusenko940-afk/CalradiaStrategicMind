using System;
using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Settings;
using HarmonyLib;

namespace CalradiaStrategicMind.Harmony
{
    public static class CsmHarmonyBootstrap
    {
        private const string HarmonyId = "calradia.strategic.mind.ai.diagnostics";
        private static bool _patchAttempted;
        private static bool _patched;

        public static void Apply()
        {
            if (_patchAttempted)
            {
                return;
            }

            _patchAttempted = true;
            var patchedMethods = "none";
            try
            {
                if (!DefenseControllerSettings.EnableHarmonyAiOverwriteDiagnostics)
                {
                    CsmLogger.Info("Observed CSM Harmony bootstrap: isEnabled=False, patched=False, patchedMethods='none', reason='Harmony AI diagnostics bootstrap'");
                    return;
                }

                var harmony = new HarmonyLib.Harmony(HarmonyId);
                harmony.PatchAll(typeof(CsmHarmonyBootstrap).Assembly);
                _patched = !string.IsNullOrWhiteSpace(CsmAiPartyThinkBehaviorPatch.PatchedMethodName);
                patchedMethods = BuildPatchedMethodSummary();
                CsmLogger.Info(
                    $"Observed CSM Harmony bootstrap: isEnabled=True, patched={_patched}, patchedMethods='{patchedMethods}', reason='Harmony AI diagnostics bootstrap'");
            }
            catch (Exception exception)
            {
                _patched = false;
                CsmLogger.Error("Observed CSM Harmony bootstrap failed", exception);
                CsmLogger.Info(
                $"Observed CSM Harmony bootstrap: isEnabled=True, patched=False, patchedMethods='{patchedMethods}', reason='Harmony AI diagnostics bootstrap'");
            }
        }

        private static string BuildPatchedMethodSummary()
        {
            var aiMethod = string.IsNullOrWhiteSpace(CsmAiPartyThinkBehaviorPatch.PatchedMethodName)
                ? "none"
                : CsmAiPartyThinkBehaviorPatch.PatchedMethodName;
            var movementMethods = string.IsNullOrWhiteSpace(CsmMovementSetterDiagnosticsPatch.PatchedMethodNames)
                ? "none"
                : CsmMovementSetterDiagnosticsPatch.PatchedMethodNames;
            return aiMethod + "; movementSetters=" + movementMethods;
        }
    }
}
