using System;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// A group of guests arriving together.
    ///
    /// Deliberately thin for M0/M1. Cuisine preferences, occasion, and the named archetypes
    /// (Influencer, Romantic Couple, Business Luncher) are M2 — building them now would be
    /// building ahead, and the satisfaction formula needs none of them to be meaningful.
    ///
    /// Parties are produced by <see cref="SimulationRunner"/> as the clock passes through a
    /// <see cref="ServiceWindow"/>; the window carries the demand rate, so there is no
    /// separate arrival model to keep in step with the running simulation.
    /// </summary>
    public sealed class CustomerParty
    {
        public CustomerParty(string id, int size, long arrivalTick, int patienceMinutes, decimal priceSensitivity)
        {
            if (size < 1) throw new ArgumentOutOfRangeException(nameof(size), "A party has at least one guest.");
            if (patienceMinutes < 0) throw new ArgumentOutOfRangeException(nameof(patienceMinutes));

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
}
