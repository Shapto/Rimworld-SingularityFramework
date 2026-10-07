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
    /// Works out the shape of any weapon from its texture: its forward direction, length, muzzle, and the tip zone where effects attach.
    /// Works for every weapon mod without per-weapon setup. Results are cached per weapon type.
    /// Must run on the main thread, since it reads textures.
    /// </summary>
    public static class WeaponShapeAnalyzer
    {
        private const float WidthSampleBand = 0.03f;           // how close to the sample point a pixel must be to count for width
        private const float VisibleAlphaThreshold = 0.1f;
        private const float FallbackWidth = 0.1f;
        private const float RestingAimAngle = 143f;               // vanilla's holding angle for a drafted pawn at rest
        private const float LevelHoldThreshold = 0.2f;            // below this, the weapon is held too level to tell "up"
        private const float HeadWidthFactor = 1.6f;      // wider than this times the typical width counts as a head
        private const float ZoneFrontStepBack = 0.02f;        // how far to step back per check, as a fraction of the weapon's length
        private const float ZoneFrontPosition = 0.80f;
        private const float ZoneBackPosition = 0.35f;
        private const int SpineSampleCount = 16;
        private const float TaperWidthFactor = 0.6f;   // narrower than this times the typical width counts as the tapering point
        private const float MuzzleSlicePosition = 0.95f;   // where the barrel's width and center line are measured

        private static readonly Dictionary<ThingDef, WeaponShape> cachedShapes = new Dictionary<ThingDef, WeaponShape>();

        //I have to kill myself
        public static WeaponShape GetShape(ThingDef weaponDef)
        {
            if (cachedShapes.TryGetValue(weaponDef, out WeaponShape cachedShape)) return cachedShape;

            WeaponShape shape = Analyze(weaponDef);
            cachedShapes[weaponDef] = shape;
            return shape;
        }

        private static WeaponShape Analyze(ThingDef weaponDef)
        {
            var shape = new WeaponShape { forward = Vector2.right };

            Texture2D texture = weaponDef.graphic?.MatSingle?.mainTexture as Texture2D;
            if (texture == null) return shape;

            Color[] pixels = ReadPixels(texture);
            int textureWidth = texture.width;
            int textureHeight = texture.height;

            // Forward: exact for guns (their angle offset aims the barrel), read from the pixels for melee weapons.
            Vector2 forward;
            if (weaponDef.IsRangedWeapon)
            {
                float forwardAngleRadians = (90f - weaponDef.equippedAngleOffset) * Mathf.Deg2Rad;
                forward = new Vector2(Mathf.Sin(forwardAngleRadians), Mathf.Cos(forwardAngleRadians));
            }
            else if (!TryFindMeleeForward(pixels, textureWidth, textureHeight, weaponDef.equippedAngleOffset, out forward))
            {
                return shape;
            }
            Vector2 perpendicular = new Vector2(-forward.y, forward.x);
            shape.forward = forward;

            // Pass 1: the weapon's back end and tip along forward.
            float minimumAlong = float.MaxValue;
            float maximumAlong = float.MinValue;
            for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
            {
                if (pixels[pixelIndex].a < VisibleAlphaThreshold) continue;
                float along = Vector2.Dot(LocalPosition(pixelIndex, textureWidth, textureHeight), forward);
                minimumAlong = Mathf.Min(minimumAlong, along);
                maximumAlong = Mathf.Max(maximumAlong, along);
            }
            if (minimumAlong > maximumAlong) return shape;   // no visible pixels at all
            float weaponLength = maximumAlong - minimumAlong;

            // The weapon's typical width: the median of evenly spaced slices along its length.
            var sampledWidths = new List<float>();
            for (int sampleIndex = 1; sampleIndex < 20; sampleIndex++)
            {
                float sampleAlong = minimumAlong + weaponLength * sampleIndex / 20f;
                if (TryMeasureSlice(pixels, textureWidth, textureHeight, forward, perpendicular, sampleAlong, out float sampleWidth, out _)) sampledWidths.Add(sampleWidth);
            }
            if (sampledWidths.Count == 0) return shape;
            sampledWidths.Sort();
            float typicalWidth = sampledWidths[sampledWidths.Count / 2];

            // Muzzle: the weapon's tip, on the center line measured just behind it.
            shape.length = weaponLength;
            float muzzleSliceAlong = minimumAlong + weaponLength * MuzzleSlicePosition;
            if (TryMeasureSlice(pixels, textureWidth, textureHeight, forward, perpendicular, muzzleSliceAlong, out float muzzleWidth, out float muzzleSide))
            {
                shape.muzzlePoint = forward * maximumAlong + perpendicular * muzzleSide;
                shape.muzzleWidth = muzzleWidth;
            }
            else
            {
                shape.muzzlePoint = forward * maximumAlong;
                shape.muzzleWidth = typicalWidth;
            }

            // Front of the zone: stepped back toward the handle while inside a bulky head or a tapering point.
            float zoneFront = ZoneFrontPosition;
            while (zoneFront - ZoneFrontStepBack > ZoneBackPosition)
            {
                float frontAlong = minimumAlong + weaponLength * zoneFront;
                bool measured = TryMeasureSlice(pixels, textureWidth, textureHeight, forward, perpendicular, frontAlong, out float frontWidth, out _);
                bool normalWidth = measured && frontWidth <= typicalWidth * HeadWidthFactor && frontWidth >= typicalWidth * TaperWidthFactor;
                if (normalWidth) break;
                zoneFront -= ZoneFrontStepBack;
            }

            // Spine across the zone: the weapon's center and width at evenly spaced points, from back to front.
            float lastSide = 0f;
            float lastWidth = typicalWidth;
            for (int sampleIndex = 0; sampleIndex < SpineSampleCount; sampleIndex++)
            {
                float samplePosition = Mathf.Lerp(ZoneBackPosition, zoneFront, sampleIndex / (float)(SpineSampleCount - 1));
                float sampleAlong = minimumAlong + weaponLength * samplePosition;

                if (TryMeasureSlice(pixels, textureWidth, textureHeight, forward, perpendicular, sampleAlong, out float sampleWidth, out float sampleSide))
                {
                    lastSide = sampleSide;
                    lastWidth = sampleWidth;
                }
                shape.spinePoints.Add(forward * sampleAlong + perpendicular * lastSide);
                shape.spineWidths.Add(lastWidth);
            }
            return shape;
        }

        /// <summary>
        /// Finds a melee weapon's forward direction from its pixels: the long axis, pointing toward the heavier half
        /// (the blade or head), refined along the front part so hilts and crossguards don't tilt it.
        /// </summary>
        private static bool TryFindMeleeForward(Color[] pixels, int textureWidth, int textureHeight, float equippedAngleOffset, out Vector2 forward)
        {
            forward = Vector2.right;

            // Center of all visible pixels.
            Vector2 positionSum = Vector2.zero;
            int visibleCount = 0;
            for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
            {
                if (pixels[pixelIndex].a < VisibleAlphaThreshold) continue;
                positionSum += LocalPosition(pixelIndex, textureWidth, textureHeight);
                visibleCount++;
            }
            if (visibleCount == 0) return false;
            Vector2 center = positionSum / visibleCount;

            // Long axis: the direction the visible pixels are most spread out in.
            float spreadXX = 0f, spreadYY = 0f, spreadXY = 0f;
            for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
            {
                if (pixels[pixelIndex].a < VisibleAlphaThreshold) continue;
                Vector2 fromCenter = LocalPosition(pixelIndex, textureWidth, textureHeight) - center;
                spreadXX += fromCenter.x * fromCenter.x;
                spreadYY += fromCenter.y * fromCenter.y;
                spreadXY += fromCenter.x * fromCenter.y;
            }
            float axisAngle = 0.5f * Mathf.Atan2(2f * spreadXY, spreadXX - spreadYY);
            forward = new Vector2(Mathf.Cos(axisAngle), Mathf.Sin(axisAngle));

            // Tip: the end on the side holding more of the weapon.
            float minimumAlong = float.MaxValue;
            float maximumAlong = float.MinValue;
            for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
            {
                if (pixels[pixelIndex].a < VisibleAlphaThreshold) continue;
                float along = Vector2.Dot(LocalPosition(pixelIndex, textureWidth, textureHeight), forward);
                minimumAlong = Mathf.Min(minimumAlong, along);
                maximumAlong = Mathf.Max(maximumAlong, along);
            }
            // Tip: at rest, weapons are held like an aim toward the lower right, so the business end points right.
            float restingDrawAngleRadians = (RestingAimAngle - 90f + equippedAngleOffset) * Mathf.Deg2Rad;
            float heldRightward = forward.x * Mathf.Cos(restingDrawAngleRadians) + forward.y * Mathf.Sin(restingDrawAngleRadians);
            float heldUpward = -forward.x * Mathf.Sin(restingDrawAngleRadians) + forward.y * Mathf.Cos(restingDrawAngleRadians);

            bool pointsBackward;
            if (Mathf.Abs(heldRightward) >= LevelHoldThreshold)
            {
                pointsBackward = heldRightward < 0f;
            }
            else if (Mathf.Abs(heldUpward) >= LevelHoldThreshold)
            {
                // Held nearly vertical: the business end is up.
                pointsBackward = heldUpward < 0f;
            }
            else
            {
                float midpointAlong = (minimumAlong + maximumAlong) / 2f;
                pointsBackward = Vector2.Dot(center, forward) > midpointAlong;
            }

            if (pointsBackward)
            {
                forward = -forward;
                float flippedMinimum = -maximumAlong;
                maximumAlong = -minimumAlong;
                minimumAlong = flippedMinimum;
            }

            // Refine along the front part only.
            float weaponLength = maximumAlong - minimumAlong;
            float sliceBand = weaponLength * 0.04f;
            Vector2 rearSliceCenter = SliceCenter(pixels, textureWidth, textureHeight, forward, minimumAlong + weaponLength * 0.55f, sliceBand);
            Vector2 frontSliceCenter = SliceCenter(pixels, textureWidth, textureHeight, forward, minimumAlong + weaponLength * 0.90f, sliceBand);
            if (frontSliceCenter != rearSliceCenter) forward = (frontSliceCenter - rearSliceCenter).normalized;

            return true;
        }

        /// <summary>
        /// The average position of the visible pixels within the band around a point along the weapon.
        /// </summary>
        private static Vector2 SliceCenter(Color[] pixels, int textureWidth, int textureHeight, Vector2 forward, float alongTarget, float band)
        {
            Vector2 positionSum = Vector2.zero;
            int pixelCount = 0;
            for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
            {
                if (pixels[pixelIndex].a < VisibleAlphaThreshold) continue;
                Vector2 localPosition = LocalPosition(pixelIndex, textureWidth, textureHeight);
                if (Mathf.Abs(Vector2.Dot(localPosition, forward) - alongTarget) > band) continue;

                positionSum += localPosition;
                pixelCount++;
            }
            return pixelCount > 0 ? positionSum / pixelCount : Vector2.zero;
        }

        /// <summary>
        /// The weapon's width and center line across one slice. False if the slice has no visible pixels.
        /// </summary>
        private static bool TryMeasureSlice(Color[] pixels, int textureWidth, int textureHeight, Vector2 forward, Vector2 perpendicular, float alongTarget, out float width, out float sideCenter)
        {
            width = 0f;
            sideCenter = 0f;
            float minimumSide = float.MaxValue;
            float maximumSide = float.MinValue;
            for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
            {
                if (pixels[pixelIndex].a < VisibleAlphaThreshold) continue;
                Vector2 localPosition = LocalPosition(pixelIndex, textureWidth, textureHeight);
                if (Mathf.Abs(Vector2.Dot(localPosition, forward) - alongTarget) > WidthSampleBand) continue;

                float side = Vector2.Dot(localPosition, perpendicular);
                minimumSide = Mathf.Min(minimumSide, side);
                maximumSide = Mathf.Max(maximumSide, side);
            }
            if (minimumSide > maximumSide) return false;

            width = maximumSide - minimumSide;
            sideCenter = (minimumSide + maximumSide) / 2f;
            return true;
        }

        /// <summary>
        /// Textures on the graphics card can't be read directly, so this copies one into a readable form.
        /// </summary>
        internal static Color[] ReadPixels(Texture2D source)
        {
            RenderTexture temporaryTexture = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, temporaryTexture);

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = temporaryTexture;
            var readableTexture = new Texture2D(source.width, source.height, TextureFormat.ARGB32, false);
            readableTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readableTexture.Apply();
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(temporaryTexture);

            Color[] pixels = readableTexture.GetPixels();
            UnityEngine.Object.Destroy(readableTexture);
            return pixels;
        }

        /// <summary>
        /// A pixel's position in sprite units (-0.5 to 0.5), with Unity textures starting at the bottom-left.
        /// </summary>
        private static Vector2 LocalPosition(int pixelIndex, int textureWidth, int textureHeight)
        {
            float localX = (pixelIndex % textureWidth + 0.5f) / textureWidth - 0.5f;
            float localY = (pixelIndex / textureWidth + 0.5f) / textureHeight - 0.5f;
            return new Vector2(localX, localY);
        }
    }
}
