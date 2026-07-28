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
    /// Temporary instrument, like <see cref="Sweep"/>. Where the sweep asks "is this
    /// CONFIGURATION viable?", this asks the question that actually decides difficulty:
    /// starting from the real opening position, with real opening cash, can you grow — and
    /// how much slack do you have while you try?
    /// </summary>
    public class Campaign
    {
        private readonly ITestOutputHelper _out;
        public Campaign(ITestOutputHelper o) { _out = o; }

        private static ServiceWindow[] HoursFor(string s)
        {
            switch (s)
            {
                case "city": return new[] { new ServiceWindow("Lunch", 12, 15), new ServiceWindow("Dinner", 18, 23) };
                case "business": return new[] { new ServiceWindow("Breakfast", 7, 10), new ServiceWindow("Lunch", 12, 15) };
                case "nightlife": return new[] { new ServiceWindow("Dinner", 18, 23), new ServiceWindow("Late", 23, 2) };
                default: return new[] { new ServiceWindow("Dinner", 18, 23) };
            }
        }

        private static Neighborhood SiteFor(string k)
        {
            switch (k)
            {
                case "city": return Neighborhood.CityCenter();
                case "business": return Neighborhood.BusinessDistrict();
                case "nightlife": return Neighborhood.NightlifeQuarter();
                default: return Neighborhood.SuburbanHighStreet();
            }
        }

        [Fact(Skip = "Measuring instrument. Remove this Skip to run.")]
        public void TwelveMonthsFromTheRealStartingPosition()
        {
            const decimal Bankroll = 30000m;

            _out.WriteLine("Starting position: 30,000 bankroll, one unit of each station, 12 covers,");
            _out.WriteLine("1 cook + 1 server, reinvesting greedily whenever affordable.");
            _out.WriteLine("");
            _out.WriteLine("site        m1cash    m3cash    m6cash   m12cash   seats  units  crew   verdict");
            _out.WriteLine("---------------------------------------------------------------------------------");

            foreach (var siteKey in new[] { "city", "business", "nightlife", "suburban" })
            {
                var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
                var site = SiteFor(siteKey);
                var company = new Company("c", "C", definitions, Bankroll);
                var r = company.OpenRestaurant("s", site.Name, LocationType.BrickAndMortar);

                r.Location = site;
                r.FloorArea = 90m;
                company.Economy.Record(0, LedgerCategory.CapitalExpenditure, site.LeasePremium, "Key money", r.Id);

                foreach (var recipe in definitions.Recipes) r.Menu.Add(recipe.Id);
                company.SupplierPolicy.AssignAll("valley-produce");

                r.ServiceWindows.Clear();
                foreach (var w in HoursFor(siteKey)) r.ServiceWindows.Add(w);

                foreach (var stationId in r.Menu.Recipes.Select(x => x.StationId).Distinct())
                {
                    var model = definitions.EquipmentFor(stationId).FirstOrDefault();
                    if (model != null) r.BuyEquipment(model, 1);
                }

                r.BuyTables("t0", "Tables", 12 * 120m, 12);
                r.Payroll.Hire(new Employee("c0", "Cook", StaffRole.Cook, 16m));
                r.Payroll.Hire(new Employee("s0", "Server", StaffRole.Server, 12m));

                foreach (var id in definitions.IngredientIds) { r.Inventory.SetPar(id, 300m, 2000m); r.Inventory.Receive(id, 2000m); }

                var runner = new SimulationRunner(r, new GameClock(), 4242, InterruptPolicy.None());
                var marks = new Dictionary<int, decimal>();
                var lastBooked = runner.Snapshot();

                for (var month = 1; month <= 12; month++)
                {
                    for (var day = 0; day < 30; day++)
                    {
                        runner.AdvanceDays(1);
                        foreach (var stock in r.Inventory.Items.ToList())
                            if (stock.IsBelowPar) r.Inventory.Receive(stock.IngredientId, stock.SuggestedReorderQuantity);
                    }

                    // book the month
                    var now = runner.Snapshot();
                    var tick = runner.Clock.Tick;
                    company.Economy.Record(tick, LedgerCategory.Revenue, now.Revenue - lastBooked.Revenue, "Takings", r.Id);
                    company.Economy.Record(tick, LedgerCategory.FoodCost, now.FoodCost - lastBooked.FoodCost, "Ingredients", r.Id);
                    company.Economy.Record(tick, LedgerCategory.LaborCost, now.LaborCost - lastBooked.LaborCost, "Wages", r.Id);
                    company.Economy.Record(tick, LedgerCategory.Overhead, site.MonthlyRent, "Rent", r.Id);
                    lastBooked = now;

                    // reinvest greedily: keep two months of rent as a buffer, spend the rest
                    var buffer = site.MonthlyRent * 2m;
                    var spendable = company.Economy.CashOnHand - buffer;

                    while (spendable > 0m)
                    {
                        var boughtSomething = false;

                        // a cook first if the kitchen is under-manned, then capacity
                        var units = r.Kitchen.Stations.Sum(s => s.ConcurrentCapacity);
                        if (r.Payroll.CountOf(StaffRole.Cook) * KitchenPass.PlatesPerCook < units && spendable > 3000m)
                        {
                            r.Payroll.Hire(new Employee("c" + Guid.NewGuid().ToString("N").Substring(0, 4), "Cook", StaffRole.Cook, 16m));
                            spendable -= 3000m; boughtSomething = true;   // reserve a month of their wages
                        }
                        else if (r.SeatingCapacity > r.ServableSeats && spendable > 2200m)
                        {
                            r.Payroll.Hire(new Employee("s" + Guid.NewGuid().ToString("N").Substring(0, 4), "Server", StaffRole.Server, 12m));
                            spendable -= 2200m; boughtSomething = true;
                        }
                        else
                        {
                            var oven = definitions.GetEquipment("oven-commercial");
                            if (r.HasRoomFor(oven.Footprint) && spendable > oven.Cost)
                            {
                                try { r.BuyEquipment(oven, r.Kitchen.Get("oven").ConcurrentCapacity + 1, tick); spendable -= oven.Cost; boughtSomething = true; }
                                catch (InvalidOperationException) { }
                            }
                            else if (r.HasRoomFor(14m) && spendable > 1200m)
                            {
                                r.BuyTables("t" + month + Guid.NewGuid().ToString("N").Substring(0, 4), "More tables", 1200m, 10, 0.55m, tick);
                                spendable -= 1200m; boughtSomething = true;
                            }
                            else if (r.ExpansionHeadroom >= 20m && spendable > 20m * site.ExtensionCostPerSquareMeter)
                            {
                                r.ExtendBuilding(20m, tick); spendable -= 20m * site.ExtensionCostPerSquareMeter; boughtSomething = true;
                            }
                        }

                        if (!boughtSomething) break;
                    }

                    if (month == 1 || month == 3 || month == 6 || month == 12)
                        marks[month] = company.Economy.CashOnHand;
                }

                var verdict = company.Economy.CashOnHand > 60000m ? "thriving"
                    : company.Economy.CashOnHand > 15000m ? "comfortable"
                    : company.Economy.CashOnHand > 0m ? "surviving" : "BUST";

                _out.WriteLine(string.Format("{0,-10}{1,9:N0}{2,10:N0}{3,10:N0}{4,10:N0}{5,8}{6,7}{7,6}   {8}",
                    siteKey, marks[1], marks[3], marks[6], marks[12],
                    r.SeatingCapacity, r.Kitchen.Stations.Sum(s => s.ConcurrentCapacity),
                    r.Payroll.Headcount, verdict));
            }
        }
    }
}
