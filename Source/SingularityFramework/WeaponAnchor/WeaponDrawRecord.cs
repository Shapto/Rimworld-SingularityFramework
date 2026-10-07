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
    /// Where a pawn's weapon was drawn this frame. Filled by the vanilla drawing patch, and overridden by animation mod patches when they draw the weapon,
    /// so effects anchored to the weapon always follow it as it's actually shown.
    /// </summary>
    public class WeaponDrawRecord
    {
        public ThingDef weaponDef;
        public Vector3 position;
        public float drawAngle;     // degrees, clockwise on screen
        public bool flipped;        // drawn mirrored
        public Vector2 drawSize;
        public int frame;

        // Set when the weapon was drawn by an animation mod with a full transform, instead of vanilla's angle and size.
        public bool usesMatrix;
        public Matrix4x4 drawMatrix;
        public bool flippedVertically;

        private static readonly Dictionary<Pawn, WeaponDrawRecord> recordsByPawn = new Dictionary<Pawn, WeaponDrawRecord>();

        public static void Record(Pawn pawn, ThingDef weaponDef, Vector3 position, float drawAngle, bool flipped, Vector2 drawSize)
        {
            if (!recordsByPawn.TryGetValue(pawn, out WeaponDrawRecord record))
            {
                record = new WeaponDrawRecord();
                recordsByPawn[pawn] = record;
            }

            // An animation mod's record for this frame takes priority over vanilla's.
            if (record.usesMatrix && record.frame == Time.frameCount) return;

            record.weaponDef = weaponDef;
            record.position = position;
            record.drawAngle = drawAngle;
            record.flipped = flipped;
            record.drawSize = drawSize;
            record.frame = Time.frameCount;
            record.usesMatrix = false;
        }

        /// <summary>
        /// Records a weapon drawn with a full transform matrix (for example by an animation mod).
        /// The matrix maps the weapon's quad (-0.5 to 0.5 on x and z) to the world, like vanilla's plane mesh.
        /// </summary>
        public static void RecordMatrix(Pawn pawn, ThingDef weaponDef, Matrix4x4 drawMatrix, bool flippedHorizontally, bool flippedVertically)
        {
            if (!recordsByPawn.TryGetValue(pawn, out WeaponDrawRecord record))
            {
                record = new WeaponDrawRecord();
                recordsByPawn[pawn] = record;
            }
            record.weaponDef = weaponDef;
            record.usesMatrix = true;
            record.drawMatrix = drawMatrix;
            record.flipped = flippedHorizontally;
            record.flippedVertically = flippedVertically;
            record.position = drawMatrix.MultiplyPoint3x4(Vector3.zero);
            record.frame = Time.frameCount;
        }



        /// <summary>
        /// The pawn's weapon transform, only if it was drawn this frame (otherwise the weapon isn't visible).
        /// </summary>
        public static bool TryGetCurrent(Pawn pawn, out WeaponDrawRecord record)
        {
            return recordsByPawn.TryGetValue(pawn, out record) && record.frame >= Time.frameCount - 1;
        }

        /// <summary>
        /// Turns a length in sprite units into world units, using the weapon's average draw size.
        /// </summary>
        public float SpriteLengthToWorld(float spriteLength)
        {
            if (usesMatrix)
            {
                float averageScale = (drawMatrix.MultiplyVector(Vector3.right).magnitude + drawMatrix.MultiplyVector(Vector3.forward).magnitude) / 2f;
                return spriteLength * averageScale;
            }
            return spriteLength * (drawSize.x + drawSize.y) / 2f;
        }

        /// <summary>
        /// Turns a point on the weapon's sprite (in sprite units, -0.5 to 0.5) into a position in the world.
        /// </summary>
        public Vector3 SpritePointToWorld(Vector2 spritePoint)
        {
            if (usesMatrix)
            {
                Vector3 localPoint = new Vector3(spritePoint.x * (flipped ? -1f : 1f), 0f, spritePoint.y * (flippedVertically ? -1f : 1f));
                return drawMatrix.MultiplyPoint3x4(localPoint);
            }

            float localX = spritePoint.x * drawSize.x * (flipped ? -1f : 1f);
            float localY = spritePoint.y * drawSize.y;
            Vector3 rotatedOffset = Quaternion.AngleAxis(drawAngle, Vector3.up) * new Vector3(localX, 0f, localY);
            return position + rotatedOffset;
        }
    }
}
