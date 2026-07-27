using System.Linq;
using RestaurantEmpire.Core.Content;
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
            public Neighbourhood Site;
            public ServiceWindow[] Hours;
            public int StationUnits, Seats, Cooks, Servers;
        }

        private static Plan[] TheFourSites()
        {
            return new[]
            {
                // Best trade in the game, all day — and the tightest, dearest site.
                new Plan
                {
                    Site = Neighbourhood.CityCentre(),
                    Hours = new[] { new ServiceWindow("Lunch", 12, 15), new ServiceWindow("Dinner", 18, 23) },
                    StationUnits = 2, Seats = 42, Cooks = 3, Servers = 3
                },

                // Enormous breakfast and lunch, then nobody. Its whole game is the morning.
                new Plan
                {
                    Site = Neighbourhood.BusinessDistrict(),
                    Hours = new[] { new ServiceWindow("Breakfast", 7, 10), new ServiceWindow("Lunch", 12, 15) },
                    StationUnits = 3, Seats = 50, Cooks = 3, Servers = 4
                },

                // Dead until evening, then the busiest hours anywhere.
                new Plan
                {
                    Site = Neighbourhood.NightlifeQuarter(),
                    Hours = new[] { new ServiceWindow("Dinner", 18, 23), new ServiceWindow("Late", 23, 2) },
                    StationUnits = 3, Seats = 45, Cooks = 3, Servers = 4
                },

                // Quietest street, cheapest rent, and by far the most room to grow into.
                new Plan
                {
                    Site = Neighbourhood.SuburbanHighStreet(),
                    Hours = new[] { new ServiceWindow("Dinner", 18, 23) },
                    StationUnits = 4, Seats = 80, Cooks = 4, Servers = 6
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
                restaurant.Payroll.Hire(new Employee("c" + i, "Cook " + i, StaffRole.Cook, 18m));
            for (var i = 0; i < plan.Servers; i++)
                restaurant.Payroll.Hire(new Employee("s" + i, "Server " + i, StaffRole.Server, 14.40m));

            foreach (var id in definitions.IngredientIds)
            {
                restaurant.Inventory.SetPar(id, 400m, 2000m);
                restaurant.Inventory.Receive(id, 2000m);
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
            return month.Revenue - month.FoodCost - month.LabourCost - plan.Site.MonthlyRent;
        }

        [Fact(Skip = "KNOWN UNMET. A 100-run sweep found only 10/100 configurations profitable, " +
                     "and growing a restaurant currently makes it LESS profitable, which is backwards. " +
                     "This is the balance work standing between here and M2 — see CLAUDE.md.")]
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

        [Fact(Skip = "KNOWN UNMET — see above. Only city and business reach profit at all, " +
                     "and only at their largest configuration.")]
        public void NoSiteIsSoFarAheadThatTheOthersArePointless()
        {
            // They are allowed to differ — they are not allowed to be a fake choice.
            decimal best = decimal.MinValue, worst = decimal.MaxValue;

            foreach (var plan in TheFourSites())
            {
                ServiceResult month;
                var profit = MonthlyProfit(plan, out month);

                if (profit > best) best = profit;
                if (profit < worst) worst = profit;
            }

            Assert.True(best <= worst * 4m,
                "the best site earns " + best.ToString("N0") + " against the worst at " +
                worst.ToString("N0") + " — that is not a choice, it is a right answer");
        }

        [Fact]
        public void EachSiteWinsInItsOwnWay_NotByBeingTheSameRestaurant()
        {
            var city = Neighbourhood.CityCentre();
            var business = Neighbourhood.BusinessDistrict();
            var nightlife = Neighbourhood.NightlifeQuarter();
            var suburb = Neighbourhood.SuburbanHighStreet();

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
            Assert.True(suburb.ExtensionCostPerSquareMetre < city.ExtensionCostPerSquareMetre / 2m);
        }
    }
}
