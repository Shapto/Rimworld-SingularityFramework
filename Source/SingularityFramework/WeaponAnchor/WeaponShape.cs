using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.WeaponAnchor
{
    /// <summary>
    /// The shape of a weapon sprite, in sprite units (the texture spans -0.5 to 0.5).
    /// The tip zone is a stretch of the weapon's spine near its far end, where effects can attach to the weapon.
    /// Positions in the zone run from the handle side (0) toward the tip (1).
    /// </summary>
    public class WeaponShape
    {
        public Vector2 forward;
        public List<Vector2> spinePoints = new List<Vector2>();   // the weapon's center line across the tip zone, back to front
        public List<float> spineWidths = new List<float>();       // weapon width at each spine point
        public Vector2 muzzlePoint;   // the barrel's tip, on its center line (ranged weapons)
        public float muzzleWidth;     // barrel width at the muzzle
        public float length;          // the weapon's length along forward, in sprite units

        /// <summary>
        /// The point at a given fraction of the tip zone (0 = handle side, 1 = tip),
        /// which way the weapon runs there, and how wide it is.
        /// </summary>
        public bool TryGetZonePoint(float zoneFraction, out Vector2 position, out Vector2 direction, out float width)
        {
            position = Vector2.zero;
            direction = forward;
            width = 0f;
            if (spinePoints.Count == 0) return false;
            if (spinePoints.Count == 1)
            {
                position = spinePoints[0];
                width = spineWidths[0];
                return true;
            }

            float exactIndex = Mathf.Clamp01(zoneFraction) * (spinePoints.Count - 1);
            int lowerIndex = Mathf.Clamp(Mathf.FloorToInt(exactIndex), 0, spinePoints.Count - 2);
            float blend = exactIndex - lowerIndex;

            position = Vector2.Lerp(spinePoints[lowerIndex], spinePoints[lowerIndex + 1], blend);
            width = Mathf.Lerp(spineWidths[lowerIndex], spineWidths[lowerIndex + 1], blend);

            Vector2 localDirection = spinePoints[lowerIndex + 1] - spinePoints[lowerIndex];
            direction = localDirection.sqrMagnitude > 0f ? localDirection.normalized : forward;
            return true;
        }

        /// <summary>
        /// Where slot number "slotIndex" (0 = handle side) of "slotCount" evenly spaced slots sits in the tip zone,
        /// for effects that line up several items along the weapon. A single slot goes in the middle.
        /// </summary>
        public bool TryGetSlotPlacement(int slotIndex, int slotCount, out Vector2 position, out Vector2 direction, out float width)
        {
            float zoneFraction = slotCount <= 1 ? 0.5f : (float)slotIndex / (slotCount - 1);
            return TryGetZonePoint(zoneFraction, out position, out direction, out width);
        }
    }
}
