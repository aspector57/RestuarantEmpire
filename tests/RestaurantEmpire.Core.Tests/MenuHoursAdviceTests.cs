using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;
using Xunit.Abstractions;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// A menu that does not cover the hours you open is paid for twice — in the parties who
    /// walk, and in the stock that rots waiting for a service it was never going to sell in.
    /// Measured, it is what costs Fine Dining its own best market: in a nightlife quarter its
    /// card scores the highest appeal in the game and it still loses, because the late service
    /// has nothing late-appropriate on it.
    /// </summary>
    public class MenuHoursAdviceTests
    {
        private readonly ITestOutputHelper _out;
        public MenuHoursAdviceTests(ITestOutputHelper o) { _out = o; }

        private static Restaurant Open(string[] menu, params ServiceWindow[] windows)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("co", "Co", definitions, 400000m);
            var r = company.OpenRestaurant("r", "R", LocationType.BrickAndMortar);

            r.Location = Neighborhood.NightlifeQuarter();
            r.FloorArea = r.Location.MaxFloorArea;
            r.Menu.Add(menu);
            company.SupplierPolicy.AssignAll("valley-produce");
            r.Reputation.Restore(Reputation.Neutral, Reputation.MealsToBecomeKnown);

            r.ServiceWindows.Clear();
            foreach (var w in windows) r.ServiceWindows.Add(w);

            foreach (var stationId in r.Menu.Recipes.Select(x => x.StationId).Distinct())
            {
                var model = definitions.EquipmentFor(stationId).FirstOrDefault();
                if (model != null && r.HasRoomFor(model.Footprint * 3)) r.BuyEquipment(model, 3);
            }

            if (r.HasRoomFor(36 * 15m)) r.BuyTables("t", "Tables", 4320m, 36);
            for (var i = 0; i < 4; i++) r.Payroll.Hire(new Employee("c" + i, "Cook", StaffRole.Cook, 16m, 0.5m));
            for (var i = 0; i < 3; i++) r.Payroll.Hire(new Employee("s" + i, "Server", StaffRole.Server, 12m, 0.5m));

            foreach (var recipe in r.Menu.Recipes)
                foreach (var line in recipe.Ingredients) { r.Inventory.SetPar(line.IngredientId, 20m, 600m); r.Inventory.Receive(line.IngredientId, 40m); }

            return r;
        }

        private static ServiceResult TradeAMonth(Restaurant r)
        {
            var runner = new SimulationRunner(r, new GameClock(), 4242, InterruptPolicy.None());
            runner.AdvanceDays(30);
            return runner.Snapshot();
        }

        [Fact]
        public void OpeningAServiceTheMenuCannotFeedIsCalledOut()
        {
            // A breakfast-only card against a dinner-and-late operation: the room fills with
            // people who cannot order anything.
            var r = Open(new[] { "eggs-benedict", "flat-white" },
                new ServiceWindow("Dinner", 18, 23), new ServiceWindow("Late", 23, 2));

            var trading = TradeAMonth(r);
            var advice = new Advisor(r).Review(trading).ToList();

            foreach (var a in advice) _out.WriteLine($"  [{a.Id}] {a.Headline} — {a.Reasoning}");

            var menuAdvice = advice.FirstOrDefault(a => a.Id == "opportunity:menu");
            Assert.True(menuAdvice != null,
                $"{trading.PartiesLostToMenu} parties left without ordering and the Advisor never mentioned the menu");

            // It must name the service that is bare, not just report a number.
            Assert.True(menuAdvice.Reasoning.ToLowerInvariant().Contains("dinner"), menuAdvice.Reasoning);
        }

        [Fact]
        public void AMenuThatCoversItsHoursGetsNoSuchAdvice()
        {
            var r = Open(new[] { "margherita", "house-focaccia", "caprese-salad", "sea-bass" },
                new ServiceWindow("Dinner", 18, 23));

            var trading = TradeAMonth(r);
            var advice = new Advisor(r).Review(trading).ToList();

            _out.WriteLine($"parties lost to menu: {trading.PartiesLostToMenu} of {trading.PartiesArrived} arrived");
            Assert.False(advice.Any(a => a.Id == "opportunity:menu"));
        }

        /// <summary>
        /// It must never name the mechanism. Same standard as the rest of the Advisor: the
        /// player is told what is happening in their restaurant, not what the model calls it.
        /// </summary>
        [Fact]
        public void ItSpeaksAboutTheRestaurantRatherThanTheModel()
        {
            var r = Open(new[] { "eggs-benedict", "flat-white" },
                new ServiceWindow("Dinner", 18, 23), new ServiceWindow("Late", 23, 2));

            var advice = new Advisor(r).Review(TradeAMonth(r))
                .First(a => a.Id == "opportunity:menu");

            var said = (advice.Headline + " " + advice.Reasoning).ToLowerInvariant();
            foreach (var jargon in new[] { "daypart", "partieslosttomenu", "suitsdaypart", "archetype" })
                Assert.False(said.IndexOf(jargon, System.StringComparison.Ordinal) >= 0, "said: " + said);
        }
    }
}
