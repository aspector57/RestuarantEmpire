using System;
using System.Collections.Generic;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Definitions;
using RestaurantEmpire.Core.Model;
using Xunit;
using Xunit.Abstractions;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// THE QUESTION WE HAVE NEVER ASKED: are there several ways to run a restaurant here, and
    /// does the right one depend on where you are?
    ///
    /// `Sweep` asks "is this configuration profitable?" and has answered 100/100 for months
    /// while Aaron kept finding the game too easy — because profitability is not the same
    /// question as strategy. A game with one dominant strategy can be profitable everywhere
    /// and still have nothing to decide.
    ///
    /// Borrowed from the parallel implementation (`HSpector1/Restaurant`), whose harness runs
    /// 9 strategies x 3 scenarios x 200 seeds and reports "distinct winners across scenarios:
    /// 2/3, no single dominant strategy". That is a real gate on whether something is a game,
    /// and we did not have one.
    ///
    /// Reports rather than asserts: this is an instrument, and turning it into a pass/fail is
    /// how you end up tuning the game to satisfy a test.
    /// </summary>
    public class StrategyDiversity
    {
        private readonly ITestOutputHelper _out;
        public StrategyDiversity(ITestOutputHelper o) { _out = o; }

        private sealed class Strategy
        {
            public string Name = "";
            public string[] Menu = Array.Empty<string>();
            public string Supplier = "valley-produce";
            public decimal PriceMultiplier = 1m;
            public int Units, Seats, Cooks, Servers;
            public decimal CookSkill = 0.5m;
        }

        private static Strategy[] Strategies()
        {
            return new[]
            {
                new Strategy { Name = "Cheap and cheerful", Supplier = "budget-wholesale",
                    Menu = new[] { "margherita", "house-focaccia", "caprese-salad" },
                    PriceMultiplier = 0.9m, Units = 3, Seats = 44, Cooks = 4, Servers = 3, CookSkill = 0.35m },

                new Strategy { Name = "Neighbourhood standard", Supplier = "valley-produce",
                    Menu = new[] { "margherita", "house-focaccia", "caprese-salad", "sea-bass" },
                    PriceMultiplier = 1.1m, Units = 3, Seats = 36, Cooks = 4, Servers = 3, CookSkill = 0.5m },

                new Strategy { Name = "Fine dining", Supplier = "premium-harvest",
                    Menu = new[] { "truffle-risotto", "sea-bass", "caprese-salad" },
                    PriceMultiplier = 1.35m, Units = 3, Seats = 24, Cooks = 5, Servers = 3, CookSkill = 0.85m },

                new Strategy { Name = "High volume", Supplier = "budget-wholesale",
                    Menu = new[] { "margherita", "house-focaccia" },
                    PriceMultiplier = 1m, Units = 5, Seats = 60, Cooks = 7, Servers = 5, CookSkill = 0.45m },

                new Strategy { Name = "Coffee and counter", Supplier = "valley-produce",
                    Menu = new[] { "flat-white", "house-focaccia", "eggs-benedict" },
                    PriceMultiplier = 1.15m, Units = 2, Seats = 28, Cooks = 3, Servers = 2, CookSkill = 0.5m },

                new Strategy { Name = "Broad menu", Supplier = "valley-produce",
                    Menu = new[] { "margherita", "house-focaccia", "caprese-salad", "sea-bass", "truffle-risotto", "flat-white" },
                    PriceMultiplier = 1.1m, Units = 4, Seats = 40, Cooks = 5, Servers = 3, CookSkill = 0.55m },
            };
        }

        private static (Neighborhood site, ServiceWindow[] hours, string label)[] Markets()
        {
            return new[]
            {
                (Neighborhood.CityCenter(), new[] { new ServiceWindow("Lunch", 12, 15), new ServiceWindow("Dinner", 18, 23) }, "city"),
                (Neighborhood.BusinessDistrict(), new[] { new ServiceWindow("Breakfast", 7, 10), new ServiceWindow("Lunch", 12, 15) }, "business"),
                (Neighborhood.NightlifeQuarter(), new[] { new ServiceWindow("Dinner", 18, 23), new ServiceWindow("Late", 23, 2) }, "nightlife"),
                (Neighborhood.SuburbanHighStreet(), new[] { new ServiceWindow("Dinner", 18, 23) }, "suburban"),
            };
        }

        [Fact(Skip = "Measuring instrument. Remove this Skip to run.")]
        public void DoDifferentMarketsWantDifferentRestaurants()
        {
            var strategies = Strategies();
            var markets = Markets();
            const int seeds = 5;

            _out.WriteLine($"{strategies.Length} strategies x {markets.Length} markets x {seeds} seeds, three months each.");
            _out.WriteLine("Net is monthly profit after rent, averaged across seeds.");
            _out.WriteLine("");

            var header = "strategy".PadRight(24);
            foreach (var m in markets) header += m.label.PadLeft(12);
            _out.WriteLine(header);
            _out.WriteLine(new string('-', header.Length));

            var netByMarket = new Dictionary<string, Dictionary<string, decimal>>();
            foreach (var m in markets) netByMarket[m.label] = new Dictionary<string, decimal>();

            foreach (var strategy in strategies)
            {
                var row = strategy.Name.PadRight(24);
                foreach (var market in markets)
                {
                    var total = 0m;
                    for (var seed = 0; seed < seeds; seed++)
                        total += MonthlyNet(strategy, market.site, market.hours, 4242 + seed * 977);

                    var mean = total / seeds;
                    netByMarket[market.label][strategy.Name] = mean;
                    row += mean.ToString("N0").PadLeft(12);
                }
                _out.WriteLine(row);
            }

            _out.WriteLine("");

            var winners = new List<string>();
            foreach (var market in markets)
            {
                var best = netByMarket[market.label].OrderByDescending(p => p.Value).First();
                var viable = netByMarket[market.label].Count(p => p.Value > 0m);
                winners.Add(best.Key);
                _out.WriteLine($"  {market.label,-11} winner: {best.Key,-24} viable: {viable}/{strategies.Length}");
            }

            var distinct = winners.Distinct().Count();
            _out.WriteLine("");
            _out.WriteLine($"  distinct winners across markets: {distinct}/{markets.Length}");
            _out.WriteLine(distinct <= 1
                ? "  ONE STRATEGY DOMINATES EVERYWHERE — there is no decision here."
                : "  different markets want different restaurants.");

            Assert.True(distinct >= 1);
        }

        private static decimal MonthlyNet(Strategy strategy, Neighborhood site, ServiceWindow[] hours, long seed)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("co", "Co", definitions, 400000m);
            var r = company.OpenRestaurant("r", "R", LocationType.BrickAndMortar);

            r.Location = site;
            r.FloorArea = site.MaxFloorArea;
            r.Menu.Add(strategy.Menu);
            company.SupplierPolicy.AssignAll(strategy.Supplier);
            r.Reputation.Restore(Reputation.Neutral, Reputation.MealsToBecomeKnown);

            foreach (var id in r.Menu.RecipeIds) company.Pricing.AdjustPrice(id, strategy.PriceMultiplier);

            r.ServiceWindows.Clear();
            foreach (var w in hours) r.ServiceWindows.Add(w);

            foreach (var stationId in r.Menu.Recipes.Select(x => x.StationId).Distinct())
            {
                var model = definitions.EquipmentFor(stationId).FirstOrDefault();
                if (model != null && r.HasRoomFor(model.Footprint * strategy.Units))
                    r.BuyEquipment(model, strategy.Units);
            }

            if (r.HasRoomFor(strategy.Seats * 15m)) r.BuyTables("t", "Tables", strategy.Seats * 120m, strategy.Seats);

            for (var i = 0; i < strategy.Cooks; i++)
                r.Payroll.Hire(new Employee("c" + i, "Cook", StaffRole.Cook, 12m + strategy.CookSkill * 16m, strategy.CookSkill));
            for (var i = 0; i < strategy.Servers; i++)
                r.Payroll.Hire(new Employee("s" + i, "Server", StaffRole.Server, 12m, 0.5m));

            var used = new HashSet<string>();
            foreach (var recipe in r.Menu.Recipes)
                foreach (var line in recipe.Ingredients) used.Add(line.IngredientId);

            foreach (var id in used) { r.Inventory.SetPar(id, 20m, 600m); r.Inventory.Receive(id, 40m); }

            var clock = new GameClock();
            var runner = new SimulationRunner(r, clock, seed, InterruptPolicy.None());

            runner.AdvanceDays(90);
            var trading = runner.Snapshot();

            // Three months of trading, reported as a monthly figure after rent.
            var net = trading.Revenue - trading.FoodCost - trading.LaborCost - (site.MonthlyRent * 3m);
            return net / 3m;
        }
    }
}
