using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;

namespace RestaurantEmpire.Sim
{
    /// <summary>
    /// Runs one night of service and prints what happened.
    ///
    /// This is the "and logs" half of how M0 is validated (CLAUDE.md: "no graphics, no
    /// engine, validated by unit tests and logs"). It is NOT the game — there is no input,
    /// no pacing, and nothing to click. It exists so a human can turn the dials the
    /// simulation actually has and read the consequences, which is the only way to build
    /// intuition about whether the numbers behave sensibly before M1 spends real time on
    /// rendering them.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Contains("--help") || args.Contains("-h"))
            {
                PrintHelp();
                return 0;
            }

            var supplier = Arg(args, "--supplier", "valley-produce");
            var priceMultiplier = Decimal(args, "--price", 1.0m);
            var stations = Int(args, "--stations", 2);
            var demand = Double(args, "--demand", 25);
            var seats = Int(args, "--seats", 0);
            var staff = Int(args, "--staff", 3);
            var wage = Decimal(args, "--wage", 18m);
            var hours = Int(args, "--hours", 6);
            var overhead = Decimal(args, "--overhead", 300m);
            var seed = Int(args, "--seed", 4242);

            var dataDir = FindDataDirectory();
            if (dataDir == null)
            {
                Console.Error.WriteLine("Could not find the data/ directory. Run this from inside the repo.");
                return 1;
            }

            var definitions = JsonDefinitionLoader.LoadFromDirectory(dataDir);
            foreach (var warning in definitions.LoadWarnings) Console.WriteLine("content warning: " + warning);

            var company = new Company("player-co", "Your Restaurant Group", definitions, 20000m);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            foreach (var recipe in definitions.Recipes) restaurant.Menu.Add(recipe.Id);
            restaurant.SeatingCapacity = seats;

            try
            {
                company.SupplierPolicy.AssignAll(supplier);
            }
            catch (Exception)
            {
                Console.Error.WriteLine("Unknown supplier '" + supplier + "'. Options: " +
                    string.Join(", ", definitions.Suppliers.Select(s => s.Id)));
                return 1;
            }

            if (priceMultiplier != 1.0m)
                foreach (var id in restaurant.Menu.RecipeIds) company.Pricing.AdjustPrice(id, priceMultiplier);

            // Install a station for every station the menu needs.
            foreach (var stationId in restaurant.Menu.Recipes.Select(r => r.StationId).Distinct())
                restaurant.Kitchen.Install(stationId, Title(stationId), stations);

            foreach (var id in definitions.IngredientIds) restaurant.Inventory.Receive(id, 100000m);

            var clock = new GameClock();
            clock.AdvanceHours(17);   // dinner service starts at 5pm

            Console.WriteLine();
            Console.WriteLine("=== " + restaurant.Name + " — " + clock.Now.ToString("dddd d MMMM yyyy") + ", dinner ===");
            Console.WriteLine("sourcing " + supplier + "  ·  prices x" + priceMultiplier +
                              "  ·  " + stations + " slot(s) per station  ·  " +
                              (seats == 0 ? "unlimited seats" : seats + " seats"));

            PrintMenu(restaurant);

            var result = ServiceSimulation.Run(restaurant, clock.Tick, 180, new DemandModel(demand, seed), seed);
            company.Economy.RecordService(restaurant, result, clock.Tick);

            var labour = staff * hours * wage;
            if (labour > 0m) company.Economy.Record(clock.Tick, LedgerCategory.LaborCost, labour,
                staff + " staff x " + hours + "h @ " + wage, restaurant.Id);
            if (overhead > 0m) company.Economy.Record(clock.Tick, LedgerCategory.Overhead, overhead,
                "Nightly share of rent and utilities", restaurant.Id);

            PrintService(result);
            PrintBooks(company, restaurant, clock);
            PrintMatrix(restaurant, result);
            PrintComplaints(result);

            Console.WriteLine();
            Console.WriteLine("Try: --supplier premium-harvest --price 1.5   (buy better, charge for it)");
            Console.WriteLine("     --stations 1                            (choke the kitchen)");
            Console.WriteLine("     --demand 60                             (more guests than you can cook for)");
            Console.WriteLine("     --help                                  (all the dials)");
            Console.WriteLine();

            return 0;
        }

        private static void PrintMenu(Restaurant restaurant)
        {
            var costing = restaurant.Costing;

            Console.WriteLine();
            Console.WriteLine("MENU                      price     cost   food%   margin   station");
            Console.WriteLine("  ------------------------------------------------------------------------");

            foreach (var recipe in restaurant.Menu.Recipes)
            {
                Console.WriteLine(string.Format("  {0,-22} {1,7:0.00}  {2,7:0.00}  {3,6:P0}  {4,7:0.00}   {5} ({6} min)",
                    recipe.Name, costing.MenuPrice(recipe.Id), costing.PlateCost(recipe.Id),
                    costing.FoodCostRatio(recipe.Id), costing.ContributionMargin(recipe.Id),
                    recipe.StationId, recipe.PrepMinutes));
            }
        }

        private static void PrintService(ServiceResult r)
        {
            Console.WriteLine();
            Console.WriteLine("SERVICE");
            Console.WriteLine("  parties arrived      " + r.PartiesArrived);
            if (r.PartiesTurnedAway > 0)
                Console.WriteLine("  turned away          " + r.PartiesTurnedAway + "   (dining room full)");
            Console.WriteLine("  covers served        " + r.CoversServed);
            Console.WriteLine("  walked out           " + r.Walkouts +
                (r.WastedFoodCost > 0m ? "   (" + r.WastedFoodCost.ToString("0.00") + " of food binned)" : ""));
            if (r.EightySixed > 0)
                Console.WriteLine("  86'd                 " + r.EightySixed);
            Console.WriteLine("  longest wait         " + r.LongestWaitMinutes + " min");
            Console.WriteLine("  busiest station      " + (r.BusiestStationId ?? "—"));
            Console.WriteLine("  avg satisfaction     " + r.AverageSatisfaction.ToString("0.00") + "   " + Stars(r.AverageSatisfaction));
        }

        private static void PrintBooks(Company company, Restaurant restaurant, GameClock clock)
        {
            var books = company.Economy.Summarize(clock.Tick, clock.Tick, restaurant.Id);

            Console.WriteLine();
            Console.WriteLine("THE BOOKS");
            Console.WriteLine(string.Format("  revenue              {0,9:0.00}", books.Revenue));
            Console.WriteLine(string.Format("  food cost            {0,9:0.00}   {1,6:P0} of revenue", books.FoodCost, books.FoodCostRatio));
            Console.WriteLine(string.Format("  labour               {0,9:0.00}   {1,6:P0} of revenue", books.LaborCost, books.LaborCostRatio));
            Console.WriteLine(string.Format("  overhead             {0,9:0.00}", books.Overhead));
            Console.WriteLine("  ------------------------------------------------");
            Console.WriteLine(string.Format("  PRIME COST           {0,9:0.00}   {1,6:P1}   {2}",
                books.PrimeCost, books.PrimeCostRatio, Verdict(books.Band)));
            Console.WriteLine(string.Format("  net profit           {0,9:0.00}   {1}",
                books.NetProfit, books.NetProfit >= 0m ? "" : "  <-- losing money"));
            Console.WriteLine(string.Format("  cash on hand         {0,9:0.00}", company.Economy.CashOnHand));
        }

        private static void PrintMatrix(Restaurant restaurant, ServiceResult result)
        {
            if (result.TotalUnitsSold == 0) return;

            var analysis = MenuEngineering.Analyze(
                restaurant, result.UnitsSoldByRecipeId.ToDictionary(p => p.Key, p => p.Value));

            Console.WriteLine();
            Console.WriteLine("MENU MATRIX (from tonight's actual sales)");

            foreach (var item in analysis.Items.OrderByDescending(i => i.TotalContribution))
            {
                Console.WriteLine(string.Format("  {0,-22} {1,-10} sold {2,3}   margin {3,6:0.00}   earned {4,8:0.00}",
                    item.Name, item.Classification, item.UnitsSold, item.ContributionMargin, item.TotalContribution));
            }

            Console.WriteLine("  Star = protect · Plowhorse = popular, thin · Puzzle = profitable, ignored · Dog = cut");
        }

        private static void PrintComplaints(ServiceResult result)
        {
            if (result.Diagnostics.Count == 0) return;

            Console.WriteLine();
            Console.WriteLine("WHAT WENT WRONG (top 5 of " + result.Diagnostics.Count + ")");

            foreach (var line in result.Diagnostics.Take(5)) Console.WriteLine("  · " + line);
        }

        private static string Verdict(PrimeCostBand band)
        {
            switch (band)
            {
                case PrimeCostBand.Excellent: return "excellent — better than most real operators";
                case PrimeCostBand.Healthy: return "healthy — where a good kitchen lives";
                case PrimeCostBand.Tight: return "tight — survivable for fine dining, not much else";
                case PrimeCostBand.Unsustainable: return "UNSUSTAINABLE — losing on every cover";
                default: return "no revenue to judge";
            }
        }

        private static string Stars(decimal satisfaction)
        {
            var full = (int)Math.Round(satisfaction * 5m, MidpointRounding.AwayFromZero);
            return new string('*', Math.Max(0, full)) + new string('.', Math.Max(0, 5 - full));
        }

        private static string Title(string id)
        {
            return string.Join(" ", id.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));
        }

        private static string FindDataDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "data");
                if (File.Exists(Path.Combine(candidate, "ingredients.json"))) return candidate;

                dir = dir.Parent;
            }

            return null;
        }

        private static string Arg(string[] args, string name, string fallback)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        private static int Int(string[] args, string name, int fallback)
        {
            int value;
            return int.TryParse(Arg(args, name, null), out value) ? value : fallback;
        }

        private static double Double(string[] args, string name, double fallback)
        {
            double value;
            return double.TryParse(Arg(args, name, null), out value) ? value : fallback;
        }

        private static decimal Decimal(string[] args, string name, decimal fallback)
        {
            decimal value;
            return decimal.TryParse(Arg(args, name, null), out value) ? value : fallback;
        }

        private static void PrintHelp()
        {
            Console.WriteLine(@"
Runs one dinner service and prints what happened. This is a inspection tool for the
M0 simulation core, not the game — there is nothing to click and no pacing yet.

  --supplier <id>    budget-wholesale | valley-produce | premium-harvest   (default valley-produce)
  --price <mult>     multiply every menu price, e.g. 1.5                   (default 1.0)
  --stations <n>     slots per kitchen station                             (default 2)
  --demand <n>       parties per hour at the peak of service               (default 25)
  --seats <n>        dining room capacity, 0 for unlimited                 (default 0)
  --staff <n>        people on tonight                                     (default 3)
  --wage <n>         hourly wage                                           (default 18)
  --hours <n>        length of shift                                       (default 6)
  --overhead <n>     nightly rent and utilities                            (default 300)
  --seed <n>         change this for a different night                     (default 4242)

Examples:
  dotnet run --project src/RestaurantEmpire.Sim
  dotnet run --project src/RestaurantEmpire.Sim -- --supplier premium-harvest --price 1.5
  dotnet run --project src/RestaurantEmpire.Sim -- --stations 1 --demand 40
");
        }
    }
}
