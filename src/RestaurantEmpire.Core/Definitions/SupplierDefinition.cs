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

        public SupplierDefinition(string id, string name, int qualityTier, IDictionary<string, decimal> unitPrices)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Supplier id is required.", nameof(id));

            Id = id;
            Name = name ?? id;
            QualityTier = qualityTier;
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
