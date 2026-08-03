using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Definitions
{
    /// <summary>
    /// A source the company can buy ingredients from. Loaded from data/suppliers.json.
    ///
    /// This is the "first-class object with a stable ID" the Phase 6 policy-propagation
    /// contract requires. Prices live here and only here, so changing which supplier is
    /// assigned to an ingredient changes cost everywhere that ingredient is used.
    /// </summary>
    public sealed class SupplierDefinition
    {
        private readonly Dictionary<string, decimal> _unitPrices;

        public string Id { get; }
        public string Name { get; }

        /// <summary>1 (cheapest/lowest quality) to 5 (premium). Feeds dish quality from M1 onward.</summary>
        public int QualityTier { get; }

        /// <summary>
        /// How many days of an ingredient's shelf life are already gone when it arrives.
        ///
        /// THIS IS WHAT MAKES A NATIONAL DISTRIBUTOR A DECISION RATHER THAN A DISCOUNT. A
        /// local grower drops small and often, so what lands is fresh; a national contract
        /// ships bulk through a depot, so a four-day fish arrives with two days left. It is
        /// cheaper per unit and it reaches the pass in worse condition, which is exactly the
        /// trade the design doc describes and the reason freshness had to exist first.
        ///
        /// Zero for everything that keeps — days spent are meaningless on flour.
        /// </summary>
        public int DaysInTransit { get; }

        /// <summary>
        /// Units a week this supplier expects you to take. Below it, they will not deal
        /// with you at all.
        ///
        /// THE POINT OF THE MINIMUM IS THAT IT CANNOT BE MET BY ONE RESTAURANT. That is what
        /// makes national sourcing a decision expansion UNLOCKS rather than a better option
        /// available from day one — "sourcing at ten restaurants is the identical decision as
        /// at one" is the flat-scaling anti-pattern this exists to break. Zero means anyone
        /// can buy, which is every local supplier.
        /// </summary>
        public decimal MinimumWeeklyVolume { get; }

        public SupplierDefinition(string id, string name, int qualityTier, IDictionary<string, decimal> unitPrices,
            int daysInTransit = 0, decimal minimumWeeklyVolume = 0m)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Supplier id is required.", nameof(id));

            Id = id;
            Name = name ?? id;
            QualityTier = qualityTier;
            DaysInTransit = daysInTransit < 0 ? 0 : daysInTransit;
            MinimumWeeklyVolume = minimumWeeklyVolume < 0m ? 0m : minimumWeeklyVolume;
            _unitPrices = unitPrices == null
                ? new Dictionary<string, decimal>()
                : new Dictionary<string, decimal>(unitPrices);
        }

        /// <summary>Ingredient ids this supplier can actually deliver.</summary>
        public IEnumerable<string> CarriedIngredientIds
        {
            get { return _unitPrices.Keys; }
        }

        public bool Carries(string ingredientId)
        {
            return ingredientId != null && _unitPrices.ContainsKey(ingredientId);
        }

        /// <summary>Price for one <see cref="IngredientDefinition.Unit"/> of the given ingredient.</summary>
        public decimal UnitPriceFor(string ingredientId)
        {
            decimal price;
            if (!_unitPrices.TryGetValue(ingredientId, out price))
            {
                throw new InvalidOperationException(
                    "Supplier '" + Id + "' does not carry ingredient '" + ingredientId + "'.");
            }

            return price;
        }

        public bool TryGetUnitPrice(string ingredientId, out decimal price)
        {
            if (ingredientId == null)
            {
                price = 0m;
                return false;
            }

            return _unitPrices.TryGetValue(ingredientId, out price);
        }

        public override string ToString()
        {
            return Name + " (" + Id + ", tier " + QualityTier + ")";
        }
    }
}
