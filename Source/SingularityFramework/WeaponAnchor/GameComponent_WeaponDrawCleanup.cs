using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.WeaponAnchor
{
    /// <summary>
    /// Clears the framework's per-pawn caches when a game loads, and keeps the weapon draw records from holding onto pawns that are gone.
    /// </summary>
    public class GameComponent_WeaponDrawCleanup : GameComponent
    {
        private const int CleanupIntervalTicks = 2500;

        public GameComponent_WeaponDrawCleanup(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            WeaponDrawRecord.ClearAll();
            Strikes.StrikeTracker.ClearAll();
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % CleanupIntervalTicks == 0) WeaponDrawRecord.RemoveStaleRecords();
        }
    }
}
