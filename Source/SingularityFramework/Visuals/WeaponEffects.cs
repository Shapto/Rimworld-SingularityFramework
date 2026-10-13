using RimWorld;
using SingularityFramework.WeaponAnchor;
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
    /// Spawns visual effects at points on a pawn's held weapon, as it's currently drawn (including animation mods).
    /// Positions along the weapon use the tip zone: 0 is the handle side, 1 is the tip.
    /// Falls back to the pawn's position when the weapon isn't visible.
    /// </summary>
    public static class WeaponEffects
    {
        /// <summary>
        /// The world position of a point in the weapon's tip zone, and the angle the weapon runs at there.
        /// Returns false (with the pawn's position) when the weapon wasn't drawn recently.
        /// </summary>
        public static bool TryGetZonePointInWorld(Pawn pawn, float zoneFraction, out Vector3 worldPosition, out float worldAngle)
        {
            worldPosition = pawn.DrawPos;
            worldAngle = pawn.Rotation.AsAngle;

            if (!WeaponDrawRecord.TryGetCurrent(pawn, out WeaponDrawRecord record) || record.weaponDef == null) return false;

            WeaponShape shape = WeaponShapeAnalyzer.GetShape(record.weaponDef);
            if (!shape.TryGetZonePoint(zoneFraction, out Vector2 spritePosition, out Vector2 spriteDirection, out _)) return false;

            worldPosition = record.SpritePointToWorld(spritePosition);
            Vector3 aheadPosition = record.SpritePointToWorld(spritePosition + spriteDirection * 0.1f);
            worldAngle = (aheadPosition - worldPosition).AngleFlat();
            return true;
        }

        /// <summary>
        /// The world position of a weapon's tip when the weapon's sprite is drawn with this matrix by something other than a pawn,
        /// such as a thrown weapon in flight. Uses the same tip point the shape analyzer finds for held weapons.
        /// </summary>
        public static Vector3 TipInWorld(ThingDef weaponDefinition, Matrix4x4 drawMatrix)
        {
            Vector2 tipPoint = WeaponShapeAnalyzer.GetShape(weaponDefinition).muzzlePoint;
            return drawMatrix.MultiplyPoint3x4(new Vector3(tipPoint.x, 0f, tipPoint.y));
        }

        /// <summary>
        /// The world position of the weapon's muzzle (ranged weapons, or the gun part of a gunblade).
        /// </summary>
        public static bool TryGetMuzzleInWorld(Pawn pawn, out Vector3 worldPosition)
        {
            worldPosition = pawn.DrawPos;
            if (!WeaponDrawRecord.TryGetCurrent(pawn, out WeaponDrawRecord record) || record.weaponDef == null) return false;

            worldPosition = record.SpritePointToWorld(WeaponShapeAnalyzer.GetShape(record.weaponDef).muzzlePoint);
            return true;
        }

        /// <summary>
        /// Throws a fleck at a point in the weapon's tip zone, rotated to match the weapon there.
        /// </summary>
        public static void ThrowFleckAtZonePoint(Pawn pawn, FleckDef fleckDefinition, float zoneFraction, float scale = 1f)
        {
            if (pawn?.Map == null || fleckDefinition == null) return;

            TryGetZonePointInWorld(pawn, zoneFraction, out Vector3 worldPosition, out float worldAngle);
            FleckCreationData fleckData = FleckMaker.GetDataStatic(worldPosition, pawn.Map, fleckDefinition, scale);
            fleckData.rotation = worldAngle;
            pawn.Map.flecks.CreateFleck(fleckData);
        }

        /// <summary>
        /// Throws a fleck at the weapon's muzzle.
        /// </summary>
        public static void ThrowFleckAtMuzzle(Pawn pawn, FleckDef fleckDefinition, float scale = 1f)
        {
            if (pawn?.Map == null || fleckDefinition == null) return;

            TryGetMuzzleInWorld(pawn, out Vector3 worldPosition);
            pawn.Map.flecks.CreateFleck(FleckMaker.GetDataStatic(worldPosition, pawn.Map, fleckDefinition, scale));
        }

        /// <summary>
        /// The world position of a point on the held weapon's sprite, and the world direction of a sprite-space direction there.
        /// Handles mirroring and animation mods through the weapon's draw record. False when the weapon wasn't drawn recently.
        /// </summary>
        public static bool TryGetSpritePointInWorld(Pawn pawn, Vector2 spritePoint, Vector2 spriteDirection, out Vector3 worldPosition, out Vector3 worldDirection)
        {
            worldPosition = pawn.DrawPos;
            worldDirection = Vector3.forward;
            if (!WeaponDrawRecord.TryGetCurrent(pawn, out WeaponDrawRecord record) || record.weaponDef == null) return false;

            worldPosition = record.SpritePointToWorld(spritePoint);
            Vector3 aheadPosition = record.SpritePointToWorld(spritePoint + spriteDirection * 0.1f);
            worldDirection = aheadPosition - worldPosition;
            worldDirection.y = 0f;
            worldDirection = worldDirection.normalized;
            return true;
        }

        /// <summary>
        /// The held weapon's anchor group by name, or null if it has none (or isn't being drawn).
        /// </summary>
        public static WeaponAnchorGroup AnchorGroupOf(Pawn pawn, string groupName, out Vector2 textureSize)
        {
            textureSize = Vector2.one;
            if (!WeaponDrawRecord.TryGetCurrent(pawn, out WeaponDrawRecord record) || record.weaponDef == null) return null;

            ModExtension_WeaponAnchors anchors = record.weaponDef.GetModExtension<ModExtension_WeaponAnchors>();
            if (anchors == null) return null;
            textureSize = anchors.textureSize;
            return anchors.GroupNamed(groupName);
        }

        /// <summary>
        /// One anchor point of the held weapon in the world, with its outward direction. Use for exact spots like exhausts.
        /// </summary>
        public static bool TryGetAnchorInWorld(Pawn pawn, string groupName, int pointIndex, out Vector3 worldPosition, out Vector3 worldDirection)
        {
            worldPosition = pawn.DrawPos;
            worldDirection = Vector3.forward;
            WeaponAnchorGroup anchorGroup = AnchorGroupOf(pawn, groupName, out Vector2 textureSize);
            if (anchorGroup == null || pointIndex < 0 || pointIndex >= anchorGroup.PointCount) return false;

            return TryGetSpritePointInWorld(pawn, anchorGroup.SpritePoint(pointIndex, textureSize), anchorGroup.SpriteDirection, out worldPosition, out worldDirection);
        }

        /// <summary>
        /// A random spot along an anchor group's line in the world, with its outward direction. Use for effects spread along a blade.
        /// </summary>
        public static bool TryGetRandomAnchorAlongLine(Pawn pawn, string groupName, out Vector3 worldPosition, out Vector3 worldDirection)
        {
            worldPosition = pawn.DrawPos;
            worldDirection = Vector3.forward;
            WeaponAnchorGroup anchorGroup = AnchorGroupOf(pawn, groupName, out Vector2 textureSize);
            if (anchorGroup == null || anchorGroup.PointCount == 0) return false;

            return TryGetSpritePointInWorld(pawn, anchorGroup.RandomSpritePointAlongLine(textureSize), anchorGroup.SpriteDirection, out worldPosition, out worldDirection);
        }
    }
}
