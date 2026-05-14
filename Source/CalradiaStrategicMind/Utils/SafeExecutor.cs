using System;
using CalradiaStrategicMind.Logging;

namespace CalradiaStrategicMind.Utils
{
    public static class SafeExecutor
    {
        public static void Run(string operationName, Action action)
        {
            if (action == null)
            {
                CsmLogger.Warn($"Skipped operation '{operationName}' because action is null");
                return;
            }

            try
            {
                action();
            }
            catch (Exception exception)
            {
                CsmLogger.Error($"Operation '{operationName}' failed", exception);
            }
        }

        public static T Run<T>(string operationName, Func<T> action, T fallback)
        {
            if (action == null)
            {
                CsmLogger.Warn($"Skipped operation '{operationName}' because function is null");
                return fallback;
            }

            try
            {
                return action();
            }
            catch (Exception exception)
            {
                CsmLogger.Error($"Operation '{operationName}' failed", exception);
                return fallback;
            }
        }
    }
}
