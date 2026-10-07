using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Strikes
{
    /// <summary>
    /// Deals one more instance of damage of a chosen type to the target, separate from the attack itself.
    /// It goes through the target's armor on its own, so a burn against heat-resistant armor can do very little.
    /// </summary>
    public class OnHitEffect_ExtraDamage : OnHitEffect
    {
        public DamageDef damageDefinition;
        public float damageAmount;
        public float armorPenetration;
        public bool ignoreArmor;

        protected override void Apply(Pawn attacker, Thing target)
        {
            if (damageDefinition == null || damageAmount <= 0f) return;
            if (target is Pawn targetPawn && targetPawn.Dead) return;

            float hitAngle = (target.Position - attacker.Position).AngleFlat;
            ThingDef weaponDefinition = attacker.equipment?.Primary?.def;

            var extraDamage = new DamageInfo(damageDefinition, damageAmount, armorPenetration, hitAngle, attacker, null, weaponDefinition);
            if (ignoreArmor) extraDamage.SetIgnoreArmor(true);
            target.TakeDamage(extraDamage);
        }
    }

}
