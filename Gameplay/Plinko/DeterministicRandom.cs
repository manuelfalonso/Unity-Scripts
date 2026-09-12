namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// Small, allocation-free, seeded pseudo random number generator with a stable algorithm.
    /// </summary>
    /// <remarks>
    /// Implements xorshift128. <c>UnityEngine.Random</c> is deliberately not used: its state is global and
    /// shared with the rest of the engine, and its algorithm is not contractually stable across Unity
    /// versions, so it cannot back a reproducible bake. <c>System.Random</c> is likewise avoided because its
    /// implementation has changed between .NET runtimes.
    /// </remarks>
    public sealed class DeterministicRandom
    {
        private uint _x;
        private uint _y;
        private uint _z;
        private uint _w;

        /// <summary>The seed this generator is currently running from.</summary>
        public int Seed { get; private set; }

        /// <summary>
        /// Creates a generator whose output depends only on <paramref name="seed"/>.
        /// </summary>
        public DeterministicRandom(int seed)
        {
            Reseed(seed);
        }

        /// <summary>
        /// Restarts the stream from <paramref name="seed"/>, exactly as a freshly constructed generator would.
        /// </summary>
        /// <remarks>
        /// Lets a caller give every unit of work its own stream without allocating a generator per unit, which
        /// is what makes a bake's drops independent of the order they are simulated in.
        /// </remarks>
        public void Reseed(int seed)
        {
            Seed = seed;

            // Scramble the seed into four words so that adjacent seeds produce unrelated streams.
            uint state = unchecked((uint)seed);
            _x = NextScramble(ref state);
            _y = NextScramble(ref state);
            _z = NextScramble(ref state);
            _w = NextScramble(ref state);

            // xorshift is degenerate at an all-zero state.
            if ((_x | _y | _z | _w) == 0u)
            {
                _x = 0x9E3779B9u;
            }
        }

        /// <summary>
        /// Returns the next raw 32 bit value.
        /// </summary>
        public uint NextUInt()
        {
            uint t = _x ^ (_x << 11);
            _x = _y;
            _y = _z;
            _z = _w;
            _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
            return _w;
        }

        /// <summary>
        /// Returns a value in the range [0, 1).
        /// </summary>
        public double NextDouble()
        {
            // Uses the high 24 bits, which are the best mixed, and divides by 2^24.
            return (NextUInt() >> 8) * (1.0 / 16777216.0);
        }

        /// <summary>
        /// Returns a value in the range [0, <paramref name="maxExclusive"/>).
        /// Returns 0 when <paramref name="maxExclusive"/> is not positive.
        /// </summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                return 0;
            }

            return (int)(NextDouble() * maxExclusive);
        }

        private static uint NextScramble(ref uint state)
        {
            unchecked
            {
                state += 0x9E3779B9u;
                uint z = state;
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                return z ^ (z >> 16);
            }
        }
    }
}
