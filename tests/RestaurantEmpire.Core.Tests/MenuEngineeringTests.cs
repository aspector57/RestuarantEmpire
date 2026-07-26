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
        private static readonly Dictionary<string, int> AWeekOfSales = new Dictionary<string, int>
        {
            { "margherita",      450 }, // 45% of covers
            { "house-focaccia",  350 }, // 35%
            { "truffle-risotto", 120 }, // 12%
            { "caprese-salad",    80 }  //  8%
        };

        private static Restaurant BuildFlagship(out Company company, string supplierId = "valley-produce")
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme-group", "Acme Restaurant Group", definitions);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Menu.Add("margherita", "house-focaccia", "truffle-risotto", "caprese-salad");
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
            Assert.Equal(12.09576m, analysis.AverageContributionMargin);

            // High margin (13.988) + high volume (45%) -> protect it.
            Assert.Equal(MenuClassification.Star, analysis["margherita"].Classification);

            // Low margin (8.428) + high volume (35%) -> popular but barely profitable.
            Assert.Equal(MenuClassification.Plowhorse, analysis["house-focaccia"].Classification);

            // High margin (15.76) + low volume (12%) -> profitable, nobody orders it.
            Assert.Equal(MenuClassification.Puzzle, analysis["truffle-risotto"].Classification);

            // Low margin (12.002, just under the 12.09576 bar) + low volume (8%) -> cut or relaunch.
            Assert.Equal(MenuClassification.Dog, analysis["caprese-salad"].Classification);
        }

        [Fact]
        public void ASupplierSwitch_SilentlyReclassifiesDishes_WithNothingRecalculatedByHand()
        {
            // THIS is the mechanic. The player changes one purchasing decision; the menu
            // matrix rearranges itself underneath them. In Restaurant Empire II this could
            // not happen, because costs were frozen into each recipe until hand-edited.
            var flagship = BuildFlagship(out var company);

            var before = MenuEngineering.Analyze(flagship, AWeekOfSales);
            Assert.Equal(MenuClassification.Puzzle, before["truffle-risotto"].Classification);
            Assert.Equal(MenuClassification.Dog, before["caprese-salad"].Classification);

            // Move the whole book to the premium supplier. Truffle more than doubles.
            company.SupplierPolicy.AssignAll("premium-harvest");

            var after = MenuEngineering.Analyze(flagship, AWeekOfSales);

            // The risotto's margin collapses from 15.76 to 2.16 — it is now a Dog.
            Assert.Equal(MenuClassification.Dog, after["truffle-risotto"].Classification);

            // And the caprese, barely below the bar before, is now above it — a Puzzle.
            Assert.Equal(MenuClassification.Puzzle, after["caprese-salad"].Classification);

            // Same sales, same menu, same code. Only the purchasing policy moved.
            Assert.Equal(before.TotalUnitsSold, after.TotalUnitsSold);
        }

        [Fact]
        public void PlateCostAndFoodCostRatio_TrackTheAssignedSupplier()
        {
            var flagship = BuildFlagship(out var company, "budget-wholesale");

            // margherita on budget: 0.25*1.20 + 0.15*2.00 + 0.12*6.00 + 0.02*1.00 = 1.34
            Assert.Equal(1.34m, flagship.Costing.PlateCost("margherita"));
            Assert.Equal(1.34m / 16.00m, flagship.Costing.FoodCostRatio("margherita"));

            company.SupplierPolicy.AssignAll("premium-harvest");

            // margherita on premium: 0.25*2.60 + 0.15*5.00 + 0.12*14.00 + 0.02*2.80 = 3.136
            Assert.Equal(3.136m, flagship.Costing.PlateCost("margherita"));
            Assert.Equal(3.136m / 16.00m, flagship.Costing.FoodCostRatio("margherita"));
        }

        [Fact]
        public void TotalContribution_WeightsMarginByHowOftenTheDishActuallySells()
        {
            var flagship = BuildFlagship(out _);

            var analysis = MenuEngineering.Analyze(flagship, AWeekOfSales);

            // The focaccia is the weakest dish per plate but sells 350 covers a week,
            // so it out-earns the risotto that looks far better on paper.
            Assert.Equal(8.428m * 350, analysis["house-focaccia"].TotalContribution);
            Assert.Equal(15.76m * 120, analysis["truffle-risotto"].TotalContribution);
            Assert.True(analysis["house-focaccia"].TotalContribution > analysis["truffle-risotto"].TotalContribution);
        }
    }
}
