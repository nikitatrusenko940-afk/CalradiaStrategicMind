using CalradiaStrategicMind.Behaviors;
using CalradiaStrategicMind.Harmony;
using CalradiaStrategicMind.Logging;
using CalradiaStrategicMind.Utils;
using TaleWorlds.CampaignSystem;
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
                CsmHarmonyBootstrap.Apply();
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

                var campaignGameStarter = gameStarterObject as CampaignGameStarter;
                if (campaignGameStarter == null)
                {
                    CsmLogger.Info("Game starter is not CampaignGameStarter; strategic observation behavior was not registered");
                    return;
                }

                campaignGameStarter.AddBehavior(new StrategicObservationBehavior());
                CsmLogger.Info("Strategic observation behavior registered");
                campaignGameStarter.AddBehavior(new ExperimentalDefenseScoreInfluenceBehavior());
                CsmLogger.Info("Experimental defense score influence behavior registered");
            });
        }
    }
}
