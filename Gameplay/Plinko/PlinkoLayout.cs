using System;
using System.Collections.Generic;
using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// A catch point: the horizontal mouth a Drop Token falls into to end its drop.
    /// </summary>
    /// <remarks>
    /// Not fixed to the bottom of the board. Placing one mid-board gives the "additional catch points" of the
    /// spec, which behave exactly like the bottom row.
    /// </remarks>
    [Serializable]
    public struct PlinkoCatchPoint
    {
        [Tooltip("Left edge of the mouth, in board units.")]
        public float MinX;

        [Tooltip("Right edge of the mouth, in board units.")]
        public float MaxX;

        [Tooltip("Height of the mouth. A ball crossing this height downhill between MinX and MaxX is caught.")]
        public float Y;

        /// <summary>
        /// Creates a catch point.
        /// </summary>
        public PlinkoCatchPoint(float minX, float maxX, float y)
        {
            MinX = minX;
            MaxX = maxX;
            Y = y;
        }

        /// <summary>Width of the mouth.</summary>
        public float Width => MaxX - MinX;

        /// <summary>Centre of the mouth.</summary>
        public Vector2 Center => new Vector2((MinX + MaxX) * 0.5f, Y);

        /// <summary>
        /// Whether <paramref name="x"/> falls within the mouth.
        /// </summary>
        public bool ContainsX(float x)
        {
            return x >= MinX && x <= MaxX;
        }
    }

    /// <summary>
    /// A reflective line segment: an outer wall, or one edge of a side shape.
    /// </summary>
    /// <remarks>
    /// The spec's side shapes are triangles, which is two or three of these. Walls reflect rather than absorb,
    /// so a ball driven outwards comes back into play instead of being lost.
    /// </remarks>
    [Serializable]
    public struct PlinkoWall
    {
        [Tooltip("One end of the segment, in board units.")]
        public Vector2 From;

        [Tooltip("The other end of the segment, in board units.")]
        public Vector2 To;

        /// <summary>
        /// Creates a wall segment.
        /// </summary>
        public PlinkoWall(Vector2 from, Vector2 to)
        {
            From = from;
            To = to;
        }

        /// <summary>Vector along the segment.</summary>
        public Vector2 Delta => To - From;
    }

    /// <summary>
    /// A hand placed Plinko board: where the pegs, entries, catch points and walls actually are.
    /// </summary>
    /// <remarks>
    /// Positions are explicit rather than generated, because the spec calls for peg arrangements that are
    /// modular per level. <see cref="PlinkoLayoutGenerator"/> fills these lists for the common shapes; a
    /// designer is free to author or edit them directly.
    /// <para>
    /// Board space has X horizontal and Y vertical with up positive.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class PlinkoLayout
    {
        [Tooltip("Centre of every peg, in board units.")]
        public List<Vector2> Pegs = new List<Vector2>();

        [Tooltip("Radius shared by every peg. The spec requires pegs to be a single size.")]
        public float PegRadius = 0.12f;

        [Tooltip("Drop points, one per Drop Button, ordered left to right.")]
        public List<Vector2> Entries = new List<Vector2>();

        [Tooltip("Catch points, ordered left to right along the bottom row first.")]
        public List<PlinkoCatchPoint> CatchPoints = new List<PlinkoCatchPoint>();

        [Tooltip("Reflective segments: outer walls and the edges of side shapes.")]
        public List<PlinkoWall> Walls = new List<PlinkoWall>();

        [Tooltip("Play area. A ball leaving this rectangle voids the simulated drop.")]
        public Rect PlayArea = new Rect(-6f, -14f, 12f, 18f);

        /// <summary>Number of entries.</summary>
        public int EntryCount => Entries.Count;

        /// <summary>Number of catch points.</summary>
        public int CatchPointCount => CatchPoints.Count;

        /// <summary>
        /// Validates the layout and returns a human readable reason when it cannot be simulated.
        /// </summary>
        /// <param name="error">The first problem found, or <c>null</c> when the layout is usable.</param>
        /// <returns><c>true</c> when the layout can be simulated.</returns>
        public bool Validate(out string error)
        {
            if (Entries == null || Entries.Count == 0)
            {
                error = "The layout needs at least one entry.";
                return false;
            }

            if (CatchPoints == null || CatchPoints.Count == 0)
            {
                error = "The layout needs at least one catch point.";
                return false;
            }

            if (PegRadius <= 0f)
            {
                error = "PegRadius must be greater than zero.";
                return false;
            }

            for (int i = 0; i < CatchPoints.Count; i++)
            {
                if (CatchPoints[i].Width <= 0f)
                {
                    error = $"Catch point {i} has a width of zero or less.";
                    return false;
                }
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                if (!PlayArea.Contains(Entries[i]))
                {
                    error = $"Entry {i} at {Entries[i]} sits outside the play area {PlayArea}.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Adds the four outer walls of the play area, so a ball can never leave sideways.
        /// </summary>
        /// <remarks>
        /// Only the sides and the ceiling are added. The floor is left open because a ball that reaches it has
        /// missed every catch point, which is a layout error worth surfacing rather than hiding.
        /// </remarks>
        public void AddPlayAreaWalls()
        {
            var bottomLeft = new Vector2(PlayArea.xMin, PlayArea.yMin);
            var topLeft = new Vector2(PlayArea.xMin, PlayArea.yMax);
            var bottomRight = new Vector2(PlayArea.xMax, PlayArea.yMin);
            var topRight = new Vector2(PlayArea.xMax, PlayArea.yMax);

            Walls.Add(new PlinkoWall(bottomLeft, topLeft));
            Walls.Add(new PlinkoWall(bottomRight, topRight));
        }

        /// <summary>
        /// Adds an upward pointing triangular side shape, as three reflective segments.
        /// </summary>
        /// <param name="apex">Tip of the triangle.</param>
        /// <param name="halfWidth">Half the width of its base.</param>
        /// <param name="height">Distance from the base down to the apex. Negative points the apex upward.</param>
        public void AddTriangle(Vector2 apex, float halfWidth, float height)
        {
            var left = new Vector2(apex.x - halfWidth, apex.y + height);
            var right = new Vector2(apex.x + halfWidth, apex.y + height);

            Walls.Add(new PlinkoWall(left, apex));
            Walls.Add(new PlinkoWall(apex, right));
            Walls.Add(new PlinkoWall(right, left));
        }

        /// <summary>
        /// Returns the index of the catch point whose mouth contains <paramref name="x"/> at the given height,
        /// or -1 when there is none.
        /// </summary>
        public int FindCatchPointAt(float x, float y, float tolerance = 1e-3f)
        {
            for (int i = 0; i < CatchPoints.Count; i++)
            {
                if (Mathf.Abs(CatchPoints[i].Y - y) <= tolerance && CatchPoints[i].ContainsX(x))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Creates a deep copy, so a caller can tweak a layout without mutating a shared instance.
        /// </summary>
        public PlinkoLayout Clone()
        {
            return new PlinkoLayout
            {
                Pegs = new List<Vector2>(Pegs),
                PegRadius = PegRadius,
                Entries = new List<Vector2>(Entries),
                CatchPoints = new List<PlinkoCatchPoint>(CatchPoints),
                Walls = new List<PlinkoWall>(Walls),
                PlayArea = PlayArea,
            };
        }
    }
}
