using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// Builds the peg, entry and catch point lists for common board shapes.
    /// </summary>
    /// <remarks>
    /// A convenience over hand placing every peg, not a requirement. The output is an ordinary
    /// <see cref="PlinkoLayout"/> whose lists can be edited afterwards, which is what keeps layouts modular
    /// per level as the spec asks.
    /// </remarks>
    public static class PlinkoLayoutGenerator
    {
        /// <summary>
        /// The classic horizontally symmetric Plinko board: a staggered peg grid, entries above it and catch
        /// points tiling the bottom.
        /// </summary>
        /// <param name="rows">Peg rows.</param>
        /// <param name="columns">Pegs per row.</param>
        /// <param name="entryCount">Drop points, spread across the top and aligned with row 0 pegs.</param>
        /// <param name="catchPointCount">Catch points tiling the bottom of the board.</param>
        /// <param name="columnSpacing">Horizontal distance between pegs in a row.</param>
        /// <param name="rowSpacing">Vertical distance between rows.</param>
        /// <param name="pegRadius">Radius of every peg.</param>
        public static PlinkoLayout CreateStaggeredGrid(
            int rows = 12,
            int columns = 9,
            int entryCount = 5,
            int catchPointCount = 9,
            float columnSpacing = 1f,
            float rowSpacing = 0.9f,
            float pegRadius = 0.12f)
        {
            rows = Mathf.Max(1, rows);
            columns = Mathf.Max(2, columns);

            var layout = new PlinkoLayout { PegRadius = pegRadius };

            float stagger = columnSpacing * 0.5f;
            float width = (columns - 1) * columnSpacing + stagger;
            float left = -width * 0.5f;

            for (int row = 0; row < rows; row++)
            {
                float rowOffset = (row % 2) == 1 ? stagger : 0f;
                float y = -row * rowSpacing;

                for (int column = 0; column < columns; column++)
                {
                    layout.Pegs.Add(new Vector2(left + rowOffset + column * columnSpacing, y));
                }
            }

            float halfSpan = width * 0.5f + columnSpacing * 0.6f;
            float entryY = rowSpacing * 1.5f;
            float catchY = -(rows - 1) * rowSpacing - rowSpacing * 1.2f;

            AddSpreadEntries(layout, entryCount, -width * 0.5f, width * 0.5f, entryY);
            AddTiledCatchPoints(layout, catchPointCount, -halfSpan, halfSpan, catchY);

            layout.PlayArea = new Rect(
                -halfSpan, catchY - rowSpacing, halfSpan * 2f, entryY - catchY + rowSpacing * 2f);
            layout.AddPlayAreaWalls();

            return layout;
        }

        /// <summary>
        /// A deliberately sparse test board: one peg directly below each entry, every peg at a different
        /// height, and catch points tiling the bottom.
        /// </summary>
        /// <remarks>
        /// Built to stress the bounce model rather than to play well. With a single peg per column the drop's
        /// whole outcome comes from one impact plus wall reflections, so it shows immediately whether the
        /// bounce variants produce real spread or just two mirrored options.
        /// </remarks>
        /// <param name="count">Number of entries, and of pegs, and of catch points.</param>
        /// <param name="columnSpacing">Horizontal distance between entries.</param>
        /// <param name="firstPegDepth">Depth of the first peg below the entry row.</param>
        /// <param name="depthStep">Extra depth added for each successive peg, staggering their heights.</param>
        /// <param name="pegRadius">Radius of every peg.</param>
        public static PlinkoLayout CreateStaircase(
            int count = 5,
            float columnSpacing = 2f,
            float firstPegDepth = 1.5f,
            float depthStep = 1.2f,
            float pegRadius = 0.12f)
        {
            count = Mathf.Max(1, count);

            var layout = new PlinkoLayout { PegRadius = pegRadius };

            float width = (count - 1) * columnSpacing;
            float left = -width * 0.5f;
            float entryY = 2f;

            for (int i = 0; i < count; i++)
            {
                float x = left + i * columnSpacing;
                layout.Entries.Add(new Vector2(x, entryY));
                layout.Pegs.Add(new Vector2(x, entryY - firstPegDepth - i * depthStep));
            }

            float deepestPeg = entryY - firstPegDepth - (count - 1) * depthStep;
            float catchY = deepestPeg - 2.5f;
            float halfSpan = width * 0.5f + columnSpacing * 0.5f;

            AddTiledCatchPoints(layout, count, -halfSpan, halfSpan, catchY);

            layout.PlayArea = new Rect(-halfSpan, catchY - 1f, halfSpan * 2f, entryY - catchY + 2f);
            layout.AddPlayAreaWalls();

            return layout;
        }

        /// <summary>
        /// Spreads entries evenly between two heights-aligned x positions.
        /// </summary>
        public static void AddSpreadEntries(PlinkoLayout layout, int count, float minX, float maxX, float y)
        {
            count = Mathf.Max(1, count);

            if (count == 1)
            {
                layout.Entries.Add(new Vector2((minX + maxX) * 0.5f, y));
                return;
            }

            for (int i = 0; i < count; i++)
            {
                layout.Entries.Add(new Vector2(Mathf.Lerp(minX, maxX, i / (float)(count - 1)), y));
            }
        }

        /// <summary>
        /// Adds catch points that tile a span edge to edge, leaving no gap for a ball to slip through.
        /// </summary>
        public static void AddTiledCatchPoints(
            PlinkoLayout layout, int count, float minX, float maxX, float y)
        {
            count = Mathf.Max(1, count);
            float width = (maxX - minX) / count;

            for (int i = 0; i < count; i++)
            {
                layout.CatchPoints.Add(new PlinkoCatchPoint(minX + i * width, minX + (i + 1) * width, y));
            }
        }
    }
}
