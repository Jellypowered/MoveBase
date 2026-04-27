using RimWorld;
using Verse;

namespace HomeMover
{
    public class GameSaveComponent : GameComponent
    {
        private const int UpdateInterval = GenDate.TicksPerHour / 10;

        private static int _lastUpdateTick = 0;

        public GameSaveComponent(Game game) { }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                DesignatorHomeMover.ClearCache();
                _lastUpdateTick = 0;
            }

            DesignatorHomeMover.ExposeData();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (_lastUpdateTick + UpdateInterval > Current.Game.tickManager.TicksGame)
            {
                return;
            }
            else
            {
                DesignatorHomeMover.PlaceWaitingBuildings();

                _lastUpdateTick += UpdateInterval;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
        }
    }
}
