using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using SingularityFramework.Geometry;

namespace SingularityFramework.Sequences
{
    /// <summary>
    /// Ability verb for line dashes: while aiming, outlines every cell the dash will cross,
    /// and refuses targets the dash can't reach in a straight line.
    /// On the final stage of a chained ability, falls back to vanilla targeting for the finisher.
    /// </summary>
    public class Verb_CastAbilityLineDash : Verb_CastAbility
    {
        private static readonly Color LinePreviewColor = new Color(1f, 0.55f, 0.2f);

        private CompAbilityEffect_LineDash DashComp => ability?.CompOfType<CompAbilityEffect_LineDash>();

        private bool IsDashStage
        {
            get
            {
                CompAbilityEffect_Chain chain = ability?.CompOfType<CompAbilityEffect_Chain>();
                return DashComp != null && (chain == null || !chain.IsFinalStage);
            }
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            if (!IsDashStage || !target.IsValid)
            {
                base.DrawHighlight(target);
                return;
            }

            List<IntVec3> lineCells = LineStrike.CellsOnLine(CasterPawn.Position, target.Cell);
            Color previewColor = ValidateTarget(target, false) ? LinePreviewColor : Color.red;
            GenDraw.DrawFieldEdges(lineCells, previewColor);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages)) return false;
            if (!IsDashStage) return true;

            Map map = CasterPawn.Map;
            if (!target.Cell.Standable(map))
            {
                if (showMessages) Messages.Message("Sing_DashTargetNotStandable".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (!LineStrike.IsLineWalkable(CasterPawn.Position, target.Cell, map, DashComp.Props.strikeSettings))
            {
                if (showMessages) Messages.Message("Sing_DashLineBlocked".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return true;
        }
    }
}
