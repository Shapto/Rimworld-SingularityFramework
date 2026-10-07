using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Sequences
{
    /// <summary>
    /// Settings for a landing slam, put on the flyer's ThingDef as a mod extension.
    /// </summary>
    public class ModExtension_LandingSlam : DefModExtension
    {
        public DamageDef damageDefinition;
        public float radius = 2.9f;
        public int damageAmount = 20;
        public float armorPenetration = -1f;
        public SoundDef explosionSound;
    }

    /// <summary>
    /// A vanilla-style jump flyer that slams the landing spot with an explosion. The pawn can't be hit while airborne,
    /// since flyers take it off the map for the flight. The jumper is never hurt by their own slam.
    /// </summary>
    public class PawnFlyer_LandingSlam : PawnFlyer
    {
        protected override void RespawnPawn()
        {
            Pawn landingPawn = FlyingPawn;
            Map map = Map;
            IntVec3 landingCell = Position;

            base.RespawnPawn();

            ModExtension_LandingSlam slamSettings = def.GetModExtension<ModExtension_LandingSlam>();
            if (slamSettings?.damageDefinition == null || map == null) return;

            GenExplosion.DoExplosion(
                center: landingCell,
                map: map,
                radius: slamSettings.radius,
                damType: slamSettings.damageDefinition,
                instigator: landingPawn,
                damAmount: slamSettings.damageAmount,
                armorPenetration: slamSettings.armorPenetration,
                explosionSound: slamSettings.explosionSound,
                weapon: landingPawn?.equipment?.Primary?.def,
                ignoredThings: new List<Thing> { landingPawn });
        }
    }
}
