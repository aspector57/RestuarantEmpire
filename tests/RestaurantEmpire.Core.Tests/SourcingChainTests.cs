using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// The sourcing inheritance chain: Company -> (Region, at M4) -> Restaurant.
    ///
    /// The design doc's Suppliers contract asks for "a single decision that automatically
    /// updates every recipe and location, with any exceptions requiring explicit opt-in
    /// rather than every instance requiring opt-in by default." Both halves matter:
    /// propagation must be the default, and an exception must be possible without
    /// reintroducing Restaurant Empire II's per-instance editing tax.
    /// </summary>
    public class SourcingChainTests
    {
        // COMPUTED FROM THE CONTENT, NOT PINNED TO IT. These were literals, so repricing the
        // menu in a data file failed every test in this class — and every one of them was still
        // true. What this class is about is PROPAGATION: that a company-wide switch reaches
        // every location. The exact margin is content, and Architecture Rule 2 says content must
        // be changeable by editing a data file.
        /// <summary>
        /// The margin with everything on Valley and ONE ingredient switched — which is what
        /// these tests actually exercise, and what the original literals meant when they were
        /// annotated "tomato at 3.00".
        /// </summary>
        private static decimal MarginOn(string tomatoSupplierId)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var recipe = definitions.GetRecipe("margherita");
            var baseline = definitions.GetSupplier("valley-produce");
            var swapped = definitions.GetSupplier(tomatoSupplierId);

            var cost = 0m;
            foreach (var line in recipe.Ingredients)
            {
                var from = line.IngredientId == "tomato" ? swapped : baseline;
                cost += from.UnitPriceFor(line.IngredientId) * line.Quantity;
            }

            return recipe.MenuPrice - cost;
        }

        private static readonly decimal MargheritaOnValley  = MarginOn("valley-produce");
        private static readonly decimal MargheritaOnPremium = MarginOn("premium-harvest");
        private static readonly decimal MargheritaOnBudget  = MarginOn("budget-wholesale");

        private static Company BuildTwoLocations(out Restaurant flagship, out Restaurant truck)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("acme-group", "Acme Restaurant Group", definitions);

            flagship = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);
            truck = company.OpenRestaurant("truck", "Acme Truck", LocationType.FoodTruck);

            flagship.Menu.Add("margherita");
            truck.Menu.Add("margherita");

            company.SupplierPolicy.AssignAll("valley-produce");

            return company;
        }

        [Fact]
        public void ARestaurantWithNoOverrides_InheritsEverythingFromTheCompany()
        {
            BuildTwoLocations(out var flagship, out var truck);

            // The healthy default: a location's own scope is completely empty.
            Assert.Empty(flagship.SupplierPolicy.LocalAssignments);
            Assert.Empty(truck.SupplierPolicy.LocalAssignments);

            Assert.Equal(MargheritaOnValley, flagship.Costing.ContributionMargin("margherita"));
            Assert.Equal(MargheritaOnValley, truck.Costing.ContributionMargin("margherita"));

            // ...and it can say plainly who made the call.
            Assert.Equal("Acme Restaurant Group", flagship.SupplierPolicy.ResolvedFromScopeName("tomato"));
        }

        [Fact]
        public void ALocationCanOverrideOneIngredient_WithoutAffectingAnyOtherLocation()
        {
            BuildTwoLocations(out var flagship, out var truck);

            // The flagship buys its tomatoes from the good grower. The truck doesn't.
            flagship.SupplierPolicy.Assign("tomato", "premium-harvest");

            Assert.Equal(MargheritaOnPremium, flagship.Costing.ContributionMargin("margherita"));
            Assert.Equal(MargheritaOnValley, truck.Costing.ContributionMargin("margherita"));

            Assert.True(flagship.SupplierPolicy.HasLocalOverride("tomato"));
            Assert.False(truck.SupplierPolicy.HasLocalOverride("tomato"));

            Assert.Equal("The Flagship", flagship.SupplierPolicy.ResolvedFromScopeName("tomato"));
            Assert.Equal("Acme Restaurant Group", truck.SupplierPolicy.ResolvedFromScopeName("tomato"));
        }

        [Fact]
        public void OverridingOneIngredient_LeavesEveryOtherIngredientInheriting()
        {
            BuildTwoLocations(out var flagship, out _);

            flagship.SupplierPolicy.Assign("tomato", "premium-harvest");

            // Only tomato diverged. Flour, mozzarella and basil still follow the company.
            Assert.Single(flagship.SupplierPolicy.LocalAssignments);
            Assert.Equal("Acme Restaurant Group", flagship.SupplierPolicy.ResolvedFromScopeName("mozzarella"));
            Assert.Equal("valley-produce", flagship.SupplierPolicy.ResolveSupplierId("mozzarella"));
        }

        [Fact]
        public void ACompanyWideSwitch_StillReachesEveryLocationThatHasNotOptedOut()
        {
            // This is the property that must survive adding overrides at all: the company
            // dial still moves everything, EXCEPT where someone deliberately said otherwise.
            var company = BuildTwoLocations(out var flagship, out var truck);

            flagship.SupplierPolicy.Assign("tomato", "premium-harvest"); // flagship opts out

            company.SupplierPolicy.Assign("tomato", "budget-wholesale"); // one company-wide write

            Assert.Equal(MargheritaOnBudget, truck.Costing.ContributionMargin("margherita"));  // followed
            Assert.Equal(MargheritaOnPremium, flagship.Costing.ContributionMargin("margherita")); // held its override
        }

        [Fact]
        public void ClearingAnOverride_FallsBackToWhateverTheCompanyCurrentlySays()
        {
            var company = BuildTwoLocations(out var flagship, out _);

            flagship.SupplierPolicy.Assign("tomato", "premium-harvest");
            Assert.Equal(MargheritaOnPremium, flagship.Costing.ContributionMargin("margherita"));

            company.SupplierPolicy.Assign("tomato", "budget-wholesale");
            Assert.True(flagship.SupplierPolicy.ClearOverride("tomato"));

            // Note it falls back to the CURRENT company value, not the one in force when
            // the override was created. Nothing was snapshotted.
            Assert.Equal(MargheritaOnBudget, flagship.Costing.ContributionMargin("margherita"));
            Assert.False(flagship.SupplierPolicy.ClearOverride("tomato")); // nothing left to clear
        }

        [Fact]
        public void ARestaurantOpenedLater_ImmediatelyInheritsTheCurrentCompanyPolicy()
        {
            // Expansion must not require re-doing sourcing. A new location is already
            // sourced the moment it opens.
            var company = BuildTwoLocations(out _, out _);
            company.SupplierPolicy.Assign("tomato", "budget-wholesale");

            var newSite = company.OpenRestaurant("second-city", "Second City", LocationType.BrickAndMortar);
            newSite.Menu.Add("margherita");

            Assert.Empty(newSite.SupplierPolicy.LocalAssignments);
            Assert.Equal(MargheritaOnBudget, newSite.Costing.ContributionMargin("margherita"));
        }

        [Fact]
        public void TheChainIsWalkedLive_SoAnUnassignedIngredientReportsTheScopeThatFailed()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("acme-group", "Acme Restaurant Group", definitions);
            var flagship = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            Assert.False(flagship.SupplierPolicy.IsAssigned("tomato"));
            Assert.Null(flagship.SupplierPolicy.ResolvedFromScopeName("tomato"));

            var error = Assert.Throws<System.InvalidOperationException>(
                () => flagship.SupplierPolicy.UnitPriceFor("tomato"));

            Assert.Contains("The Flagship", error.Message);
            Assert.Contains("inherits from", error.Message);
        }
    }
}
