using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// Something bought and put in the dining room: tables, chairs, lighting, art.
    ///
    /// Two things come out of a fit-out, and the design is emphatic about their relative
    /// weight (Phase 4, Furniture/Layout):
    ///
    ///   SEATS are a hard, mechanical constraint. A dining room bigger than the kitchen can
    ///   feed is the dominant layout failure mode, so this number really bites.
    ///
    ///   COMFORT is deliberately a SMALL, CAPPED nudge on satisfaction — never a headline
    ///   score, never something to optimise. Cutting corners on decor while broke is the
    ///   intended early-game experience, so bare walls should mildly disappoint, not sink
    ///   the restaurant. There is no "decor score" for the player to manage, on purpose.
    /// </summary>
    public sealed class Fitting
    {
        public Fitting(string id, string name, decimal cost, int seats = 0, decimal comfort = 0.5m)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Fitting id is required.", nameof(id));
            if (cost < 0m) throw new ArgumentOutOfRangeException(nameof(cost), "A fitting cannot cost less than nothing.");
            if (seats < 0) throw new ArgumentOutOfRangeException(nameof(seats));
            if (comfort < 0m || comfort > 1m) throw new ArgumentOutOfRangeException(nameof(comfort), "Comfort runs 0 to 1.");

            Id = id;
            Name = name ?? id;
            Cost = cost;
            Seats = seats;
            Comfort = comfort;
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>What it cost to buy and install. Charged to the books on the day it goes in.</summary>
        public decimal Cost { get; }

        /// <summary>Covers this seats. Zero for pure decor.</summary>
        public int Seats { get; }

        /// <summary>0 (a plastic stool under a bare bulb) to 1 (genuinely lovely). Weighted lightly.</summary>
        public decimal Comfort { get; }

        public override string ToString()
        {
            return Name + (Seats > 0 ? " (" + Seats + " seats)" : " (decor)");
        }
    }

    /// <summary>
    /// The dining room: everything installed, what it seats, and how nice it is.
    ///
    /// Capacity is DERIVED from what has actually been bought rather than declared, so a
    /// bigger room is something you pay for. An empty room reports zero, which the
    /// simulation reads as "capacity not modelled" and lets everyone in — that keeps a
    /// bare-bones test or a food truck from having to furnish itself first.
    /// </summary>
    public sealed class DiningRoom
    {
        private readonly List<Fitting> _fittings = new List<Fitting>();

        internal DiningRoom() { }

        public IReadOnlyList<Fitting> Fittings { get { return _fittings; } }

        /// <summary>Total covers the room can seat. Zero means nothing has been installed.</summary>
        public int Seats
        {
            get
            {
                var total = 0;
                foreach (var fitting in _fittings) total += fitting.Seats;

                return total;
            }
        }

        /// <summary>What the whole fit-out cost.</summary>
        public decimal InstalledValue
        {
            get
            {
                var total = 0m;
                foreach (var fitting in _fittings) total += fitting.Cost;

                return total;
            }
        }

        /// <summary>
        /// Average comfort across the room, 0 to 1, or a neutral 0.5 when nothing is
        /// installed. Weighted by seats so a single beautiful chair among forty plastic ones
        /// does not flatter the average.
        /// </summary>
        public decimal Comfort
        {
            get
            {
                if (_fittings.Count == 0) return 0.5m;

                decimal weighted = 0m;
                var weight = 0;

                foreach (var fitting in _fittings)
                {
                    var w = fitting.Seats > 0 ? fitting.Seats : 1;
                    weighted += fitting.Comfort * w;
                    weight += w;
                }

                return weight == 0 ? 0.5m : weighted / weight;
            }
        }

        internal void Add(Fitting fitting)
        {
            if (fitting == null) throw new ArgumentNullException(nameof(fitting));
            _fittings.Add(fitting);
        }

        internal bool Remove(string fittingId)
        {
            var index = _fittings.FindIndex(f => f.Id == fittingId);
            if (index < 0) return false;

            _fittings.RemoveAt(index);
            return true;
        }
    }
}
