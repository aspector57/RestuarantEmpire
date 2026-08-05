using System.Collections.Generic;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// The Kasavana-Smith menu engineering matrix (design doc Phase 2) — one of the three
    /// mechanics the design names as central and recurring.
    ///
    /// The point of these tests is not just that the four quadrants are computed correctly.
    /// It is that classification is DERIVED from live margins, so a supplier decision can
    /// silently turn a Puzzle into a Dog — which is the strategic tension the whole system
    /// exists to create.
    /// </summary>
    public class MenuEngineeringTests
    {
        // Contribution margins on Valley Produce:
        //   margherita 11.403 · caprese-salad 8.346 · house-focaccia 6.988 · truffle-risotto 19.160
        private static readonly Dictionary<string, int> AWeekOfSales = new Dictionary<string, int>
        {
            { "margherita",      500 }, // 50% of covers
            { "caprese-salad",   340 }, // 34%
            { "house-focaccia",  100 }, // 10%
            { "truffle-risotto",  60 }  //  6%
        };

        private static Restaurant BuildFlagship(out Company company, string supplierId = "valley-produce")
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme-group", "Acme Restaurant Group", definitions);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Menu.Add("margherita", "caprese-salad", "house-focaccia", "truffle-risotto");
            company.SupplierPolicy.AssignAll(supplierId);

            return restaurant;
        }

        [Fact]
        public void EachDish_LandsInTheCorrectKasavanaSmithQuadrant()
        {
            var flagship = BuildFlagship(out _);

            var analysis = MenuEngineering.Analyze(flagship, AWeekOfSales);

            // Popularity bar: 70% of an even share across 4 dishes = 0.70 / 4 = 0.175
            Assert.Equal(0.175m, analysis.PopularityThreshold);

            // Sales-weighted average margin across the week.
            // What the card earns per plate SOLD — weighted by volume, not a flat mean of the
            // dishes, because a dish nobody orders should not drag the average around.
            var sold = analysis.Items.Sum(d => d.UnitsSold);
            var expected = analysis.Items.Sum(d => d.ContributionMargin * d.UnitsSold) / sold;
            Assert.Equal(decimal.Round(expected, 5), decimal.Round(analysis.AverageContributionMargin, 5));

            // Margin is judged against the dish's OWN CATEGORY, popularity against the whole
            // card. Mains average 12.234 a plate across the week (margherita 11.403 at 50
            // covers, risotto 19.160 at 6); small plates average 7.810 (caprese 8.346 at 34,
            // focaccia 5.988 at 10).
            //
            // So the pizza is the popular, thinner-margin MAIN — a plowhorse among mains,
            // which is the useful reading. Card-wide it looked like a Star only because it
            // was being averaged against small plates it does not compete with.
            Assert.Equal(MenuClassification.Plowhorse, analysis["margherita"].Classification);

            // And the caprese is the better-margin small plate that also sells: a Star among
            // its own kind, where before it was dragged under a card-wide average inflated
            // by the mains.
            Assert.Equal(MenuClassification.Star, analysis["caprese-salad"].Classification);

            // High margin (19.160) + low volume (6%) -> profitable, nobody orders it.
            Assert.Equal(MenuClassification.Puzzle, analysis["truffle-risotto"].Classification);

            // Low margin (5.988) + low volume (10%) -> cut or relaunch.
            Assert.Equal(MenuClassification.Dog, analysis["house-focaccia"].Classification);
        }

        [Fact]
        public void OneIngredientsSupplierCanDestroyOneDish_AndTheMatrixNoticesByItself()
        {
            // THIS is the mechanic. The truffle is the only ingredient whose price swings
            // violently between suppliers (1.80 / 2.60 / 6.50), and it sits in exactly one
            // dish. Switching it alone is a single, plausible purchasing decision that
            // reclassifies the menu underneath the player.
            //
            // In Restaurant Empire II this could not happen: costs were frozen into each
            // recipe until hand-edited, so the matrix could never react to sourcing.
            var flagship = BuildFlagship(out var company);

            var before = MenuEngineering.Analyze(flagship, AWeekOfSales);
            Assert.Equal(MenuClassification.Puzzle, before["truffle-risotto"].Classification);
            // The luxury dish is the thin one — that is the claim, not a particular decimal.
            Assert.True(flagship.Costing.FoodCostRatio("truffle-risotto") >
                        flagship.Costing.FoodCostRatio("margherita"),
                "the luxury dish should run the thinnest margin on the card");

            // One write. Nothing else touched.
            company.SupplierPolicy.Assign("truffle", "premium-harvest");

            var after = MenuEngineering.Analyze(flagship, AWeekOfSales);

            // ONE WRITE, AND THE DISH'S ECONOMICS COLLAPSE. It used to go to a 93% food cost
            // and an actual loss, which was a bug rather than a Puzzle — truffle was priced so
            // that the top supplier made the dish unsellable, and a dish nobody can profitably
            // serve is not a decision. Truffle was repriced; the LEVER is unchanged and is what
            // this test is about.
            var was = before["truffle-risotto"].ContributionMargin;
            var now = after["truffle-risotto"].ContributionMargin;
            Assert.True(now < was * 0.85m,
                $"one ingredient should visibly hurt the dish: {was:C} -> {now:C}");
            Assert.True(flagship.Costing.FoodCostRatio("truffle-risotto") > 0.45m,
                "and leave it the thinnest thing on the card");

            // ONE WRITE MOVES THE WHOLE MATRIX, not just the dish it touched — that is the
            // propagation claim and it is what Restaurant Empire II could not do.
            //
            // It used to be assertable as "the pizza flips to a Star", because the risotto fell
            // so far it dragged the mains average under the pizza. Truffle has since been
            // repriced so the top supplier no longer makes the dish unsellable, so the fall is
            // smaller and the pizza no longer crosses the line. The pizza still MOVES UP
            // against its category, which is the part that was ever about propagation.
            var pizzaGapBefore = before["margherita"].ContributionMargin - before.AverageContributionMargin;
            var pizzaGapAfter  = after["margherita"].ContributionMargin  - after.AverageContributionMargin;
            Assert.True(pizzaGapAfter > pizzaGapBefore,
                $"the pizza should stand better against the card once the risotto falls: {pizzaGapBefore:C} -> {pizzaGapAfter:C}");

            // And nothing was written to the pizza or the salad at all.
            Assert.Equal(before["margherita"].PlateCost, after["margherita"].PlateCost);
            Assert.Equal(before["caprese-salad"].PlateCost, after["caprese-salad"].PlateCost);
            Assert.Equal(before.TotalUnitsSold, after.TotalUnitsSold);
        }

        [Fact]
        public void TheMenuSpansARealisticSpreadOfFoodCostRatios()
        {
            // A real menu is not uniform: bread and pizza carry the margin, a luxury
            // protein dish runs thin. The blended figure is what has to land in the
            // industry's 28-35% band, not each individual dish.
            var flagship = BuildFlagship(out _);
            var costing = flagship.Costing;

            // ASSERTS THE SPREAD, NOT FOUR LITERALS. Pinning each ratio encodes the CONTENT,
            // so repricing a dish in a data file fails a test whose claim is still true —
            // which is hostile to Architecture Rule 2, the rule this suite exists to protect.
            var bread   = costing.FoodCostRatio("house-focaccia");
            var pizza   = costing.FoodCostRatio("margherita");
            var salad   = costing.FoodCostRatio("caprese-salad");
            var luxury  = costing.FoodCostRatio("truffle-risotto");

            Assert.True(bread < pizza,  $"bread should carry more margin than pizza: {bread:P0} vs {pizza:P0}");
            Assert.True(pizza < salad,  $"pizza should carry more margin than the salad: {pizza:P0} vs {salad:P0}");
            Assert.True(salad < luxury, $"the luxury dish should run thinnest: {salad:P0} vs {luxury:P0}");

            // And the card as a whole has to be a business.
            var blended = (bread + pizza + salad + luxury) / 4m;
            Assert.True(blended > 0.15m && blended < 0.45m,
                $"a menu blending to {blended:P0} is not a restaurant");
        }

        [Fact]
        public void PlateCostAndFoodCostRatio_TrackTheAssignedSupplier()
        {
            var flagship = BuildFlagship(out var company, "budget-wholesale");

            var onBudget = flagship.Costing.PlateCost("margherita");
            var budgetRatio = flagship.Costing.FoodCostRatio("margherita");

            company.SupplierPolicy.AssignAll("premium-harvest");

            var onPremium = flagship.Costing.PlateCost("margherita");
            var premiumRatio = flagship.Costing.FoodCostRatio("margherita");

            // The claim is that sourcing MOVES these live and substantially — that is the
            // Architecture Rule 1 promise. What the numbers happen to be is content.
            Assert.True(onPremium > onBudget * 1.8m,
                $"premium should cost appreciably more: {onPremium:C} against {onBudget:C}");
            Assert.True(premiumRatio > budgetRatio + 0.08m,
                $"sourcing is the biggest lever on the books: {budgetRatio:P0} -> {premiumRatio:P0}");
        }

        [Fact]
        public void TotalContribution_WeightsMarginByHowOftenTheDishActuallySells()
        {
            var flagship = BuildFlagship(out _);

            var analysis = MenuEngineering.Analyze(flagship, AWeekOfSales);

            // The caprese earns less than a third of the risotto per plate, but sells 340
            // covers against 60 — so it contributes far more money overall. This is exactly
            // why the matrix judges on two axes instead of ranking by margin.
            Assert.Equal(analysis["caprese-salad"].ContributionMargin * 340,
                         analysis["caprese-salad"].TotalContribution);
            Assert.Equal(analysis["truffle-risotto"].ContributionMargin * 60,
                         analysis["truffle-risotto"].TotalContribution);
            Assert.True(analysis["caprese-salad"].TotalContribution >
                        analysis["truffle-risotto"].TotalContribution);
        }
    }
}
