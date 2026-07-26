using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Definitions;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Kitchen throughput: station capacity math and the queueing that turns it into a
    /// real constraint. Built on the Escoffier brigade model from the design's Phase 2.
    /// </summary>
    public class KitchenTests
    {
        private static Restaurant BuildStockedKitchen(out DefinitionRegistry definitions, int ovenSlots = 1)
        {
            definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            var company = new Company("acme", "Acme Restaurant Group", definitions);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Menu.Add("margherita", "caprese-salad", "truffle-risotto", "house-focaccia");
            company.SupplierPolicy.AssignAll("valley-produce");

            restaurant.Kitchen.Install("oven", "Wood Oven", ovenSlots);
            restaurant.Kitchen.Install("garde-manger", "Garde Manger");
            restaurant.Kitchen.Install("saute", "Saute");

            foreach (var id in definitions.IngredientIds) restaurant.Inventory.Receive(id, 10000m);

            return restaurant;
        }

        [Fact]
        public void OrdersToTheSameStation_QueueBehindEachOther()
        {
            var restaurant = BuildStockedKitchen(out var definitions);
            var pass = restaurant.Kitchen.OpenPass(0);
            var margherita = definitions.GetRecipe("margherita"); // 9 minutes at the oven

            var first = pass.Fire(margherita, 0, restaurant.Inventory);
            var second = pass.Fire(margherita, 0, restaurant.Inventory);
            var third = pass.Fire(margherita, 0, restaurant.Inventory);

            Assert.Equal(0, first.StartedTick);
            Assert.Equal(9, first.CompletedTick);
            Assert.Equal(0, first.QueuedMinutes);

            Assert.Equal(9, second.StartedTick);   // waited for the oven
            Assert.Equal(18, second.CompletedTick);
            Assert.Equal(9, second.QueuedMinutes);

            Assert.Equal(27, third.CompletedTick);
            Assert.Equal(18, third.QueuedMinutes);

            // Cooking time never changed — the whole delay is queueing. That distinction is
            // what lets the game say "the oven backed up" instead of "service was slow".
            Assert.Equal(9, third.CookMinutes);
            Assert.Equal(27, third.WaitMinutes);
        }

        [Fact]
        public void TwoDishesSharingOneStation_ContendForIt()
        {
            // Margherita and focaccia both cook in the oven. That is the entire reason a
            // menu decision is also a throughput decision.
            var restaurant = BuildStockedKitchen(out var definitions);
            var pass = restaurant.Kitchen.OpenPass(0);

            var pizza = pass.Fire(definitions.GetRecipe("margherita"), 0, restaurant.Inventory);   // 9 min
            var bread = pass.Fire(definitions.GetRecipe("house-focaccia"), 0, restaurant.Inventory); // 6 min

            Assert.Equal("oven", pizza.StationId);
            Assert.Equal("oven", bread.StationId);
            Assert.Equal(9, bread.StartedTick);   // the bread waited on the pizza
            Assert.Equal(15, bread.CompletedTick);
        }

        [Fact]
        public void DishesOnDifferentStations_DoNotContend()
        {
            var restaurant = BuildStockedKitchen(out var definitions);
            var pass = restaurant.Kitchen.OpenPass(0);

            var pizza = pass.Fire(definitions.GetRecipe("margherita"), 0, restaurant.Inventory);    // oven
            var salad = pass.Fire(definitions.GetRecipe("caprese-salad"), 0, restaurant.Inventory); // garde-manger

            Assert.Equal(0, pizza.StartedTick);
            Assert.Equal(0, salad.StartedTick);   // started simultaneously
            Assert.Equal(4, salad.CompletedTick);
        }

        [Fact]
        public void AddingASecondSlot_LetsTwoPlatesRunAtOnce()
        {
            var restaurant = BuildStockedKitchen(out var definitions, ovenSlots: 2);
            var pass = restaurant.Kitchen.OpenPass(0);
            var margherita = definitions.GetRecipe("margherita");

            var first = pass.Fire(margherita, 0, restaurant.Inventory);
            var second = pass.Fire(margherita, 0, restaurant.Inventory);
            var third = pass.Fire(margherita, 0, restaurant.Inventory);

            Assert.Equal(0, first.StartedTick);
            Assert.Equal(0, second.StartedTick);  // both ovens going
            Assert.Equal(9, third.StartedTick);   // third waits for whichever frees first
        }

        [Fact]
        public void BetterEquipment_CooksFaster()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var margherita = definitions.GetRecipe("margherita"); // 9 minutes baseline

            var standard = new KitchenStation("oven", "Wood Oven");
            var fast = new KitchenStation("oven", "Deck Oven", 1, 2.0m);

            Assert.Equal(9, standard.MinutesFor(margherita));
            Assert.Equal(5, fast.MinutesFor(margherita)); // 9 / 2, rounded up
        }

        [Fact]
        public void CapacityFor_ReportsTheCeilingAServiceCannotExceed()
        {
            var restaurant = BuildStockedKitchen(out var definitions, ovenSlots: 2);
            var margherita = definitions.GetRecipe("margherita");

            // 180 minutes / 9 per pizza = 20 per slot, times 2 slots.
            Assert.Equal(40, restaurant.Kitchen.CapacityFor(margherita, 180));

            // A dish whose station isn't installed has a ceiling of zero.
            var barren = restaurant.Company.OpenRestaurant("empty", "Empty Shell", LocationType.BrickAndMortar);
            Assert.Equal(0, barren.Kitchen.CapacityFor(margherita, 180));
        }

        [Fact]
        public void ADishWithNoStationInstalled_Is86dWithANamedReason()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("acme", "Acme Restaurant Group", definitions);
            var restaurant = company.OpenRestaurant("truck", "Acme Truck", LocationType.FoodTruck);

            restaurant.Kitchen.Install("oven", "Tiny Oven");   // no saute station
            company.SupplierPolicy.AssignAll("valley-produce");
            foreach (var id in definitions.IngredientIds) restaurant.Inventory.Receive(id, 100m);

            var ticket = restaurant.Kitchen.OpenPass(0)
                .Fire(definitions.GetRecipe("truffle-risotto"), 0, restaurant.Inventory);

            Assert.False(ticket.WasServed);
            Assert.Equal(TicketOutcome.NoStation, ticket.Outcome);
            Assert.Contains("saute", ticket.FailureReason);

            // A build problem is reported, not thrown — the player needs to see it.
            Assert.NotNull(ticket.FailureReason);
        }

        [Fact]
        public void RunningOutMidService_86sTheDish_AndLeavesTheWalkInUntouched()
        {
            var restaurant = BuildStockedKitchen(out var definitions);
            var pass = restaurant.Kitchen.OpenPass(0);

            // Enough mozzarella for exactly one margherita (0.15kg), plenty of everything else.
            restaurant.Inventory.TryConsume("mozzarella", restaurant.Inventory.QuantityOf("mozzarella"));
            restaurant.Inventory.Receive("mozzarella", 0.15m);

            var margherita = definitions.GetRecipe("margherita");

            Assert.True(pass.Fire(margherita, 0, restaurant.Inventory).WasServed);

            var flourBefore = restaurant.Inventory.QuantityOf("flour");
            var second = pass.Fire(margherita, 0, restaurant.Inventory);

            Assert.Equal(TicketOutcome.OutOfStock, second.Outcome);
            Assert.Contains("mozzarella", second.FailureReason);

            // Atomicity: the failed dish consumed nothing at all, not even the flour it could afford.
            Assert.Equal(flourBefore, restaurant.Inventory.QuantityOf("flour"));
        }

        [Fact]
        public void ATicketDoesNotKnowWhereTheOrderCameFrom()
        {
            // Architecture Rule 6, enforced structurally: no channel/table/delivery field
            // exists, so delivery is additive at M5 rather than a kitchen rewrite.
            var fields = typeof(Ticket).GetProperties();

            Assert.DoesNotContain(fields, p =>
                p.Name.Contains("Channel") || p.Name.Contains("Table") || p.Name.Contains("Delivery"));
        }
    }
}
