using System;
using System.Collections.Generic;
using RestaurantEmpire.Core.Definitions;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// What the restaurant charges for each dish.
    ///
    /// Pricing is a PLAYER DECISION, not content. The recipe's own MenuPrice is only a
    /// suggested starting point shipped with the dish; the moment a company or a location
    /// sets its own, that wins. Phase 4's Recipes contract says a dish can "be added to or
    /// dropped from the menu, priced", and Phase 9 asks specifically for "per-instance
    /// overrides for local menu/pricing" so a franchise brand can be adapted locally.
    ///
    /// Resolution walks the same chain as <see cref="SupplierPolicy"/> —
    /// Restaurant -> Company -> the recipe's shipped default — and caches nothing, so a
    /// price change moves every dependent margin, ratio and classification on the next read.
    ///
    /// This is the lever that makes expensive sourcing a strategy rather than a trap: you
    /// cannot buy premium ingredients and charge mid-market prices, but you CAN buy premium
    /// and charge accordingly, which is exactly how real fine dining works.
    /// </summary>
    public sealed class PricingPolicy
    {
        private readonly DefinitionRegistry _definitions;
        private readonly PricingPolicy _inheritsFrom;
        private readonly Dictionary<string, decimal> _prices;

        internal PricingPolicy(DefinitionRegistry definitions, string scopeName, PricingPolicy inheritsFrom)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            _definitions = definitions;
            _inheritsFrom = inheritsFrom;
            _prices = new Dictionary<string, decimal>(StringComparer.Ordinal);

            ScopeName = string.IsNullOrWhiteSpace(scopeName) ? "unnamed scope" : scopeName;
        }

        /// <summary>Name reported when this scope is the one that set the price.</summary>
        public string ScopeName { get; }

        /// <summary>The scope this falls back to, or null at the top of the chain.</summary>
        public PricingPolicy InheritsFrom { get { return _inheritsFrom; } }

        /// <summary>Prices set AT THIS SCOPE. Empty means "everything is inherited", the normal case.</summary>
        public IReadOnlyDictionary<string, decimal> LocalPrices { get { return _prices; } }

        /// <summary>
        /// Sets what this scope charges. Free dishes are allowed (a comp, a tasting portion);
        /// negative prices are not.
        /// </summary>
        public void SetPrice(string recipeId, decimal price)
        {
            if (!_definitions.HasRecipe(recipeId))
                throw new DefinitionNotFoundException("recipe", recipeId);

            if (price < 0m)
                throw new ArgumentOutOfRangeException(nameof(price), "A dish cannot have a negative price.");

            _prices[recipeId] = price;
        }

        /// <summary>Applies a multiplier to the currently resolved price. 1.15m is "put everything up 15%".</summary>
        public void AdjustPrice(string recipeId, decimal multiplier)
        {
            if (multiplier < 0m)
                throw new ArgumentOutOfRangeException(nameof(multiplier), "Price multiplier cannot be negative.");

            SetPrice(recipeId, ResolvePrice(recipeId) * multiplier);
        }

        /// <summary>Drops a local price so this scope goes back to inheriting.</summary>
        public bool ClearOverride(string recipeId)
        {
            return recipeId != null && _prices.Remove(recipeId);
        }

        public bool HasLocalOverride(string recipeId)
        {
            return recipeId != null && _prices.ContainsKey(recipeId);
        }

        /// <summary>
        /// What this scope charges right now: its own price, else the nearest ancestor's,
        /// else the price the dish shipped with. Always answers — unlike sourcing, a dish
        /// can never be "unpriced".
        /// </summary>
        public decimal ResolvePrice(string recipeId)
        {
            var scope = ResolveScope(recipeId);

            return scope != null
                ? scope._prices[recipeId]
                : _definitions.GetRecipe(recipeId).MenuPrice;
        }

        /// <summary>The scope that set this price, or null when it is still the shipped default.</summary>
        public PricingPolicy ResolveScope(string recipeId)
        {
            var key = recipeId ?? string.Empty;
            var scope = this;

            while (scope != null)
            {
                if (scope._prices.ContainsKey(key)) return scope;
                scope = scope._inheritsFrom;
            }

            return null;
        }

        /// <summary>
        /// Who decided this price — a location, the company, or nobody yet. Supports the
        /// "every outcome traces to a named cause" contract for money as well as for waits.
        /// </summary>
        public string ResolvedFromScopeName(string recipeId)
        {
            var scope = ResolveScope(recipeId);
            return scope == null ? "menu default" : scope.ScopeName;
        }
    }
}
