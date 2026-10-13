using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SingularityFramework.WeaponAnchor
{
    /// <summary>
    /// A named set of points on a weapon's texture, plus the direction effects leave them in.
    /// Used either as separate points (a gunblade's exhausts) or as a line traced along the sprite (a burning blade's edge).
    /// Points and direction are in image pixels as an image editor shows them: x to the right, y downward.
    /// </summary>
    public class WeaponAnchorGroup
    {
        public string name;
        public List<Vector2> pixelPoints = new List<Vector2>();
        public Vector2 pixelDirection = new Vector2(0f, -1f);

        public int PointCount => pixelPoints.Count;

        public Vector2 SpriteDirection => new Vector2(pixelDirection.x, -pixelDirection.y).normalized;

        /// <summary>
        /// A point converted to sprite units (-0.5 to 0.5, y up)
        /// </summary>
        public Vector2 SpritePoint(int pointIndex, Vector2 textureSize) => PixelToSprite(pixelPoints[pointIndex], textureSize);

        /// <summary>
        /// A random point along the line through all the points, in sprite units. Longer segments get picked more often.
        /// </summary>
        public Vector2 RandomSpritePointAlongLine(Vector2 textureSize)
        {
            if (pixelPoints.Count == 1) return SpritePoint(0, textureSize);

            float totalLength = 0f;
            for (int segmentIndex = 0; segmentIndex < pixelPoints.Count - 1; segmentIndex++)
                totalLength += Vector2.Distance(pixelPoints[segmentIndex], pixelPoints[segmentIndex + 1]);

            float distanceAlongLine = Rand.Range(0f, totalLength);
            for (int segmentIndex = 0; segmentIndex < pixelPoints.Count - 1; segmentIndex++)
            {
                float segmentLength = Vector2.Distance(pixelPoints[segmentIndex], pixelPoints[segmentIndex + 1]);
                if (distanceAlongLine <= segmentLength || segmentIndex == pixelPoints.Count - 2)
                {
                    float segmentFraction = segmentLength > 0f ? Mathf.Clamp01(distanceAlongLine / segmentLength) : 0f;
                    return PixelToSprite(Vector2.Lerp(pixelPoints[segmentIndex], pixelPoints[segmentIndex + 1], segmentFraction), textureSize);
                }
                distanceAlongLine -= segmentLength;
            }
            return SpritePoint(pixelPoints.Count - 1, textureSize);
        }

        private static Vector2 PixelToSprite(Vector2 pixelPoint, Vector2 textureSize)
        {
            return new Vector2((pixelPoint.x + 0.5f) / textureSize.x - 0.5f, 0.5f - (pixelPoint.y + 0.5f) / textureSize.y);
        }
    }

    /// <summary>
    /// Marks named points on a weapon's texture so effects can be spawned exactly there while it's held.
    /// </summary>
    public class ModExtension_WeaponAnchors : DefModExtension
    {
        public Vector2 textureSize = new Vector2(1024f, 1024f);
        public List<WeaponAnchorGroup> anchorGroups = new List<WeaponAnchorGroup>();

        public WeaponAnchorGroup GroupNamed(string groupName) => anchorGroups.FirstOrDefault(group => group.name == groupName);
    }
}
