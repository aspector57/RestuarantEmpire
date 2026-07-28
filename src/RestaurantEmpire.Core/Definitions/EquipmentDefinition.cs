using System;

namespace RestaurantEmpire.Core.Definitions
{
    /// <summary>
    /// A model of kitchen equipment you can buy — one line in the catalogue.
    ///
    /// Every station type comes in a cheap, a standard and a premium version, and the
    /// premium tier is deliberately faster AND smaller per unit. That is what stops the
    /// answer to every problem being "buy another oven": once the building is full, better
    /// equipment is the only remaining way to add throughput. Fifteen cheap ovens is not a
    /// strategy, it is a dining room you no longer have.
    /// </summary>
    public sealed class EquipmentDefinition
    {
        public EquipmentDefinition(
            string id, string stationId, string name,
            decimal cost, decimal speedMultiplier, decimal footprint, decimal quality = 0.5m)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Equipment id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(stationId)) throw new ArgumentException("Equipment must name a station.", nameof(stationId));
            if (cost < 0m) throw new ArgumentOutOfRangeException(nameof(cost));
            if (speedMultiplier <= 0m) throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
            if (footprint <= 0m) throw new ArgumentOutOfRangeException(nameof(footprint), "Equipment has to take up some room.");

            Id = id;
            StationId = stationId;
            Name = name ?? id;
            Cost = cost;
            SpeedMultiplier = speedMultiplier;
            Footprint = footprint;
            Quality = quality;
        }

        public string Id { get; }

        /// <summary>Which brigade station this equips.</summary>
        public string StationId { get; }

        public string Name { get; }

        /// <summary>Price of one unit.</summary>
        public decimal Cost { get; }

        /// <summary>Above 1.0 cooks faster than baseline.</summary>
        public decimal SpeedMultiplier { get; }

        /// <summary>Square meters one unit occupies, including room to work around it.</summary>
        public decimal Footprint { get; }

        /// <summary>0 to 1. Reserved for the dish-quality contribution equipment will make later.</summary>
        public decimal Quality { get; }

        /// <summary>Throughput per square meter — the number that decides whether upgrading beats expanding.</summary>
        public decimal SpeedPerSquareMeter { get { return SpeedMultiplier / Footprint; } }

        public override string ToString()
        {
            return Name + " (" + Cost.ToString("N0") + ", x" + SpeedMultiplier + ", " + Footprint + "m2)";
        }
    }
}
