using System.Collections.Generic;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// The baked drops, indexed by entry and catch point.
    /// </summary>
    /// <remarks>
    /// <see cref="Draw"/> is the runtime entry point: name an entry and a catch point and get a recorded drop
    /// that ends there. It cycles a shuffled order so the same trajectory does not come back until every
    /// other one for that pair has been used, because replaying an identical drop for an identical outcome is
    /// how a player works out that the outcome was decided in advance.
    /// </remarks>
    public sealed class PlinkoPathSet
    {
        private readonly List<PlinkoPath>[] _paths;
        private readonly int[][] _drawOrder;
        private readonly int[] _drawCursor;

        /// <summary>Number of entries this set covers.</summary>
        public int EntryCount { get; }

        /// <summary>Number of catch points this set covers.</summary>
        public int CatchPointCount { get; }

        /// <summary>
        /// Creates an empty set sized for a layout.
        /// </summary>
        public PlinkoPathSet(int entryCount, int catchPointCount)
        {
            EntryCount = entryCount;
            CatchPointCount = catchPointCount;

            int pairCount = entryCount * catchPointCount;
            _paths = new List<PlinkoPath>[pairCount];
            _drawOrder = new int[pairCount][];
            _drawCursor = new int[pairCount];

            for (int i = 0; i < pairCount; i++)
            {
                _paths[i] = new List<PlinkoPath>();
            }
        }

        /// <summary>
        /// Replaces the paths stored for an entry and catch point pair.
        /// </summary>
        public void SetPaths(int entryIndex, int catchPointIndex, List<PlinkoPath> paths)
        {
            int pair = GetPairIndex(entryIndex, catchPointIndex);
            _paths[pair] = paths;
            _drawOrder[pair] = null;
            _drawCursor[pair] = 0;
        }

        /// <summary>
        /// Every path stored for an entry and catch point pair.
        /// </summary>
        public IReadOnlyList<PlinkoPath> GetPaths(int entryIndex, int catchPointIndex)
        {
            return _paths[GetPairIndex(entryIndex, catchPointIndex)];
        }

        /// <summary>
        /// How many paths are stored for an entry and catch point pair.
        /// </summary>
        public int GetPathCount(int entryIndex, int catchPointIndex)
        {
            return _paths[GetPairIndex(entryIndex, catchPointIndex)].Count;
        }

        /// <summary>
        /// Whether any drop from this entry was ever seen to land in this catch point.
        /// </summary>
        public bool IsReachable(int entryIndex, int catchPointIndex)
        {
            return GetPathCount(entryIndex, catchPointIndex) > 0;
        }

        /// <summary>
        /// Total number of paths in the set.
        /// </summary>
        public int TotalPathCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _paths.Length; i++)
                {
                    total += _paths[i].Count;
                }

                return total;
            }
        }

        /// <summary>
        /// Returns a recorded drop for the requested pair, cycling a shuffled order so a trajectory does not
        /// repeat until the pool is exhausted. Returns <c>null</c> when the pair has no paths.
        /// </summary>
        /// <param name="entryIndex">Entry the token drops from.</param>
        /// <param name="catchPointIndex">Catch point the token must land in.</param>
        /// <param name="random">Seeded generator used to shuffle each cycle.</param>
        public PlinkoPath Draw(int entryIndex, int catchPointIndex, DeterministicRandom random)
        {
            int pair = GetPairIndex(entryIndex, catchPointIndex);
            List<PlinkoPath> pool = _paths[pair];

            if (pool.Count == 0)
            {
                return null;
            }

            if (_drawOrder[pair] == null || _drawCursor[pair] >= pool.Count)
            {
                _drawOrder[pair] = BuildShuffledOrder(pool.Count, random);
                _drawCursor[pair] = 0;
            }

            return pool[_drawOrder[pair][_drawCursor[pair]++]];
        }

        private static int[] BuildShuffledOrder(int count, DeterministicRandom random)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }

            // Fisher-Yates, so each cycle is a fresh permutation rather than a rotation of the last.
            for (int i = count - 1; i > 0; i--)
            {
                int j = random.NextInt(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            return order;
        }

        private int GetPairIndex(int entryIndex, int catchPointIndex)
        {
            return entryIndex * CatchPointCount + catchPointIndex;
        }
    }
}
