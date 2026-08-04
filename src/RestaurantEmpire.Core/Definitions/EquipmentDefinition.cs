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
            decimal cost, decimal speedMultiplier, decimal footprint, decimal quality = 0.5m, decimal capacity = 0m,
            int platesAtOnce = 1)
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
            Capacity = capacity < 0m ? 0m : capacity;
            PlatesAtOnce = platesAtOnce < 1 ? 1 : platesAtOnce;
        }

        public string Id { get; }

        /// <summary>Which brigade station this equips.</summary>
        public string StationId { get; }

        public string Name { get; }

        /// <summary>Price of one unit.</summary>
        public decimal Cost { get; }

        /// <summary>
        /// How many plates ONE unit works at the same time. A deck oven holds several pizzas;
        /// a four-burner range has four burners.
        ///
        /// Every cooking station used to be one plate at a time, which is why a deck oven made
        /// FIVE PIZZAS AN HOUR and the only way to add throughput was to buy another box.
        /// Aaron: *"I still had to buy too many ovens even when I didn't add tables."* Exactly
        /// right, and this was why — the lever sweep agreed independently, reporting "how many
        /// ovens: more is always better, so it is a purchase not a choice."
        /// </summary>
        public int PlatesAtOnce { get; }

        /// <summary>Above 1.0 cooks faster than baseline.</summary>
        public decimal SpeedMultiplier { get; }

        /// <summary>Square meters one unit occupies, including room to work around it.</summary>
        public decimal Footprint { get; }

        /// <summary>0 to 1. Reserved for the dish-quality contribution equipment will make later.</summary>
        public decimal Quality { get; }

        /// <summary>
        /// Units of stock this holds. Zero for anything that cooks rather than stores.
        ///
        /// Storage is EQUIPMENT on purpose, so it competes for the same floor as the kitchen
        /// and the dining room. That is what turns "order deep" from a thing only spoilage
        /// punishes into a decision with a price you can see: a bigger walk-in is either
        /// capital, or covers you no longer have room for.
        /// </summary>
        public decimal Capacity { get; }

        /// <summary>Throughput per square meter — the number that decides whether upgrading beats expanding.</summary>
        public decimal SpeedPerSquareFoot { get { return SpeedMultiplier / Footprint; } }

        public override string ToString()
        {
            return Name + " (" + Cost.ToString("N0") + ", x" + SpeedMultiplier + ", " + Footprint + "m2)";
        }
    }
}
