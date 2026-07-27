using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;

namespace RestaurantEmpire.Sim
{
    /// <summary>
    /// The M1 rhythm harness: drive the simulation forward and be interrupted by it.
    ///
    /// This is how M1 exit test (b) gets answered. The mechanism bar (a) is settled by unit
    /// tests — the sim provably pauses and resumes cleanly. But whether the
    /// fast-forward-with-interrupts loop has a PULSE is a question only a human sitting in
    /// front of a month of simulated time can answer, so this asks directly after every
    /// stop: was that worth stopping for? It keeps score and reports the tally on the way out.
    ///
    /// Still not the game. No graphics, no watching service unfold. But it is the loop.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Contains("--help") || args.Contains("-h")) { Help(); return 0; }

            var dataDir = FindDataDirectory();
            if (dataDir == null)
            {
                Console.Error.WriteLine("Could not find the data/ directory. Run this from inside the repo.");
                return 1;
            }

            var definitions = JsonDefinitionLoader.LoadFromDirectory(dataDir);
            foreach (var w in definitions.LoadWarnings) Console.WriteLine("content warning: " + w);

            var supplier = Arg(args, "--supplier", "valley-produce");
            var priceMultiplier = Dec(args, "--price", 1.0m);
            var stations = Int(args, "--stations", 3);
            var demand = Dbl(args, "--demand", 25);
            var seats = Int(args, "--seats", 0);
            var seed = Int(args, "--seed", 4242);

            var company = new Company("player-co", "Your Restaurant Group", definitions, Dec(args, "--cash", 20000m));
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            // The menu is a choice. Offering nothing anyone wants at breakfast is allowed,
            // and is exactly how you pay a morning's labour for an empty room.
            var menuArg = Arg(args, "--menu", "all");
            foreach (var recipe in definitions.Recipes)
            {
                var wanted = menuArg == "all"
                    || menuArg.Split(',').Contains(recipe.Id)
                    || (menuArg == "dinner" && recipe.SuitsDaypart(Daypart.Dinner));

                if (wanted) restaurant.Menu.Add(recipe.Id);
            }
            if (seats > 0) restaurant.BuyTables("tables", "Tables and chairs", seats * 120m, seats, 0.55m);

            if (!definitions.HasSupplier(supplier))
            {
                Console.Error.WriteLine("Unknown supplier. Options: " +
                    string.Join(", ", definitions.Suppliers.Select(s => s.Id)));
                return 1;
            }

            company.SupplierPolicy.AssignAll(supplier);
            if (priceMultiplier != 1.0m)
                foreach (var id in restaurant.Menu.RecipeIds) company.Pricing.AdjustPrice(id, priceMultiplier);

            restaurant.Location = Where(Arg(args, "--location", "suburban"));

            // The player picks the hours. Whether anybody is out there is the location's call.
            restaurant.ServiceWindows.Clear();
            foreach (var spec in Arg(args, "--hours", "12-15,18-23").Split(','))
            {
                var parts = spec.Split('-');
                if (parts.Length != 2) continue;

                int from, to;
                if (!int.TryParse(parts[0], out from) || !int.TryParse(parts[1], out to)) continue;

                restaurant.ServiceWindows.Add(new ServiceWindow(NameFor(from), from, to));
            }

            // The fit-out is bought, not conjured — every slot at every station is capital out.
            var perSlot = Dec(args, "--station-cost", 2800m);
            foreach (var stationId in restaurant.Menu.Recipes.Select(r => r.StationId).Distinct())
                restaurant.BuyStation(stationId, Title(stationId), perSlot * stations, stations);

            // Par levels: the standing policy the morning delivery is ordered against.
            var par = Dec(args, "--stock", 2000m);
            foreach (var id in definitions.IngredientIds)
            {
                restaurant.Inventory.SetPar(id, par * 0.35m, par);
                restaurant.Inventory.Receive(id, par);
            }

            var runner = new SimulationRunner(restaurant, new GameClock(), seed, new InterruptPolicy
            {
                WalkoutStreakThreshold = Int(args, "--walkout-streak", 4),
                CashFloor = Dec(args, "--cash-floor", 0m),
                StopOnStockout = true
            });

            // Labour scales with how long the doors are open. Nothing in the core generates
            // labour until Employees arrive at M1, but charging it flat per DAY made long
            // hours look free, which is exactly the illusion the location model exists to kill.
            var openHours = restaurant.ServiceWindows.Sum(w => w.LengthMinutes) / 60m;

            var session = new Session(runner, company,
                labourPerDay: Dec(args, "--labour-per-hour", 72m) * openHours,
                overheadPerDay: Dec(args, "--overhead", 300m));

            session.StationSlotCost = perSlot;

            Console.WriteLine();
            Console.WriteLine("  " + restaurant.Location.Name + "  ·  sourcing " + supplier +
                              "  ·  prices x" + priceMultiplier + "  ·  " + stations + " slot(s)/station  ·  " +
                              (seats == 0 ? "unlimited seats" : seats + " seats"));
            Console.WriteLine("  open: " + string.Join(", ", restaurant.ServiceWindows.Select(w => w.ToString())));

            foreach (var window in restaurant.ServiceWindows)
            {
                var potential = window.PotentialPartiesIn(restaurant.Location);
                if (potential < 4)
                {
                    Console.WriteLine("  !! " + window.Name + " sees barely any passing trade here (" +
                                      potential.ToString("0.0") + " parties across the whole service).");
                }
            }

            var autoDays = Int(args, "--auto", 0);
            return autoDays > 0 ? session.RunAuto(autoDays) : session.RunInteractive();
        }

        // ---- The loop ----

        private sealed class Session
        {
            private readonly SimulationRunner _runner;
            private readonly Company _company;
            private readonly decimal _labourPerDay, _overheadPerDay;

            public decimal StationSlotCost = 2800m;

            private ServiceResult _lastSeen;
            private ServiceResult _lastBooked;
            private int _stops, _worthIt, _daysBooked;

            public Session(SimulationRunner runner, Company company, decimal labourPerDay, decimal overheadPerDay)
            {
                _runner = runner;
                _company = company;
                _labourPerDay = labourPerDay;
                _overheadPerDay = overheadPerDay;
                _lastSeen = runner.Snapshot();
                _lastBooked = _lastSeen;
            }

            /// <summary>Advances, books any completed days, and reports what happened.</summary>
            private AdvanceResult Step(long ticks)
            {
                var before = _runner.Snapshot();
                var result = _runner.Advance(ticks);
                var after = _runner.Snapshot();

                for (var day = 0; day < result.Elapsed.Days; day++) BookADay();

                Report.Happened(new Delta(before, after), _runner.Clock);
                _lastSeen = after;

                return result;
            }

            /// <summary>
            /// The game loop's job, not the simulation's: turn a day of trading into ledger
            /// entries. The runner reports the boundary; deciding what it costs is up here.
            /// </summary>
            private void BookADay()
            {
                var now = _runner.Snapshot();
                var tick = _runner.Clock.Tick;
                var id = _runner.Restaurant.Id;

                // The morning delivery, ordered against standing par levels — the Factorio
                // "set it up once, trust it" pattern. Deliberately the game loop's job, not
                // the simulation's.
                //
                // NOTE: this is currently free. Ingredients are charged when USED, not when
                // BOUGHT, so holding a deep pantry ties up no cash and nothing ever spoils.
                // Both are real gaps — see CLAUDE.md on what makes long hours a real cost.
                foreach (var stock in _runner.Restaurant.Inventory.Items.ToList())
                {
                    if (stock.IsBelowPar)
                        _runner.Restaurant.Inventory.Receive(stock.IngredientId, stock.SuggestedReorderQuantity);
                }

                var revenue = now.Revenue - _lastBooked.Revenue;
                var food = now.FoodCost - _lastBooked.FoodCost;

                if (revenue > 0m) _company.Economy.Record(tick, LedgerCategory.Revenue, revenue, "Day's takings", id);
                if (food > 0m) _company.Economy.Record(tick, LedgerCategory.FoodCost, food, "Day's ingredients", id);
                if (_labourPerDay > 0m) _company.Economy.Record(tick, LedgerCategory.LaborCost, _labourPerDay, "Brigade", id);
                if (_overheadPerDay > 0m) _company.Economy.Record(tick, LedgerCategory.Overhead, _overheadPerDay, "Rent and utilities", id);

                _lastBooked = now;
                _daysBooked++;
            }

            public int RunAuto(int days)
            {
                Console.WriteLine("  simulating " + days + " days, stopping at every interrupt...");

                long remaining = (long)days * GameClock.TicksPerDay;

                while (remaining > 0)
                {
                    var step = Step(remaining);
                    remaining -= step.TicksAdvanced;

                    if (!step.StoppedEarly) break;

                    _stops++;
                    Report.TheInterrupt(step.Interrupt);
                }

                Summary();
                return 0;
            }

            public int RunInteractive()
            {
                while (true)
                {
                    Report.Header(_runner);
                    Console.WriteLine();
                    Console.Write("  [h]our [d]ay [w]eek [m]onth   [a]ct   [b]ooks [k]menu [x]matrix   [q]uit > ");

                    var input = Console.ReadLine();
                    if (input == null) { Summary(); return 0; }   // piped stdin ran out

                    var key = input.Trim().ToLowerInvariant();
                    long ticks;

                    switch (key)
                    {
                        case "h": ticks = GameClock.TicksPerHour; break;
                        case "d": ticks = GameClock.TicksPerDay; break;
                        case "w": ticks = GameClock.TicksPerWeek; break;
                        case "m": ticks = 30L * GameClock.TicksPerDay; break;

                        case "b": Report.Books(_company, _runner.Restaurant); continue;
                        case "k": Report.Menu(_runner.Restaurant); continue;
                        case "x": Report.Matrix(_runner.Restaurant, _runner.Snapshot()); continue;
                        case "a": if (!Act()) { Summary(); return 0; } continue;

                        case "q": Summary(); return 0;
                        default: continue;
                    }

                    // Keep going until we've covered the whole jump, asking about each stop.
                    var remaining = ticks;

                    while (remaining > 0)
                    {
                        var before = _runner.Snapshot();
                        var step = Step(remaining);
                        remaining -= step.TicksAdvanced;

                        if (!step.StoppedEarly) break;

                        _stops++;
                        Report.TheInterrupt(step.Interrupt);
                        Report.Complaints(new Delta(before, _runner.Snapshot()));

                        Console.WriteLine();
                        Console.Write("  ##  Worth stopping for? [y/n]   [a]ct on it   [s]top jumping > ");

                        var verdict = Console.ReadLine();
                        if (verdict == null) { Summary(); return 0; }

                        verdict = verdict.Trim().ToLowerInvariant();
                        if (verdict.StartsWith("y")) _worthIt++;
                        if (verdict.StartsWith("a")) { _worthIt++; if (!Act()) { Summary(); return 0; } break; }
                        if (verdict.StartsWith("s")) break;
                    }
                }
            }

            /// <summary>
            /// The levers. Being stopped is pointless if you cannot do anything about it —
            /// "was that worth stopping for?" is unanswerable when the only options are
            /// carry on or quit.
            ///
            /// Returns false when input runs out.
            /// </summary>
            private bool Act()
            {
                var restaurant = _runner.Restaurant;

                while (true)
                {
                    Console.WriteLine();
                    Console.WriteLine("  cash " + _runner.ProjectedCash.ToString("N2"));
                    Console.Write("  [1] buy a slot at a station   [2] change prices   " +
                                  "[3] switch supplier   [4] change hours   [enter] back > ");

                    var input = Console.ReadLine();
                    if (input == null) return false;

                    switch (input.Trim())
                    {
                        case "1":
                            Console.Write("    which station? (" +
                                string.Join(", ", restaurant.Kitchen.Stations.Select(s => s.Id + " x" + s.ConcurrentCapacity)) + ") > ");
                            var stationId = Console.ReadLine();
                            if (stationId == null) return false;

                            KitchenStation existing;
                            if (restaurant.Kitchen.TryGet(stationId.Trim(), out existing))
                            {
                                // Stations are immutable, so buying capacity replaces the
                                // station with a bigger one and bills for the extra slot.
                                restaurant.BuyStation(existing.Id, existing.Name, StationSlotCost,
                                    existing.ConcurrentCapacity + 1, existing.SpeedMultiplier, _runner.Clock.Tick);

                                Console.WriteLine("    " + existing.Name + " now runs " +
                                    (existing.ConcurrentCapacity + 1) + " plates at once. Cost " +
                                    StationSlotCost.ToString("N2") + ".");
                            }
                            else Console.WriteLine("    no such station.");
                            break;

                        case "2":
                            Console.Write("    multiply every price by > ");
                            var multiplierText = Console.ReadLine();
                            if (multiplierText == null) return false;

                            decimal multiplier;
                            if (decimal.TryParse(multiplierText.Trim(), out multiplier) && multiplier > 0m)
                            {
                                foreach (var id in restaurant.Menu.RecipeIds) _company.Pricing.AdjustPrice(id, multiplier);
                                Report.Menu(restaurant);
                            }
                            else Console.WriteLine("    that isn't a number.");
                            break;

                        case "3":
                            Console.Write("    which supplier? (" +
                                string.Join(", ", _company.Definitions.Suppliers.Select(s => s.Id)) + ") > ");
                            var supplierId = Console.ReadLine();
                            if (supplierId == null) return false;

                            if (_company.Definitions.HasSupplier(supplierId.Trim()))
                            {
                                _company.SupplierPolicy.AssignAll(supplierId.Trim());
                                Console.WriteLine("    now buying everything from " + supplierId.Trim() + ".");
                                Report.Menu(restaurant);
                            }
                            else Console.WriteLine("    no such supplier.");
                            break;

                        case "4":
                            Console.Write("    hours, e.g. 12-15,18-23 > ");
                            var hours = Console.ReadLine();
                            if (hours == null) return false;

                            var parsed = new List<ServiceWindow>();
                            foreach (var spec in hours.Split(','))
                            {
                                var parts = spec.Trim().Split('-');
                                int from, to;
                                if (parts.Length == 2 && int.TryParse(parts[0], out from) && int.TryParse(parts[1], out to))
                                {
                                    try { parsed.Add(new ServiceWindow(NameFor(from), from, to)); }
                                    catch (ArgumentOutOfRangeException) { }
                                }
                            }

                            if (parsed.Count > 0)
                            {
                                restaurant.ServiceWindows.Clear();
                                foreach (var window in parsed) restaurant.ServiceWindows.Add(window);

                                foreach (var window in parsed)
                                {
                                    var potential = window.PotentialPartiesIn(restaurant.Location);
                                    Console.WriteLine("    " + window + "  —  about " + potential.ToString("0.0") +
                                        " parties pass by across that service" + (potential < 4 ? "   !! barely worth opening" : ""));
                                }
                            }
                            else Console.WriteLine("    couldn't read those hours.");
                            break;

                        default:
                            return true;
                    }
                }
            }

            private void Summary()
            {
                Console.WriteLine();
                Console.WriteLine("  ================ M1(b): DID THE LOOP HAVE A PULSE? ================");
                Console.WriteLine("  simulated       " + _runner.Clock.DayNumber + " days");
                Console.WriteLine("  interrupted     " + _stops + " times");

                if (_worthIt > 0 || _stops > 0)
                    Console.WriteLine("  worth stopping  " + _worthIt + " of " + _stops);

                if (_stops == 0)
                    Console.WriteLine("  -> nothing ever stopped you. That is the failure mode: fast-forward with no pulse.");
                else if (_stops > _runner.Clock.DayNumber * 3)
                    Console.WriteLine("  -> stopped very often. Interrupt fatigue is the other failure mode.");

                Report.Books(_company, _runner.Restaurant);
                Console.WriteLine();
            }
        }

        // ---- Plumbing ----

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

        private static Neighbourhood Where(string key)
        {
            switch ((key ?? string.Empty).ToLowerInvariant())
            {
                case "city": return Neighbourhood.CityCentre();
                case "business": return Neighbourhood.BusinessDistrict();
                case "nightlife": return Neighbourhood.NightlifeQuarter();
                default: return Neighbourhood.SuburbanHighStreet();
            }
        }

        private static string NameFor(int startHour)
        {
            if (startHour < 11) return "Breakfast";
            if (startHour < 16) return "Lunch";
            if (startHour < 22) return "Dinner";

            return "Late Night";
        }

        private static string Title(string id)
        {
            return string.Join(" ", id.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));
        }

        private static string Arg(string[] a, string n, string d)
        {
            var i = Array.IndexOf(a, n);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : d;
        }

        private static int Int(string[] a, string n, int d)
        {
            int v; return int.TryParse(Arg(a, n, null), out v) ? v : d;
        }

        private static double Dbl(string[] a, string n, double d)
        {
            double v; return double.TryParse(Arg(a, n, null), out v) ? v : d;
        }

        private static decimal Dec(string[] a, string n, decimal d)
        {
            decimal v; return decimal.TryParse(Arg(a, n, null), out v) ? v : d;
        }

        private static void Help()
        {
            Console.WriteLine(@"
Drive the restaurant forward and let it interrupt you. This is the M1 rhythm harness:
the question it exists to answer is whether being stopped feels worth it.

  (no args)              interactive — jump by hour/day/week/month, judge each stop
  --auto <days>          run N days non-interactively, printing every interrupt

  --supplier <id>        budget-wholesale | valley-produce | premium-harvest
  --price <mult>         multiply every menu price, e.g. 1.5
  --stations <n>         slots per kitchen station          (default 3)
  --location <where>     suburban | city | business | nightlife
  --hours <spec>         e.g. 7-10,12-15,18-23   (default 12-15,18-23)
  --menu <what>          all | dinner | a,comma,list of recipe ids
  --seats <n>            dining room capacity, 0 = unlimited
  --cash <n>             opening cash                       (default 20000)
  --stock <n>            opening stock per ingredient       (default 2000)
  --labour-per-hour <n>  labour per hour the doors are open (default 72)
  --overhead <n>         rent and utilities per day         (default 300)
  --walkout-streak <n>   walkouts in a row before stopping  (default 4)
  --cash-floor <n>       cash level that stops the sim      (default 0)
  --seed <n>             a different world

Examples:
  dotnet run --project src/RestaurantEmpire.Sim
  dotnet run --project src/RestaurantEmpire.Sim -- --auto 30
  dotnet run --project src/RestaurantEmpire.Sim -- --auto 14 --stations 1 --stock 300
");
        }
    }
}
