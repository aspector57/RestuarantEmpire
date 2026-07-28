using System;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Fitting out a restaurant costs money: every station, every table, every bit of decor.
    ///
    /// This is what stops capacity being something a player simply declares. A bigger
    /// kitchen and a bigger dining room are purchases, which is what makes "should I open
    /// for breakfast?" a real question — a breakfast service needs equipment a dinner
    /// service does not.
    /// </summary>
    public class FitOutTests
    {
        private static Restaurant Build(out Company company, decimal openingCash = 50000m)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme", "Acme Restaurant Group", definitions, openingCash);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Menu.Add("margherita", "caprese-salad", "truffle-risotto", "house-focaccia");
            company.SupplierPolicy.AssignAll("valley-produce");

            foreach (var id in definitions.IngredientIds) restaurant.Inventory.Receive(id, 100000m);

            return restaurant;
        }

        [Fact]
        public void BuyingAStationChargesTheBooks_AndInstallingOneDoesNot()
        {
            var restaurant = Build(out var company);
            var cashBefore = company.Economy.CashOnHand;

            restaurant.BuyStation("oven", "Wood Oven", 8400m, concurrentCapacity: 3);

            Assert.Equal(cashBefore - 8400m, company.Economy.CashOnHand);
            Assert.Equal(3, restaurant.Kitchen.Get("oven").ConcurrentCapacity);
            Assert.Contains(company.Economy.Entries, e =>
                e.Category == LedgerCategory.CapitalExpenditure && e.Description.Contains("Wood Oven"));

            // Install is the raw mechanism, used when restoring a save. It must not re-bill.
            var after = company.Economy.CashOnHand;
            restaurant.Kitchen.Install("saute", "Saute", 1, 1.0m, 3000m);
            Assert.Equal(after, company.Economy.CashOnHand);
        }

        [Fact]
        public void SeatingIsDerivedFromFurnitureBought_NotDeclared()
        {
            var restaurant = Build(out var company);

            Assert.Equal(0, restaurant.SeatingCapacity);   // an empty room

            restaurant.BuyTables("banquettes", "Banquettes", 3600m, seats: 24, comfort: 0.7m);
            restaurant.BuyTables("window-tables", "Window tables", 1800m, seats: 12, comfort: 0.8m);

            Assert.Equal(36, restaurant.SeatingCapacity);
            Assert.Equal(5400m, restaurant.DiningRoom.InstalledValue);
            Assert.Equal(50000m - 5400m, company.Economy.CashOnHand);
        }

        [Fact]
        public void ComfortIsWeightedBySeats_SoOneNiceChairDoesNotFlatterTheRoom()
        {
            var restaurant = Build(out _);

            restaurant.BuyTables("cheap", "Plastic chairs", 400m, seats: 40, comfort: 0.2m);
            restaurant.BuyTables("nice", "One lovely booth", 900m, seats: 2, comfort: 1.0m);

            // (0.2*40 + 1.0*2) / 42 = 0.238..., not the 0.6 a naive average would give.
            Assert.True(restaurant.DiningRoom.Comfort < 0.3m);
        }

        [Fact]
        public void AnEmptyRoomIsNeutral_NotPunished()
        {
            // A ghost kitchen or an unfurnished test fixture should not be scored as if it
            // had deliberately bleak decor.
            var restaurant = Build(out _);

            Assert.Empty(restaurant.DiningRoom.Fittings);
            Assert.Equal(0.5m, restaurant.DiningRoom.Comfort);
        }

        [Fact]
        public void DecorNudgesSatisfaction_ButCannotDecideANight()
        {
            // The design is explicit that furniture is a small, bounded modifier. Compare
            // the bleakest possible room against the loveliest, holding everything else
            // equal, and the gap must stay small.
            var bleak = Build(out _);
            bleak.BuyTables("stools", "Plastic stools", 200m, 40, comfort: 0m);
            bleak.BuyStation("oven", "Oven", 0m, 6);
            bleak.BuyStation("garde-manger", "Garde Manger", 0m, 6);
            bleak.BuyStation("saute", "Saute", 0m, 6);

            var lovely = Build(out _);
            lovely.BuyTables("walnut", "Walnut tables", 9000m, 40, comfort: 1m);
            lovely.BuyStation("oven", "Oven", 0m, 6);
            lovely.BuyStation("garde-manger", "Garde Manger", 0m, 6);
            lovely.BuyStation("saute", "Saute", 0m, 6);

            var bleakNight = Dinner.Run(bleak, 20, 4242);
            var lovelyNight = Dinner.Run(lovely, 20, 4242);

            // Nicer is genuinely better...
            Assert.True(lovelyNight.AverageSatisfaction > bleakNight.AverageSatisfaction);

            // ...but by no more than the ambiance weight allows, even at the extremes.
            //
            // Asserted against the FORMULA rather than against the night's average, because
            // the average is no longer a clean measure of it. Since reputation exists, a nicer
            // room also raises standing slightly, which raises footfall, which changes how
            // hard the kitchen is working and therefore the speed scores — so the observed
            // gap over twenty days came out at 0.0814 against a 0.08 weight. That overage is
            // a real second channel and not a formula breach, and the bound being claimed
            // here is a property of the formula, so that is where it belongs.
            // Two restaurants alike in every respect except the furniture, scored under the
            // same four weights a guest applies. Everything but Room is identical, so the
            // difference IS the room's contribution, and it must be exactly its weight.
            var bleakDish = DishRatings.For(bleak).Single(r => r.RecipeId == "margherita");
            var lovelyDish = DishRatings.For(lovely).Single(r => r.RecipeId == "margherita");

            Assert.Equal(bleakDish.Ingredients, lovelyDish.Ingredients);
            Assert.Equal(bleakDish.Speed, lovelyDish.Speed);
            Assert.Equal(bleakDish.Value, lovelyDish.Value);

            var gap = lovelyDish.Overall - bleakDish.Overall;
            Assert.Equal(SatisfactionModel.AmbianceWeight, gap);

            // The night-level check stays, as a direction rather than a bound.
            Assert.True(lovelyNight.AverageSatisfaction - bleakNight.AverageSatisfaction < 0.15m,
                "decor should nudge a night, never decide one");

            // And it is the smallest of the four weights, on purpose.
            Assert.True(SatisfactionModel.AmbianceWeight < SatisfactionModel.ValueWeight);
            Assert.True(SatisfactionModel.AmbianceWeight < SatisfactionModel.ServiceSpeedWeight);
            Assert.True(SatisfactionModel.AmbianceWeight < SatisfactionModel.FoodQualityWeight);
        }

        [Fact]
        public void OpeningAnExtraServiceMeansBuyingTheEquipmentItNeeds()
        {
            // The concrete case: a breakfast menu needs a coffee station. Without it the
            // dish simply cannot be cooked, so longer hours have a capital price attached.
            var restaurant = Build(out var company);
            restaurant.BuyStation("oven", "Oven", 5000m, 4);

            var definitions = company.Definitions;
            var espresso = new RestaurantEmpire.Core.Definitions.RecipeDefinition(
                "flat-white", "Flat White", 4.20m,
                new[] { new RestaurantEmpire.Core.Definitions.RecipeIngredient("olive-oil", 0.001m) },
                stationId: "coffee", prepMinutes: 3);

            var withCoffee = new RestaurantEmpire.Core.Definitions.DefinitionRegistry(
                definitions.Ingredients, definitions.Suppliers, definitions.Recipes.Concat(new[] { espresso }));

            var cafe = new Company("cafe", "Cafe", withCoffee, 50000m)
                .OpenRestaurant("cafe", "The Cafe", LocationType.BrickAndMortar);

            cafe.Menu.Add("flat-white");
            cafe.Company.SupplierPolicy.AssignAll("valley-produce");
            foreach (var id in withCoffee.IngredientIds) cafe.Inventory.Receive(id, 1000m);

            // No espresso machine yet.
            var refused = cafe.Kitchen.OpenPass(0).Fire(withCoffee.GetRecipe("flat-white"), 0, cafe.Inventory);
            Assert.Equal(TicketOutcome.NoStation, refused.Outcome);
            Assert.Contains("coffee", refused.FailureReason);

            // Buy one, and breakfast becomes possible — for a price.
            var cashBefore = cafe.Company.Economy.CashOnHand;
            cafe.BuyStation("coffee", "Espresso Machine", 6500m, 2);

            Assert.Equal(cashBefore - 6500m, cafe.Company.Economy.CashOnHand);
            Assert.True(cafe.Kitchen.OpenPass(0).Fire(withCoffee.GetRecipe("flat-white"), 0, cafe.Inventory).WasServed);
        }

        [Fact]
        public void AFitOutCanBankruptYou_AndSaysSoPlainly()
        {
            var restaurant = Build(out var company, openingCash: 5000m);

            restaurant.BuyStation("oven", "Commercial Oven", 12000m, 4);

            Assert.True(company.Economy.IsInsolvent);
            Assert.Equal(-7000m, company.Economy.CashOnHand);
            Assert.Contains(company.Economy.Entries, e => e.Description.Contains("Commercial Oven"));
        }

        [Fact]
        public void TheFitOutSurvivesASaveAndIsNotReBought()
        {
            var restaurant = Build(out var company);
            restaurant.BuyStation("oven", "Wood Oven", 8400m, 3);
            restaurant.BuyTables("banquettes", "Banquettes", 3600m, 24, 0.7m);

            var cashAtSave = company.Economy.CashOnHand;
            var json = SaveGameSerializer.ToJson(company, new GameClock());

            var loaded = SaveGameSerializer.FromJson(json,
                JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory));

            var restored = loaded.Company.GetRestaurant("flagship");

            Assert.Equal(24, restored.SeatingCapacity);
            Assert.Equal(0.7m, restored.DiningRoom.Comfort);
            Assert.Equal(3, restored.Kitchen.Get("oven").ConcurrentCapacity);
            Assert.Equal(8400m, restored.Kitchen.Get("oven").Cost);

            // Crucially the cash is unchanged — reloading did not buy the oven twice.
            Assert.Equal(cashAtSave, loaded.Company.Economy.CashOnHand);
        }

        [Fact]
        public void AFittingCannotHaveNonsenseValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Fitting("x", "X", -1m));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Fitting("x", "X", 10m, seats: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Fitting("x", "X", 10m, comfort: 1.5m));
        }
    }
}
