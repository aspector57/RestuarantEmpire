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
    /// THE M1(b) INSTRUMENT. The bar M1(b) has been missing since it was split.
    ///
    /// M1(b) asks whether the fast-forward-with-interrupts loop has a pulse, and the honest
    /// answer has been "we cannot tell" — Aaron judged it from a scrolling text log, said the
    /// stops were worth it, then immediately corrected himself: *"I was being a bit generous
    /// here."* A polite yes is not evidence. The diagnosis was that interrupts named a problem
    /// and never an action, and the Advisor is what fixes that. So the bar becomes a number:
    ///
    ///     DOES AN ADVISOR-GUIDED OPENING SURVIVE TWELVE MONTHS?
    ///
    /// This is <see cref="Campaign"/>'s twin and the comparison is the whole point. Campaign
    /// plays the naive way — it buys an oven whenever one fits and only buys tables when the
    /// floor will not take another, which is exactly the mistake Aaron made at the keyboard
    /// ("I bought a ton of ovens and kept getting backed up"). It busts on three of four sites.
    /// This one does nothing except what the Advisor tells it, in the Advisor's own words.
    ///
    /// If the advised run survives where the naive one busts, the Advisor is carrying a player
    /// across the gap, which is the thing M1(b) is really asking about. If it does not, the
    /// Advisor is not yet advice — and either way the answer is a figure rather than a feeling.
    /// </summary>
    public class AdvisedCampaign
    {
        private readonly ITestOutputHelper _out;
        public AdvisedCampaign(ITestOutputHelper o) { _out = o; }

        private sealed class Site
        {
            public string Key = "";
            public Neighborhood Where = null!;
            public ServiceWindow[] Hours = null!;
        }

        private static Site[] Sites()
        {
            return new[]
            {
                new Site { Key = "city", Where = Neighborhood.CityCenter(),
                    Hours = new[] { new ServiceWindow("Lunch", 12, 15), new ServiceWindow("Dinner", 18, 23) } },
                new Site { Key = "business", Where = Neighborhood.BusinessDistrict(),
                    Hours = new[] { new ServiceWindow("Breakfast", 7, 10), new ServiceWindow("Lunch", 12, 15) } },
                new Site { Key = "nightlife", Where = Neighborhood.NightlifeQuarter(),
                    Hours = new[] { new ServiceWindow("Dinner", 18, 23), new ServiceWindow("Late", 23, 2) } },
                new Site { Key = "suburban", Where = Neighborhood.SuburbanHighStreet(),
                    Hours = new[] { new ServiceWindow("Dinner", 18, 23) } }
            };
        }

        [Fact(Skip = "Measuring instrument. Remove this Skip to run.")]
        public void AnAdvisedOpeningAgainstANaiveOne()
        {
            _out.WriteLine("Identical opening on every site: 30,000 bankroll less key money, one");
            _out.WriteLine("second-hand unit of each station the menu needs, 12 covers, 1 cook, 1 server.");
            _out.WriteLine("The only difference is who decides what to buy next.");
            _out.WriteLine("");
            _out.WriteLine("site        m3cash    m6cash   m12cash   seats units crew   taken  verdict");
            _out.WriteLine("---------------------------------------------------------------------------");

            var survived = 0;
            foreach (var site in Sites())
            {
                var taken = new Dictionary<string, int>();
                var cash = Play(site, taken, out var seats, out var units, out var crew, out var m3, out var m6);

                var verdict = cash > 0m ? "surviving" : "BUST";
                if (cash > 0m) survived++;

                _out.WriteLine(string.Format(
                    "{0,-10} {1,9:N0} {2,9:N0} {3,9:N0} {4,7} {5,5} {6,4}   {7,5}  {8}",
                    site.Key, m3, m6, cash, seats, units, crew,
                    taken.Values.Sum(), verdict));
            }

            _out.WriteLine("");
            _out.WriteLine("advised runs surviving twelve months: " + survived + " of 4");

            // Deliberately no assertion on the count. This is an instrument, and turning a
            // measurement into a pass/fail is how you end up tuning the game to satisfy the
            // test rather than the player.
            Assert.True(survived >= 0);
        }

        /// <summary>
        /// AARON'S OWN WINNING SEQUENCE, run against the C# engine.
        ///
        /// He beat the browser build in about ten minutes: *"basically just used the best
        /// ingredients and then simmed, then added a few seats, then team, then best
        /// equipment."* Neither automated policy can find a survivable path in C#, so either
        /// the browser port is materially easier than the engine it mirrors, or the path is
        /// narrow enough that only a person spots it. Scripting his exact order settles which,
        /// and it is a measurement rather than a question worth asking him.
        /// </summary>
        [Fact(Skip = "Measuring instrument. Remove this Skip to run.")]
        public void ThePlayersOwnSequence()
        {
            _out.WriteLine("Aaron's order: premium sourcing from day one, then seats, then crew,");
            _out.WriteLine("then the best equipment — buying each only when it is affordable.");
            _out.WriteLine("");
            _out.WriteLine("site        m3cash    m6cash   m12cash   seats units crew  verdict");
            _out.WriteLine("-------------------------------------------------------------------");

            var survived = 0;
            foreach (var site in Sites())
            {
                var cash = PlayLikeAaron(site, out var seats, out var units, out var crew, out var m3, out var m6);
                if (cash > 0m) survived++;

                _out.WriteLine(string.Format("{0,-10} {1,9:N0} {2,9:N0} {3,9:N0} {4,7} {5,5} {6,4}  {7}",
                    site.Key, m3, m6, cash, seats, units, crew, cash > 0m ? "surviving" : "BUST"));
            }

            _out.WriteLine("");
            _out.WriteLine("player-sequence runs surviving twelve months: " + survived + " of 4");
            Assert.True(survived >= 0);
        }

        /// <summary>
        /// THE DECIDING EXPERIMENT. Can the opening bankroll even BUY a build that is known
        /// to make money?
        ///
        /// The sweep says a good static configuration earns on all four sites. Every
        /// incremental policy — naive, Advisor-guided, and the player's own order — busts.
        /// So either no path reaches a good build, or a good build is simply unaffordable
        /// from 30,000 less key money. This buys the sweep's own profitable build on day one
        /// and trades twelve months. If it survives, the configuration is reachable and the
        /// policies were at fault. If it cannot even be bought, the opening capital is the
        /// problem and no advice can fix it.
        /// </summary>
        [Fact(Skip = "Measuring instrument. Remove this Skip to run.")]
        public void CanTheOpeningBankrollEvenBuyAWorkingRestaurant()
        {
            _out.WriteLine("Buying the sweep's profitable build on day one, out of 30,000 less key money.");
            _out.WriteLine("");
            _out.WriteLine("site        wanted    afford   built      m12cash  verdict");
            _out.WriteLine("------------------------------------------------------------");

            foreach (var site in Sites())
            {
                var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
                var bankroll = 30000m - site.Where.LeasePremium;
                var company = new Company("co", "Co", definitions, bankroll);
                var r = company.OpenRestaurant("r", site.Key, LocationType.BrickAndMortar);

                r.Location = site.Where;
                r.FloorArea = site.Where.MaxFloorArea;
                foreach (var recipe in definitions.Recipes) r.Menu.Add(recipe.Id);
                company.SupplierPolicy.AssignAll("valley-produce");
                r.ServiceWindows.Clear();
                foreach (var w in site.Hours) r.ServiceWindows.Add(w);

                // The sweep's shape: four units of each station, forty covers, staffed to match.
                const int Units = 4, Seats = 40, Cooks = 8, Servers = 3;
                var wanted = 0m;
                foreach (var stationId in r.Menu.Recipes.Select(x => x.StationId).Distinct())
                {
                    var model = definitions.EquipmentFor(stationId).FirstOrDefault();
                    if (model != null) wanted += model.Cost * Units;
                }
                wanted += Seats * 120m;

                var built = 0;
                foreach (var stationId in r.Menu.Recipes.Select(x => x.StationId).Distinct())
                {
                    var model = definitions.EquipmentFor(stationId).FirstOrDefault();
                    if (model == null) continue;
                    if (company.Economy.CashOnHand < model.Cost * Units) continue;
                    if (!r.HasRoomFor(model.Footprint * Units)) continue;
                    r.BuyEquipment(model, Units); built++;
                }

                if (company.Economy.CashOnHand > Seats * 120m && r.HasRoomFor(Seats * 15m))
                    r.BuyTables("t", "Tables", Seats * 120m, Seats);

                for (var i = 0; i < Cooks; i++) r.Payroll.Hire(new Employee("c" + i, "Cook", StaffRole.Cook, 16m));
                for (var i = 0; i < Servers; i++) r.Payroll.Hire(new Employee("s" + i, "Server", StaffRole.Server, 12m));
                foreach (var id in definitions.IngredientIds) { r.Inventory.SetPar(id, 300m, 2000m); r.Inventory.Receive(id, 2000m); }

                var clock = new GameClock();
                var runner = new SimulationRunner(r, clock, 4242, InterruptPolicy.None());
                for (var month = 1; month <= 12; month++)
                {
                    runner.AdvanceDays(30);
                    company.Economy.Record(clock.Tick, LedgerCategory.Overhead, site.Where.MonthlyRent, "Rent", r.Id);
                    foreach (var stock in r.Inventory.Items.ToList())
                        if (stock.IsBelowPar) r.Inventory.Receive(stock.IngredientId, stock.SuggestedReorderQuantity);
                }

                var end = company.Economy.CashOnHand;
                _out.WriteLine(string.Format("{0,-10} {1,8:N0} {2,9:N0} {3,4}/{4}  {5,11:N0}  {6}",
                    site.Key, wanted, bankroll, built,
                    r.Menu.Recipes.Select(x => x.StationId).Distinct().Count(),
                    end, end > 0m ? "surviving" : "BUST"));
            }
        }

        private static decimal PlayLikeAaron(Site site, out int seats, out int units, out int crew,
            out decimal m3, out decimal m6)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("co", "Co", definitions, 30000m - site.Where.LeasePremium);
            var r = company.OpenRestaurant("r", "The " + site.Key, LocationType.BrickAndMortar);

            r.Location = site.Where;
            r.FloorArea = 970m;

            foreach (var recipe in definitions.Recipes) r.Menu.Add(recipe.Id);
            company.SupplierPolicy.AssignAll("premium-harvest");   // best ingredients, day one

            r.ServiceWindows.Clear();
            foreach (var w in site.Hours) r.ServiceWindows.Add(w);

            foreach (var stationId in r.Menu.Recipes.Select(x => x.StationId).Distinct())
            {
                var cheapest = definitions.EquipmentFor(stationId).FirstOrDefault();
                if (cheapest != null && r.HasRoomFor(cheapest.Footprint)) r.BuyEquipment(cheapest, 1);
            }
            r.BuyTables("t0", "Tables", 12 * 120m, 12);
            r.Payroll.Hire(new Employee("c0", "Cook", StaffRole.Cook, 16m));
            r.Payroll.Hire(new Employee("s0", "Server", StaffRole.Server, 12m));
            foreach (var id in definitions.IngredientIds) { r.Inventory.SetPar(id, 120m, 900m); r.Inventory.Receive(id, 900m); }

            var clock = new GameClock();
            var runner = new SimulationRunner(r, clock, 4242, InterruptPolicy.None());
            m3 = 0m; m6 = 0m;

            for (var month = 1; month <= 12; month++)
            {
                runner.AdvanceDays(30);
                company.Economy.Record(clock.Tick, LedgerCategory.Overhead, site.Where.MonthlyRent, "Rent", r.Id);

                foreach (var stock in r.Inventory.Items.ToList())
                    if (stock.IsBelowPar) r.Inventory.Receive(stock.IngredientId, stock.SuggestedReorderQuantity);

                var cash = company.Economy.CashOnHand;
                var keep = site.Where.MonthlyRent * 2m;   // he was not spending to the last dollar

                // 1. SEATS, while there is floor and money.
                while (cash - keep > 1200m && r.HasRoomFor(150m))
                {
                    r.BuyTables("t" + month + "-" + r.SeatingCapacity, "Tables", 1200m, 10);
                    cash = company.Economy.CashOnHand;
                }

                // 2. TEAM to match the room and the pass.
                while (r.SeatingCapacity > r.Payroll.CountOf(StaffRole.Server) * 14 && cash - keep > 3000m)
                { r.Payroll.Hire(new Employee("s" + Guid.NewGuid().ToString("N").Substring(0,4), "Server", StaffRole.Server, 12m)); cash = company.Economy.CashOnHand; }

                var kitchenUnits = r.Kitchen.Stations.Sum(x => x.ConcurrentCapacity);
                while (r.Payroll.CountOf(StaffRole.Cook) * KitchenPass.PlatesPerCook < kitchenUnits && cash - keep > 4000m)
                { r.Payroll.Hire(new Employee("c" + Guid.NewGuid().ToString("N").Substring(0,4), "Cook", StaffRole.Cook, 16m)); cash = company.Economy.CashOnHand; }

                // 3. BEST EQUIPMENT, once the room is paying for it.
                foreach (var station in r.Kitchen.Stations.ToList())
                {
                    var best = definitions.EquipmentFor(station.Id)
                        .Where(e => e.SpeedMultiplier > station.SpeedMultiplier)
                        .OrderByDescending(e => e.SpeedMultiplier).FirstOrDefault();

                    if (best != null && cash - keep > best.Cost * station.ConcurrentCapacity)
                    {
                        try { r.BuyEquipment(best, station.ConcurrentCapacity); cash = company.Economy.CashOnHand; }
                        catch (InvalidOperationException) { }
                    }
                }

                if (month == 3) m3 = company.Economy.CashOnHand;
                if (month == 6) m6 = company.Economy.CashOnHand;
            }

            seats = r.SeatingCapacity;
            units = r.Kitchen.Stations.Sum(x => x.ConcurrentCapacity);
            crew = r.Payroll.Headcount;
            return company.Economy.CashOnHand;
        }

        private static decimal Play(Site site, IDictionary<string, int> taken,
            out int seats, out int units, out int crew, out decimal m3, out decimal m6)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("co", "Co", definitions, 30000m - site.Where.LeasePremium);
            var r = company.OpenRestaurant("r", "The " + site.Key, LocationType.BrickAndMortar);

            r.Location = site.Where;
            r.FloorArea = 970m;

            foreach (var recipe in definitions.Recipes) r.Menu.Add(recipe.Id);
            company.SupplierPolicy.AssignAll("valley-produce");

            r.ServiceWindows.Clear();
            foreach (var w in site.Hours) r.ServiceWindows.Add(w);

            // The same minimal opening the naive campaign gets: the cheapest thing that can
            // cook each dish on the card, twelve covers, and two members of staff.
            foreach (var stationId in r.Menu.Recipes.Select(x => x.StationId).Distinct())
            {
                var cheapest = definitions.EquipmentFor(stationId).FirstOrDefault();
                if (cheapest != null && r.HasRoomFor(cheapest.Footprint)) r.BuyEquipment(cheapest, 1);
            }
            r.BuyTables("t0", "Tables", 12 * 120m, 12);
            r.Payroll.Hire(new Employee("c0", "Cook", StaffRole.Cook, 16m));
            r.Payroll.Hire(new Employee("s0", "Server", StaffRole.Server, 12m));

            foreach (var id in definitions.IngredientIds) { r.Inventory.SetPar(id, 120m, 900m); r.Inventory.Receive(id, 900m); }

            var clock = new GameClock();
            var runner = new SimulationRunner(r, clock, 4242, InterruptPolicy.None());

            m3 = 0m; m6 = 0m;

            for (var month = 1; month <= 12; month++)
            {
                runner.AdvanceDays(30);
                var trading = runner.Snapshot();

                company.Economy.Record(clock.Tick, LedgerCategory.Overhead,
                    site.Where.MonthlyRent, "Rent", r.Id);

                // ---- the entire policy: do what the Advisor says, and nothing else ----
                foreach (var s in new Advisor(r).Review(trading))
                    Act(company, r, definitions, s, taken);

                if (month == 3) m3 = company.Economy.CashOnHand;
                if (month == 6) m6 = company.Economy.CashOnHand;
            }

            seats = r.SeatingCapacity;
            units = 0;
            foreach (var st in r.Kitchen.Stations) units += st.ConcurrentCapacity;
            crew = r.Payroll.Headcount;

            return company.Economy.CashOnHand;
        }

        /// <summary>
        /// Carry out one suggestion, taking the Advisor at its word. Nothing here decides
        /// anything on its own — if the Advisor does not raise it, it does not happen, which
        /// is what makes the result a measurement of the ADVICE rather than of a heuristic
        /// wearing the Advisor's name.
        /// </summary>
        private static void Act(Company company, Restaurant r, DefinitionRegistry definitions,
            Suggestion s, IDictionary<string, int> taken)
        {
            var cash = company.Economy.CashOnHand;
            var id = s.Id;

            void Count(string k) { int n; taken.TryGetValue(k, out n); taken[k] = n + 1; }

            if (id.StartsWith("restock:"))
            {
                var stock = r.Inventory.Items.FirstOrDefault(x => x.IngredientId == s.SubjectId);
                if (stock != null) { r.Inventory.Receive(stock.IngredientId, stock.SuggestedReorderQuantity); Count("restock"); }
                return;
            }

            if (id == "understaffed:kitchen" && cash > 4000m)
            {
                r.Payroll.Hire(new Employee("c" + Guid.NewGuid().ToString("N").Substring(0, 4), "Cook", StaffRole.Cook, 16m));
                Count("hire cook");
                return;
            }

            if (id == "understaffed:floor" && cash > 3000m)
            {
                r.Payroll.Hire(new Employee("s" + Guid.NewGuid().ToString("N").Substring(0, 4), "Server", StaffRole.Server, 12m));
                Count("hire server");
                return;
            }

            if (id.StartsWith("feature:")) { r.Menu.Feature(s.SubjectId); Count("feature"); return; }

            // "We're turning away more trade than we're keeping" — and it names the station.
            if (id == "opportunity:capacity" && s.SubjectId != null)
            {
                var model = definitions.EquipmentFor(s.SubjectId)
                    .OrderBy(e => e.Cost)
                    .FirstOrDefault(e => e.Cost <= cash * 0.5m && r.HasRoomFor(e.Footprint));

                if (model != null)
                {
                    var station = r.Kitchen.Get(s.SubjectId);
                    var to = (station == null ? 0 : station.ConcurrentCapacity) + 1;
                    try { r.BuyEquipment(model, to); Count("more kitchen"); } catch (InvalidOperationException) { }
                }
                return;
            }

            if (id == "opportunity:room" && s.SubjectId == "seats")
            {
                // Ten covers at a time, the same increment the harness offers a player.
                if (cash > 2000m && r.HasRoomFor(150m))
                {
                    try
                    {
                        r.BuyTables("t" + Guid.NewGuid().ToString("N").Substring(0, 4),
                            "More tables", 1200m, 10);
                        Count("more covers");
                    }
                    catch (InvalidOperationException) { }
                }
                return;
            }

            if (id == "opportunity:space" && s.Price.HasValue)
            {
                var buy = 215m;
                if (cash > buy * s.Price.Value * 3m && r.ExpansionHeadroom >= buy)
                { r.ExtendBuilding(buy); Count("extend"); }
                return;
            }

            if (id == "opportunity:upgrade")
            {
                // Built out to the limit; the only throughput left is a better machine in the
                // same footprint. Upgrade whichever station is busiest that we can afford.
                foreach (var station in r.Kitchen.Stations.OrderByDescending(x => x.ConcurrentCapacity))
                {
                    var better = definitions.EquipmentFor(station.Id)
                        .Where(e => e.SpeedMultiplier > station.SpeedMultiplier && e.Cost <= cash * 0.4m)
                        .OrderByDescending(e => e.SpeedMultiplier)
                        .FirstOrDefault();

                    if (better != null)
                    {
                        try { r.BuyEquipment(better, station.ConcurrentCapacity); Count("upgrade"); return; }
                        catch (InvalidOperationException) { }
                    }
                }
            }
        }
    }
}
