using System;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Guests order what they want at the hour they turn up.
    ///
    /// This is the answer to "why isn't every restaurant open 24/7". You may absolutely
    /// serve the dinner menu at 8am; nobody will order it, so you pay a morning's labour to
    /// watch people read the menu and leave. Wanting the breakfast trade means having
    /// breakfast dishes — which means the equipment they need.
    ///
    /// One optional field on a recipe, and no new system. Deliberately kept that cheap.
    /// </summary>
    public class DaypartMenuTests
    {
        private static Restaurant Build(out Company company, params string[] menu)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme", "Acme Restaurant Group", definitions, 200000m);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Menu.Add(menu);
            company.SupplierPolicy.AssignAll("valley-produce");
            restaurant.Location = Neighbourhood.CityCentre();

            foreach (var stationId in restaurant.Menu.Recipes.Select(r => r.StationId).Distinct())
                restaurant.Kitchen.Install(stationId, stationId, 6);

            foreach (var id in definitions.IngredientIds) restaurant.Inventory.Receive(id, 1000000m);

            return restaurant;
        }

        private static ServiceResult RunBreakfast(Restaurant restaurant)
        {
            restaurant.ServiceWindows.Clear();
            restaurant.ServiceWindows.Add(new ServiceWindow("Breakfast", 7, 10));

            var clock = new GameClock();
            clock.AdvanceHours(7);

            var runner = new SimulationRunner(restaurant, clock, 4242, InterruptPolicy.None());
            runner.AdvanceHours(4);

            return runner.Snapshot();
        }

        [Fact]
        public void TheHourDecidesTheDaypart_NotWhatYouCalledTheService()
        {
            Assert.Equal(Daypart.Breakfast, Dayparts.At(8));
            Assert.Equal(Daypart.Lunch, Dayparts.At(13));
            Assert.Equal(Daypart.Dinner, Dayparts.At(20));
            Assert.Equal(Daypart.LateNight, Dayparts.At(1));

            // A window named "Dinner" running at 8am is still breakfast to the people walking in.
            Assert.Equal(Daypart.Breakfast, Dayparts.At(new DateTime(2026, 3, 2, 8, 30, 0)));
        }

        [Fact]
        public void AnUntaggedDishSellsAtAnyHour()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var focaccia = definitions.GetRecipe("house-focaccia");

            Assert.Empty(focaccia.Dayparts);
            Assert.True(focaccia.SuitsDaypart(Daypart.Breakfast));
            Assert.True(focaccia.SuitsDaypart(Daypart.LateNight));
        }

        [Fact]
        public void ATaggedDishIsOnlyWantedWhenItsTimeComes()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var risotto = definitions.GetRecipe("truffle-risotto");

            Assert.True(risotto.SuitsDaypart(Daypart.Dinner));
            Assert.False(risotto.SuitsDaypart(Daypart.Breakfast));
        }

        [Fact]
        public void OpeningBreakfastWithTheDinnerMenu_MeansPeopleLeaveWithoutOrdering()
        {
            // The whole point, in one test. Truffle risotto and margherita at 8am.
            var restaurant = Build(out _, "truffle-risotto", "margherita", "caprese-salad");

            var morning = RunBreakfast(restaurant);

            Assert.True(morning.PartiesArrived > 0);          // the trade was there
            Assert.Equal(0, morning.CoversServed);            // and you sold none of it
            Assert.Equal(0m, morning.Revenue);
            Assert.True(morning.PartiesLostToMenu > 0);
            Assert.Contains(morning.Diagnostics, d => d.Contains("nothing on the menu suits breakfast"));
        }

        [Fact]
        public void AddingARelevantBreakfastMenu_TurnsTheSameMorningIntoTrade()
        {
            var wrongMenu = Build(out _, "truffle-risotto", "margherita", "caprese-salad");
            var rightMenu = Build(out _, "truffle-risotto", "margherita", "flat-white", "eggs-benedict");

            var lost = RunBreakfast(wrongMenu);
            var earned = RunBreakfast(rightMenu);

            Assert.Equal(0m, lost.Revenue);
            Assert.True(earned.Revenue > 0m);
            Assert.True(earned.CoversServed > 0);
            Assert.Equal(0, earned.PartiesLostToMenu);

            // And only the breakfast dishes actually sold — nobody ordered risotto at 8am.
            Assert.DoesNotContain("truffle-risotto", earned.UnitsSoldByRecipeId.Keys);
            Assert.Contains("flat-white", earned.UnitsSoldByRecipeId.Keys);
        }

        [Fact]
        public void ABreakfastMenuIsUselessWithoutTheEquipmentItNeeds()
        {
            // Coffee needs a coffee station. This is where the espresso machine stops being
            // a metaphor and starts being capital expenditure.
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("acme", "Acme", definitions, 200000m);
            var cafe = company.OpenRestaurant("cafe", "The Cafe", LocationType.BrickAndMortar);

            cafe.Menu.Add("flat-white");
            company.SupplierPolicy.AssignAll("valley-produce");
            cafe.Location = Neighbourhood.CityCentre();
            foreach (var id in definitions.IngredientIds) cafe.Inventory.Receive(id, 100000m);

            var withoutMachine = RunBreakfast(cafe);
            Assert.Equal(0, withoutMachine.CoversServed);
            Assert.True(withoutMachine.EightySixed > 0);
            Assert.Contains(withoutMachine.Diagnostics, d => d.Contains("coffee") && d.Contains("station"));

            // Buy the machine and the same morning works.
            var cashBefore = company.Economy.CashOnHand;
            cafe.BuyStation("coffee", "Espresso Machine", 6500m, 2);
            Assert.Equal(cashBefore - 6500m, company.Economy.CashOnHand);

            Assert.True(RunBreakfast(cafe).CoversServed > 0);
        }

        [Fact]
        public void TheSameMenuEarnsAtDinnerAndEarnsNothingAtBreakfast()
        {
            // Identical restaurant, identical menu, identical location. Only the hour differs.
            var restaurant = Build(out _, "truffle-risotto", "margherita", "caprese-salad");

            restaurant.ServiceWindows.Clear();
            restaurant.ServiceWindows.Add(new ServiceWindow("Dinner", 19, 22));

            var clock = new GameClock();
            clock.AdvanceHours(19);
            var evening = new SimulationRunner(restaurant, clock, 4242, InterruptPolicy.None());
            evening.AdvanceHours(4);

            Assert.True(evening.Snapshot().Revenue > 0m);
            Assert.Equal(0, evening.Snapshot().PartiesLostToMenu);

            var morning = RunBreakfast(Build(out _, "truffle-risotto", "margherita", "caprese-salad"));
            Assert.Equal(0m, morning.Revenue);
        }

        [Fact]
        public void DaypartsAreDataDriven_LikeEverythingElseAboutARecipe()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            Assert.Equal(new[] { Daypart.Breakfast }, definitions.GetRecipe("eggs-benedict").Dayparts);
            Assert.Equal(new[] { Daypart.Breakfast, Daypart.Lunch }, definitions.GetRecipe("flat-white").Dayparts);
            Assert.Empty(definitions.LoadWarnings);
        }
    }
}
