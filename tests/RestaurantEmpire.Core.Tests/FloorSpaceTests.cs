using System;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// The kitchen and the dining room share one building.
    ///
    /// This is the answer to "why can't I just buy fifteen ovens?" — you can, and then you
    /// have nowhere to seat anybody. Floor space is not an arbitrary cap; it is the
    /// constraint that turns equipment into a decision, and it is the design's dominant
    /// layout failure mode made mechanical: an impressive dining room fed by an undersized
    /// kitchen, or a magnificent kitchen with six covers in front of it.
    /// </summary>
    public class FloorSpaceTests
    {
        private static Restaurant Build(out Company company, decimal floorArea = 970m)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme", "Acme Restaurant Group", definitions, 500000m);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);
            restaurant.FloorArea = floorArea;

            restaurant.Menu.Add("margherita", "caprese-salad", "truffle-risotto", "house-focaccia");
            company.SupplierPolicy.AssignAll("valley-produce");

            foreach (var id in definitions.IngredientIds) restaurant.Inventory.Receive(id, 100000m);

            return restaurant;
        }

        [Fact]
        public void TheCatalogueLoadsFromData_LikeEverythingElse()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            Assert.True(definitions.EquipmentCount > 0);
            Assert.Empty(definitions.LoadWarnings);

            var ovens = definitions.EquipmentFor("oven").ToList();
            Assert.Equal(3, ovens.Count);
            Assert.True(ovens[0].Cost < ovens[2].Cost);   // cheapest first
        }

        [Fact]
        public void PremiumEquipmentIsFasterAndSmaller_SoUpgradingBeatsAccumulating()
        {
            // The whole reason a shop with tiers is more interesting than a quantity slider.
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var ovens = definitions.EquipmentFor("oven").ToList();

            var cheap = ovens.First();
            var best = ovens.Last();

            Assert.True(best.SpeedMultiplier > cheap.SpeedMultiplier);
            Assert.True(best.Footprint < cheap.Footprint);
            Assert.True(best.SpeedPerSquareFoot > cheap.SpeedPerSquareFoot * 2m);
        }

        [Fact]
        public void FifteenOvensIsNotAStrategy_ItIsADiningRoomYouNoLongerHave()
        {
            var restaurant = Build(out var company, floorArea: 970m);
            var oven = company.Definitions.GetEquipment("oven-commercial");

            // 4 sq ft each, so fifteen is 60 sq ft of a 90 sq ft unit.
            restaurant.BuyEquipment(oven, 15);
            Assert.Equal(645m, restaurant.Kitchen.Footprint);
            Assert.Equal(325m, restaurant.FreeFloorArea);

            // 30 sq ft left seats about 21 covers, and that is before a single other station.
            restaurant.BuyTables("tables", "Tables", 2400m, 21);
            Assert.True(restaurant.FreeFloorArea < 12m);

            var more = Assert.Throws<InvalidOperationException>(
                () => restaurant.BuyTables("more", "More tables", 1200m, 10));

            Assert.Contains("No room", more.Message);
            Assert.Contains("competing for the same floor", more.Message);
        }

        [Fact]
        public void UpgradingToABetterModel_BuysThroughputWithoutBuyingSpace()
        {
            // The move that matters once the building is full.
            var restaurant = Build(out var company, floorArea: 970m);
            var commercial = company.Definitions.GetEquipment("oven-commercial");
            var hearth = company.Definitions.GetEquipment("oven-hearth");

            restaurant.BuyEquipment(commercial, 4);
            var beforeSpace = restaurant.Kitchen.Footprint;
            var beforeSpeed = restaurant.Kitchen.Get("oven").SpeedMultiplier;

            restaurant.BuyEquipment(hearth, 4);   // replaces, does not stack

            Assert.Equal(4, restaurant.Kitchen.Get("oven").ConcurrentCapacity);
            Assert.True(restaurant.Kitchen.Get("oven").SpeedMultiplier > beforeSpeed);
            Assert.True(restaurant.Kitchen.Footprint < beforeSpace);   // faster AND smaller
        }

        [Fact]
        public void BuyingMoreOfWhatYouHaveAddsUnits_AndIsBilledPerUnit()
        {
            var restaurant = Build(out var company);
            var oven = company.Definitions.GetEquipment("oven-commercial");

            restaurant.BuyEquipment(oven, 2);
            var cashAfterFirst = company.Economy.CashOnHand;

            restaurant.BuyEquipment(oven, 1);

            Assert.Equal(3, restaurant.Kitchen.Get("oven").ConcurrentCapacity);
            Assert.Equal(cashAfterFirst - oven.Cost, company.Economy.CashOnHand);
        }

        [Fact]
        public void RunningOutOfFloorIsReportedPlainly_NotSilentlyAllowed()
        {
            var restaurant = Build(out var company, floorArea: 215m);
            var oven = company.Definitions.GetEquipment("oven-commercial");

            var error = Assert.Throws<InvalidOperationException>(() => restaurant.BuyEquipment(oven, 10));

            Assert.Contains("No room", error.Message);
            Assert.Contains("215.0 sq ft", error.Message);
            Assert.Contains("bigger building", error.Message);

            // And nothing was bought or billed.
            Assert.Equal(0, restaurant.Kitchen.StationCount);
            Assert.Equal(500000m, company.Economy.CashOnHand);
        }

        [Fact]
        public void AnUnmeasuredBuildingConstrainsNothing()
        {
            // Ghost kitchens, food trucks and bare test fixtures should not need a lease.
            var restaurant = Build(out var company, floorArea: 0m);

            restaurant.BuyEquipment(company.Definitions.GetEquipment("oven-commercial"), 50);
            restaurant.BuyTables("tables", "Tables", 100m, 500);

            Assert.Equal(50, restaurant.Kitchen.Get("oven").ConcurrentCapacity);
            Assert.Equal(500, restaurant.SeatingCapacity);
        }

        [Fact]
        public void ARealisticUnitFitsARealisticRestaurant()
        {
            // Sanity check on the numbers: a 900 sq ft unit should comfortably hold a working
            // kitchen and a proper dining room, and nothing like fifteen ovens.
            var restaurant = Build(out var company, floorArea: 900m);
            var definitions = company.Definitions;

            restaurant.BuyEquipment(definitions.GetEquipment("oven-commercial"), 3);      // 129
            restaurant.BuyEquipment(definitions.GetEquipment("saute-commercial"), 2);     //  76
            restaurant.BuyEquipment(definitions.GetEquipment("gm-refrigerated"), 2);      //  56
            restaurant.BuyTables("tables", "Tables and chairs", 4800m, 40);               // 600

            Assert.Equal(40, restaurant.SeatingCapacity);
            Assert.True(restaurant.UsedFloorArea <= 900m);
            Assert.True(restaurant.FreeFloorArea < 45m, "a working restaurant should nearly fill its unit");
        }
    }
}
