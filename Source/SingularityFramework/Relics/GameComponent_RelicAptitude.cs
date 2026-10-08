using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Relics
{
    /// <summary>
    /// Remembers which pawns have learned their aptitude for which relics, and drops rejected relics on the next tick
    /// (dropping in the middle of equipping isn't safe).
    /// </summary>
    public class GameComponent_RelicAptitude : GameComponent
    {
        public static GameComponent_RelicAptitude Instance => Current.Game.GetComponent<GameComponent_RelicAptitude>();

        private HashSet<string> knownAptitudes = new HashSet<string>();

        private readonly List<KeyValuePair<Pawn, ThingWithComps>> pendingDrops = new List<KeyValuePair<Pawn, ThingWithComps>>();

        public GameComponent_RelicAptitude(Game game) { }

        public void QueueDrop(Pawn pawn, ThingWithComps relic) => pendingDrops.Add(new KeyValuePair<Pawn, ThingWithComps>(pawn, relic));

        public override void GameComponentTick()
        {
            if (pendingDrops.Count == 0) return;

            foreach (KeyValuePair<Pawn, ThingWithComps> pendingDrop in pendingDrops)
            {
                Pawn pawn = pendingDrop.Key;
                ThingWithComps relic = pendingDrop.Value;
                if (pawn?.equipment == null || relic == null || relic.Destroyed || !pawn.equipment.Contains(relic)) continue;

                if (pawn.Spawned) pawn.equipment.TryDropEquipment(relic, out _, pawn.Position, false);
                else pawn.equipment.Remove(relic);
            }
            pendingDrops.Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }
    }
}
