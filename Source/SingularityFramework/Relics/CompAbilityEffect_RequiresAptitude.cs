using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Relics
{
    public class CompProperties_AbilityRequiresAptitude : CompProperties_AbilityEffect
    {
        public AptitudeLevel minimumAptitude = AptitudeLevel.Full;

        public CompProperties_AbilityRequiresAptitude() => compClass = typeof(CompAbilityEffect_RequiresAptitude);
    }

    /// <summary>
    /// Disables the ability unless the caster's aptitude for their held relic is high enough.
    /// </summary>
    public class CompAbilityEffect_RequiresAptitude : CompAbilityEffect
    {
        public new CompProperties_AbilityRequiresAptitude Props => (CompProperties_AbilityRequiresAptitude)props;

        public override bool GizmoDisabled(out string reason)
        {
            Pawn caster = parent.pawn;
            ThingWithComps relic = caster?.equipment?.Primary;
            CompRelicAptitude aptitudeComp = relic?.TryGetComp<CompRelicAptitude>();

            if (aptitudeComp != null && RelicAptitude.GetAptitude(caster, aptitudeComp.Props, relic.def) < Props.minimumAptitude)
            {
                reason = "Sing_AptitudeTooLow".Translate(RelicAptitude.Label(Props.minimumAptitude));
                return true;
            }
            return base.GizmoDisabled(out reason);
        }
    }
}
