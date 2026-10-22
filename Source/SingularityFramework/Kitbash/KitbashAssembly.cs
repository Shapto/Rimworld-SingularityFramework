using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// One piece as placed: which piece, where (piece pixels to canvas pixels), its outline on the canvas and the pixels
    /// it covers. Overlays laid over gaps aren't part of the edge-to-edge tiling, so later pieces don't attach to them.
    /// </summary>
    public class PlacedPiece
    {
        public KitbashPiece piece;
        public Affine2D placement;
        public ConvexPolygon outline;
        public List<PixelSpan> spans;
        public bool inTiling;
    }

    /// <summary>
    /// Builds a shape out of pieces, the way an artist kitbashes: outline the shape with big pieces, fill it big to small,
    /// each new piece laid edge to edge against the ones already there like lego, then cover what's left with pieces
    /// laid over the gaps. Optionally the top edge is crowned with pointed pieces standing straight up.
    /// </summary>
    public class KitbashAssembly
    {
        // no piece may end up thinner than this share of the big piece length: squashed slivers read as clutter
        private static readonly Dictionary<KitbashSize, float> MinimumThickness = new Dictionary<KitbashSize, float>
        {
            { KitbashSize.Large, 0.2f },
            { KitbashSize.Medium, 0.14f },
            { KitbashSize.Small, 0.1f }
        };

        /// <summary>
        /// How a shape's top can end: how long a stretch of top edge each point stands on, how tall the points are
        /// (both as shares of the big piece length), and at most how many points.
        /// </summary>
        private struct CrownStyle
        {
            public float weight;
            public float segmentMinimum, segmentMaximum;
            public float heightMinimum, heightMaximum;
            public int maximumPoints;
        }

        private static readonly CrownStyle[] CrownStyles =
        {
            new CrownStyle { weight = 0.35f, segmentMinimum = 0.26f, segmentMaximum = 0.34f, heightMinimum = 0.3f, heightMaximum = 0.7f, maximumPoints = 3 },    // several
            new CrownStyle { weight = 0.35f, segmentMinimum = 0.32f, segmentMaximum = 0.45f, heightMinimum = 0.35f, heightMaximum = 0.75f, maximumPoints = 2 },  // a pair
            new CrownStyle { weight = 0.3f, segmentMinimum = 0.4f, segmentMaximum = 0.55f, heightMinimum = 0.45f, heightMaximum = 0.9f, maximumPoints = 1 }      // one tall point
        };

        public readonly PixelMask target;    // what the assembly tries to cover
        public readonly PixelMask allowed;   // where pieces may go at all
        public readonly List<KitbashPiece> pieces;
        public readonly float bigLength;     // how long a big piece is, in canvas pixels
        public readonly int scale;           // canvas pixels per texture pixel; small distances are measured in these

        /// <summary>
        /// Where the shape's real outline is. The outline pass only lays pieces along outline inside this (null: all of it).
        /// </summary>
        public PixelMask outlineToFollow;

        /// <summary>
        /// Seeds the crown style separately, so every facing of one pawn ends the same way. Null: uses the build's own dice.
        /// </summary>
        public int? crownStyleSeed;

        public bool crowned = true;

        public readonly List<PlacedPiece> placed = new List<PlacedPiece>();

        private readonly KitbashRandom random;
        private readonly PixelMask covered;
        private readonly PixelMask tiling;
        private readonly Dictionary<string, int> usage = new Dictionary<string, int>();
        private PixelMask tilingBorder;   // a band around the tiling's outline, for measuring how much a new piece touches it

        public KitbashAssembly(PixelMask target, PixelMask allowed, List<KitbashPiece> pieces, KitbashRandom random, float bigLength, int scale)
        {
            this.target = target;
            this.allowed = allowed;
            this.pieces = pieces;
            this.random = random;
            this.bigLength = bigLength;
            this.scale = scale;
            covered = new PixelMask(target.width, target.height);
            tiling = new PixelMask(target.width, target.height);
        }

        public int Width => target.width;
        public int Height => target.height;

        /// <summary>
        /// Everything the placed pieces cover so far.
        /// </summary>
        public PixelMask Covered()
        {
            var mask = new PixelMask(Width, Height);
            foreach (PlacedPiece placedPiece in placed) mask.Fill(placedPiece.spans);
            return mask;
        }

        // ----- the whole process -----

        public KitbashAssembly Build()
        {
            float bigMinimum = bigLength * 0.7f, bigMaximum = bigLength * 1.3f;
            float mediumMinimum = bigLength * 0.4f, mediumMaximum = bigLength * 0.8f;
            float smallMinimum = bigLength * 0.2f, smallMaximum = bigLength * 0.5f;

            List<Edge> targetOutline = OutlineEdges(target, bigLength * 0.45f, random, bigLength * 0.12f);
            if (outlineToFollow != null) targetOutline = targetOutline.FindAll(edge => outlineToFollow[Mathf.FloorToInt(edge.Middle.x), Mathf.FloorToInt(edge.Middle.y)]);

            // 0. the top row: pointed pieces standing on the shape's top edge, tip up
            if (crowned) CrownTopEdge();
            // 1. outline with big pieces, laid along the shape's own outline, inside it
            PlaceOnEdges(() => RemainingOutline(targetOutline), KitbashSize.Large, bigMinimum, bigMaximum, 0.6f, 1.0f, 0.05f, 60, 7, 0.6f);
            // 2. fill with big, then medium, then small, edge to edge
            PlaceOnEdges(FreeEdges, KitbashSize.Large, bigMinimum, bigMaximum, 0.7f, 1.3f, 0.05f, 80, 4, 0.35f);
            PlaceOnEdges(FreeEdges, KitbashSize.Medium, mediumMinimum, mediumMaximum, 0.7f, 1.3f, 0.06f, 80, 4, 0.5f);
            PlaceOnEdges(FreeEdges, KitbashSize.Small, smallMinimum, smallMaximum, 0.7f, 1.4f, 0.08f, 80, 2, 0.8f);
            // 3. whatever is left: pieces laid over the gaps
            PatchGaps(8);
            return this;
        }

        /// <summary>
        /// Draws every placed piece in order, then closes the hairline seams between them.
        /// </summary>
        public KitbashCanvas Render()
        {
            var canvas = new KitbashCanvas(Width, Height);
            foreach (PlacedPiece placedPiece in placed) canvas.DrawPiece(placedPiece.piece, placedPiece.placement);
            canvas.CloseSeams(scale * 2, SeamHoleArea);
            return canvas;
        }

        // ----- bookkeeping -----

        private void Add(KitbashPiece piece, Affine2D placement, ConvexPolygon outline, List<PixelSpan> spans, bool inTiling)
        {
            usage[piece.name] = UsageOf(piece) + 1;
            placed.Add(new PlacedPiece { piece = piece, placement = placement, outline = outline, spans = spans, inTiling = inTiling });
            covered.Fill(spans);
            if (!inTiling) return;
            tiling.Fill(spans);
            tilingBorder = null;
        }

        private int UsageOf(KitbashPiece piece)
        {
            return usage.TryGetValue(piece.name, out int count) ? count : 0;
        }

        /// <summary>
        /// Discourages reusing a piece, and forbids putting a piece right next to another copy of itself.
        /// False when the placement is forbidden.
        /// </summary>
        private bool TryRepeatPenalty(KitbashPiece piece, ConvexPolygon outline, out float penalty)
        {
            penalty = 0f;
            foreach (PlacedPiece placedPiece in placed)
            {
                if (placedPiece.piece == piece && outline.DistanceTo(placedPiece.outline) < 2 * scale) return false;
            }
            penalty = UsageOf(piece) * bigLength * 3f;
            return true;
        }

        /// <summary>
        /// How much of a new piece's outline runs along the tiling's outline, in pixels.
        /// </summary>
        private float Contact(ConvexPolygon outline)
        {
            if (!tiling.Any()) return 0f;
            if (tilingBorder == null) tilingBorder = tiling.Dilated(scale).AndNot(tiling.Eroded(scale));
            float contact = 0f;
            foreach (Vector2 sample in outline.SamplePerimeter(1f)) if (tilingBorder[Mathf.FloorToInt(sample.x), Mathf.FloorToInt(sample.y)]) contact += 1f;
            return contact;
        }

        private List<KitbashPiece> PiecesOfSize(KitbashSize size)
        {
            return pieces.FindAll(piece => piece.size == size);
        }

        // ----- edges to build on -----

        /// <summary>
        /// A shape's outline as a chain of straight segments, roughly "segmentLength" long. "greeble" pushes each corner in
        /// or out by up to that many pixels, so pieces laid along it make an irregular edge instead of tracing it.
        /// </summary>
        public static List<Edge> OutlineEdges(PixelMask mask, float segmentLength, KitbashRandom random, float greeble)
        {
            var edges = new List<Edge>();
            List<List<Vector2>> rings = mask.OutlineRings();
            if (rings.Count == 0) return edges;
            List<Vector2> longest = rings[0];
            foreach (List<Vector2> ring in rings) if (ring.Count > longest.Count) longest = ring;

            List<Vector2> corners = ConvexPolygon.SimplifyRing(longest, segmentLength * 0.15f);
            if (random != null && greeble > 0f)
            {
                Vector2 middle = Vector2.zero;
                foreach (Vector2 corner in corners) middle += corner;
                middle /= corners.Count;
                for (int index = 0; index < corners.Count; index++)
                {
                    Vector2 outward = (corners[index] - middle).normalized;
                    corners[index] += outward * random.Uniform(-greeble, greeble);
                }
            }
            for (int index = 0; index < corners.Count; index++)
            {
                Vector2 start = corners[index];
                Vector2 end = corners[(index + 1) % corners.Count];
                int count = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(start, end) / segmentLength));
                for (int step = 0; step < count; step++) edges.Add(new Edge(start + (end - start) * (step / (float)count), start + (end - start) * ((step + 1) / (float)count)));
            }
            return edges;
        }

        /// <summary>
        /// The outline segments no piece has reached yet.
        /// </summary>
        private List<Edge> RemainingOutline(List<Edge> targetOutline)
        {
            PixelMask band = covered.Dilated(scale);
            return targetOutline.FindAll(edge => !band[Mathf.FloorToInt(edge.Middle.x), Mathf.FloorToInt(edge.Middle.y)]);
        }

        /// <summary>
        /// The open edges of the tiling so far (its outer outline and the outlines of holes in it), wherever they're still
        /// inside the shape: the places a new piece can be laid against.
        /// </summary>
        private List<Edge> FreeEdges()
        {
            var edges = new List<Edge>();
            if (!tiling.Any()) return edges;
            foreach (List<Vector2> ring in tiling.OutlineRings())
            {
                List<Vector2> corners = ConvexPolygon.SimplifyRing(ring, scale * 0.5f);
                for (int index = 0; index < corners.Count; index++)
                {
                    var edge = new Edge(corners[index], corners[(index + 1) % corners.Count]);
                    if (edge.Length < 2.5f * scale) continue;
                    if (target[Mathf.FloorToInt(edge.Middle.x), Mathf.FloorToInt(edge.Middle.y)]) edges.Add(edge);
                }
            }
            return edges;
        }

        // ----- placing pieces edge to edge -----

        private struct Candidate
        {
            public KitbashPiece piece;
            public Affine2D placement;
            public ConvexPolygon outline;
        }

        /// <summary>
        /// Every way one random piece of this size can lay one of its edges along the given edge, either way round.
        /// </summary>
        private List<Candidate> CandidatesOnEdge(Edge edge, List<KitbashPiece> sized, float lengthMinimum, float lengthMaximum, float stretchMinimum, float stretchMaximum)
        {
            var results = new List<Candidate>();
            KitbashPiece piece = random.Choice(sized);
            bool mirrored = random.Chance(0.5f);
            Affine2D mirror = piece.MirrorTransform(mirrored);
            Vector2[] outline = piece.OutlinePoints(mirrored);
            int edgeIndex = random.Index(outline.Length);
            Vector2 sourceStart = outline[edgeIndex];
            Vector2 sourceEnd = outline[(edgeIndex + 1) % outline.Length];
            float stretch = random.Uniform(stretchMinimum, stretchMaximum);
            foreach (Edge targetEdge in new[] { edge, edge.Reversed })
            {
                if (!Affine2D.TryLayEdge(sourceStart, sourceEnd, targetEdge.start, targetEdge.end, stretch, out Affine2D layEdge)) continue;
                Affine2D placement = Affine2D.Compose(layEdge, mirror);
                ConvexPolygon placedOutline = piece.outline.Transformed(placement);
                if (placedOutline.Area < 1f) continue;
                float longest = placedOutline.LongestSide;
                if (longest >= lengthMinimum && longest <= lengthMaximum) results.Add(new Candidate { piece = piece, placement = placement, outline = placedOutline });
            }
            return results;
        }

        /// <summary>
        /// Round after round, adds the best-scoring piece of this size laid along one of the available edges: the one that
        /// covers the most new ground, spills least outside the shape and sits snuggest against what's there.
        /// </summary>
        private void PlaceOnEdges(System.Func<List<Edge>> edgesSource, KitbashSize size, float lengthMinimum, float lengthMaximum, float stretchMinimum, float stretchMaximum, float maximumOverlap, int attempts, int rounds, float spillPenalty)
        {
            List<KitbashPiece> sized = PiecesOfSize(size);
            if (sized.Count == 0) return;
            for (int round = 0; round < rounds; round++)
            {
                List<Edge> edges = edgesSource();
                if (edges.Count == 0) return;
                PixelMask uncovered = target.AndNot(covered);
                bool hasTiling = tiling.Any();

                bool found = false;
                float bestScore = 0f;
                Candidate best = default(Candidate);
                List<PixelSpan> bestSpans = null;
                for (int attempt = 0; attempt < attempts; attempt++)
                {
                    Edge edge = random.Choice(edges);
                    foreach (Candidate candidate in CandidatesOnEdge(edge, sized, lengthMinimum, lengthMaximum, stretchMinimum, stretchMaximum))
                    {
                        if (candidate.outline.Thickness < bigLength * MinimumThickness[candidate.piece.size]) continue;
                        List<PixelSpan> spans = candidate.outline.Rasterize(Width, Height);
                        int area = PixelMask.CountSpans(spans);
                        if (area == 0) continue;
                        if (hasTiling && tiling.CountIn(spans) / (float)area > maximumOverlap) continue;
                        if (allowed.CountIn(spans) / (float)area < 0.95f) continue;
                        if (!TryRepeatPenalty(candidate.piece, candidate.outline, out float penalty)) continue;

                        int gain = uncovered.CountIn(spans);
                        int spill = area - target.CountIn(spans);
                        float score = gain - spill * spillPenalty + Contact(candidate.outline) * 2f - penalty;
                        if (found && score <= bestScore) continue;
                        found = true;
                        bestScore = score;
                        best = candidate;
                        bestSpans = spans;
                    }
                }
                if (!found || bestScore <= 0f) return;
                Add(best.piece, best.placement, best.outline, bestSpans, true);
            }
        }

        // ----- pieces laid over whatever is left -----

        /// <summary>
        /// For each leftover gap: a medium or big piece laid on top, stretched to cover it. Keeps going until no hole is
        /// left inside the shape (or it runs out of passes): a hole walled in by pieces is never left open.
        /// </summary>
        public void PatchGaps(int passes)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                // each pass that still finds holes is less fussy about how much of a hole one piece must cover
                float holeCoverage = Mathf.Max(0.4f, 0.8f - 0.1f * pass);
                if (!PatchGapsOnce(holeCoverage)) return;
            }
        }

        private bool PatchGapsOnce(float holeCoverage)
        {
            // holes in the outlines, and holes in the finished picture: a crystal's outline is its convex hull, so notches in
            // its pixels can leave holes the outlines don't show, and closing the seams can wall a gap in
            PixelMask drawn = DrawnMask().Closed(scale * 2);
            PixelMask enclosed = covered.HolesFilled().AndNot(covered).Or(drawn.HolesFilled().AndNot(drawn).And(allowed));
            enclosed = WithoutSmallPieces(enclosed, SeamHoleArea);
            PixelMask uncovered = target.AndNot(covered).Opened(1).Or(enclosed);
            int[] labels = uncovered.Label(out int gapCount);
            if (gapCount == 0) return false;

            // each gap's size and shape, in one pass over the pixels
            var spreads = new PointSpread.Accumulator[gapCount + 1];
            var isHole = new bool[gapCount + 1];
            for (int index = 0; index < labels.Length; index++)
            {
                int label = labels[index];
                if (label == 0) continue;
                if (spreads[label] == null) spreads[label] = new PointSpread.Accumulator();
                spreads[label].Add(index % Width, index / Width);
                if (enclosed.cells[index]) isHole[label] = true;
            }
            // a big walled-in area is just a gap the fill didn't reach: it gets a stretched piece like any gap, since no
            // single piece in its own proportions could cover it
            for (int label = 1; label <= gapCount; label++) if (isHole[label] && spreads[label].Count > bigLength * bigLength * 0.25f) isHole[label] = false;

            bool added = false;
            bool holesLeft = false;
            for (int label = 1; label <= gapCount; label++)
            {
                int gapArea = spreads[label].Count;
                if (gapArea < 2 * scale * scale && !isHole[label]) continue;
                PointSpread gap = spreads[label].Result();
                float gapLong = gap.LongExtent + 2 * scale;
                float gapShort = gap.ShortExtent + 2 * scale;
                if (gapLong < bigLength * 0.2f && !isHole[label]) continue;

                // a hole may take a big or a medium piece, whichever fits; a gap takes the size its length calls for
                List<KitbashPiece> sized = isHole[label] ? pieces.FindAll(piece => piece.size != KitbashSize.Small) : PiecesOfSize(gapLong < bigLength * 0.7f ? KitbashSize.Medium : KitbashSize.Large);
                if (sized.Count == 0) continue;
                PixelMask gapMask = LabelMask(labels, label);

                bool found = false;
                float bestScore = 0f;
                Candidate best = default(Candidate);
                List<PixelSpan> bestSpans = null;
                int attempts = isHole[label] ? 150 : 30;
                for (int attempt = 0; attempt < attempts; attempt++)
                {
                    KitbashPiece piece = random.Choice(sized);
                    bool mirrored = random.Chance(0.5f);
                    Affine2D mirror = piece.MirrorTransform(mirrored);
                    PointSpread pieceSpread = PointSpread.Of(piece.OutlinePoints(mirrored));
                    float longScale, shortScale;
                    if (isHole[label])
                    {
                        // a hole gets a whole, normal-sized piece laid over it (in its own proportions), never a tiny patch
                        float uniformScale = Mathf.Max(Mathf.Max(gapLong / pieceSpread.LongExtent, gapShort / pieceSpread.ShortExtent), bigLength * 0.45f / pieceSpread.LongExtent) * random.Uniform(1.05f, 1.4f);
                        longScale = shortScale = uniformScale;
                    }
                    else
                    {
                        // a gap gets a piece stretched to its own proportions
                        longScale = gapLong / pieceSpread.LongExtent * random.Uniform(1f, 1.25f);
                        shortScale = gapShort / pieceSpread.ShortExtent * random.Uniform(1f, 1.25f);
                    }
                    float rotation = gap.MajorAngle - pieceSpread.MajorAngle + (random.Chance(0.5f) ? Mathf.PI : 0f);
                    // a hole isn't always centred on its middle, so the piece is nudged around it
                    Vector2 nudge = isHole[label] ? new Vector2(random.Uniform(-1f, 1f), random.Uniform(-1f, 1f)) * gapShort * 0.4f : Vector2.zero;
                    Affine2D placement = Affine2D.Compose(FitTransform(pieceSpread, longScale, shortScale, rotation, gap.center + nudge), mirror);
                    ConvexPolygon outline = piece.outline.Transformed(placement);

                    if (outline.LongestSide < bigLength * 0.35f || outline.Thickness < bigLength * MinimumThickness[piece.size]) continue;
                    List<PixelSpan> spans = outline.Rasterize(Width, Height);
                    int area = PixelMask.CountSpans(spans);
                    if (area == 0 || allowed.CountIn(spans) / (float)area < 0.99f) continue;
                    if (!TryRepeatPenalty(piece, outline, out float penalty))
                    {
                        // a copy next to itself is bad, but a hole left open is worse
                        if (!isHole[label]) continue;
                        penalty = bigLength * 6f;
                    }
                    int filled = gapMask.CountIn(spans);
                    // a hole should be closed by one piece: most of it at once; any slivers left go next pass
                    if (isHole[label] && filled < gapArea * holeCoverage) continue;

                    float score = filled - (area - target.CountIn(spans)) * 3f - penalty * 0.3f;
                    if (found && score <= bestScore) continue;
                    found = true;
                    bestScore = score;
                    best = new Candidate { piece = piece, placement = placement, outline = outline };
                    bestSpans = spans;
                }
                // a hole is closed by the best piece found even if it costs (spill, a reused piece); a gap only if it's worth it
                if (!found || (bestScore <= 0f && !isHole[label]))
                {
                    if (isHole[label]) holesLeft = true;
                    continue;
                }
                Add(best.piece, best.placement, best.outline, bestSpans, false);
                added = true;
            }
            return added || holesLeft;
        }

        /// <summary>
        /// Holes smaller than this are closed when the seams are, so they don't need a piece of their own.
        /// </summary>
        private int SeamHoleArea => 12 * scale * scale;

        /// <summary>
        /// The pixels the pieces actually draw (before seams are closed).
        /// </summary>
        private PixelMask DrawnMask()
        {
            var canvas = new KitbashCanvas(Width, Height);
            foreach (PlacedPiece placedPiece in placed) canvas.DrawPiece(placedPiece.piece, placedPiece.placement);
            return canvas.SolidMask();
        }

        private static PixelMask WithoutSmallPieces(PixelMask mask, int minimumArea)
        {
            int[] labels = mask.Label(out int count);
            var sizes = new int[count + 1];
            foreach (int label in labels) sizes[label]++;
            var result = new PixelMask(mask.width, mask.height);
            for (int index = 0; index < labels.Length; index++) result.cells[index] = labels[index] != 0 && sizes[labels[index]] >= minimumArea;
            return result;
        }

        private PixelMask LabelMask(int[] labels, int label)
        {
            var mask = new PixelMask(Width, Height);
            for (int index = 0; index < labels.Length; index++) mask.cells[index] = labels[index] == label;
            return mask;
        }

        /// <summary>
        /// Scales a piece by longScale along its own long axis and shortScale across it, turns it by "rotation" and centres
        /// it on "middle".
        /// </summary>
        private static Affine2D FitTransform(PointSpread pieceSpread, float longScale, float shortScale, float rotation, Vector2 middle)
        {
            Vector2 along = pieceSpread.major;
            Vector2 across = pieceSpread.minor;
            // scale = longScale * along·alongᵀ + shortScale * across·acrossᵀ
            float scaleXX = longScale * along.x * along.x + shortScale * across.x * across.x;
            float scaleXY = longScale * along.x * along.y + shortScale * across.x * across.y;
            float scaleYY = longScale * along.y * along.y + shortScale * across.y * across.y;
            float cosine = Mathf.Cos(rotation);
            float sine = Mathf.Sin(rotation);
            float linearXX = cosine * scaleXX - sine * scaleXY;
            float linearXY = cosine * scaleXY - sine * scaleYY;
            float linearYX = sine * scaleXX + cosine * scaleXY;
            float linearYY = sine * scaleXY + cosine * scaleYY;
            Vector2 center = pieceSpread.center;
            return new Affine2D(
                linearXX, linearXY, middle.x - (linearXX * center.x + linearXY * center.y),
                linearYX, linearYY, middle.y - (linearYX * center.x + linearYY * center.y));
        }

        // ----- the top: pointed pieces standing up -----

        /// <summary>
        /// The edges of a pointed piece that can serve as its base: roughly across its long axis, at the end away from its tip.
        /// </summary>
        public static List<Edge> BaseEdges(Vector2[] outline)
        {
            var bases = new List<Edge>();
            PointSpread spread = PointSpread.Of(outline);
            float maximumProjection = float.MinValue, minimumProjection = float.MaxValue;
            foreach (Vector2 point in outline)
            {
                float projection = Vector2.Dot(point - spread.center, spread.major);
                maximumProjection = Mathf.Max(maximumProjection, projection);
                minimumProjection = Mathf.Min(minimumProjection, projection);
            }
            float tipSide = maximumProjection > -minimumProjection ? 1f : -1f;
            for (int index = 0; index < outline.Length; index++)
            {
                Vector2 start = outline[index];
                Vector2 end = outline[(index + 1) % outline.Length];
                Vector2 direction = end - start;
                float length = direction.magnitude;
                if (length < 1e-6f) continue;
                bool acrossAxis = Mathf.Abs(Vector2.Dot(direction / length, spread.major)) < 0.85f;
                bool atBase = Vector2.Dot((start + end) / 2f - spread.center, spread.major) * tipSide < 0f;
                if (acrossAxis && atBase) bases.Add(new Edge(start, end));
            }
            return bases;
        }

        /// <summary>
        /// The shape's top row: pointed pieces standing on its top edge, tip straight up, base as wide as the stretch they
        /// stand on. How the top ends is rolled per shape: several points, a pair, or one tall point.
        /// </summary>
        private void CrownTopEdge()
        {
            List<KitbashPiece> pointed = pieces.FindAll(piece => piece.pointed);
            if (pointed.Count == 0) return;
            KitbashRandom styleRandom = crownStyleSeed.HasValue ? new KitbashRandom(crownStyleSeed.Value) : random;
            var weights = new List<float>();
            foreach (CrownStyle candidateStyle in CrownStyles) weights.Add(candidateStyle.weight);
            CrownStyle style = styleRandom.WeightedChoice(CrownStyles, weights);

            if (!target.TryGetRowRange(out int topRow, out int bottomRow)) return;
            float topLimit = topRow + (bottomRow - topRow) * 0.12f;

            float segment = bigLength * random.Uniform(style.segmentMinimum, style.segmentMaximum);
            List<Edge> topEdges = OutlineEdges(target, segment, null, 0f).FindAll(edge => edge.Middle.y < topLimit && Mathf.Abs(edge.end.x - edge.start.x) > Mathf.Abs(edge.end.y - edge.start.y) * 1.5f);
            if (topEdges.Count == 0) return;
            random.Shuffle(topEdges);
            if (topEdges.Count > style.maximumPoints) topEdges.RemoveRange(style.maximumPoints, topEdges.Count - style.maximumPoints);

            foreach (Edge edge in topEdges)
            {
                Vector2 middle = edge.Middle;
                bool found = false;
                float bestScore = 0f;
                Candidate best = default(Candidate);
                List<PixelSpan> bestSpans = null;
                for (int attempt = 0; attempt < 60; attempt++)
                {
                    KitbashPiece piece = random.Choice(pointed);
                    List<Edge> bases = BaseEdges(piece.outline.points);
                    if (bases.Count == 0) continue;
                    Edge sourceBase = random.Chance(0.6f) ? Longest(bases) : random.Choice(bases);
                    Vector2 baseMiddle = sourceBase.Middle;
                    Vector2 sourceTip = piece.outline.points[0];
                    foreach (Vector2 point in piece.outline.points) if (Vector2.Distance(point, baseMiddle) > Vector2.Distance(sourceTip, baseMiddle)) sourceTip = point;

                    float height = bigLength * random.Uniform(style.heightMinimum, style.heightMaximum);
                    // the point connects by its base to the top edge, and its tip goes straight up above that edge
                    Vector2 targetTip = new Vector2(middle.x + random.Uniform(-0.08f, 0.08f) * height, middle.y - height);
                    foreach (Edge targetEdge in new[] { edge, edge.Reversed })
                    {
                        if (!Affine2D.TryFromThreePoints(sourceBase.start, sourceBase.end, sourceTip, targetEdge.start, targetEdge.end, targetTip, out Affine2D placement)) continue;
                        ConvexPolygon outline = piece.outline.Transformed(placement);
                        if (outline.Area < 1f) continue;
                        outline.GetBounds(out float minimumX, out _, out float maximumX, out _);
                        if (maximumX - minimumX > height * 0.9f) continue;   // a point, not a slab
                        List<PixelSpan> spans = outline.Rasterize(Width, Height);
                        int area = PixelMask.CountSpans(spans);
                        if (area == 0) continue;
                        if (tiling.Any() && tiling.CountIn(spans) / (float)area > 0.08f) continue;
                        if (allowed.CountIn(spans) / (float)area < 0.97f) continue;
                        if (!TryRepeatPenalty(piece, outline, out float penalty)) continue;
                        float score = Contact(outline) * 2f + height - penalty + random.Uniform(0f, bigLength * 0.3f);
                        if (found && score <= bestScore) continue;
                        found = true;
                        bestScore = score;
                        best = new Candidate { piece = piece, placement = placement, outline = outline };
                        bestSpans = spans;
                    }
                }
                if (found) Add(best.piece, best.placement, best.outline, bestSpans, true);
            }
        }

        private static Edge Longest(List<Edge> edges)
        {
            Edge longest = edges[0];
            foreach (Edge edge in edges) if (edge.Length > longest.Length) longest = edge;
            return longest;
        }
    }
}
