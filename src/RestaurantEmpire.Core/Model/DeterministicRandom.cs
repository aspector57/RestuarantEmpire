namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// A small, fixed random number generator (xorshift64*).
    ///
    /// Deliberately NOT System.Random. The .NET runtime does not guarantee that
    /// System.Random produces the same sequence across versions or platforms, which is
    /// fine for shuffling a playlist and unacceptable for a simulation: the same seed must
    /// always produce the same service, forever, on every machine.
    ///
    /// That matters for three things this project has already committed to — tests that
    /// assert on simulated outcomes, save files that must reload into the same world, and
    /// (later) being able to explain to a player exactly why a night went the way it did.
    /// Twenty lines here buys all of that.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(long seed)
        {
            // Any non-zero state works; zero would lock the generator at zero forever.
            _state = seed == 0 ? 0x9E3779B97F4A7C15UL : unchecked((ulong)seed);
            Seed = seed;
        }

        public long Seed { get; }

        public ulong NextULong()
        {
            unchecked
            {
                _state ^= _state >> 12;
                _state ^= _state << 25;
                _state ^= _state >> 27;

                return _state * 2685821657736338717UL;
            }
        }

        /// <summary>Uniform in [0, 1).</summary>
        public double NextDouble()
        {
            // Top 53 bits give a double the full mantissa without bias.
            return (NextULong() >> 11) * (1.0 / 9007199254740992.0);
        }

        /// <summary>Uniform integer in [0, maxExclusive).</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return (int)(NextDouble() * maxExclusive);
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + Next(maxExclusive - minInclusive);
        }

        /// <summary>True with the given probability.</summary>
        public bool Chance(double probability)
        {
            return NextDouble() < probability;
        }
    }
}
