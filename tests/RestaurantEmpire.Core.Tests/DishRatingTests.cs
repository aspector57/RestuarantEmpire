using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Ingredient quality as something guests ACT on, and the star readout that shows why.
    ///
    /// The gap this closes: `MenuCosting.IngredientQuality` was live and correct, fed the
    /// satisfaction score, and changed nothing else. Measured before the fix, on one seed
    /// with only the supplier swapped: budget stock served 4,089 covers with 151 walkouts,
    /// premium served 4,089 covers with 151 walkouts. Identical. The cheapest supplier was
    /// strictly dominant and free — inside the sourcing system this project exists to fix.
    /// </summary>
    public class DishRatingTests
    {
        private static Restaurant Build(out Company company, string supplierId, decimal priceMultiplier = 1m)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme", "Acme", definitions, 60000m);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Location = Neighborhood.SuburbanHighStreet();
            restaurant.FloorArea = 2150m;
            restaurant.Menu.Add("margherita", "caprese-salad", "truffle-risotto", "house-focaccia");
            company.SupplierPolicy.AssignAll(supplierId);

            if (priceMultiplier != 1m)
                foreach (var id in restaurant.Menu.RecipeIds) company.Pricing.AdjustPrice(id, priceMultiplier);

            restaurant.BuyEquipment(definitions.GetEquipment("oven-commercial"), 4);
            restaurant.BuyEquipment(definitions.GetEquipment("gm-refrigerated"), 3);
            restaurant.BuyEquipment(definitions.GetEquipment("saute-commercial"), 3);

            foreach (var id in definitions.IngredientIds)
            {
                restaurant.Inventory.SetPar(id, 200m, 3000m);
                restaurant.Inventory.Receive(id, 3000m);
            }

            restaurant.BuyTables("t", "Tables", 4000m, 32);
            for (var i = 0; i < 8; i++) restaurant.Payroll.Hire(new Employee("c" + i, "Cook", StaffRole.Cook, 16m));
            for (var i = 0; i < 3; i++) restaurant.Payroll.Hire(new Employee("s" + i, "Server", StaffRole.Server, 12m));

            return restaurant;
        }

        // ---- Quality has to change behavior, not just the score afterwards ----

        [Fact]
        public void CheapIngredientsAtPremiumPricesCostYouTrade()
        {
            // The specific thing Aaron predicted: "if you use cheap ingredients and charge a
            // premium, people will notice and either complain or not order it."
            var cheapAndDear = Build(out _, "budget-wholesale", priceMultiplier: 1.6m);
            var goodAndDear = Build(out _, "valley-produce", priceMultiplier: 1.6m);

            var cheapNight = Dinner.Run(cheapAndDear, 30, 99);
            var goodNight = Dinner.Run(goodAndDear, 30, 99);

            // Guests read the menu and leave, without having to eat first.
            Assert.True(cheapNight.PartiesPutOffByThePrices > goodNight.PartiesPutOffByThePrices);
            Assert.True(cheapNight.UnitsSoldByRecipeId.Values.Sum() < goodNight.UnitsSoldByRecipeId.Values.Sum());

            // And it costs more than the ingredient saving is worth.
            Assert.True(cheapNight.Revenue - cheapNight.FoodCost < goodNight.Revenue - goodNight.FoodCost);
        }

        [Fact]
        public void QualityIsJudgedAgainstThePrice_NotInTheAbstract()
        {
            // Budget stock is not a sin. A cheap dish made of cheap things is honest, and
            // plenty of people want exactly that — so the SAME ingredients are fine at a fair
            // price and offensive at a steep one. It is the mismatch that gets punished.
            // Priced as designed against priced at three times it. 1.8x used to read as
            // "honest" and no longer does — resistance now builds from about a third above
            // the designed price rather than waiting for double.
            var honest = SatisfactionModel.ScoreValue(1m, 1m, 0.2m);
            var gouging = SatisfactionModel.ScoreValue(3m, 1m, 0.2m);

            Assert.True(honest > gouging);
            Assert.True(honest > SatisfactionModel.WalkAwayValueThreshold);
            Assert.True(gouging < SatisfactionModel.WalkAwayValueThreshold);
        }

        [Fact]
        public void BetterIngredientsMakeTheSamePriceFeelLikeBetterValue()
        {
            var budget = SatisfactionModel.ScoreValue(4m, 1m, 0.2m);
            var premium = SatisfactionModel.ScoreValue(4m, 1m, 1.0m);

            Assert.True(premium > budget);
        }

        [Fact]
        public void QualityIsIgnoredWhenNothingHasBeenSourced()
        {
            // Zero means "no opinion", not "terrible" — otherwise every test fixture and
            // every unsourced dish would read as a swindle.
            Assert.Equal(SatisfactionModel.ScoreValue(3m, 1m),
                         SatisfactionModel.ScoreValue(3m, 1m, 0m));
        }

        // ---- The star readout ----

        [Fact]
        public void EveryDishRatesOutOfFive_AndTheStarsAreOnlyADisplay()
        {
            var restaurant = Build(out _, "valley-produce");
            var ratings = DishRatings.For(restaurant);

            Assert.Equal(4, ratings.Count);
            Assert.All(ratings, r =>
            {
                Assert.InRange(r.Stars, 0m, 5m);

                // The total is exactly the four components under the guest's own weights —
                // it is a lens over them, never a fifth number with a life of its own.
                var expected = (r.Ingredients * SatisfactionModel.FoodQualityWeight)
                             + (r.Speed * SatisfactionModel.ServiceSpeedWeight)
                             + (r.Value * SatisfactionModel.ValueWeight)
                             + (r.Room * SatisfactionModel.AmbianceWeight);
                Assert.Equal(expected * 5m, r.Stars);
            });
        }

        [Fact]
        public void TheRatingNamesTheCause_NotJustTheNumber()
        {
            // Binding Principle 2: every outcome traces to a specific named cause, never an
            // opaque score. "2.4 stars" is useless; "budget ingredients at a price that
            // implies better" tells you what to actually do, and what it will cost.
            var starved = Build(out _, "budget-wholesale", priceMultiplier: 2m);

            var rating = DishRatings.For(starved).First(r => r.RecipeId == "truffle-risotto");

            Assert.True(rating.Stars < 4m);
            Assert.False(string.IsNullOrWhiteSpace(rating.Verdict));
            Assert.Contains(rating.Weakest, new[] { "ingredients", "speed", "value", "room" });
        }

        [Fact]
        public void SwitchingSupplierMovesEveryRatingAtOnce_WithNoManualEdits()
        {
            // The M0 exit test, restated for the read surface: ratings are computed live, so
            // one write to one assignment record moves every dependent dish. Nothing cached,
            // nothing to re-enter by hand (Architecture Rule 1).
            var restaurant = Build(out var company, "budget-wholesale");
            var before = DishRatings.For(restaurant).ToDictionary(r => r.RecipeId, r => r.Stars);

            company.SupplierPolicy.AssignAll("premium-harvest");
            var after = DishRatings.For(restaurant).ToDictionary(r => r.RecipeId, r => r.Stars);

            Assert.All(after, pair => Assert.True(pair.Value > before[pair.Key]));
        }

        [Fact]
        public void TheWeakestComponentIsWeighted_SoItPointsAtWhatIsWorthFixing()
        {
            // A poor room scores badly but is rarely why a dish is failing — it carries an
            // eighth of the weight ingredients do. Naming the lowest raw number instead of
            // the biggest weighted loss would send the player to redecorate.
            var restaurant = Build(out var company, "premium-harvest");
            company.Pricing.SetPrice("margherita", 60m);   // absurd, so value is the problem

            var rating = DishRatings.For(restaurant).First(r => r.RecipeId == "margherita");

            Assert.Equal("value", rating.Weakest);
            Assert.Equal("costs more than it looks worth", rating.Verdict);
        }
    }
}
