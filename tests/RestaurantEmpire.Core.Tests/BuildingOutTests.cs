using System;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Growing the building — the crude first form of build mode.
    ///
    /// Expansion is capital-gated and, more interestingly, LOCATION-gated. You cannot knock
    /// through into the building next door in a city centre, so a wonderful pitch can be one
    /// you outgrow and cannot fix. That makes choosing a location a bet on your ceiling as
    /// much as on your footfall, and the two are deliberately in tension: the best traffic
    /// comes with the least room to grow.
    /// </summary>
    public class BuildingOutTests
    {
        private static Restaurant Build(out Company company, Neighbourhood where, decimal floorArea = 90m)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme", "Acme Restaurant Group", definitions, 500000m);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Location = where;
            restaurant.FloorArea = floorArea;
            restaurant.Menu.Add("margherita");
            company.SupplierPolicy.AssignAll("valley-produce");

            return restaurant;
        }

        [Fact]
        public void TheBestTrafficComesWithTheLeastRoomToGrow()
        {
            // The tension that makes location a real choice rather than "pick the busiest".
            var city = Neighbourhood.CityCentre();
            var suburb = Neighbourhood.SuburbanHighStreet();

            Assert.True(city.TrafficAtHour(13) > suburb.TrafficAtHour(13));   // better trade
            Assert.True(city.MaxFloorArea < suburb.MaxFloorArea);             // less room
            Assert.True(city.ExtensionCostPerSquareMetre > suburb.ExtensionCostPerSquareMetre * 2m); // dearer land
        }

        [Fact]
        public void ASuburbanSiteHasRoomBehindIt_AndLandIsCheap()
        {
            var restaurant = Build(out var company, Neighbourhood.SuburbanHighStreet(), floorArea: 90m);
            var cashBefore = company.Economy.CashOnHand;

            Assert.Equal(190m, restaurant.ExpansionHeadroom);   // 280 cap, at 90

            restaurant.ExtendBuilding(60m);

            Assert.Equal(150m, restaurant.FloorArea);
            Assert.Equal(cashBefore - (60m * 340m), company.Economy.CashOnHand);
            Assert.Contains(company.Economy.Entries, e => e.Description.Contains("Extended into"));
        }

        [Fact]
        public void ACitySiteRunsOutOfBuilding_AndSaysWhyPlainly()
        {
            // Aaron's case exactly: you cannot just knock down the wall.
            var restaurant = Build(out _, Neighbourhood.CityCentre(), floorArea: 90m);

            Assert.Equal(40m, restaurant.ExpansionHeadroom);   // 130 cap, at 90

            restaurant.ExtendBuilding(40m);                    // take everything there is
            Assert.Equal(130m, restaurant.FloorArea);
            Assert.Equal(0m, restaurant.ExpansionHeadroom);

            var blocked = Assert.Throws<InvalidOperationException>(() => restaurant.ExtendBuilding(10m));

            Assert.Contains("City Centre", blocked.Message);
            Assert.Contains("building next door", blocked.Message);
        }

        [Fact]
        public void OutgrowingACitySiteLeavesUpgradingAsTheOnlyMoveLeft()
        {
            // The moment the two systems meet: no room to build, so the only way to add
            // throughput is better equipment in the same space.
            var restaurant = Build(out var company, Neighbourhood.CityCentre(), floorArea: 130m);
            var definitions = company.Definitions;

            restaurant.BuyEquipment(definitions.GetEquipment("oven-secondhand"), 10); // 50.0m2
            restaurant.BuyTables("tables", "Tables", 6800m, 57);                      // 79.8m2

            Assert.True(restaurant.FreeFloorArea < 1m);
            Assert.Equal(0m, restaurant.ExpansionHeadroom);
            Assert.Throws<InvalidOperationException>(() => restaurant.ExtendBuilding(10m));

            // Adding another cheap oven is impossible...
            Assert.Throws<InvalidOperationException>(
                () => restaurant.BuyEquipment(definitions.GetEquipment("oven-secondhand"), 11));

            // ...but replacing eight second-hand ovens with eight hearth ovens fits, and is
            // more than twice the throughput in less space.
            var before = restaurant.Kitchen.Get("oven").SpeedMultiplier;
            restaurant.BuyEquipment(definitions.GetEquipment("oven-hearth"), 10);

            Assert.True(restaurant.Kitchen.Get("oven").SpeedMultiplier > before * 2m);
            Assert.True(restaurant.FreeFloorArea > 14m);   // and it freed up room as well
        }

        [Fact]
        public void AnUnconstrainedSiteCanGrowForever()
        {
            var restaurant = Build(out _, Neighbourhood.Flat(10), floorArea: 50m);

            Assert.Equal(decimal.MaxValue, restaurant.ExpansionHeadroom);
            restaurant.ExtendBuilding(500m);
            Assert.Equal(550m, restaurant.FloorArea);
        }

        [Fact]
        public void ExtendingByNothingIsRejected()
        {
            var restaurant = Build(out _, Neighbourhood.SuburbanHighStreet());
            Assert.Throws<ArgumentOutOfRangeException>(() => restaurant.ExtendBuilding(0m));
        }
    }
}
