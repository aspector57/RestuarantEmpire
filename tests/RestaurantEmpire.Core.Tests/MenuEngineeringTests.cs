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
            Assert.Equal(10.38754m, analysis.AverageContributionMargin);

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
            Assert.Equal(19.16m, before["truffle-risotto"].ContributionMargin);
            Assert.Equal(0.436m, decimal.Round(flagship.Costing.FoodCostRatio("truffle-risotto"), 3));

            // One write. Nothing else touched.
            company.SupplierPolicy.Assign("truffle", "premium-harvest");

            var after = MenuEngineering.Analyze(flagship, AWeekOfSales);

            // The dish's margin collapses from 19.16 to -0.34 and it drops to a Dog: at 93%
            // food cost it is now sold at a near loss.
            Assert.Equal(-0.34m, after["truffle-risotto"].ContributionMargin);
            Assert.Equal(MenuClassification.Dog, after["truffle-risotto"].Classification);
            Assert.True(flagship.Costing.FoodCostRatio("truffle-risotto") > 0.9m);

            // Every other dish is untouched — it was one ingredient, in one dish.
            // The pizza becomes a Star: the risotto's collapse drags the MAINS average from
            // 12.234 down to 10.145, and the pizza's 11.403 now clears it. That is the
            // propagation working — one supplier write reclassified two dishes.
            Assert.Equal(MenuClassification.Star, after["margherita"].Classification);
            Assert.Equal(MenuClassification.Star, after["caprese-salad"].Classification);
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

            Assert.Equal(0.126m, decimal.Round(costing.FoodCostRatio("house-focaccia"), 3));  // 0.1265 rounds to even
            Assert.Equal(0.186m, decimal.Round(costing.FoodCostRatio("margherita"), 3));
            Assert.Equal(0.241m, decimal.Round(costing.FoodCostRatio("caprese-salad"), 3));
            Assert.Equal(0.436m, decimal.Round(costing.FoodCostRatio("truffle-risotto"), 3));
        }

        [Fact]
        public void PlateCostAndFoodCostRatio_TrackTheAssignedSupplier()
        {
            var flagship = BuildFlagship(out var company, "budget-wholesale");

            // margherita on budget: 0.25*1.20 + 0.20*2.00 + 0.15*6.00 + 0.02*1.00 + 0.015*6.00 = 1.71
            Assert.Equal(1.71m, flagship.Costing.PlateCost("margherita"));

            company.SupplierPolicy.AssignAll("premium-harvest");

            // margherita on premium: 0.65 + 1.00 + 2.10 + 0.056 + 0.33 = 4.136
            Assert.Equal(4.136m, flagship.Costing.PlateCost("margherita"));

            // Sourcing alone swings this dish from a 14% food cost to a 34% one — the
            // single biggest lever the player has over the books.
            Assert.Equal(0.295m, decimal.Round(flagship.Costing.FoodCostRatio("margherita"), 3));
        }

        [Fact]
        public void TotalContribution_WeightsMarginByHowOftenTheDishActuallySells()
        {
            var flagship = BuildFlagship(out _);

            var analysis = MenuEngineering.Analyze(flagship, AWeekOfSales);

            // The caprese earns less than a third of the risotto per plate, but sells 340
            // covers against 60 — so it contributes far more money overall. This is exactly
            // why the matrix judges on two axes instead of ranking by margin.
            Assert.Equal(8.346m * 340, analysis["caprese-salad"].TotalContribution);
            Assert.Equal(19.16m * 60, analysis["truffle-risotto"].TotalContribution);
            Assert.True(analysis["caprese-salad"].TotalContribution >
                        analysis["truffle-risotto"].TotalContribution);
        }
    }
}
