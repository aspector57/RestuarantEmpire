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
    /// IS A SECOND RESTAURANT A NEW DECISION, OR JUST A BIGGER NUMBER?
    ///
    /// Aaron's day-6,994 run settled that the game has no cash sink: nineteen years, twelve
    /// seats, $2.4M, and nothing to spend it on. Expansion is the answer to that — but only
    /// if it passes the project's own anti-pattern test. "Flat scaling: bigger numbers are
    /// not new decisions. Scale must add new KINDS of tradeoff."
    ///
    /// So this measures before anything is built for it. If two restaurants is arithmetically
    /// twice one restaurant, the feature is a spreadsheet with a theme and needs the Region
    /// sourcing tier (and whatever else) to become a decision. If the sites genuinely
    /// interact, expansion is already interesting and the work is to surface it.
    ///
    /// Nothing here is a test of correctness. It is a measuring instrument, like Sweep.
    /// </summary>
    public class SecondRestaurant
    {
        private readonly ITestOutputHelper _out;
        public SecondRestaurant(ITestOutputHelper o) { _out = o; }

        private static readonly string[] Card = { "margherita", "house-focaccia", "caprese-salad" };

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

        /// <summary>Fits out one site to the same shape every time, and bills the company for it.</summary>
        private static Restaurant Open(Company company, string id, string siteKey, int ovens, int seats, int cooks,
            Region region = null)
        {
            var site = SiteFor(siteKey);
            var r = company.OpenRestaurant(id, site.Name, LocationType.BrickAndMortar, region);

            r.Location = site;
            r.FloorArea = Math.Min(900m, site.MaxFloorArea);

            foreach (var dish in Card) r.Menu.Add(dish);

            r.ServiceWindows.Clear();
            r.ServiceWindows.Add(new ServiceWindow("Dinner", 18, 23));

            var definitions = company.Definitions;
            r.BuyEquipment(definitions.EquipmentFor("oven").First(x => x.Id == "oven-secondhand"), ovens);
            r.BuyEquipment(definitions.EquipmentFor("garde-manger").First(x => x.Id == "gm-refrigerated"), 2);
            r.BuyEquipment(definitions.EquipmentFor("cold-storage").First(x => x.Id == "cold-walkin"), 1);
            r.BuyEquipment(definitions.EquipmentFor("dry-storage").First(x => x.Id == "dry-stockroom"), 1);

            r.BuyTables("t", "Tables", seats * 120m, seats);

            for (var i = 0; i < cooks; i++) r.Payroll.Hire(new Employee(id + "-c" + i, "Cook", StaffRole.Cook, 16m));
            for (var i = 0; i < 2; i++) r.Payroll.Hire(new Employee(id + "-s" + i, "Server", StaffRole.Server, 12m));

            foreach (var ing in r.Menu.Recipes.SelectMany(x => x.Ingredients).Select(x => x.IngredientId).Distinct())
            {
                r.Inventory.SetPar(ing, 60m, 400m);
                r.Inventory.Receive(ing, 150m);
            }

            return r;
        }

        private sealed class Result
        {
            public decimal Revenue, Food, Labor, Rent;
            public int Covers, TurnedAway;
            public decimal Net { get { return Revenue - Food - Labor - Rent; } }
        }

        /// <summary>
        /// Trades every restaurant in the company on ONE clock, for the same days. Each site
        /// gets its own runner and its own seed offset, because two restaurants that share a
        /// random sequence are not two restaurants.
        /// </summary>
        private Result TradeAll(Company company, int days, long seed)
        {
            var clocks = new List<SimulationRunner>();
            var i = 0;

            foreach (var r in company.Restaurants)
            {
                var clock = new GameClock();
                clock.AdvanceHours(18);
                clocks.Add(new SimulationRunner(r, clock, seed + (i++ * 7919L), InterruptPolicy.None()));
            }

            foreach (var runner in clocks) runner.Advance((long)days * GameClock.TicksPerDay);

            var total = new Result();
            for (var k = 0; k < clocks.Count; k++)
            {
                var m = clocks[k].Snapshot();
                total.Revenue += m.Revenue;
                total.Food += m.FoodCost;
                total.Labor += m.LaborCost;
                total.Covers += m.CoversServed;
                total.TurnedAway += m.PartiesTurnedAway;
                total.Rent += company.Restaurants[k].Location.MonthlyRent * (days / 30m);
            }

            return total;
        }

        private Result Portfolio(int days, long seed, params (string Site, int Ovens, int Seats, int Cooks)[] sites)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("c", "Group", definitions, 400000m);
            company.SupplierPolicy.AssignAll("valley-produce");

            var n = 0;
            foreach (var s in sites) Open(company, "r" + (n++), s.Site, s.Ovens, s.Seats, s.Cooks);

            return TradeAll(company, days, seed);
        }

        [Fact(Skip = "Measuring instrument, not a test. Run by removing this Skip.")]
        public void IsTheSecondSiteANewDecisionOrABiggerNumber()
        {
            const int days = 180;
            const long seed = 20240802L;

            _out.WriteLine("ONE RESTAURANT AGAINST TWO — " + days + " days, identical fit-out per site");
            _out.WriteLine("");
            _out.WriteLine(string.Format("{0,-34} {1,12} {2,10} {3,12}", "portfolio", "net", "covers", "turned away"));

            var suburban = Portfolio(days, seed, ("suburban", 3, 24, 2));
            Show("suburban alone", suburban);

            var city = Portfolio(days, seed, ("city", 3, 24, 2));
            Show("city alone", city);

            var both = Portfolio(days, seed, ("suburban", 3, 24, 2), ("city", 3, 24, 2));
            Show("suburban + city", both);

            var twoSuburban = Portfolio(days, seed, ("suburban", 3, 24, 2), ("suburban", 3, 24, 2));
            Show("suburban + suburban", twoSuburban);

            var four = Portfolio(days, seed,
                ("suburban", 3, 24, 2), ("city", 3, 24, 2), ("business", 3, 24, 2), ("nightlife", 3, 24, 2));
            Show("all four sites", four);

            _out.WriteLine("");
            _out.WriteLine("=== IS IT FLAT SCALING? ===");

            var expected = suburban.Net + city.Net;
            _out.WriteLine("suburban alone + city alone   = " + expected.ToString("N0"));
            _out.WriteLine("the two of them together      = " + both.Net.ToString("N0"));
            _out.WriteLine("difference                    = " + (both.Net - expected).ToString("N0") +
                           "  (" + (expected == 0 ? 0 : (both.Net - expected) / Math.Abs(expected)).ToString("P1") + ")");
            _out.WriteLine("");
            _out.WriteLine("If that difference is ~0, a second restaurant is ARITHMETIC and the feature");
            _out.WriteLine("needs something — regional sourcing, shared attention, a demand pool — to");
            _out.WriteLine("become a decision. 'Bigger numbers are not new decisions.'");
            _out.WriteLine("");
            _out.WriteLine("Two of the SAME site: " + twoSuburban.Net.ToString("N0") +
                           " against " + (suburban.Net * 2).ToString("N0") + " for twice one.");
            _out.WriteLine("If those match too, sites do not compete for the same street either.");
        }

        /// <summary>
        /// DOES THE NATIONAL CONTRACT MAKE SCALE A DIFFERENT DECISION?
        ///
        /// The measurement above says two restaurants is 0.4% away from arithmetic. This is
        /// the answer to that: a distributor who will not deal with you until you are big
        /// enough, and whose goods land three days into their life because they came through
        /// a depot. Cheaper per unit, a grade lower, and older on arrival.
        ///
        /// The test that matters is not "is it cheaper" — it is whether the right answer
        /// DEPENDS on something. If national wins everywhere it is a free upgrade; if it
        /// loses everywhere it is dead content. It should win on a card built from things
        /// that keep, and lose on one built from fish.
        /// </summary>
        [Fact(Skip = "Measuring instrument, not a test. Run by removing this Skip.")]
        public void DoesGoingNationalPayAndWhenDoesItNot()
        {
            const int days = 180;
            const long seed = 20240802L;

            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var national = definitions.GetSupplier("atlantic-national");

            _out.WriteLine("THE GATE — who can even open the account?");
            foreach (var count in new[] { 1, 2, 4, 6 })
            {
                var company = new Company("c", "Group", definitions, 800000m);
                company.SupplierPolicy.AssignAll("valley-produce");
                var region = company.CreateRegion("east", "East Region");

                for (var i = 0; i < count; i++) Open(company, "r" + i, "suburban", 3, 24, 2, region);

                TradeAll(company, 30, seed);   // trade first: the gate is on usage, not stock

                _out.WriteLine(string.Format("  {0} restaurant{1}: {2,6:N0} units/week — {3}",
                    count, count == 1 ? " " : "s", region.WeeklyVolume,
                    region.CanContractWith(national) ? "ACCOUNT OPEN" : "refused"));
            }

            _out.WriteLine("");
            _out.WriteLine("AND IS IT WORTH TAKING? four restaurants, " + days + " days");
            _out.WriteLine(string.Format("  {0,-38} {1,12} {2,10} {3,10}", "", "net", "covers", "standing"));

            foreach (var menu in new[] { "stable", "perishable" })
            {
                foreach (var supplier in new[] { "valley-produce", "atlantic-national" })
                {
                    var company = new Company("c", "Group", definitions, 800000m);
                    company.SupplierPolicy.AssignAll(supplier);
                    var region = company.CreateRegion("east", "East Region");

                    for (var i = 0; i < 4; i++)
                    {
                        var r = Open(company, "r" + i, "suburban", 3, 24, 2, region);

                        if (menu != "perishable") continue;

                        // Swap the card onto things that do not keep, and stock for it.
                        r.Menu.Remove("house-focaccia");
                        r.Menu.Add("sea-bass");
                        foreach (var ing in r.Menu.Recipes.SelectMany(x => x.Ingredients)
                                             .Select(x => x.IngredientId).Distinct())
                        {
                            r.Inventory.SetPar(ing, 60m, 400m);
                            r.Inventory.Receive(ing, 150m);
                        }
                    }

                    var result = TradeAll(company, days, seed);
                    _out.WriteLine(string.Format("  {0,-38} {1,12:N0} {2,10:N0} {3,10:N0}",
                        menu + " card, " + (supplier == "atlantic-national" ? "NATIONAL" : "local"),
                        result.Net, result.Covers,
                        company.Restaurants[0].Reputation.Standing * 100m));
                }
            }
        }

        /// <summary>
        /// DOES OPENING NEXT DOOR TO YOURSELF NOW COST SOMETHING?
        ///
        /// Before cannibalization, two suburban sites earned 120,858 against 119,600 for twice
        /// one restaurant — opening a clone next door was free. A street holds a finite number
        /// of people and your own second restaurant drinks from the same well.
        ///
        /// The bar is not just "two clones earn less". It is that SPREADING OUT beats
        /// CLUSTERING, because that is what turns a portfolio into a decision instead of a
        /// purchase order.
        /// </summary>
        [Fact(Skip = "Measuring instrument, not a test. Run by removing this Skip.")]
        public void DoesClusteringCostYouAnything()
        {
            const int days = 180;
            const long seed = 20240802L;

            var one = Portfolio(days, seed, ("suburban", 3, 24, 2));
            var clustered = Portfolio(days, seed, ("suburban", 3, 24, 2), ("suburban", 3, 24, 2));
            var spread = Portfolio(days, seed, ("suburban", 3, 24, 2), ("city", 3, 24, 2));

            _out.WriteLine("ONE SITE, TWO ON THE SAME STREET, TWO ON DIFFERENT STREETS — " + days + " days");
            _out.WriteLine("");
            Show("one suburban restaurant", one);
            Show("twice that, on paper", new Result { Revenue = one.Revenue * 2, Food = one.Food * 2,
                                                      Labor = one.Labor * 2, Rent = one.Rent * 2,
                                                      Covers = one.Covers * 2 });
            Show("two, both suburban (clustered)", clustered);
            Show("two, suburban + city (spread)", spread);

            _out.WriteLine("");
            _out.WriteLine("clustering against twice-one : " + (clustered.Net - one.Net * 2).ToString("N0"));
            _out.WriteLine("spreading against clustering : " + (spread.Net - clustered.Net).ToString("N0"));
            _out.WriteLine("");
            _out.WriteLine("If clustering now LOSES against twice-one, a street is finite. If spreading");
            _out.WriteLine("beats clustering, where you put the next one is a decision worth making.");
        }

        private void Show(string label, Result r)
        {
            _out.WriteLine(string.Format("{0,-34} {1,12:N0} {2,10:N0} {3,12:N0}",
                label, r.Net, r.Covers, r.TurnedAway));
        }
    }
}
