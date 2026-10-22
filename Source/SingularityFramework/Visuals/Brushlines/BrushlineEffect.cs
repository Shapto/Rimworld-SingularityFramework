using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework.Visuals.Brushlines
{
    /// <summary>
    /// A brush line animation on a map (a stream pulled into a pawn, a weapon forming). Its look advances with the game's
    /// speed and stops while paused; anything it does to the game (destroying the blood it pulled away) happens on ticks,
    /// so it happens even when the map isn't being looked at.
    /// </summary>
    public abstract class BrushLineEffect
    {
        public readonly BrushPalette palette;
        public readonly float durationSeconds;
        public float ageSeconds;

        private int ticksUntilMoment = -1;
        private Action moment;

        public virtual AltitudeLayer Layer => AltitudeLayer.MoteLow;

        protected BrushLineEffect(BrushPalette palette, float durationSeconds)
        {
            this.palette = palette;
            this.durationSeconds = durationSeconds;
        }

        public float Progress => Mathf.Clamp01(ageSeconds / durationSeconds);

        public bool Finished => ageSeconds >= durationSeconds && ticksUntilMoment < 0;

        /// <summary>
        /// Runs "action" once, at "progress" (0 to 1) through the effect, counted in ticks.
        /// </summary>
        public void AtMoment(float progress, Action action)
        {
            moment = action;
            ticksUntilMoment = Mathf.Max(0, Mathf.RoundToInt(durationSeconds * progress * GenTicks.TicksPerRealSecond));
        }

        public void Tick()
        {
            if (ticksUntilMoment < 0) return;
            if (ticksUntilMoment-- > 0) return;
            ticksUntilMoment = -1;
            moment?.Invoke();
        }

        public abstract void Draw(BrushLineDrawer drawer, float progress);
    }

    /// <summary>
    /// Runs every brush line effect on a map: moves them along each frame, draws them in one go per palette, and gives
    /// their game effects their ticks.
    /// </summary>
    public class MapComponent_BrushLines : MapComponent
    {
        private readonly List<BrushLineEffect> effects = new List<BrushLineEffect>();
        private readonly Dictionary<(BrushPalette palette, AltitudeLayer layer), BrushLineDrawer> drawers = new Dictionary<(BrushPalette palette, AltitudeLayer layer), BrushLineDrawer>();

        public MapComponent_BrushLines(Map map) : base(map)
        {
        }

        public static void Start(Map map, BrushLineEffect effect)
        {
            map?.GetComponent<MapComponent_BrushLines>()?.effects.Add(effect);
        }

        public override void MapComponentTick()
        {
            foreach (BrushLineEffect effect in effects) effect.Tick();
        }

        public override void MapComponentUpdate()
        {
            if (effects.Count == 0) return;
            // the look follows game time: faster at higher speeds, still while paused
            float step = Find.TickManager.Paused ? 0f : Time.deltaTime * Find.TickManager.TickRateMultiplier;
            bool visible = Find.CurrentMap == map;
            for (int index = effects.Count - 1; index >= 0; index--)
            {
                BrushLineEffect effect = effects[index];
                effect.ageSeconds += step;
                if (effect.Finished)
                {
                    effects.RemoveAt(index);
                    continue;
                }
                if (visible && effect.ageSeconds < effect.durationSeconds) effect.Draw(DrawerFor(effect.palette, effect.Layer), effect.Progress);
            }
            if (!visible) return;
            foreach (BrushLineDrawer drawer in drawers.Values) drawer.DrawAll();
        }

        private BrushLineDrawer DrawerFor(BrushPalette palette, AltitudeLayer layer)
        {
            if (!drawers.TryGetValue((palette, layer), out BrushLineDrawer drawer))
            {
                drawer = new BrushLineDrawer(palette, layer.AltitudeFor());
                drawers[(palette, layer)] = drawer;
            }
            return drawer;
        }
    }
}
