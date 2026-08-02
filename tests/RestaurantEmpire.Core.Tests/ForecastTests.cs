using System;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;
using Xunit.Abstractions;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// The forecast is only worth anything if it is roughly right about a night it has never
    /// seen, and honest about which of the three ceilings is holding the restaurant back.
    /// Those are the two claims worth pinning; the exact numbers are not.
    /// </summary>
    public class ForecastTests
    {
        private readonly ITestOutputHelper _out;
        public ForecastTests(ITestOutputHelper o) { _out = o; }

        private static Restaurant Build(int seats, int cooks, int units)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("co", "Co", definitions, 400000m);
            var r = company.OpenRestaurant("r", "R", LocationType.BrickAndMortar);

            r.Location = Neighborhood.SuburbanHighStreet();
            r.FloorArea = r.Location.MaxFloorArea;
            r.Menu.Add(new[] { "margherita", "house-focaccia", "caprese-salad" });
            company.SupplierPolicy.AssignAll("valley-produce");
            r.Reputation.Restore(Reputation.Neutral, Reputation.MealsToBecomeKnown);

            r.ServiceWindows.Clear();
            r.ServiceWindows.Add(new ServiceWindow("Dinner", 18, 23));

            foreach (var stationId in r.Menu.Recipes.Select(x => x.StationId).Distinct())
            {
                var model = definitions.EquipmentFor(stationId).FirstOrDefault();
                if (model != null && r.HasRoomFor(model.Footprint * units)) r.BuyEquipment(model, units);
            }

            if (r.HasRoomFor(seats * 15m)) r.BuyTables("t", "Tables", seats * 120m, seats);

            for (var i = 0; i < cooks; i++)
                r.Payroll.Hire(new Employee("c" + i, "Cook", StaffRole.Cook, 16m, 0.5m));
            for (var i = 0; i < 3; i++)
                r.Payroll.Hire(new Employee("s" + i, "Server", StaffRole.Server, 12m, 0.5m));

            foreach (var recipe in r.Menu.Recipes)
                foreach (var line in recipe.Ingredients) { r.Inventory.SetPar(line.IngredientId, 40m, 900m); r.Inventory.Receive(line.IngredientId, 120m); }

            return r;
        }

        /// <summary>
        /// THE BAR: the forecast must land near a night it has not seen. It is a projection of
        /// expected values against a simulation that rolls dice, so it will never be exact —
        /// but if it were wildly off it would be worse than useless, because a player would
        /// plan against it.
        /// </summary>
        [Fact]
        public void TheForecastLandsNearTheNightItPredicts()
        {
            // Across shapes AND seeds, because a forecast that happens to be right about one
            // restaurant on one night has told us nothing. These deliberately span all three
            // constraints: demand-bound, seat-bound and kitchen-bound.
            var shapes = new[]
            {
                new { Seats = 40, Cooks = 4, Units = 3, Label = "balanced" },
                new { Seats = 12, Cooks = 5, Units = 4, Label = "tiny room" },
                new { Seats = 60, Cooks = 2, Units = 1, Label = "short kitchen" },
                new { Seats = 30, Cooks = 3, Units = 2, Label = "modest" },
            };

            var errors = new System.Collections.Generic.List<decimal>();

            foreach (var shape in shapes)
            {
                for (var seed = 0; seed < 3; seed++)
                {
                    var r = Build(shape.Seats, shape.Cooks, shape.Units);
                    var clock = new GameClock();
                    var forecast = ServiceForecast.ForDay(r, clock.Now);

                    var runner = new SimulationRunner(r, clock, 4242 + seed * 977, InterruptPolicy.None());
                    runner.AdvanceDays(1);
                    var actual = runner.Snapshot();

                    var autopsy = new ServiceAutopsy(forecast, actual);
                    errors.Add(autopsy.CoverError);

                    if (seed == 0)
                        _out.WriteLine($"{shape.Label,-14} forecast {forecast.Covers,6:N0} actual {actual.CoversServed,5}  " +
                                       $"({autopsy.CoverError:P0} out, bound by {forecast.Constraint})");
                }
            }

            errors.Sort();
            var median = errors[errors.Count / 2];
            var worst = errors[errors.Count - 1];
            _out.WriteLine("");
            _out.WriteLine($"median error {median:P0}, worst {worst:P0}, across {errors.Count} nights");

            // A projection of expected values against a simulation that rolls dice will never
            // be exact, and should not be — the gap is the information. But a player plans
            // against this, so being consistently far out would make it worse than useless.
            Assert.True(median < 0.25m, $"median forecast error was {median:P0}");
            Assert.True(worst < 0.60m, $"worst forecast error was {worst:P0}");
        }

        /// <summary>
        /// A tiny dining room in front of a big kitchen must report SEATS, and a big room in
        /// front of one cook must report KITCHEN. These have opposite fixes, and buying the
        /// wrong one is exactly the mistake Aaron made playing — "I bought a ton of ovens and
        /// kept getting backed up".
        /// </summary>
        [Fact]
        public void TheForecastNamesWhichCeilingIsActuallyBinding()
        {
            var cramped = ServiceForecast.ForDay(Build(seats: 8, cooks: 6, units: 5), new GameClock().Now);
            var shortStaffed = ServiceForecast.ForDay(Build(seats: 60, cooks: 1, units: 5), new GameClock().Now);

            _out.WriteLine("8 seats, 6 cooks:  " + cramped.Reads);
            _out.WriteLine("60 seats, 1 cook:  " + shortStaffed.Reads);

            Assert.Equal("seats", cramped.Constraint);
            Assert.Equal("kitchen", shortStaffed.Constraint);
        }

        /// <summary>
        /// The autopsy must stay quiet when the night went to plan. One that always has an
        /// opinion stops being read — the same standard the Advisor is held to.
        /// </summary>
        [Fact]
        public void AnUneventfulNightGetsNoPostMortem()
        {
            var r = Build(seats: 40, cooks: 4, units: 3);
            var clock = new GameClock();
            var forecast = ServiceForecast.ForDay(r, clock.Now);

            var runner = new SimulationRunner(r, clock, 4242, InterruptPolicy.None());
            runner.AdvanceDays(1);

            var autopsy = new ServiceAutopsy(forecast, runner.Snapshot());

            if (autopsy.AsExpected)
            {
                Assert.Empty(autopsy.Surprises);
                Assert.Equal("The night went roughly to plan.", autopsy.Headline);
            }
            else
            {
                // If it WAS off, it owes the player a reason rather than a bare percentage.
                Assert.NotEmpty(autopsy.Surprises);
                _out.WriteLine(autopsy.Headline);
            }
        }

        /// <summary>
        /// The forecast must MOVE when the restaurant changes, or it is a decoration. Raising
        /// every price should visibly thin the crowd it expects, because price now decides who
        /// sets off in the first place.
        /// </summary>
        [Fact]
        public void RaisingPricesLowersWhatTheForecastExpects()
        {
            var cheap = Build(seats: 40, cooks: 4, units: 3);
            var dear = Build(seats: 40, cooks: 4, units: 3);

            foreach (var id in dear.Menu.RecipeIds) dear.Company.Pricing.AdjustPrice(id, 1.8m);

            var atFairPrices = ServiceForecast.ForDay(cheap, new GameClock().Now);
            var atHighPrices = ServiceForecast.ForDay(dear, new GameClock().Now);

            _out.WriteLine($"fair: {atFairPrices.DemandCovers:N0} covers of demand");
            _out.WriteLine($"dear: {atHighPrices.DemandCovers:N0} covers of demand");

            Assert.True(atHighPrices.DemandCovers < atFairPrices.DemandCovers,
                "a dearer menu should expect fewer people to set off");
        }
    }
}
