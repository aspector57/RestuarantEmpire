using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Definitions;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// M0 EXIT TEST #1 (CLAUDE.md, "M0 exit tests"):
    ///
    ///   "Switching a Supplier assignment updates every dependent Recipe's
    ///    contribution margin with ZERO manual edits."
    ///
    /// This is the concrete, testable form of the fix Restaurant Empire II never got:
    /// in that game, changing a supplier meant hand-editing every recipe that used the
    /// ingredient, in every restaurant. Here, one write to one assignment record must
    /// move every dependent margin, and nothing else.
    ///
    /// The whole architecture is judged by this file.
    /// </summary>
    public class SupplierPropagationExitTests
    {
        // Plate costs with every ingredient sourced from Valley Produce.
        //   margherita      0.25*1.80 + 0.15*3.00 + 0.12*9.00 + 0.02*1.60 = 2.012  -> 16.00 - 2.012 = 13.988
        //   caprese-salad   0.20*3.00 + 0.15*9.00 + 0.03*1.60             = 1.998  -> 14.00 - 1.998 = 12.002
        //   truffle-risotto 0.12*4.50 + 8.00*2.60 + 0.05*18.00            = 22.24  -> 38.00 - 22.24 = 15.76
        //   house-focaccia  0.30*1.80 + 0.02*1.60                         = 0.572  ->  9.00 - 0.572 =  8.428
        private const decimal MargheritaOnValley = 13.988m;
        private const decimal CapreseOnValley = 12.002m;
        private const decimal RisottoOnValley = 15.76m;
        private const decimal FocacciaOnValley = 8.428m;

        // After tomato alone moves to Premium Harvest (5.00/kg instead of 3.00/kg):
        //   margherita      tomato line goes 0.45 -> 0.75, cost 2.312 -> 16.00 - 2.312 = 13.688
        //   caprese-salad   tomato line goes 0.60 -> 1.00, cost 2.398 -> 14.00 - 2.398 = 11.602
        private const decimal MargheritaOnPremiumTomato = 13.688m;
        private const decimal CapreseOnPremiumTomato = 11.602m;

        private static Restaurant BuildFlagship(out Company company)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme-group", "Acme Restaurant Group", definitions);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Menu.Add("margherita", "caprese-salad", "truffle-risotto", "house-focaccia");
            company.SupplierPolicy.AssignAll("valley-produce");

            return restaurant;
        }

        [Fact]
        public void SwitchingOneSupplierAssignment_UpdatesEveryDependentRecipeMargin_WithZeroManualEdits()
        {
            var flagship = BuildFlagship(out var company);

            // Baseline: every recipe priced off Valley Produce.
            Assert.Equal(MargheritaOnValley, flagship.Costing.ContributionMargin("margherita"));
            Assert.Equal(CapreseOnValley, flagship.Costing.ContributionMargin("caprese-salad"));
            Assert.Equal(RisottoOnValley, flagship.Costing.ContributionMargin("truffle-risotto"));
            Assert.Equal(FocacciaOnValley, flagship.Costing.ContributionMargin("house-focaccia"));

            // ---- THE ONE WRITE. No recipe is touched. No menu is touched. ----
            company.SupplierPolicy.Assign("tomato", "premium-harvest");

            // Every recipe that uses tomato has moved...
            Assert.Equal(MargheritaOnPremiumTomato, flagship.Costing.ContributionMargin("margherita"));
            Assert.Equal(CapreseOnPremiumTomato, flagship.Costing.ContributionMargin("caprese-salad"));

            // ...and every recipe that does not use tomato is untouched.
            Assert.Equal(RisottoOnValley, flagship.Costing.ContributionMargin("truffle-risotto"));
            Assert.Equal(FocacciaOnValley, flagship.Costing.ContributionMargin("house-focaccia"));
        }

        [Fact]
        public void SwitchingOneSupplierAssignment_PropagatesToEveryLocation_NotJustOne()
        {
            // The Phase 6 contract says "every Recipe and every location" — a single
            // company-level policy write must reach a second restaurant it was never
            // told about.
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("acme-group", "Acme Restaurant Group", definitions);

            var flagship = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);
            var truck = company.OpenRestaurant("truck", "Acme Truck", LocationType.FoodTruck);

            flagship.Menu.Add("margherita", "caprese-salad");
            truck.Menu.Add("margherita");

            company.SupplierPolicy.AssignAll("valley-produce");

            Assert.Equal(MargheritaOnValley, flagship.Costing.ContributionMargin("margherita"));
            Assert.Equal(MargheritaOnValley, truck.Costing.ContributionMargin("margherita"));

            company.SupplierPolicy.Assign("tomato", "premium-harvest");

            Assert.Equal(MargheritaOnPremiumTomato, flagship.Costing.ContributionMargin("margherita"));
            Assert.Equal(MargheritaOnPremiumTomato, truck.Costing.ContributionMargin("margherita"));
        }

        [Fact]
        public void ContributionMargin_IsComputedLiveEveryRead_NeverCached()
        {
            // Architecture Rule 1: "nothing is cached." Reading the same recipe three
            // times across three different assignments must give three different answers
            // without anything being invalidated or refreshed in between.
            var flagship = BuildFlagship(out var company);

            company.SupplierPolicy.Assign("tomato", "budget-wholesale");
            var onBudget = flagship.Costing.ContributionMargin("margherita");

            company.SupplierPolicy.Assign("tomato", "valley-produce");
            var onValley = flagship.Costing.ContributionMargin("margherita");

            company.SupplierPolicy.Assign("tomato", "premium-harvest");
            var onPremium = flagship.Costing.ContributionMargin("margherita");

            // Cheaper tomatoes -> bigger margin. Strictly ordered, no stale reads.
            Assert.True(onBudget > onValley);
            Assert.True(onValley > onPremium);
            Assert.Equal(MargheritaOnValley, onValley);
            Assert.Equal(MargheritaOnPremiumTomato, onPremium);
        }

        [Fact]
        public void RecipeDefinitions_CarryNoCostAtAll_SoAStaleCopyCannotExist()
        {
            // The strongest form of the guarantee: it is not that we remember to refresh
            // recipe costs — it is that a RecipeDefinition has nowhere to put one.
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var recipe = definitions.GetRecipe("margherita");

            var costLikeProperties = typeof(RecipeDefinition)
                .GetProperties()
                .Where(p => p.Name.Contains("Cost") || p.Name.Contains("Margin"))
                .ToList();

            Assert.Empty(costLikeProperties);
            Assert.NotEmpty(recipe.Ingredients); // it holds ingredient IDs and quantities, nothing more
        }
    }
}
