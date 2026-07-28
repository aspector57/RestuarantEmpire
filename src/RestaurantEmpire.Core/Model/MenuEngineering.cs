using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// The four Kasavana-Smith quadrants (1982, still the restaurant industry standard).
    /// Every dish sits in one, judged on two axes: how profitable it is, and how often
    /// it sells.
    /// </summary>
    public enum MenuClassification
    {
        /// <summary>High margin, high volume. Protect it — don't touch the recipe or the price.</summary>
        Star = 0,

        /// <summary>Low margin, high volume. Popular but barely profitable: fix price or portion, don't cut.</summary>
        Plowhorse = 1,

        /// <summary>High margin, low volume. Profitable but under-ordered: needs visibility, not removal.</summary>
        Puzzle = 2,

        /// <summary>Low margin, low volume. Cut it or relaunch it.</summary>
        Dog = 3
    }

    /// <summary>One dish's position on the matrix, with the numbers behind it.</summary>
    public sealed class MenuItemAnalysis
    {
        internal MenuItemAnalysis(
            string recipeId, string name, decimal menuPrice, decimal plateCost,
            decimal contributionMargin, int unitsSold, decimal popularityShare,
            MenuClassification classification)
        {
            RecipeId = recipeId;
            Name = name;
            MenuPrice = menuPrice;
            PlateCost = plateCost;
            ContributionMargin = contributionMargin;
            UnitsSold = unitsSold;
            PopularityShare = popularityShare;
            Classification = classification;
        }

        public string RecipeId { get; }
        public string Name { get; }
        public decimal MenuPrice { get; }
        public decimal PlateCost { get; }
        public decimal ContributionMargin { get; }
        public int UnitsSold { get; }

        /// <summary>This dish's share of all covers sold in the period (0.25 = a quarter of orders).</summary>
        public decimal PopularityShare { get; }

        public MenuClassification Classification { get; }

        /// <summary>Total money this dish contributed over the period.</summary>
        public decimal TotalContribution { get { return ContributionMargin * UnitsSold; } }

        public override string ToString()
        {
            return Name + ": " + Classification;
        }
    }

    /// <summary>A whole menu classified, plus the two thresholds each dish was judged against.</summary>
    public sealed class MenuAnalysis
    {
        private readonly Dictionary<string, MenuItemAnalysis> _byRecipeId;

        internal MenuAnalysis(
            IList<MenuItemAnalysis> items, decimal averageContributionMargin,
            decimal popularityThreshold, int totalUnitsSold)
        {
            Items = new List<MenuItemAnalysis>(items).AsReadOnly();
            AverageContributionMargin = averageContributionMargin;
            PopularityThreshold = popularityThreshold;
            TotalUnitsSold = totalUnitsSold;

            _byRecipeId = new Dictionary<string, MenuItemAnalysis>();
            foreach (var item in items) _byRecipeId[item.RecipeId] = item;
        }

        public IReadOnlyList<MenuItemAnalysis> Items { get; }

        /// <summary>Sales-weighted average margin. Dishes at or above this count as high-margin.</summary>
        public decimal AverageContributionMargin { get; }

        /// <summary>Share of orders a dish must hit to count as popular. Dishes at or above are high-volume.</summary>
        public decimal PopularityThreshold { get; }

        public int TotalUnitsSold { get; }

        public MenuItemAnalysis this[string recipeId]
        {
            get
            {
                MenuItemAnalysis found;
                if (!_byRecipeId.TryGetValue(recipeId ?? string.Empty, out found))
                    throw new InvalidOperationException("Recipe '" + recipeId + "' was not part of this analysis.");

                return found;
            }
        }

        public IEnumerable<MenuItemAnalysis> OfClass(MenuClassification classification)
        {
            foreach (var item in Items)
            {
                if (item.Classification == classification) yield return item;
            }
        }
    }

    /// <summary>
    /// Runs the Kasavana-Smith menu engineering matrix over a restaurant's current menu.
    ///
    /// Because margins are read live from <see cref="MenuCosting"/>, this reclassifies
    /// itself the moment a supplier changes: a Star whose key ingredient got more
    /// expensive can decay into a Plowhorse without anyone recalculating anything.
    /// That live reaction is the mechanic — the design doc calls it one of three central
    /// recurring decisions in the whole game.
    /// </summary>
    public static class MenuEngineering
    {
        /// <summary>
        /// Standard industry factor: a dish counts as popular if it reaches 70% of the
        /// share it would get if every dish on the menu sold equally.
        /// </summary>
        public const decimal DefaultPopularityFactor = 0.70m;

        public static MenuAnalysis Analyze(
            Restaurant restaurant,
            IDictionary<string, int> unitsSoldByRecipeId,
            decimal popularityFactor = DefaultPopularityFactor)
        {
            if (restaurant == null) throw new ArgumentNullException(nameof(restaurant));
            if (unitsSoldByRecipeId == null) throw new ArgumentNullException(nameof(unitsSoldByRecipeId));

            var definitions = restaurant.Company.Definitions;
            var costing = restaurant.Costing;
            var recipeIds = restaurant.Menu.RecipeIds;

            if (recipeIds.Count == 0)
                return new MenuAnalysis(new List<MenuItemAnalysis>(), 0m, 0m, 0);

            // Pass 1: gather live margins and volumes.
            var margins = new decimal[recipeIds.Count];
            var units = new int[recipeIds.Count];
            var totalUnits = 0;
            var totalContribution = 0m;

            for (var i = 0; i < recipeIds.Count; i++)
            {
                int sold;
                if (!unitsSoldByRecipeId.TryGetValue(recipeIds[i], out sold)) sold = 0;

                margins[i] = costing.ContributionMargin(recipeIds[i]);
                units[i] = sold;
                totalUnits += sold;
                totalContribution += margins[i] * sold;
            }

            // MARGIN IS MEASURED WITHIN A CATEGORY. POPULARITY IS NOT.
            //
            // The split is deliberate and the two axes are not the same kind of number.
            //
            // CONTRIBUTION MARGIN IS SCALE-BOUND TO ITS CATEGORY. A $3.80 coffee cannot
            // out-earn a $34 risotto in absolute dollars however well it is priced, so
            // measuring it against a card-wide average reports a category error as a business
            // verdict. Aaron found this by playing: "it says dog on a flat white but the
            // stars are a 4.5, and it's as expensive as I can make it." He was right.
            //
            // POPULARITY SHARE IS NOT SCALE-BOUND. "What fraction of everything we sold was
            // this dish" is a real question with a comparable answer across the whole card,
            // and keeping it card-wide is what stops the matrix collapsing on a small menu:
            // measured per category, two dishes in a category BOTH clear a 0.7/2 bar, so
            // Puzzles and Dogs become unreachable until every category has several dishes.
            var catUnits = new Dictionary<string, int>();
            var catContribution = new Dictionary<string, decimal>();
            var catMarginSum = new Dictionary<string, decimal>();
            var catCount = new Dictionary<string, int>();

            for (var i = 0; i < recipeIds.Count; i++)
            {
                var category = definitions.GetRecipe(recipeIds[i]).Category;

                int u; catUnits.TryGetValue(category, out u); catUnits[category] = u + units[i];
                decimal c; catContribution.TryGetValue(category, out c);
                catContribution[category] = c + (margins[i] * units[i]);
                decimal m; catMarginSum.TryGetValue(category, out m); catMarginSum[category] = m + margins[i];
                int n; catCount.TryGetValue(category, out n); catCount[category] = n + 1;
            }

            // Kept for the read surface: the card-wide figures are still what the books show.
            decimal averageMargin;
            if (totalUnits > 0)
            {
                averageMargin = totalContribution / totalUnits;
            }
            else
            {
                var sum = 0m;
                for (var i = 0; i < margins.Length; i++) sum += margins[i];
                averageMargin = sum / recipeIds.Count;
            }

            var expectedShare = 1m / recipeIds.Count;
            var popularityThreshold = expectedShare * popularityFactor;

            // Pass 2: place each dish in its quadrant, against its own kind.
            var items = new List<MenuItemAnalysis>(recipeIds.Count);

            for (var i = 0; i < recipeIds.Count; i++)
            {
                var recipe = definitions.GetRecipe(recipeIds[i]);
                var category = recipe.Category;

                var unitsHere = catUnits[category];
                var countHere = catCount[category];

                var share = totalUnits > 0 ? (decimal)units[i] / totalUnits : 0m;

                var averageHere = unitsHere > 0
                    ? catContribution[category] / unitsHere
                    : catMarginSum[category] / countHere;

                var highMargin = margins[i] >= averageHere;
                var highVolume = share >= popularityThreshold;

                MenuClassification classification;
                if (highMargin && highVolume) classification = MenuClassification.Star;
                else if (!highMargin && highVolume) classification = MenuClassification.Plowhorse;
                else if (highMargin) classification = MenuClassification.Puzzle;
                else classification = MenuClassification.Dog;

                items.Add(new MenuItemAnalysis(
                    recipe.Id, recipe.Name, costing.MenuPrice(recipe.Id),
                    costing.PlateCost(recipe.Id), margins[i],
                    units[i], share, classification));
            }

            return new MenuAnalysis(items, averageMargin, popularityThreshold, totalUnits);
        }
    }
}
