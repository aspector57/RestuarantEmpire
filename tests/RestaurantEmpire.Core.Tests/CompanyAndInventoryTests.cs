using System;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Definitions;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// The Company -> Restaurant hierarchy and per-location inventory with par levels.
    /// </summary>
    public class CompanyAndInventoryTests
    {
        private static Company NewCompany()
        {
            return new Company("acme-group", "Acme Restaurant Group",
                JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory));
        }

        [Fact]
        public void ARestaurantAlwaysBelongsToACompany_EvenWhenThereIsOnlyOne()
        {
            var company = NewCompany();
            var solo = company.OpenRestaurant("first-place", "First Place", LocationType.BrickAndMortar);

            Assert.Same(company, solo.Company);
            Assert.Single(company.Restaurants);

            // There is no public constructor for a free-floating Restaurant — the hierarchy
            // is enforced by the type system, not by convention.
            var constructors = typeof(Restaurant).GetConstructors();
            Assert.Empty(constructors);
        }

        [Fact]
        public void LocationTypeIsAParameter_NotASubclass()
        {
            var company = NewCompany();

            var flagship = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);
            var truck = company.OpenRestaurant("truck", "Acme Truck", LocationType.FoodTruck);
            var ghost = company.OpenRestaurant("ghost", "Acme Delivery Kitchen", LocationType.GhostKitchen);

            Assert.Equal(3, company.Restaurants.Count);
            Assert.All(new[] { flagship, truck, ghost }, r => Assert.IsType<Restaurant>(r));
            Assert.Equal(LocationType.FoodTruck, truck.LocationType);
        }

        [Fact]
        public void OpeningTwoRestaurantsWithTheSameId_IsRejected()
        {
            var company = NewCompany();
            company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            Assert.Throws<InvalidOperationException>(
                () => company.OpenRestaurant("flagship", "Impostor", LocationType.BrickAndMortar));
        }

        [Fact]
        public void ParLevels_FlagWhatNeedsReordering_AndByHowMuch()
        {
            var company = NewCompany();
            var flagship = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            flagship.Inventory.SetPar("tomato", parMin: 10m, parMax: 40m);
            flagship.Inventory.Receive("tomato", 30m);

            Assert.False(flagship.Inventory["tomato"].IsBelowPar);
            Assert.Equal(0m, flagship.Inventory["tomato"].SuggestedReorderQuantity);

            // A busy weekend eats into it.
            Assert.True(flagship.Inventory.TryConsume("tomato", 25m));

            Assert.Equal(5m, flagship.Inventory.QuantityOf("tomato"));
            Assert.True(flagship.Inventory["tomato"].IsBelowPar);
            Assert.Equal(35m, flagship.Inventory["tomato"].SuggestedReorderQuantity); // back to the top of the band
            Assert.Contains(flagship.Inventory.BelowPar, s => s.IngredientId == "tomato");
        }

        [Fact]
        public void RunningOutMidService_ReturnsFalseRatherThanThrowing()
        {
            // An 86'd dish is a gameplay event, not an exception.
            var company = NewCompany();
            var flagship = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            flagship.Inventory.Receive("mozzarella", 2m);

            Assert.False(flagship.Inventory.TryConsume("mozzarella", 5m));
            Assert.Equal(2m, flagship.Inventory.QuantityOf("mozzarella")); // nothing consumed on failure
        }

        [Fact]
        public void EachLocationHasItsOwnStock_ButSharesTheCompanySupplierPolicy()
        {
            var company = NewCompany();
            var flagship = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);
            var truck = company.OpenRestaurant("truck", "Acme Truck", LocationType.FoodTruck);

            flagship.Inventory.Receive("tomato", 50m);
            truck.Inventory.Receive("tomato", 5m);

            Assert.Equal(50m, flagship.Inventory.QuantityOf("tomato"));
            Assert.Equal(5m, truck.Inventory.QuantityOf("tomato")); // stock is local

            company.SupplierPolicy.AssignAll("valley-produce");
            Assert.Same(flagship.Company.SupplierPolicy, truck.Company.SupplierPolicy); // policy is shared
        }

        [Fact]
        public void AssigningASupplierThatDoesNotCarryTheIngredient_IsRejectedUpFront()
        {
            var company = NewCompany();

            Assert.Throws<DefinitionNotFoundException>(
                () => company.SupplierPolicy.Assign("unobtainium", "valley-produce"));
        }

        [Fact]
        public void CostingARecipeWithNoSupplierAssigned_FailsLoudlyWithAUsefulMessage()
        {
            var company = NewCompany();
            var flagship = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);
            flagship.Menu.Add("margherita");

            // No AssignAll call — nothing is sourced yet.
            var error = Assert.Throws<InvalidOperationException>(
                () => flagship.Costing.PlateCost("margherita"));

            Assert.Contains("No supplier is assigned", error.Message);
        }

        [Fact]
        public void MenuTracksWhichIngredientsTheRestaurantActuallyDependsOn()
        {
            var company = NewCompany();
            var flagship = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);
            flagship.Menu.Add("house-focaccia");

            Assert.Equal(new[] { "basil", "flour" }, flagship.Menu.RequiredIngredientIds.OrderBy(id => id).ToArray());
        }

        [Fact]
        public void PuttingANonexistentDishOnTheMenu_IsRejected()
        {
            var company = NewCompany();
            var flagship = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            Assert.Throws<DefinitionNotFoundException>(() => flagship.Menu.Add("unicorn-steak"));
        }
    }
}
