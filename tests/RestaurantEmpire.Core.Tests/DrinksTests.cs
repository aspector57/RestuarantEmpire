using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;
using Xunit.Abstractions;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Drinks exist to do two jobs the food menu cannot: make a late service possible, and
    /// blend down the food cost of an expensive concept. Both are measured here rather than
    /// assumed, because both are the reason this was built rather than more dishes.
    /// </summary>
    public class DrinksTests
    {
        private readonly ITestOutputHelper _out;
        public DrinksTests(ITestOutputHelper o) { _out = o; }

        private static Restaurant Open(string[] menu, bool licensed, params ServiceWindow[] windows)
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

            if (licensed) Assert.True(r.ApplyForLiquorLicense());

            foreach (var stationId in r.Menu.Recipes.Select(x => x.StationId).Distinct())
            {
                var model = definitions.EquipmentFor(stationId).FirstOrDefault();
                if (model != null && r.HasRoomFor(model.Footprint * 3)) r.BuyEquipment(model, 3);
            }

            if (r.HasRoomFor(36 * 15m)) r.BuyTables("t", "Tables", 4320m, 36);
            for (var i = 0; i < 4; i++) r.Payroll.Hire(new Employee("c" + i, "Cook", StaffRole.Cook, 16m, 0.5m));
            for (var i = 0; i < 3; i++) r.Payroll.Hire(new Employee("s" + i, "Server", StaffRole.Server, 12m, 0.5m));

            foreach (var recipe in r.Menu.Recipes)
                foreach (var line in recipe.Ingredients) { r.Inventory.SetPar(line.IngredientId, 40m, 900m); r.Inventory.Receive(line.IngredientId, 200m); }

            return r;
        }

        private static ServiceResult Trade(Restaurant r, int days = 30)
        {
            var runner = new SimulationRunner(r, new GameClock(), 4242, InterruptPolicy.None());
            runner.AdvanceDays(days);
            return runner.Snapshot();
        }

        /// <summary>
        /// THE GATE. A cocktail list without a licence is exactly as unsellable as a risotto
        /// with no range — and it must not quietly sell anyway.
        /// </summary>
        [Fact]
        public void WithoutALicenceNothingAlcoholicIsSold()
        {
            var menu = new[] { "sea-bass", "caprese-salad", "house-wine", "negroni" };
            var unlicensed = Trade(Open(menu, licensed: false, new ServiceWindow("Dinner", 18, 23)));

            var alcoholSold = unlicensed.UnitsSoldByRecipeId
                .Where(p => p.Key == "house-wine" || p.Key == "negroni")
                .Sum(p => p.Value);

            _out.WriteLine($"unlicensed: {unlicensed.CoversServed} covers, {alcoholSold} alcoholic drinks");
            Assert.Equal(0, alcoholSold);
        }

        /// <summary>
        /// The licence must be a real capital gate, not a flag — refused when the money is not
        /// there, per Binding Principle 4.
        /// </summary>
        [Fact]
        public void ALicenceYouCannotAffordIsRefused()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var broke = new Company("co", "Co", definitions, 500m);
            var r = broke.OpenRestaurant("r", "R", LocationType.BrickAndMortar);

            Assert.False(r.ApplyForLiquorLicense());
            Assert.False(r.Licence.Held);
        }

        /// <summary>
        /// DRINKS ARE ADDITIVE. A guest orders one ALONGSIDE their food, never instead of it —
        /// if a drinks list merely competed for the same order it would cannibalize the kitchen
        /// rather than lift the check, which is backwards and would make the whole feature
        /// pointless.
        /// </summary>
        [Fact]
        public void ADrinksListLiftsTheCheckRatherThanReplacingFood()
        {
            var food = new[] { "sea-bass", "caprese-salad" };
            var withDrinks = new[] { "sea-bass", "caprese-salad", "house-wine", "negroni" };

            var dry = Trade(Open(food, licensed: false, new ServiceWindow("Dinner", 18, 23)));
            var wet = Trade(Open(withDrinks, licensed: true, new ServiceWindow("Dinner", 18, 23)));

            var dryPerCover = dry.Revenue / dry.CoversServed;
            var wetPerCover = wet.Revenue / wet.CoversServed;

            _out.WriteLine($"dry:  {dry.CoversServed} covers, {dry.Revenue:C0}, {dryPerCover:C2}/cover, food {dry.FoodCost / dry.Revenue:P0}");
            _out.WriteLine($"wet:  {wet.CoversServed} covers, {wet.Revenue:C0}, {wetPerCover:C2}/cover, food {wet.FoodCost / wet.Revenue:P0}");

            // The check goes up per cover — that is the additive claim.
            Assert.True(wetPerCover > dryPerCover,
                $"drinks should lift spend per cover: {wetPerCover:C2} against {dryPerCover:C2}");

            // And the food-cost ratio comes DOWN, because drinks carry a better margin than
            // the kitchen. This is how a premium concept survives in the trade.
            Assert.True(wet.FoodCost / wet.Revenue < dry.FoodCost / dry.Revenue,
                "a drinks programme should blend the food cost down");
        }

        /// <summary>
        /// THE REASON THIS WAS BUILT. Nobody orders sea bass at one in the morning, so a late
        /// service used to be a room full of people finding nothing they wanted — measured at
        /// 5,674 parties lost in a single strategy run. Drinks are what a late service sells.
        /// </summary>
        [Fact]
        public void ALateServiceBecomesWorthOpening()
        {
            var menu = new[] { "sea-bass", "caprese-salad", "truffle-risotto" };
            var withBar = new[] { "sea-bass", "caprese-salad", "truffle-risotto", "house-wine", "negroni", "draught-pint" };

            var kitchenOnly = Trade(Open(menu, licensed: false,
                new ServiceWindow("Dinner", 18, 23), new ServiceWindow("Late", 23, 2)));
            var licensed = Trade(Open(withBar, licensed: true,
                new ServiceWindow("Dinner", 18, 23), new ServiceWindow("Late", 23, 2)));

            _out.WriteLine($"kitchen only: {kitchenOnly.CoversServed} covers, {kitchenOnly.Revenue:C0}, " +
                           $"{kitchenOnly.PartiesLostToMenu} parties found nothing");
            _out.WriteLine($"with a bar:   {licensed.CoversServed} covers, {licensed.Revenue:C0}, " +
                           $"{licensed.PartiesLostToMenu} parties found nothing");

            // The first version of this asserted only "fewer" and passed on 2,506 against
            // 2,498 — technically true and completely meaningless. A bar that cannot seat
            // someone who only wants a drink is not a bar, and the weak pass was hiding that.
            Assert.Equal(0, licensed.PartiesLostToMenu);
            Assert.True(kitchenOnly.PartiesLostToMenu > 1000,
                "the control must actually be losing the late crowd, or this proves nothing");
            Assert.True(licensed.Revenue > kitchenOnly.Revenue * 1.4m,
                $"opening a late bar should pay: {licensed.Revenue:C0} against {kitchenOnly.Revenue:C0}");
        }
    }
}
