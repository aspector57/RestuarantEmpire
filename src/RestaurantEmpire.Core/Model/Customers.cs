using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// A group of guests arriving together.
    ///
    /// Deliberately thin for M0. Cuisine preferences, occasion, and the named archetypes
    /// (Influencer, Romantic Couple, Business Luncher) are M2 — building them now would be
    /// building ahead, and the satisfaction formula needs none of them to be meaningful.
    /// </summary>
    public sealed class CustomerParty
    {
        internal CustomerParty(string id, int size, long arrivalTick, int patienceMinutes, decimal priceSensitivity)
        {
            Id = id;
            Size = size;
            ArrivalTick = arrivalTick;
            PatienceMinutes = patienceMinutes;
            PriceSensitivity = priceSensitivity;
        }

        public string Id { get; }

        /// <summary>Covers in this party. Each orders one dish.</summary>
        public int Size { get; }

        public long ArrivalTick { get; }

        /// <summary>Minutes they will wait for food before giving up and walking out.</summary>
        public int PatienceMinutes { get; }

        /// <summary>1.0 is neutral. Above 1.0 means they judge price harder.</summary>
        public decimal PriceSensitivity { get; }

        public override string ToString()
        {
            return "party of " + Size + " at tick " + ArrivalTick;
        }
    }

    /// <summary>
    /// The arrival formula: who turns up, and when.
    ///
    /// M0 scope is deliberately "basic". Demand here is a peak curve plus jitter — no
    /// Reputation gate, because Reputation is M1 and gating volume by it is that
    /// milestone's job. What this DOES give us is a rush: arrivals cluster mid-service, so
    /// the kitchen is stressed unevenly and bottlenecks are something the simulation
    /// discovers rather than something a test has to stage.
    ///
    /// Fully deterministic for a given seed, via <see cref="DeterministicRandom"/>.
    /// </summary>
    public sealed class DemandModel
    {
        private readonly double _partiesPerHourAtPeak;
        private readonly long _seed;

        public DemandModel(double partiesPerHourAtPeak, long seed)
        {
            if (partiesPerHourAtPeak < 0) throw new ArgumentOutOfRangeException(nameof(partiesPerHourAtPeak));

            _partiesPerHourAtPeak = partiesPerHourAtPeak;
            _seed = seed;
        }

        /// <summary>Busiest hourly arrival rate, reached in the middle of service.</summary>
        public double PartiesPerHourAtPeak { get { return _partiesPerHourAtPeak; } }

        /// <summary>
        /// Generates the night's arrivals, in chronological order.
        ///
        /// The curve is triangular — nobody at the doors when you open, a peak mid-service,
        /// tapering to close. Crude, but it produces the shape that matters: a period where
        /// demand genuinely exceeds a small kitchen's throughput.
        /// </summary>
        public IReadOnlyList<CustomerParty> ArrivalsFor(long serviceStartTick, int serviceMinutes)
        {
            var rng = new DeterministicRandom(_seed);
            var parties = new List<CustomerParty>();

            for (var minute = 0; minute < serviceMinutes; minute++)
            {
                var throughService = (double)minute / serviceMinutes;
                var peakWeight = 1.0 - Math.Abs((2.0 * throughService) - 1.0); // 0 -> 1 -> 0

                var chancePerMinute = (_partiesPerHourAtPeak / 60.0) * peakWeight;

                if (!rng.Chance(chancePerMinute)) continue;

                parties.Add(new CustomerParty(
                    id: "party-" + (parties.Count + 1),
                    size: RollPartySize(rng),
                    arrivalTick: serviceStartTick + minute,
                    patienceMinutes: rng.Next(20, 36),
                    priceSensitivity: 0.8m + (decimal)rng.NextDouble() * 0.4m));
            }

            return parties;
        }

        /// <summary>Twos are the commonest table, then fours, then singles and larger groups.</summary>
        private static int RollPartySize(DeterministicRandom rng)
        {
            var roll = rng.NextDouble();

            if (roll < 0.15) return 1;
            if (roll < 0.60) return 2;
            if (roll < 0.80) return 3;
            if (roll < 0.95) return 4;

            return rng.Next(5, 8);
        }
    }
}
