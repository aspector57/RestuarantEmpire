using System.Linq;
using RestaurantEmpire.Core.Content;
using System.Collections.Generic;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// BALANCE INVARIANT (Aaron): every starting location must be winnable. They should
    /// look different and have their own problems — the city charges more rent, the suburbs
    /// start quieter — but none of them may be a trap.
    ///
    /// This is the kind of property that rots silently during balance work, so it is pinned
    /// here rather than left to memory. Each site is built out the way it wants to be played
    /// and has to clear a month in profit.
    /// </summary>
    public class EverySiteIsWinnableTests
    {
        private sealed class Plan
        {
            public Neighborhood Site = null!;
            public ServiceWindow[] Hours = null!;
            public int StationUnits, Seats, Cooks, Servers;
        }

        private static Plan[] TheFourSites()
        {
            return new[]
            {
                // Best trade in the game, all day — and the tightest, dearest site.
                new Plan
                {
                    Site = Neighborhood.CityCenter(),
                    Hours = new[] { new ServiceWindow("Lunch", 12, 15), new ServiceWindow("Dinner", 18, 23) },
                    StationUnits = 3, Seats = 42, Cooks = 3, Servers = 3
                },

                // Enormous breakfast and lunch, then nobody. Its whole game is the morning.
                new Plan
                {
                    Site = Neighborhood.BusinessDistrict(),
                    Hours = new[] { new ServiceWindow("Breakfast", 7, 10), new ServiceWindow("Lunch", 12, 15) },
                    StationUnits = 4, Seats = 58, Cooks = 4, Servers = 5
                },

                // Dead until evening, then the busiest hours anywhere — so it wants a big
                // kitchen and a smaller room, which is the opposite build to the suburbs.
                new Plan
                {
                    Site = Neighborhood.NightlifeQuarter(),
                    Hours = new[] { new ServiceWindow("Dinner", 18, 23), new ServiceWindow("Late", 23, 2) },
                    StationUnits = 5, Seats = 40, Cooks = 5, Servers = 3
                },

                // Quietest street, cheapest rent, and by far the most room to grow into.
                new Plan
                {
                    Site = Neighborhood.SuburbanHighStreet(),
                    Hours = new[] { new ServiceWindow("Dinner", 18, 23) },
                    StationUnits = 5, Seats = 80, Cooks = 5, Servers = 6
                }
            };
        }

        private static decimal MonthlyProfit(Plan plan, out ServiceResult month)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("acme", "Acme", definitions, 400000m);
            var restaurant = company.OpenRestaurant("site", plan.Site.Name, LocationType.BrickAndMortar);

            restaurant.Location = plan.Site;
            restaurant.FloorArea = plan.Site.MaxFloorArea;   // built out to what the site allows

            // This asks whether a SITE can pay, not whether a newly-opened restaurant can be
            // found, so start it established rather than unknown.
            restaurant.Reputation.Restore(Reputation.Neutral, Reputation.MealsToBecomeKnown);

            foreach (var recipe in definitions.Recipes) restaurant.Menu.Add(recipe.Id);
            company.SupplierPolicy.AssignAll("valley-produce");

            restaurant.ServiceWindows.Clear();
            foreach (var window in plan.Hours) restaurant.ServiceWindows.Add(window);

            foreach (var stationId in restaurant.Menu.Recipes.Select(r => r.StationId).Distinct())
            {
                var model = definitions.EquipmentFor(stationId).FirstOrDefault();
                if (model != null) restaurant.BuyEquipment(model, plan.StationUnits);
            }

            restaurant.BuyTables("tables", "Tables", plan.Seats * 120m, plan.Seats);

            for (var i = 0; i < plan.Cooks; i++)
                restaurant.Payroll.Hire(new Employee("c" + i, "Cook " + i, StaffRole.Cook, 16m));
            for (var i = 0; i < plan.Servers; i++)
                restaurant.Payroll.Hire(new Employee("s" + i, "Server " + i, StaffRole.Server, 12m));

            // Stock only what the menu cooks, and open with a small delivery rather than a
            // warehouse. Buying two thousand units of everything was harmless while nothing
            // could spoil; now it is the over-ordering the mechanic exists to punish, and it
            // was costing this fixture more than it earned. The reorder cap decides the
            // quantity from here — order to need, not to a shelf level.
            var onTheMenu = new HashSet<string>();
            foreach (var recipe in restaurant.Menu.Recipes)
                foreach (var line in recipe.Ingredients) onTheMenu.Add(line.IngredientId);

            foreach (var id in onTheMenu)
            {
                restaurant.Inventory.SetPar(id, 20m, 600m);
                restaurant.Inventory.Receive(id, 40m);
            }

            var runner = new SimulationRunner(restaurant, new GameClock(), 4242, InterruptPolicy.None());

            for (var day = 0; day < 30; day++)
            {
                runner.AdvanceDays(1);

                foreach (var stock in restaurant.Inventory.Items.ToList())
                {
                    if (stock.IsBelowPar) restaurant.Inventory.Receive(stock.IngredientId, stock.SuggestedReorderQuantity);
                }
            }

            month = runner.Snapshot();

            // Revenue, less food and wages the simulation generated, less a month's rent.
            return month.Revenue - month.FoodCost - month.LaborCost - plan.Site.MonthlyRent;
        }

        [Fact]
        public void EveryStartingLocationCanBeMadeToPay()
        {
            foreach (var plan in TheFourSites())
            {
                ServiceResult month;
                var profit = MonthlyProfit(plan, out month);

                Assert.True(profit > 0m,
                    plan.Site.Name + " could not be made profitable: " + profit.ToString("N0") +
                    " on revenue of " + month.Revenue.ToString("N0"));

                Assert.True(month.CoversServed > 500,
                    plan.Site.Name + " only served " + month.CoversServed + " covers in a month");
            }
        }

        [Fact]
        public void NoSiteIsAFakeChoice()
        {
            // Sites are ALLOWED to differ, and should — a nightlife pitch working eight
            // hours of the busiest trade in the game ought to out-earn a suburban dinner
            // house. What is not allowed is a site that cannot support a real business.
            //
            // (An earlier version of this asserted a ratio between best and worst. That was
            // a made-up bound, and tuning the plans until it passed would have been testing
            // my own arithmetic rather than the game. The property that actually matters is
            // that every site clears a living.)
            foreach (var plan in TheFourSites())
            {
                ServiceResult month;
                var profit = MonthlyProfit(plan, out month);

                Assert.True(profit > 5000m,
                    plan.Site.Name + " only clears " + profit.ToString("N0") +
                    " a month at its best build — not enough to be worth choosing");
            }
        }

        [Fact]
        public void EachSiteWinsInItsOwnWay_NotByBeingTheSameRestaurant()
        {
            var city = Neighborhood.CityCenter();
            var business = Neighborhood.BusinessDistrict();
            var nightlife = Neighborhood.NightlifeQuarter();
            var suburb = Neighborhood.SuburbanHighStreet();

            // The city charges most and gives least room.
            Assert.True(city.MonthlyRent > suburb.MonthlyRent * 2m);
            Assert.True(city.MaxFloorArea < suburb.MaxFloorArea / 2m);

            // The business district is a morning, and nothing else.
            Assert.True(business.TrafficAtHour(8) > 15);
            Assert.True(business.IsDeadAtHour(21));

            // The nightlife quarter is the exact inverse.
            Assert.True(nightlife.IsDeadAtHour(8));
            Assert.True(nightlife.TrafficAtHour(22) > 20);

            // And the suburbs are quiet but cheap and roomy — the slow, safe start.
            Assert.True(suburb.LeasePremium < city.LeasePremium / 3m);
            Assert.True(suburb.ExtensionCostPerSquareFoot < city.ExtensionCostPerSquareFoot / 2m);
        }
    }
}
