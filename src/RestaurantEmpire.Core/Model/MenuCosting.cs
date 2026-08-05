using System;
using System.Collections.Generic;
using RestaurantEmpire.Core.Definitions;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// Computes what a dish costs and what it earns, live, from the supplier assignments
    /// in force at the moment of the call.
    ///
    /// This class holds NO cached values by design. Every method walks the recipe's
    /// ingredient lines, asks the current <see cref="SupplierPolicy"/> what each one costs
    /// right now, and adds it up. That is why switching a supplier needs zero manual edits:
    /// there was never a stored number to go stale in the first place.
    /// </summary>
    public sealed class MenuCosting
    {
        private readonly DefinitionRegistry _definitions;
        private readonly SupplierPolicy _policy;
        private readonly PricingPolicy _pricing;
        private readonly DishExtras _extras;

        public MenuCosting(DefinitionRegistry definitions, SupplierPolicy policy, PricingPolicy pricing,
                           DishExtras extras = null)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (pricing == null) throw new ArgumentNullException(nameof(pricing));

            _definitions = definitions;
            _policy = policy;
            _pricing = pricing;
            _extras = extras;
        }

        /// <summary>
        /// What this restaurant charges for the dish right now — the player's price if one
        /// is set anywhere up the chain, otherwise the price the recipe shipped with.
        /// Every figure below is computed against this, never against the raw definition.
        /// </summary>
        /// <remarks>Which extras are currently on this dish. Owned by the Restaurant, read here.</remarks>
        public IReadOnlyList<string> ExtrasOn(string recipeId)
        {
            return _extras == null ? new List<string>() : _extras.On(recipeId);
        }

        /// <summary>
        /// HOW MUCH MORE THIS DISH IS WORTH for what has been put on it — diminishing, then
        /// capped by what the dish IS. See <see cref="Definitions.DishExtraDefinition"/>.
        ///
        /// Ordered by lift so the answer does not depend on the order the player ticked the
        /// boxes: two identical plates must be worth the same.
        /// </summary>
        public decimal ExtrasLift(string recipeId)
        {
            var chosen = ExtrasOn(recipeId);
            if (chosen.Count == 0) return 0m;

            var picked = new List<Definitions.DishExtraDefinition>();
            foreach (var id in chosen)
            {
                var extra = _definitions.GetExtra(recipeId, id);
                if (extra != null) picked.Add(extra);
            }

            picked.Sort((a, b) => b.Lift.CompareTo(a.Lift));

            var lift = 0m;
            var factor = 1m;
            for (var i = 0; i < picked.Count; i++)
            {
                lift += picked[i].Lift * factor;
                factor *= Tuning.ExtraDiminishing;
            }

            var ceiling = _definitions.LiftCeilingFor(_definitions.GetRecipe(recipeId).Category);
            return lift > ceiling ? ceiling : lift;
        }

        /// <summary>
        /// What the dish is DESIGNED to sell for, once you account for what is on it.
        ///
        /// This is the half that makes extras pay at all. The first version raised cost and
        /// quality only — and quality reaches money solely through the slow reputation chain,
        /// so profit fell at every step and the right answer was always "add nothing". A
        /// dressed-up plate has to be JUDGED against a higher bar, or charging for it reads
        /// as gouging.
        /// </summary>
        public decimal DesignedPrice(string recipeId)
        {
            return _definitions.GetRecipe(recipeId).MenuPrice * (1m + ExtrasLift(recipeId));
        }

        public decimal MenuPrice(string recipeId)
        {
            return _pricing.ResolvePrice(recipeId);
        }

        /// <summary>
        /// Total ingredient cost to put one plate of this dish in front of a guest,
        /// at today's supplier assignments.
        /// </summary>
        public decimal PlateCost(string recipeId)
        {
            var recipe = _definitions.GetRecipe(recipeId);
            var total = 0m;

            for (var i = 0; i < recipe.Ingredients.Count; i++)
            {
                var line = recipe.Ingredients[i];
                total += _policy.UnitPriceFor(line.IngredientId) * line.Quantity;
            }

            foreach (var id in ExtrasOn(recipeId))
            {
                var extra = _definitions.GetExtra(recipeId, id);
                if (extra == null) continue;
                for (var i = 0; i < extra.Ingredients.Count; i++)
                {
                    var line = extra.Ingredients[i];
                    total += _policy.UnitPriceFor(line.IngredientId) * line.Quantity;
                }
            }

            return total;
        }

        /// <summary>
        /// Menu price minus plate cost — the money the dish actually contributes.
        /// One of the two axes of the Kasavana-Smith matrix.
        /// </summary>
        public decimal ContributionMargin(string recipeId)
        {
            return MenuPrice(recipeId) - PlateCost(recipeId);
        }

        /// <summary>Contribution margin as a share of menu price (0.72 = 72% of the price is margin).</summary>
        public decimal ContributionMarginRatio(string recipeId)
        {
            var price = MenuPrice(recipeId);
            return price == 0m ? 0m : ContributionMargin(recipeId) / price;
        }

        /// <summary>
        /// Plate cost as a share of menu price. The industry benchmark is roughly 28-35%
        /// (design doc Phase 2); this is the per-dish half of what later rolls up into prime cost.
        /// </summary>
        public decimal FoodCostRatio(string recipeId)
        {
            var price = MenuPrice(recipeId);
            return price == 0m ? 0m : PlateCost(recipeId) / price;
        }

        /// <summary>
        /// What this location charges against the price the dish shipped with. 1.0 is as
        /// designed; 3.0 means you are asking three times what it is worth, and guests
        /// notice that from the menu without eating anything.
        /// </summary>
        public decimal Markup(string recipeId)
        {
            var designed = DesignedPrice(recipeId);
            return designed == 0m ? 1m : MenuPrice(recipeId) / designed;
        }

        /// <summary>
        /// How good this dish's ingredients currently are, 0 to 1, from the quality tiers of
        /// whichever suppliers are assigned right now.
        ///
        /// Live like everything else here, which is the interesting part: switching to a
        /// cheaper supplier raises margin and lowers this in the same instant, and guests
        /// taste the difference. That is the tradeoff the whole sourcing system exists to create.
        /// </summary>
        /// <summary>
        /// How fresh this dish's ingredients are, 0 to 1 — the WEAKEST of them, because one
        /// tired component is what a guest notices. Aaron: *"if it's not fresh... they can
        /// kinda say it didn't taste super fresh."*
        ///
        /// This is what turns spoilage from a bin charge into a gradient. Before it, old stock
        /// cost exactly one thing — the waste when it turned — so there was never a reason to
        /// throw anything out early. Now serving tired stock is a real choice with a real
        /// price, and binning it is the alternative rather than a pointless loss.
        /// </summary>
        public decimal Freshness(string recipeId, Inventory inventory)
        {
            var recipe = _definitions.GetRecipe(recipeId);
            if (recipe.Ingredients.Count == 0 || inventory == null) return 1m;

            var worst = 1m;
            for (var i = 0; i < recipe.Ingredients.Count; i++)
            {
                var fresh = inventory.FreshnessOf(recipe.Ingredients[i].IngredientId, _definitions);
                if (fresh < worst) worst = fresh;
            }

            return worst;
        }

        /// <summary>
        /// How dear this restaurant looks, as an average across the card. 1.0 is priced as
        /// designed. This is what somebody knows about a place BEFORE they decide to go.
        /// </summary>
        public decimal PricePosition(IReadOnlyList<string> recipeIds)
        {
            if (recipeIds == null || recipeIds.Count == 0) return 1m;

            var total = 0m;
            for (var i = 0; i < recipeIds.Count; i++) total += Markup(recipeIds[i]);

            return total / recipeIds.Count;
        }


        /// <summary>
        /// What the whole card is made of, on average. Willingness to pay is judged against the
        /// restaurant rather than against one dish — a guest decides whether to come here, not
        /// whether to order the risotto.
        /// </summary>
        public decimal IngredientQuality(IReadOnlyList<string> recipeIds)
        {
            if (recipeIds == null || recipeIds.Count == 0) return 0.5m;

            var total = 0m;
            for (var i = 0; i < recipeIds.Count; i++) total += IngredientQuality(recipeIds[i]);

            return total / recipeIds.Count;
        }
        public decimal IngredientQuality(string recipeId)
        {
            var recipe = _definitions.GetRecipe(recipeId);
            if (recipe.Ingredients.Count == 0) return 0m;

            var totalTier = 0m;
            for (var i = 0; i < recipe.Ingredients.Count; i++)
                totalTier += _policy.ResolveSupplier(recipe.Ingredients[i].IngredientId).QualityTier;

            // Tiers run 1..5, so this lands in 0.2 (budget) .. 1.0 (premium).
            return (totalTier / recipe.Ingredients.Count) / 5m;
        }
    }
}
