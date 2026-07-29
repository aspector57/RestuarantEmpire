using System.Collections.Generic;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Food goes off, and over-ordering is what it punishes.
    ///
    /// Aaron: *"spoilage should happen over time, so your food goes bad if you are over
    /// buying"*, then the three refinements that made it survivable — *"maybe spoilage only
    /// happens on meats and produce?"*, *"you are going to be buying more before you get to 0
    /// in stock so you need a way to use the oldest stuff first"*, and *"give some grace so
    /// you don't need to be ordering every single day, but you should still be thoughtful."*
    ///
    /// The first version spoiled everything and no site could be made to pay: 94% of all food
    /// cost went in the bin. Exempting the store cupboard, capping the reorder at what gets
    /// used before it turns, and giving real shelf lives some grace brings a restaurant that
    /// orders to need to a 32% food cost — the middle of the industry's healthy band — while
    /// one that orders ten times what it needs still bins 92%.
    /// </summary>
    public class SpoilageTests
    {
        private static Restaurant Build(out Company company, decimal opening, decimal parMax = 600m)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme", "Acme", definitions, 300000m);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Location = Neighborhood.SuburbanHighStreet();
            restaurant.FloorArea = 2150m;
            restaurant.Menu.Add("margherita", "caprese-salad", "house-focaccia", "sea-bass");
            company.SupplierPolicy.AssignAll("valley-produce");
            restaurant.Reputation.Restore(Reputation.Neutral, Reputation.MealsToBecomeKnown);

            restaurant.ServiceWindows.Clear();
            restaurant.ServiceWindows.Add(new ServiceWindow("Dinner", 18, 23));

            foreach (var stationId in restaurant.Menu.Recipes.Select(r => r.StationId).Distinct())
                restaurant.BuyEquipment(definitions.EquipmentFor(stationId).First(), 4);

            restaurant.BuyTables("t", "Tables", 4000m, 32);
            for (var i = 0; i < 6; i++) restaurant.Payroll.Hire(new Employee("c" + i, "Cook", StaffRole.Cook, 16m));
            for (var i = 0; i < 3; i++) restaurant.Payroll.Hire(new Employee("s" + i, "Server", StaffRole.Server, 12m));

            foreach (var id in OnTheMenu(restaurant))
            {
                restaurant.Inventory.SetPar(id, opening / 2m, parMax);
                restaurant.Inventory.Receive(id, opening);
            }

            return restaurant;
        }

        private static IEnumerable<string> OnTheMenu(Restaurant restaurant)
        {
            var used = new HashSet<string>();
            foreach (var recipe in restaurant.Menu.Recipes)
                foreach (var line in recipe.Ingredients) used.Add(line.IngredientId);

            return used;
        }

        private static ServiceResult Trade(Restaurant restaurant, int days)
        {
            var runner = new SimulationRunner(restaurant, new GameClock(), 4242, InterruptPolicy.None());

            for (var day = 0; day < days; day++)
            {
                runner.AdvanceDays(1);

                foreach (var stock in restaurant.Inventory.Items.ToList())
                    if (stock.IsBelowPar)
                        restaurant.Inventory.Receive(stock.IngredientId, stock.SuggestedReorderQuantity);
            }

            return runner.Snapshot();
        }

        // ---- Only what actually perishes ----

        [Fact]
        public void TheStoreCupboardKeeps_AndTheFridgeDoesNot()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            foreach (var id in new[] { "flour", "olive-oil", "arborio-rice", "coffee-beans", "parmesan" })
                Assert.False(definitions.GetIngredient(id).Perishable, id + " should keep");

            foreach (var id in new[] { "sea-bass", "basil", "tomato", "mozzarella" })
                Assert.True(definitions.GetIngredient(id).Perishable, id + " should perish");

            // With grace, so a weekly rhythm works. Real sea bass is two days; this is four.
            Assert.True(definitions.GetIngredient("sea-bass").ShelfLifeDays >= 4);
        }

        [Fact]
        public void FlourLeftForAYearIsStillFlour()
        {
            var restaurant = Build(out _, opening: 100m);
            var before = restaurant.Inventory.QuantityOf("flour");

            Trade(restaurant, 60);

            Assert.True(restaurant.Inventory.QuantityOf("flour") > 0m);
            Assert.True(before > 0m);
        }

        // ---- Oldest first, so topping up early is safe ----

        [Fact]
        public void TheOldestStockIsUsedFirst_SoATopUpDoesNotRefreshWhatIsUnderIt()
        {
            // Aaron's point exactly: you reorder before you hit zero, so a new delivery must
            // go BEHIND what is already on the shelf. A single running total with an average
            // age would let every top-up quietly rejuvenate the old stock beneath it, and
            // nothing would ever spoil.
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("a", "A", definitions, 1000m);
            var restaurant = company.OpenRestaurant("r", "R", LocationType.BrickAndMortar);

            restaurant.Inventory.StartOfRun(0);
            restaurant.Inventory.Receive("sea-bass", 10m);      // day 0

            restaurant.Inventory.AdvanceTo(3);
            restaurant.Inventory.Receive("sea-bass", 10m);      // day 3, on top of the old

            // Four days on, the first delivery is past it and the second is not.
            restaurant.Inventory.AdvanceTo(4);
            var binned = restaurant.Inventory.DiscardSpoiled(4, definitions);

            Assert.Equal(10m, binned["sea-bass"]);
            Assert.Equal(10m, restaurant.Inventory.QuantityOf("sea-bass"));
        }

        [Fact]
        public void UsingStockConsumesTheOldBatchBeforeTheNewOne()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("a", "A", definitions, 1000m);
            var restaurant = company.OpenRestaurant("r", "R", LocationType.BrickAndMortar);

            restaurant.Inventory.StartOfRun(0);
            restaurant.Inventory.Receive("sea-bass", 10m);
            restaurant.Inventory.AdvanceTo(3);
            restaurant.Inventory.Receive("sea-bass", 10m);

            restaurant.Inventory.TryConsume("sea-bass", 10m);   // eats the day-0 batch

            // Nothing spoils on day 4, because the old batch was the one that got used.
            restaurant.Inventory.AdvanceTo(4);
            Assert.Empty(restaurant.Inventory.DiscardSpoiled(4, definitions));
            Assert.Equal(10m, restaurant.Inventory.QuantityOf("sea-bass"));
        }

        // ---- The lesson: order to need ----

        [Fact]
        public void OrderingToNeedIsAffordable_AndOverOrderingIsNot()
        {
            var thoughtful = Build(out _, opening: 20m);
            var wasteful = Build(out _, opening: 200m, parMax: 4000m);

            var thoughtfulMonth = Trade(thoughtful, 30);
            var wastefulMonth = Trade(wasteful, 30);

            var thoughtfulShare = thoughtfulMonth.WastedFoodCost / thoughtfulMonth.FoodCost;
            var wastefulShare = wastefulMonth.WastedFoodCost / wastefulMonth.FoodCost;

            Assert.True(thoughtfulShare < wastefulShare,
                "thoughtful binned " + thoughtfulShare.ToString("P0") +
                " against wasteful " + wastefulShare.ToString("P0"));

            // And ordering well lands the food cost in the industry's healthy band rather
            // than making the restaurant unviable, which is what the first version did.
            var foodCostRatio = thoughtfulMonth.FoodCost / thoughtfulMonth.Revenue;
            Assert.True(foodCostRatio < 0.40m, "food cost ran at " + foodCostRatio.ToString("P0"));
        }

        [Fact]
        public void OrderingWellDoesNotMeanRunningOut()
        {
            // The other half of the band: being thoughtful must not mean 86'ing dishes.
            var restaurant = Build(out _, opening: 20m);
            var month = Trade(restaurant, 30);

            Assert.Equal(0, month.PartiesLostToMenu);
            Assert.True(month.CoversServed > 1000);
        }

        [Fact]
        public void TheReorderWillNotBuyMoreThanCanBeUsedBeforeItTurns()
        {
            // Par levels are a policy for things that keep. Left uncapped, a four-day fish is
            // topped back up to a full shelf every time it dips, used a fraction, and binned —
            // which measured at 94% of all food cost.
            var restaurant = Build(out _, opening: 20m, parMax: 5000m);
            Trade(restaurant, 20);

            var fish = restaurant.Inventory.Items.Single(s => s.IngredientId == "sea-bass");

            Assert.True(fish.DailyUsage > 0m, "the fixture should be selling fish");
            Assert.True(fish.SuggestedReorderQuantity < 5000m - fish.Quantity,
                "the order should be capped well below par");
        }

        [Fact]
        public void SpoiledStockIsChargedAndSaidOutLoud()
        {
            var wasteful = Build(out _, opening: 400m, parMax: 4000m);
            var month = Trade(wasteful, 20);

            Assert.True(month.WastedFoodCost > 0m);
            Assert.Contains(month.Diagnostics, d => d.Contains("gone off"));
        }

        // ---- You pay when it arrives, not when it is cooked (Aaron) ----

        [Fact]
        public void BuyingStockTakesTheMoneyNow()
        {
            // Aaron: "you should pay when you buy it and then make money when you sell a dish
            // right?" Right, and it did not: ingredients were charged at the moment they were
            // COOKED, so a walk-in full of food cost nothing to fill and a pantry was free to
            // hold. That is what made par levels a slider rather than a decision.
            var restaurant = Build(out var company, opening: 20m);
            var before = company.Economy.CashOnHand;

            var spent = restaurant.OrderStock("sea-bass", 50m);

            Assert.True(spent > 0m);
            Assert.Equal(before - spent, company.Economy.CashOnHand);
            Assert.True(restaurant.Inventory.QuantityOf("sea-bass") >= 50m);
        }

        [Fact]
        public void StockIsNotPaidForTwice()
        {
            // The trap this had to avoid: charging on delivery AND on the plate would take the
            // money twice and quietly halve every restaurant's margin.
            var restaurant = Build(out var company, opening: 20m);

            // Standing order off, so the only money moving is the delivery we make here and
            // the night's takings. This test is about not being charged twice for the same
            // food, not about how the kitchen restocks itself.
            restaurant.StandingOrder = false;
            restaurant.OrderStock("sea-bass", 200m);

            var afterBuying = company.Economy.CashOnHand;
            var night = Dinner.Run(restaurant, 25, 99);
            company.Economy.RecordService(restaurant, night, 0);

            Assert.True(night.FoodCost > 0m, "the night should have eaten something");
            // Takings in, wages out — and the food already paid for on delivery.
            Assert.Equal(afterBuying + night.Revenue - night.LaborCost, company.Economy.CashOnHand);
        }

        [Fact]
        public void AFullPantryCostsCashUpFront()
        {
            // The new failure mode, and the interesting one: you can be profitable on paper
            // and still not have the money, because it is all sitting in the walk-in.
            var thrifty = Build(out var thriftyCo, opening: 20m);
            var hoarder = Build(out var hoarderCo, opening: 20m);

            var thriftyBefore = thriftyCo.Economy.CashOnHand;
            var hoarderBefore = hoarderCo.Economy.CashOnHand;

            foreach (var id in OnTheMenu(thrifty)) thrifty.OrderStock(id, 30m);
            foreach (var id in OnTheMenu(hoarder)) hoarder.OrderStock(id, 900m);

            var thriftySpent = thriftyBefore - thriftyCo.Economy.CashOnHand;
            var hoarderSpent = hoarderBefore - hoarderCo.Economy.CashOnHand;

            Assert.True(hoarderSpent > thriftySpent * 10m,
                "hoarding should tie up real money: " + hoarderSpent + " against " + thriftySpent);
        }

        // ---- Seeing it coming, and what tired food tastes like (Aaron) ----

        [Fact]
        public void YouCanSeeHowMuchIsAboutToTurn()
        {
            // Aaron: "we should be able to see how much is about to turn bad, because you may
            // need to order more still." The old readout gave the age of the oldest batch,
            // which says nothing about the size of the hole that is coming.
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("a", "A", definitions, 1000m);
            var restaurant = company.OpenRestaurant("r", "R", LocationType.BrickAndMortar);

            restaurant.Inventory.StartOfRun(0);
            restaurant.Inventory.Receive("sea-bass", 30m);   // 4-day life, lands day 0

            restaurant.Inventory.AdvanceTo(2);
            restaurant.Inventory.Receive("sea-bass", 50m);   // lands day 2

            // On day 2, the first batch has two days left and the second has four.
            Assert.Equal(30m, restaurant.Inventory.TurningWithin("sea-bass", 2, definitions));
            Assert.Equal(80m, restaurant.Inventory.TurningWithin("sea-bass", 4, definitions));

            // Flour is not going anywhere.
            Assert.Equal(0m, restaurant.Inventory.TurningWithin("flour", 30, definitions));
        }

        [Fact]
        public void TiredStockTastesTired_ButIsNeverInedible()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("a", "A", definitions, 1000m);
            var restaurant = company.OpenRestaurant("r", "R", LocationType.BrickAndMortar);

            restaurant.Inventory.StartOfRun(0);
            restaurant.Inventory.Receive("tomato", 100m);    // 10-day life

            Assert.Equal(1m, restaurant.Inventory.FreshnessOf("tomato", definitions));

            restaurant.Inventory.AdvanceTo(5);              // halfway: still fine
            Assert.Equal(1m, restaurant.Inventory.FreshnessOf("tomato", definitions));

            restaurant.Inventory.AdvanceTo(9);              // nearly gone: noticeably not fresh
            var tired = restaurant.Inventory.FreshnessOf("tomato", definitions);

            Assert.True(tired < 1m);
            Assert.True(tired >= 0.55m, "the worst a guest gets is 'that didn't taste fresh'");
        }

        [Fact]
        public void FreshnessReachesThePlate()
        {
            var fresh = SatisfactionModel.PlateQuality(0.6m, 0.5m, 1m);
            var stale = SatisfactionModel.PlateQuality(0.6m, 0.5m, 0.6m);

            Assert.True(stale < fresh);

            // And it compounds with the other two, so a tired plate made badly from cheap
            // stock is genuinely poor while any one of the three alone is survivable.
            Assert.True(SatisfactionModel.PlateQuality(0.2m, 0.15m, 0.6m)
                      < SatisfactionModel.PlateQuality(0.2m, 0.5m, 1m));
        }

        [Fact]
        public void ThrowingSomethingOutIsADecisionYouCanMake()
        {
            // Only worth having because tired stock now tastes tired. Before freshness
            // existed, serving it always beat binning it and nobody would ever have chosen to.
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("a", "A", definitions, 1000m);
            var restaurant = company.OpenRestaurant("r", "R", LocationType.BrickAndMortar);

            restaurant.Inventory.StartOfRun(0);
            restaurant.Inventory.Receive("basil", 20m);
            restaurant.Inventory.AdvanceTo(5);
            restaurant.Inventory.Receive("basil", 20m);      // fresh, on top of tired

            var tossed = restaurant.Inventory.Discard("basil", 20m);

            Assert.Equal(20m, tossed);
            Assert.Equal(20m, restaurant.Inventory.QuantityOf("basil"));

            // And it threw out the OLD one, so what is left is the fresh delivery.
            Assert.Equal(1m, restaurant.Inventory.FreshnessOf("basil", definitions));
        }
    }
}
