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
                    PriceMultiplier = 1.35m, Units = 3, Seats = 34, Cooks = 4, Servers = 3, CookSkill = 0.85m },

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

        /// <summary>
        /// WHY DOES FINE DINING WIN THE SUBURBS AND LOSE THE NIGHTLIFE QUARTER? The nightlife
        /// crowd is influencers and couples, who pull hard toward luxury, refined and seafood —
        /// exactly what that menu is. It should be the strategy's home market and it is not.
        ///
        /// Reports the whole P&L rather than the net, because "it loses" is not a finding. The
        /// question is which line loses it.
        /// </summary>
        [Fact(Skip = "Measuring instrument. Remove this Skip to run.")]
        public void WhyDoesFineDiningLoseItsOwnCrowd()
        {
            var fine = Strategies().First(s => s.Name == "Fine dining");
            var standard = Strategies().First(s => s.Name == "Neighbourhood standard");

            foreach (var strategy in new[] { fine, standard })
            {
                _out.WriteLine(strategy.Name.ToUpperInvariant());
                _out.WriteLine("  " + "market".PadRight(11) + "covers".PadLeft(8) + "revenue".PadLeft(10)
                               + "food".PadLeft(9) + "labor".PadLeft(9) + "rent".PadLeft(9)
                               + "net/mo".PadLeft(10) + "$/cover".PadLeft(9) + "food%".PadLeft(7)
                               + "spoiled".PadLeft(9) + "  lost to");

                foreach (var market in Markets())
                {
                    var line = Detail(strategy, market.site, market.hours, 4242);
                    _out.WriteLine("  " + market.label.PadRight(11)
                        + line.Covers.ToString("N0").PadLeft(8)
                        + line.Revenue.ToString("N0").PadLeft(10)
                        + line.Food.ToString("N0").PadLeft(9)
                        + line.Labor.ToString("N0").PadLeft(9)
                        + (market.site.MonthlyRent * 3m).ToString("N0").PadLeft(9)
                        + line.Net.ToString("N0").PadLeft(10)
                        + line.PerCover.ToString("C0").PadLeft(9)
                        + (line.Revenue <= 0 ? "-" : (line.Food / line.Revenue).ToString("P0")).PadLeft(7)
                        + line.Wasted.ToString("N0").PadLeft(9)
                        + "  menu " + line.LostMenu + " (appeal " + line.Appeal.ToString("P0") + ")");
                }
                _out.WriteLine("");
            }
        }

        /// <summary>
        /// AARON'S BAR: *"you should be able to win with any concept anywhere if you run the
        /// restaurant properly, unless the concept just totally sucks."*
        ///
        /// That makes the fine-dining result a question rather than a finding. The probe runs
        /// it BADLY — it opens a late service with nothing late-appropriate on the card, which
        /// is an operating mistake, not a property of the concept. This asks what happens when
        /// the same concept is run properly in the same market.
        /// </summary>
        [Fact(Skip = "Measuring instrument. Remove this Skip to run.")]
        public void CanFineDiningWinItsOwnMarketIfRunProperly()
        {
            var fine = Strategies().First(s => s.Name == "Fine dining");
            var nightlife = Neighborhood.NightlifeQuarter();

            var dinnerAndLate = new[] { new ServiceWindow("Dinner", 18, 23), new ServiceWindow("Late", 23, 2) };
            var dinnerOnly = new[] { new ServiceWindow("Dinner", 18, 23) };

            _out.WriteLine("Fine dining, nightlife quarter, same menu and staff — only the hours differ.");
            _out.WriteLine("");
            _out.WriteLine("  " + "hours".PadRight(18) + "net/mo".PadLeft(10) + "food%".PadLeft(8)
                           + "spoiled".PadLeft(10) + "lost to menu".PadLeft(14));

            foreach (var (hours, label) in new[] { (dinnerAndLate, "dinner + late"), (dinnerOnly, "dinner only") })
            {
                var d = Detail(fine, nightlife, hours, 4242);
                _out.WriteLine("  " + label.PadRight(18)
                    + d.Net.ToString("N0").PadLeft(10)
                    + (d.Revenue <= 0 ? "-" : (d.Food / d.Revenue).ToString("P0")).PadLeft(8)
                    + d.Wasted.ToString("N0").PadLeft(10)
                    + d.LostMenu.ToString("N0").PadLeft(14));
            }

            _out.WriteLine("");
            _out.WriteLine("And priced properly? Premium ingredients at 1.35x may simply be too cheap.");
            _out.WriteLine("  " + "price".PadRight(18) + "net/mo".PadLeft(10) + "food%".PadLeft(8)
                           + "spoiled".PadLeft(10) + "covers/mo".PadLeft(12));
            foreach (var multiplier in new[] { 1.35m, 1.6m, 1.9m, 2.2m, 2.6m })
            {
                var priced = new Strategy
                {
                    Name = fine.Name, Menu = fine.Menu, Supplier = fine.Supplier,
                    PriceMultiplier = multiplier, Units = fine.Units, Seats = fine.Seats,
                    Cooks = fine.Cooks, Servers = fine.Servers, CookSkill = fine.CookSkill
                };
                var d = Detail(priced, nightlife, dinnerOnly, 4242);
                _out.WriteLine("  " + (multiplier.ToString("0.00") + "x").PadRight(18)
                    + d.Net.ToString("N0").PadLeft(10)
                    + (d.Revenue <= 0 ? "-" : (d.Food / d.Revenue).ToString("P0")).PadLeft(8)
                    + d.Wasted.ToString("N0").PadLeft(10)
                    + d.Covers.ToString("N0").PadLeft(12));
            }

            _out.WriteLine("");
            _out.WriteLine("For comparison, the generalist in the same market on dinner only:");
            var standard = Strategies().First(s => s.Name == "Neighbourhood standard");
            var s2 = Detail(standard, nightlife, dinnerOnly, 4242);
            _out.WriteLine("  " + "standard".PadRight(18) + s2.Net.ToString("N0").PadLeft(10)
                + (s2.Food / s2.Revenue).ToString("P0").PadLeft(8)
                + s2.Wasted.ToString("N0").PadLeft(10)
                + s2.LostMenu.ToString("N0").PadLeft(14));
        }

        private sealed class Line
        {
            public int Covers, LostPrice, LostWait, LostSeats, LostMenu;
            public decimal Wasted, PerCover;
            public decimal Revenue, Food, Labor, Net, Appeal;
        }

        private static Line Detail(Strategy strategy, Neighborhood site, ServiceWindow[] hours, long seed)
        {
            var r = Open(strategy, site, hours, out var company);
            var clock = new GameClock();
            var runner = new SimulationRunner(r, clock, seed, InterruptPolicy.None());
            runner.AdvanceDays(90);
            var s = runner.Snapshot();

            // How much the card appeals to whoever is out during the hours being served.
            var appeal = 0m;
            var counted = 0;
            foreach (var w in hours)
            {
                var likely = ArchetypeProfile.LikelyAt(Dayparts.At(DateTime.Today.AddHours(w.StartHour)), site.Id);
                foreach (var a in likely) { appeal += r.Menu.AppealTo(a); counted++; }
            }

            return new Line
            {
                Covers = s.CoversServed / 3,
                Wasted = s.WastedFoodCost / 3m,
                PerCover = s.CoversServed == 0 ? 0m : s.Revenue / s.CoversServed,
                Revenue = s.Revenue / 3m,
                Food = s.FoodCost / 3m,
                Labor = s.LaborCost / 3m,
                Net = (s.Revenue - s.FoodCost - s.LaborCost) / 3m - site.MonthlyRent,
                Appeal = counted == 0 ? 1m : appeal / counted,
                LostPrice = s.PartiesPutOffByThePrices,
                LostWait = s.PartiesPutOffByTheWait,
                LostSeats = s.PartiesTurnedAway,
                LostMenu = s.PartiesLostToMenu,
            };
        }

        private static decimal MonthlyNet(Strategy strategy, Neighborhood site, ServiceWindow[] hours, long seed)
        {
            var r = Open(strategy, site, hours, out _);
            var clock = new GameClock();
            var runner = new SimulationRunner(r, clock, seed, InterruptPolicy.None());

            runner.AdvanceDays(90);
            var trading = runner.Snapshot();

            // Three months of trading, reported as a monthly figure after rent.
            var net = trading.Revenue - trading.FoodCost - trading.LaborCost - (site.MonthlyRent * 3m);
            return net / 3m;
        }

        /// <summary>Opens the restaurant this strategy describes. Shared so the headline sweep
        /// and the diagnostic cannot drift into measuring two different restaurants.</summary>
        private static Restaurant Open(Strategy strategy, Neighborhood site, ServiceWindow[] hours, out Company company)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            company = new Company("co", "Co", definitions, 400000m);
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

            return r;
        }
    }
}
