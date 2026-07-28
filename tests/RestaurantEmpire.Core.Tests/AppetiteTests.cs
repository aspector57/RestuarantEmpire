using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Who walks in decides what sells — and therefore whether the popularity axis of the
    /// Kasavana-Smith matrix means anything at all.
    ///
    /// Before this, guests picked dishes uniformly at random. With four dishes every dish
    /// landed near a 25% share, comfortably above the 17.5% popularity bar, so a Puzzle
    /// (high margin, LOW volume) could not arise naturally and half the matrix was
    /// measuring the simulation's RNG. Appetite is what fixes that.
    /// </summary>
    public class AppetiteTests
    {
        private static Restaurant Build(out Company company, Neighborhood where)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme", "Acme", definitions, 300000m);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Location = where;
            restaurant.FloorArea = 200m;

            foreach (var recipe in definitions.Recipes) restaurant.Menu.Add(recipe.Id);
            company.SupplierPolicy.AssignAll("valley-produce");

            foreach (var stationId in restaurant.Menu.Recipes.Select(r => r.StationId).Distinct())
                restaurant.BuyEquipment(definitions.EquipmentFor(stationId).First(), 4);

            restaurant.BuyTables("t", "Tables", 5000m, 40);
            for (var i = 0; i < 8; i++) restaurant.Payroll.Hire(new Employee("c" + i, "Cook", StaffRole.Cook, 16m));
            for (var i = 0; i < 3; i++) restaurant.Payroll.Hire(new Employee("s" + i, "Server", StaffRole.Server, 12m));

            foreach (var id in definitions.IngredientIds)
            {
                restaurant.Inventory.SetPar(id, 500m, 5000m);
                restaurant.Inventory.Receive(id, 5000m);
            }

            return restaurant;
        }

        /// <summary>
        /// Puzzles among dishes that actually SOLD.
        ///
        /// A dish nobody ordered has a zero popularity share and an above-average margin, so
        /// the matrix classifies it a Puzzle — correctly, but uninformatively. A breakfast
        /// dish left on a dinner menu will therefore always produce a Puzzle no matter what
        /// prices do, which would let these tests pass on an artifact rather than on the
        /// mechanism they are meant to be measuring.
        /// </summary>
        private static System.Collections.Generic.List<MenuItemAnalysis> SoldPuzzles(MenuAnalysis analysis)
        {
            return analysis.OfClass(MenuClassification.Puzzle).Where(i => i.UnitsSold > 0).ToList();
        }

        private static MenuAnalysis TradeFor(Restaurant restaurant, ServiceWindow window, int startHour, int days = 14)
        {
            restaurant.ServiceWindows.Clear();
            restaurant.ServiceWindows.Add(window);

            var clock = new GameClock();
            clock.AdvanceHours(startHour);

            var runner = new SimulationRunner(restaurant, clock, 4242, InterruptPolicy.None());

            for (var day = 0; day < days; day++)
            {
                runner.AdvanceDays(1);
                foreach (var stock in restaurant.Inventory.Items.ToList())
                    if (stock.IsBelowPar) restaurant.Inventory.Receive(stock.IngredientId, stock.SuggestedReorderQuantity);
            }

            var trading = runner.Snapshot();
            return MenuEngineering.Analyze(restaurant,
                trading.UnitsSoldByRecipeId.ToDictionary(p => p.Key, p => p.Value));
        }

        // ---- The thing this was built to fix ----

        [Fact]
        public void PopularityIsNoLongerUniform_SoThePuzzleQuadrantCanExist()
        {
            var restaurant = Build(out _, Neighborhood.BusinessDistrict());
            var lunch = TradeFor(restaurant, new ServiceWindow("Lunch", 12, 15), 12);

            var sold = lunch.Items.Where(i => i.UnitsSold > 0).ToList();
            Assert.True(sold.Count >= 4, "several dishes should sell");

            // Uniform ordering would put every dish within a whisker of an equal share.
            var top = sold.Max(i => i.PopularityShare);
            var bottom = sold.Min(i => i.PopularityShare);
            Assert.True(top > bottom * 2m,
                "share spread is only " + bottom.ToString("0.000") + " to " + top.ToString("0.000") +
                " — that is still close to uniform");

            // And the quadrant that was previously unreachable now appears — on a dish
            // people are genuinely buying, not on an unsold one.
            Assert.NotEmpty(SoldPuzzles(lunch));
        }

        [Fact]
        public void TheSameMenuGetsADifferentMatrixInADifferentNeighborhood()
        {
            // This is the payoff: a menu is not good or bad in the abstract, it is good or
            // bad for the people who actually walk past.
            var business = Build(out _, Neighborhood.BusinessDistrict());
            var nightlife = Build(out _, Neighborhood.NightlifeQuarter());

            var lunchCrowd = TradeFor(business, new ServiceWindow("Lunch", 12, 15), 12);
            var lateCrowd = TradeFor(nightlife, new ServiceWindow("Dinner", 18, 23), 18);

            var bestAtLunch = lunchCrowd.Items.OrderByDescending(i => i.PopularityShare).First().RecipeId;
            var bestAtNight = lateCrowd.Items.OrderByDescending(i => i.PopularityShare).First().RecipeId;

            Assert.NotEqual(bestAtLunch, bestAtNight);

            // The luxury dish is wanted by the evening crowd and ignored by the lunch one.
            Assert.True(lateCrowd["truffle-risotto"].PopularityShare >
                        lunchCrowd["truffle-risotto"].PopularityShare * 3m);
        }

        // ---- Appetite itself ----

        [Fact]
        public void AGuestWhoLovesSeafoodIsLikelierToOrderTheFish()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var seaBass = definitions.GetRecipe("sea-bass");
            var pizza = definitions.GetRecipe("margherita");

            var indifferent = new CustomerParty("a", 2, 0, 30, 1m, CustomerArchetype.Local);
            var lovesFish = new CustomerParty("b", 2, 0, 30, 1m, CustomerArchetype.Local, new[] { "seafood" });

            Assert.True(lovesFish.AppetiteFor(seaBass) > indifferent.AppetiteFor(seaBass));
            Assert.Equal(indifferent.AppetiteFor(pizza), lovesFish.AppetiteFor(pizza));   // unrelated dish unmoved
        }

        [Fact]
        public void ABusinessLuncherWantsSomethingQuick_ARomanticCoupleDoesNot()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var focaccia = definitions.GetRecipe("house-focaccia");   // quick, light, sharing
            var risotto = definitions.GetRecipe("truffle-risotto");   // luxury, rich

            var lunch = new CustomerParty("a", 2, 0, 20, 1m, CustomerArchetype.BusinessLuncher);
            var couple = new CustomerParty("b", 2, 0, 45, 1m, CustomerArchetype.RomanticCouple);

            Assert.True(lunch.AppetiteFor(focaccia) > lunch.AppetiteFor(risotto));
            Assert.True(couple.AppetiteFor(risotto) > couple.AppetiteFor(focaccia));
        }

        [Fact]
        public void AFamilyAvoidsTheLuxuryDish_AndAnInfluencerChasesIt()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var risotto = definitions.GetRecipe("truffle-risotto");

            var family = new CustomerParty("a", 4, 0, 30, 1.35m, CustomerArchetype.Family);
            var influencer = new CustomerParty("b", 2, 0, 20, 0.85m, CustomerArchetype.Influencer);

            Assert.True(influencer.AppetiteFor(risotto) > family.AppetiteFor(risotto) * 2);
        }

        [Fact]
        public void NobodyIsEverCompletelyUnwillingToOrderSomething()
        {
            // Appetite floors at one: people surprise you, and a dish nobody can possibly
            // order would be indistinguishable from a dish that is not on the menu.
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var family = new CustomerParty("a", 4, 0, 30, 1.35m, CustomerArchetype.Family);

            foreach (var recipe in definitions.Recipes)
                Assert.True(family.AppetiteFor(recipe) >= 1);
        }

        [Fact]
        public void ArchetypesDifferInPatienceAndInHowHardTheyJudgePrice()
        {
            var luncher = ArchetypeProfile.For(CustomerArchetype.BusinessLuncher);
            var couple = ArchetypeProfile.For(CustomerArchetype.RomanticCouple);
            var family = ArchetypeProfile.For(CustomerArchetype.Family);

            Assert.True(couple.PatienceLow > luncher.PatienceHigh);          // a couple will wait
            Assert.True(family.PriceSensitivity > luncher.PriceSensitivity); // the card is not the family's
        }

        [Fact]
        public void WhoIsOutDependsOnTheHourAndThePlace()
        {
            var lunchInTheCity = ArchetypeProfile.LikelyAt(Daypart.Lunch, "business-district");
            var lateInTheQuarter = ArchetypeProfile.LikelyAt(Daypart.LateNight, "nightlife-quarter");

            Assert.Contains(CustomerArchetype.BusinessLuncher, lunchInTheCity);
            Assert.DoesNotContain(CustomerArchetype.Influencer, lunchInTheCity);
            Assert.Contains(CustomerArchetype.Influencer, lateInTheQuarter);
        }

        [Fact]
        public void TagsAreDataDriven_LikeEverythingElseAboutADish()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            Assert.True(definitions.GetRecipe("sea-bass").HasTag("seafood"));
            Assert.True(definitions.GetRecipe("truffle-risotto").HasTag("luxury"));
            Assert.True(definitions.GetRecipe("house-focaccia").HasTag("quick"));
            Assert.Empty(definitions.LoadWarnings);
        }

        // ---- Price is the load-bearing half, and this is the experiment that showed it ----

        [Fact]
        public void PriceDrivesOrderRate_NotJustTheScoreAfterwards()
        {
            // The gap this fixes: PriceSensitivity existed from the start but was only ever
            // read on the way OUT, in the satisfaction score, deciding whether a meal felt
            // like value once eaten. It was judging, never choosing — so a guest ordered the
            // 34 risotto as readily as the 14 margherita and grumbled afterwards.
            var thrifty = new CustomerParty("a", 2, 0, 30, 1.4m, CustomerArchetype.Local);
            var expensed = new CustomerParty("b", 2, 0, 30, 0.7m, CustomerArchetype.Local);

            // A dish at twice the menu average.
            Assert.True(thrifty.PriceAppeal(2m) < thrifty.PriceAppeal(1m));
            Assert.True(thrifty.PriceAppeal(2m) < expensed.PriceAppeal(2m));

            // At the menu average, price is not a factor for anybody.
            Assert.Equal(1m, thrifty.PriceAppeal(1m));
            Assert.Equal(1m, expensed.PriceAppeal(1m));
        }

        [Fact]
        public void TheDearestDishStaysOrderable_JustLessOften()
        {
            // A floor, so an expensive dish is a Puzzle rather than decoration.
            var family = new CustomerParty("a", 4, 0, 30, 1.35m, CustomerArchetype.Family);

            Assert.True(family.PriceAppeal(4m) > 0m);
            Assert.True(family.PriceAppeal(4m) < family.PriceAppeal(1m) / 2m);
        }

        [Fact]
        public void FlatteningEveryPriceCollapsesThePuzzleQuadrant()
        {
            // The experiment, pinned. If every dish costs the same, the only thing left
            // separating them is taste — and taste alone does not create enough spread to
            // push anything under the popularity bar. Price is what makes a high-margin,
            // low-volume dish possible, which is the definition of a Puzzle.
            var realPrices = Build(out _, Neighborhood.SuburbanHighStreet());
            var realPrices2 = TradeFor(realPrices, new ServiceWindow("Dinner", 18, 23), 18);

            var flat = Build(out var flatCompany, Neighborhood.SuburbanHighStreet());
            foreach (var id in flat.Menu.RecipeIds) flatCompany.Pricing.SetPrice(id, 16m);
            var withoutPrices = TradeFor(flat, new ServiceWindow("Dinner", 18, 23), 18);

            Assert.NotEmpty(SoldPuzzles(realPrices2));
            Assert.Empty(SoldPuzzles(withoutPrices));

            // And the spread genuinely narrows rather than merely reshuffling.
            var soldWith = realPrices2.Items.Where(i => i.UnitsSold > 0).ToList();
            var soldWithout = withoutPrices.Items.Where(i => i.UnitsSold > 0).ToList();

            var spreadWith = soldWith.Max(i => i.PopularityShare) / soldWith.Min(i => i.PopularityShare);
            var spreadWithout = soldWithout.Max(i => i.PopularityShare) / soldWithout.Min(i => i.PopularityShare);

            Assert.True(spreadWith > spreadWithout * 1.5m,
                "price contributed a spread of " + spreadWith.ToString("0.0") + "x against " +
                spreadWithout.ToString("0.0") + "x without it");
        }
    }
}
