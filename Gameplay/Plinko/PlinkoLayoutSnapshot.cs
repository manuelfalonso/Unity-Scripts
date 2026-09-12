using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// A flat, array backed copy of a layout, taken once before a bake and read by the tracer's inner loop.
    /// </summary>
    /// <remarks>
    /// The tracer touches this millions of times per bake. <see cref="System.Collections.Generic.List{T}"/>
    /// costs a bounds check and an indirection on every read, which is invisible once and measurable a few
    /// million times; plain arrays are not. Taking a snapshot also means a caller editing a layout mid-bake
    /// cannot change the board out from under the simulation.
    /// </remarks>
    public sealed class PlinkoLayoutSnapshot
    {
        /// <summary>Centre of every peg.</summary>
        public readonly Vector2[] Pegs;

        /// <summary>Radius shared by every peg.</summary>
        public readonly float PegRadius;

        /// <summary>Reflective segments.</summary>
        public readonly PlinkoWall[] Walls;

        /// <summary>Catch point mouths.</summary>
        public readonly PlinkoCatchPoint[] CatchPoints;

        /// <summary>Drop points.</summary>
        public readonly Vector2[] Entries;

        /// <summary>Bounds a ball may not leave.</summary>
        public readonly Rect PlayArea;

        /// <summary>
        /// Takes a snapshot of <paramref name="layout"/>.
        /// </summary>
        public PlinkoLayoutSnapshot(PlinkoLayout layout)
        {
            Pegs = layout.Pegs.ToArray();
            PegRadius = layout.PegRadius;
            Walls = layout.Walls.ToArray();
            CatchPoints = layout.CatchPoints.ToArray();
            Entries = layout.Entries.ToArray();
            PlayArea = layout.PlayArea;
        }
    }
}
