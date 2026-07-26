using System;
using System.Collections.Generic;
using RestaurantEmpire.Core.Definitions;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// Stock of one ingredient at one location, with its par level band.
    ///
    /// Par levels are the real industry tool (design doc Phase 2): a min/max band per
    /// item. Below min, you risk running out mid-service and 86'ing a dish. Above max,
    /// cash is tied up in a pantry. The tension between those two is the actual decision.
    /// </summary>
    public sealed class IngredientStock
    {
        public string IngredientId { get; }
        public decimal Quantity { get; private set; }
        public decimal ParMin { get; private set; }
        public decimal ParMax { get; private set; }

        internal IngredientStock(string ingredientId, decimal quantity, decimal parMin, decimal parMax)
        {
            IngredientId = ingredientId;
            Quantity = quantity;
            ParMin = parMin;
            ParMax = parMax;
        }

        public bool IsBelowPar { get { return Quantity < ParMin; } }
        public bool IsAbovePar { get { return ParMax > 0m && Quantity > ParMax; } }

        /// <summary>How much to order to return to the top of the band. Zero when in band.</summary>
        public decimal SuggestedReorderQuantity
        {
            get { return IsBelowPar ? ParMax - Quantity : 0m; }
        }

        internal void SetPar(decimal parMin, decimal parMax)
        {
            if (parMin < 0m) throw new ArgumentOutOfRangeException(nameof(parMin), "Par minimum cannot be negative.");
            if (parMax < parMin) throw new ArgumentOutOfRangeException(nameof(parMax), "Par maximum cannot be below par minimum.");

            ParMin = parMin;
            ParMax = parMax;
        }

        internal void Receive(decimal quantity)
        {
            if (quantity < 0m) throw new ArgumentOutOfRangeException(nameof(quantity), "Cannot receive a negative quantity.");
            Quantity += quantity;
        }

        internal bool TryConsume(decimal quantity)
        {
            if (quantity < 0m) throw new ArgumentOutOfRangeException(nameof(quantity), "Cannot consume a negative quantity.");
            if (Quantity < quantity) return false;

            Quantity -= quantity;
            return true;
        }
    }

    /// <summary>What one restaurant currently has in the walk-in, and the par band for each item.</summary>
    public sealed class Inventory
    {
        private readonly DefinitionRegistry _definitions;
        private readonly Dictionary<string, IngredientStock> _stock;

        internal Inventory(DefinitionRegistry definitions)
        {
            _definitions = definitions;
            _stock = new Dictionary<string, IngredientStock>();
        }

        public IEnumerable<IngredientStock> Items { get { return _stock.Values; } }

        public IngredientStock this[string ingredientId] { get { return GetOrCreate(ingredientId); } }

        public IngredientStock GetOrCreate(string ingredientId)
        {
            if (!_definitions.HasIngredient(ingredientId))
                throw new DefinitionNotFoundException("ingredient", ingredientId);

            IngredientStock stock;
            if (!_stock.TryGetValue(ingredientId, out stock))
            {
                stock = new IngredientStock(ingredientId, 0m, 0m, 0m);
                _stock[ingredientId] = stock;
            }

            return stock;
        }

        public void SetPar(string ingredientId, decimal parMin, decimal parMax)
        {
            GetOrCreate(ingredientId).SetPar(parMin, parMax);
        }

        public void Receive(string ingredientId, decimal quantity)
        {
            GetOrCreate(ingredientId).Receive(quantity);
        }

        /// <summary>Returns false rather than throwing when stock is short — an 86'd dish, not a crash.</summary>
        public bool TryConsume(string ingredientId, decimal quantity)
        {
            return GetOrCreate(ingredientId).TryConsume(quantity);
        }

        public decimal QuantityOf(string ingredientId)
        {
            IngredientStock stock;
            return _stock.TryGetValue(ingredientId ?? string.Empty, out stock) ? stock.Quantity : 0m;
        }

        /// <summary>Everything currently under its par minimum — the restock list.</summary>
        public IEnumerable<IngredientStock> BelowPar
        {
            get
            {
                foreach (var stock in _stock.Values)
                {
                    if (stock.IsBelowPar) yield return stock;
                }
            }
        }
    }
}
