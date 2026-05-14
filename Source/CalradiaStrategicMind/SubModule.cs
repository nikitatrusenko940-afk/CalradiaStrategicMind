using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Utils;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CalradiaStrategicMind
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            SafeExecutor.Run("SubModule load", () =>
            {
                CsmLogger.Info("CalradiaStrategicMind loaded");
            });
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            SafeExecutor.Run("Before initial module screen", () =>
            {
                CsmLogger.Info("Initial module screen is being set as root");
            });
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            SafeExecutor.Run("Game start", () =>
            {
                CsmLogger.Info("Game start detected");
            });
        }
    }
}
