using System;
using System.Collections.Generic;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;
using Xunit.Abstractions;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Temporary balance sweep. Not a test of anything — a measuring instrument.
    /// </summary>
    public class Sweep
    {
        private readonly ITestOutputHelper _out;
        public Sweep(ITestOutputHelper o) { _out = o; }

        private sealed class Run
        {
            public string Site = "", Size = "";
            public long Seed;
            public decimal Net, PrimeRatio, Revenue;
            public int Covers, Walkouts, Interrupts, TurnedAway, LostToMenu, PutOffByWait, PutOffByPrice;
        }

        private static ServiceWindow[] HoursFor(string site)
        {
            switch (site)
            {
                case "city": return new[] { new ServiceWindow("Lunch", 12, 15), new ServiceWindow("Dinner", 18, 23) };
                case "business": return new[] { new ServiceWindow("Breakfast", 7, 10), new ServiceWindow("Lunch", 12, 15) };
                case "nightlife": return new[] { new ServiceWindow("Dinner", 18, 23), new ServiceWindow("Late", 23, 2) };
                default: return new[] { new ServiceWindow("Dinner", 18, 23) };
            }
        }

        private static Neighborhood SiteFor(string key)
        {
            switch (key)
            {
                case "city": return Neighborhood.CityCenter();
                case "business": return Neighborhood.BusinessDistrict();
                case "nightlife": return Neighborhood.NightlifeQuarter();
                default: return Neighborhood.SuburbanHighStreet();
            }
        }

        private static Run Simulate(string siteKey, string size, int units, int seats, int cooks, int servers, long seed)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var site = SiteFor(siteKey);
            var company = new Company("c", "C", definitions, 400000m);
            var r = company.OpenRestaurant("s", site.Name, LocationType.BrickAndMortar);

            r.Location = site;
            r.FloorArea = site.MaxFloorArea;

            foreach (var recipe in definitions.Recipes) r.Menu.Add(recipe.Id);
            company.SupplierPolicy.AssignAll("valley-produce");

            r.ServiceWindows.Clear();
            foreach (var w in HoursFor(siteKey)) r.ServiceWindows.Add(w);

            foreach (var stationId in r.Menu.Recipes.Select(x => x.StationId).Distinct())
            {
                var model = definitions.EquipmentFor(stationId).FirstOrDefault();
                if (model != null)
                {
                    try { r.BuyEquipment(model, units); } catch (InvalidOperationException) { }
                }
            }

            var fits = (int)(r.FreeFloorArea / 15m);
            var actualSeats = Math.Min(seats, fits);
            if (actualSeats > 0) r.BuyTables("t", "Tables", actualSeats * 120m, actualSeats);

            // staff the room you actually got, not the one you planned
            servers = Math.Max(1, (int)Math.Ceiling(actualSeats / 14.0));

            for (var i = 0; i < cooks; i++) r.Payroll.Hire(new Employee("c" + i, "Cook", StaffRole.Cook, 16m));
            for (var i = 0; i < servers; i++) r.Payroll.Hire(new Employee("s" + i, "Server", StaffRole.Server, 12m));

            foreach (var id in definitions.IngredientIds) { r.Inventory.SetPar(id, 400m, 3000m); r.Inventory.Receive(id, 3000m); }

            var runner = new SimulationRunner(r, new GameClock(), seed,
                new InterruptPolicy { WalkoutStreakThreshold = 4, CashFloor = null, StopOnStockout = true });

            var interrupts = 0;
            long remaining = 30L * GameClock.TicksPerDay;

            while (remaining > 0)
            {
                var step = runner.Advance(remaining);
                remaining -= step.TicksAdvanced;
                if (step.StoppedEarly) interrupts++;
                if (step.TicksAdvanced == 0 && !step.StoppedEarly) break;

                foreach (var stock in r.Inventory.Items.ToList())
                    if (stock.IsBelowPar) r.Inventory.Receive(stock.IngredientId, stock.SuggestedReorderQuantity);
            }

            var m = runner.Snapshot();
            var prime = m.Revenue == 0m ? 0m : (m.FoodCost + m.LaborCost) / m.Revenue;

            return new Run
            {
                Site = siteKey, Size = size, Seed = seed,
                Revenue = m.Revenue,
                Net = m.Revenue - m.FoodCost - m.LaborCost - site.MonthlyRent,
                PrimeRatio = prime, Covers = m.CoversServed, Walkouts = m.Walkouts,
                Interrupts = interrupts, TurnedAway = m.PartiesTurnedAway,
                LostToMenu = m.PartiesLostToMenu, PutOffByWait = m.PartiesPutOffByTheWait,
                PutOffByPrice = m.PartiesPutOffByThePrices
            };
        }

        [Fact(Skip = "Measuring instrument, not a test. Run by removing this Skip.")]
        public void OneHundredRuns()
        {
            var sizes = new[]
            {
                new { Name = "starter", Units = 1, Seats = 12, Cooks = 1, Servers = 1 },
                new { Name = "small",   Units = 2, Seats = 20, Cooks = 2, Servers = 2 },
                new { Name = "medium",  Units = 3, Seats = 34, Cooks = 3, Servers = 3 },
                new { Name = "large",   Units = 4, Seats = 48, Cooks = 4, Servers = 4 },
                new { Name = "maxed",   Units = 5, Seats = 62, Cooks = 5, Servers = 5 }
            };

            var seeds = new long[] { 11, 4242, 777, 90210, 31337 };
            var runs = new List<Run>();

            foreach (var site in new[] { "city", "business", "nightlife", "suburban" })
                foreach (var size in sizes)
                    foreach (var seed in seeds)
                        runs.Add(Simulate(site, size.Name, size.Units, size.Seats, size.Cooks, size.Servers, seed));

            _out.WriteLine("RUNS: " + runs.Count + "   profitable: " + runs.Count(x => x.Net > 0));
            _out.WriteLine("");
            _out.WriteLine("site       size      net(avg)   prime%   covers  walkout  turned  waitOff  priceOff  intr");
            _out.WriteLine("-------------------------------------------------------------------------------------------");

            foreach (var site in new[] { "city", "business", "nightlife", "suburban" })
            {
                foreach (var size in sizes)
                {
                    var g = runs.Where(x => x.Site == site && x.Size == size.Name).ToList();
                    _out.WriteLine(string.Format(
                        "{0,-10} {1,-8} {2,9:N0} {3,7:P0} {4,8:N0} {5,8:N0} {6,7:N0} {7,8:N0} {8,9:N0} {9,5:N1}",
                        site, size.Name, g.Average(x => x.Net), g.Average(x => x.PrimeRatio),
                        g.Average(x => x.Covers), g.Average(x => x.Walkouts), g.Average(x => x.TurnedAway),
                        g.Average(x => x.PutOffByWait), g.Average(x => x.PutOffByPrice), g.Average(x => x.Interrupts)));
                }
                _out.WriteLine("");
            }

            _out.WriteLine("=== FINDINGS ===");
            foreach (var site in new[] { "city", "business", "nightlife", "suburban" })
            {
                var g = runs.Where(x => x.Site == site).ToList();
                _out.WriteLine(site + ": best " + g.Max(x => x.Net).ToString("N0") +
                    " at " + g.OrderByDescending(x => x.Net).First().Size +
                    ", worst " + g.Min(x => x.Net).ToString("N0") +
                    ", profitable in " + g.Count(x => x.Net > 0) + "/" + g.Count);
            }

            var variance = runs.GroupBy(x => x.Site + "/" + x.Size)
                .Select(g => new { g.Key, Spread = g.Max(x => x.Net) - g.Min(x => x.Net), Avg = g.Average(x => x.Net) })
                .OrderByDescending(x => x.Spread).First();
            _out.WriteLine("widest seed-to-seed spread: " + variance.Key + " swings " + variance.Spread.ToString("N0") +
                           " around an average of " + variance.Avg.ToString("N0"));

            _out.WriteLine("interrupts per 30 days: min " + runs.Min(x => x.Interrupts) +
                           ", max " + runs.Max(x => x.Interrupts) +
                           ", avg " + runs.Average(x => x.Interrupts).ToString("N1"));
            _out.WriteLine("runs that never stopped you at all: " + runs.Count(x => x.Interrupts == 0));
        }
    }
}
