using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework.Visuals
{
    public class HediffCompProperties_Aura : HediffCompProperties
    {
        public ThingDef moteDefinition;

        /// <summary>
        /// When true, the aura only shows while the pawn is drafted.
        /// </summary>
        public bool onlyWhileDrafted;

        public HediffCompProperties_Aura() => compClass = typeof(HediffComp_Aura);
    }

    /// <summary>
    /// Shows an aura on the pawn for as long as this hediff is on them.
    /// Several aura hediffs on one pawn simply stack their glows.
    /// </summary>
    public class HediffComp_Aura : HediffComp
    {
        private Mote auraMote;

        public HediffCompProperties_Aura Props => (HediffCompProperties_Aura)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (!Pawn.Spawned || Props.moteDefinition == null) return;
            if (Props.onlyWhileDrafted && !Pawn.Drafted) return;

            if (auraMote == null || auraMote.Destroyed) auraMote = MoteMaker.MakeAttachedOverlay(Pawn, Props.moteDefinition, Vector3.zero);
            auraMote.Maintain();
        }
    }
}
