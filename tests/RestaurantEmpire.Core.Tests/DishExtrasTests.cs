using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;
using Xunit.Abstractions;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// WHAT YOU PUT ON THE PLATE, and why it has to stop paying (Aaron).
    ///
    /// *"What if we can select additional ingredients to put into dishes, raising your cost, it
    /// should raise what you can charge but not infinitely?"* The ceiling IS the mechanic —
    /// without it this is a slider that always says yes.
    ///
    /// This lived only in the browser build for a session, which is exactly the drift that
    /// recalibrated the food economics in one build and not the other. It is engine content now.
    /// </summary>
    public class DishExtrasTests
    {
        private readonly ITestOutputHelper _out;
        public DishExtrasTests(ITestOutputHelper o) { _out = o; }

        private static Restaurant Build()
        {
            var defs = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("acme-group", "Acme Restaurant Group", defs);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);
            restaurant.Menu.Add("margherita", "caprese-salad", "house-focaccia", "sea-bass");
            company.SupplierPolicy.AssignAll("valley-produce");
            return restaurant;
        }

        [Fact]
        public void PuttingSomethingOnADishCostsMoreAndIsWorthMore()
        {
            var r = Build();

            var bareCost = r.Costing.PlateCost("margherita");
            var bareWorth = r.Costing.DesignedPrice("margherita");

            r.Extras.Set("margherita", "parma", true);

            Assert.True(r.Costing.PlateCost("margherita") > bareCost,
                "prosciutto is not free");
            Assert.True(r.Costing.DesignedPrice("margherita") > bareWorth,
                "a dish with prosciutto on it is worth more than one without");

            // And taking it off puts everything back exactly — nothing is cached.
            r.Extras.Set("margherita", "parma", false);
            Assert.Equal(bareCost, r.Costing.PlateCost("margherita"));
            Assert.Equal(bareWorth, r.Costing.DesignedPrice("margherita"));
        }

        /// <summary>
        /// "But not infinitely." Each further addition is worth less than the last, and the
        /// total is capped by what the dish IS — a focaccia cannot be dressed into a main.
        /// </summary>
        [Fact]
        public void TheSecondGoodIdeaOnAPlateIsWorthLessThanTheFirst()
        {
            var r = Build();
            var extras = new[] { "buffalo", "parma", "basil" };

            var lifts = new decimal[extras.Length];
            for (var i = 0; i < extras.Length; i++)
            {
                r.Extras.Set("margherita", extras[i], true);
                lifts[i] = r.Costing.ExtrasLift("margherita");
                _out.WriteLine($"  + {extras[i],-10} lift {lifts[i]:F3}  worth {r.Costing.DesignedPrice("margherita"):C}");
            }

            var first = lifts[0];
            var second = lifts[1] - lifts[0];
            var third = lifts[2] - lifts[1];

            Assert.True(second < first, $"the second addition ({second:F3}) must add less than the first ({first:F3})");
            Assert.True(third < second, $"the third ({third:F3}) must add less than the second ({second:F3})");
            Assert.True(lifts[2] > 0m, "three additions still make the dish worth more than bare");
        }

        [Fact]
        public void ADishCannotBeDressedBeyondWhatItIs()
        {
            var defs = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var r = Build();

            // Everything available, on at once.
            foreach (var extra in defs.ExtrasFor("house-focaccia"))
                r.Extras.Set("house-focaccia", extra.Id, true);

            var ceiling = defs.LiftCeilingFor("small plate");
            Assert.True(r.Costing.ExtrasLift("house-focaccia") <= ceiling,
                $"a small plate cannot lift past {ceiling:P0}, however much is put on it");
        }

        /// <summary>
        /// Two identical plates must be worth the same. If lift depended on the order the
        /// player happened to tick the boxes, the same dish would carry two different prices.
        /// </summary>
        [Fact]
        public void TheOrderYouTickTheBoxesDoesNotChangeWhatTheDishIsWorth()
        {
            var fwd = Build();
            fwd.Extras.Set("sea-bass", "biggerfish", true);
            fwd.Extras.Set("sea-bass", "truffled", true);

            var bwd = Build();
            bwd.Extras.Set("sea-bass", "truffled", true);
            bwd.Extras.Set("sea-bass", "biggerfish", true);

            Assert.Equal(fwd.Costing.ExtrasLift("sea-bass"), bwd.Costing.ExtrasLift("sea-bass"));
            Assert.Equal(fwd.Costing.PlateCost("sea-bass"), bwd.Costing.PlateCost("sea-bass"));
        }

        /// <summary>
        /// Charging more for a dressed-up dish is not gouging, and this is the property that
        /// makes extras pay at all. Markup measures price against the DESIGNED price, so a
        /// plate carrying prosciutto is judged against a higher bar.
        /// </summary>
        [Fact]
        public void ADressedUpDishIsJudgedAgainstAHigherBar()
        {
            var r = Build();
            var bare = r.Costing.DesignedPrice("margherita");

            r.Pricing.SetPrice("margherita", bare * 1.20m);
            var gouging = r.Costing.Markup("margherita");

            r.Extras.Set("margherita", "parma", true);
            r.Extras.Set("margherita", "buffalo", true);
            var justified = r.Costing.Markup("margherita");

            _out.WriteLine($"  same price, bare {gouging:F2}x against dressed {justified:F2}x");
            Assert.True(justified < gouging,
                "the same price on a better plate must read as less of a stretch");
        }

        /// <summary>
        /// Architecture Rule 2: extras arrived as a data file. Every one must resolve, or the
        /// content is lying about what it offers.
        /// </summary>
        [Fact]
        public void TheShippedExtrasAllResolve()
        {
            var defs = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var all = defs.Recipes.SelectMany(r => defs.ExtrasFor(r.Id)).ToList();

            Assert.True(all.Count > 0, "extras.json shipped nothing");

            foreach (var extra in all)
            {
                Assert.True(extra.Lift > 0m, extra.Id + " lifts nothing, so it is only a cost");
                Assert.True(extra.Ingredients.Count > 0, extra.Id + " has no ingredients, so it is free");
                foreach (var line in extra.Ingredients)
                    Assert.True(defs.Ingredients.Any(i => i.Id == line.IngredientId),
                        extra.Id + " needs " + line.IngredientId + ", which does not exist");
            }

            Assert.DoesNotContain(defs.LoadWarnings, w => w.Contains("Extra"));
            _out.WriteLine($"  {all.Count} extras across the card, all resolving");
        }
    }
}
