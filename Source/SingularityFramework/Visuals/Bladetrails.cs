using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework.Visuals
{
    /// <summary>
    /// How a bladetrail looks over its life. Put on the mote's ThingDef as a mod extension.
    /// Timing (fade in, solid, fade out) stays in the def's vanilla mote block.
    /// </summary>
    public class ModExtension_Bladetrail : DefModExtension
    {
        public float sizeMultiplier = 1f;

        /// <summary>
        /// Size wobble (0.02 = plus or minus 2%) and how fast it waves. 0 for a crisp trail.
        /// </summary>
        public float rippleAmount = 0f;
        public float rippleSpeed = 2f;

        /// <summary>
        /// Extra fainter copies drawn behind the trail, each a bit bigger and fainter than the last.
        /// </summary>
        public int echoCount = 0;
        public float echoGrowth = 0.1f;
        public float echoFade = 0.25f;

        /// <summary>
        /// How many degrees the trail rotates through while it's visible, so one sprite reads as a swing. 0 for a static trail.
        /// </summary>
        public float sweepDegrees = 0f;

        /// <summary>
        /// Random size variation per trail, and the random spread added to its angle when spawned on a swing.
        /// </summary>
        public FloatRange scaleJitterRange = new FloatRange(1f, 1f);
        public float angleSpread = 0f;
    }

    /// <summary>
    /// A slash-shaped mote that can ripple, echo, and sweep through an arc. Tint it with the def's graphicData color.
    /// </summary>
    [StaticConstructorOnStartup]
    public class Mote_Bladetrail : MoteThrown
    {
        public bool flipped;
        public float scaleJitter = 1f;

        private static readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        private static readonly ModExtension_Bladetrail defaultSettings = new ModExtension_Bladetrail();

        private ModExtension_Bladetrail settings;

        public ModExtension_Bladetrail Settings
        {
            get
            {
                if (settings == null) settings = def.GetModExtension<ModExtension_Bladetrail>() ?? defaultSettings;
                return settings;
            }
        }

        /// <summary>
        /// The angle to draw at this frame: sweeps from one end of the arc to the other while the trail fades in and stays solid,
        /// then holds the end angle while it fades out. Mirrored trails sweep the other way.
        /// </summary>
        private float CurrentDrawAngle
        {
            get
            {
                if(Settings.sweepDegrees == 0f) return exactRotation;
                float sweepDurationSeconds = Mathf.Max(0.0001f, def.mote.fadeInTime + def.mote.solidTime);
                float sweepProgress = Mathf.Clamp01(AgeSecs / sweepDurationSeconds);
                float sweepDirection = flipped ? -1f : 1f;
                float halfSweep = Settings.sweepDegrees / 2f;
                float offset = Mathf.Lerp(-halfSweep, halfSweep, sweepProgress);
                return exactRotation + sweepDirection * offset;
            }
        }

        protected override void DrawAt(Vector3 drawLocation, bool flip = false)
        {
            float alpha = Alpha;
            if (alpha <= 0f) return;

            Material material = Graphic.MatSingle;
            Mesh mesh = flipped ? MeshPool.plane10Flip : MeshPool.plane10;
            Color baseColor = material.color;

            ModExtension_Bladetrail bladetrailSettings = Settings;
            float ageSeconds = AgeSecs;
            float rippleCalming = 1f / (1f + ageSeconds * 0.15f);
            float drawAngle = CurrentDrawAngle;
            Quaternion drawRotation = Quaternion.AngleAxis(drawAngle, Vector3.up);

            for (int echoIndex = 0; echoIndex <= bladetrailSettings.echoCount; echoIndex++)
            {
                float ripple = Mathf.Sin(ageSeconds * bladetrailSettings.rippleSpeed - echoIndex * 0.9f) * bladetrailSettings.rippleAmount * rippleCalming;
                float scale = scaleJitter * bladetrailSettings.sizeMultiplier * (1f + echoIndex * bladetrailSettings.echoGrowth + ripple);

                Color echoColor = baseColor;
                echoColor.a *= alpha * Mathf.Pow(bladetrailSettings.echoFade, echoIndex);
                propertyBlock.SetColor("_Color", echoColor);

                Vector3 echoPosition = drawLocation;
                echoPosition.y -= 0.0005f * echoIndex;

                Matrix4x4 matrix = default;
                matrix.SetTRS(echoPosition, drawRotation, new Vector3(Graphic.drawSize.x * scale, 1f, Graphic.drawSize.y * scale));
                Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, propertyBlock);
            }
        }
    }

    /// <summary>
    /// Spawns bladetrails for swings, reloads and other weapon motions.
    /// </summary>
    public static class Bladetrails
    {
        /// <summary>
        /// Spawns one bladetrail at an exact position and angle.
        /// </summary>
        public static Mote_Bladetrail Spawn(ThingDef bladetrailDefinition, Map map, Vector3 position, float angle, bool flipped)
        {
            if (bladetrailDefinition == null || map == null) return null;
            Mote_Bladetrail bladetrail = (Mote_Bladetrail)ThingMaker.MakeThing(bladetrailDefinition);
            bladetrail.exactPosition = position;
            bladetrail.exactRotation = angle;
            bladetrail.flipped = flipped;
            bladetrail.scaleJitter = bladetrail.Settings.scaleJitterRange.RandomInRange;
            GenSpawn.Spawn(bladetrail, position.ToIntVec3(), map);
            return bladetrail;
        }

        /// <summary>
        /// One specific trail, three quarters of the way from the attacker to the target, angled along the swing.
        /// </summary>
        public static Mote_Bladetrail SpawnSwing(Pawn attacker, Vector3 targetPosition, ThingDef bladetrailDefinition)
        {
            if (attacker?.Map == null || bladetrailDefinition == null) return null;
            Vector3 attackerPosition = attacker.DrawPos;
            Vector3 trailPosition = Vector3.Lerp(attackerPosition, targetPosition, 0.75f);
            float angleSpread = bladetrailDefinition.GetModExtension<ModExtension_Bladetrail>()?.angleSpread ?? 0f;
            float trailAngle = (targetPosition - attackerPosition).AngleFlat() + Rand.Range(-angleSpread, angleSpread);
            return Spawn(bladetrailDefinition, attacker.Map, trailPosition, trailAngle, Rand.Bool);
        }

        /// <summary>
        /// A random trail from the list, three quarters of the way from the attacker to the target, angled along the swing.
        /// </summary>
        public static Mote_Bladetrail SpawnSwing(Pawn attacker, Vector3 targetPosition, List<ThingDef> bladetrailDefinitions)
        {
            if (bladetrailDefinitions.NullOrEmpty()) return null;
            return SpawnSwing(attacker, targetPosition, bladetrailDefinitions.RandomElement());
        }

        /// <summary>
        /// A specific trail at a point on the held weapon, angled the way the weapon is drawn right now (reloads, revving).
        /// </summary>
        public static Mote_Bladetrail SpawnAtWeapon(Pawn pawn, ThingDef bladetrailDefinition, float zoneFraction = 0.5f)
        {
            if (pawn?.Map == null || bladetrailDefinition == null) return null;
            WeaponEffects.TryGetZonePointInWorld(pawn, zoneFraction, out Vector3 weaponPosition, out float weaponAngle);
            return Spawn(bladetrailDefinition, pawn.Map, weaponPosition, weaponAngle, Rand.Bool);
        }

        /// <summary>
        /// A random trail from the list at a point on the held weapon, angled the way the weapon is drawn right now (reloads, revving).
        /// </summary>
        public static Mote_Bladetrail SpawnAtWeapon(Pawn pawn, List<ThingDef> bladetrailDefinitions, float zoneFraction = 0.5f)
        {
            if (bladetrailDefinitions.NullOrEmpty()) return null;
            return SpawnAtWeapon(pawn, bladetrailDefinitions.RandomElement(), zoneFraction);
        }
    }
}
