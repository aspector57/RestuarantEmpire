using System.Collections.Generic;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// The customer satisfaction formula, and the headless service that ties Kitchen and
    /// Customers together.
    ///
    /// The service tests are deliberately COMPARATIVE rather than pinned to exact figures:
    /// they assert that a bigger kitchen produces fewer walkouts, that cheaper ingredients
    /// lower quality, and so on. Pinning exact revenue would make every future balance
    /// tweak look like a regression, which is how a test suite stops being trusted.
    /// The one exception is determinism, which is asserted exactly, because that is the
    /// property being tested.
    /// </summary>
    public class ServiceAndSatisfactionTests
    {
        private static Restaurant BuildRestaurant(out Company company, int slotsPerStation = 1, string supplier = "valley-produce")
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme", "Acme Restaurant Group", definitions);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Menu.Add("margherita", "caprese-salad", "truffle-risotto", "house-focaccia");
            company.SupplierPolicy.AssignAll(supplier);

            restaurant.Kitchen.Install("oven", "Wood Oven", slotsPerStation);
            restaurant.Kitchen.Install("garde-manger", "Garde Manger", slotsPerStation);
            restaurant.Kitchen.Install("saute", "Saute", slotsPerStation);

            foreach (var id in definitions.IngredientIds) restaurant.Inventory.Receive(id, 10000m);

            return restaurant;
        }

        private static ServiceResult RunDinner(Restaurant restaurant, double peakRate = 12, long demandSeed = 4242)
        {
            return ServiceSimulation.Run(restaurant, 0, 180, new DemandModel(peakRate, demandSeed), 99);
        }

        // ---- Satisfaction ----

        [Fact]
        public void PromptServiceScoresFullMarksOnSpeed_ASlowPlateDoesNot()
        {
            var restaurant = BuildRestaurant(out _);
            var definitions = restaurant.Company.Definitions;
            var pass = restaurant.Kitchen.OpenPass(0);
            var margherita = definitions.GetRecipe("margherita");

            var party = new DemandModel(1, 1).ArrivalsFor(0, 600)[0];

            var prompt = pass.Fire(margherita, 0, restaurant.Inventory);
            var quality = restaurant.Costing.IngredientQuality("margherita");
            var costRatio = restaurant.Costing.FoodCostRatio("margherita");

            var happy = SatisfactionModel.Evaluate(party, prompt, "Pizza Margherita", quality, costRatio);
            Assert.False(happy.WalkedOut);

            // Near-perfect but not quite 1.0, and that is the model being right rather than
            // generous: this guest's patience rolled at 22 min, so "completely unbothered"
            // runs out around 8.8 min, and the pizza takes 9.
            Assert.True(happy.ServiceSpeed > 0.98m);

            // A genuinely quick dish does score full marks.
            var salad = pass.Fire(definitions.GetRecipe("caprese-salad"), 0, restaurant.Inventory); // 4 min
            var delighted = SatisfactionModel.Evaluate(party, salad, "Caprese Salad",
                restaurant.Costing.IngredientQuality("caprese-salad"),
                restaurant.Costing.FoodCostRatio("caprese-salad"));
            Assert.Equal(1m, delighted.ServiceSpeed);

            // Queue five more behind it and the last one suffers.
            Ticket slow = null;
            for (var i = 0; i < 5; i++) slow = pass.Fire(margherita, 0, restaurant.Inventory);

            var unhappy = SatisfactionModel.Evaluate(party, slow, "Pizza Margherita", quality, costRatio);
            Assert.True(unhappy.ServiceSpeed < happy.ServiceSpeed);
        }

        [Fact]
        public void WaitingPastPatience_IsAWalkout_AndSaysWhichStationCausedIt()
        {
            var restaurant = BuildRestaurant(out _);
            var definitions = restaurant.Company.Definitions;
            var pass = restaurant.Kitchen.OpenPass(0);
            var risotto = definitions.GetRecipe("truffle-risotto"); // 16 minutes each

            var party = new DemandModel(1, 1).ArrivalsFor(0, 600)[0];

            Ticket ticket = null;
            for (var i = 0; i < 6; i++) ticket = pass.Fire(risotto, 0, restaurant.Inventory);

            var result = SatisfactionModel.Evaluate(party, ticket, "Black Truffle Risotto",
                restaurant.Costing.IngredientQuality("truffle-risotto"),
                restaurant.Costing.FoodCostRatio("truffle-risotto"));

            Assert.True(result.WalkedOut);
            Assert.Equal(0m, result.Overall);
            Assert.Contains("Walked out", result.Diagnosis);
            Assert.Contains("saute", result.Diagnosis);   // the named cause, not just "slow"
        }

        [Fact]
        public void CheaperIngredients_LowerPerceivedQuality_Live()
        {
            var restaurant = BuildRestaurant(out var company, supplier: "premium-harvest");

            var premiumQuality = restaurant.Costing.IngredientQuality("margherita");

            company.SupplierPolicy.AssignAll("budget-wholesale");
            var budgetQuality = restaurant.Costing.IngredientQuality("margherita");

            Assert.Equal(1.0m, premiumQuality);   // tier 5 of 5
            Assert.Equal(0.2m, budgetQuality);    // tier 1 of 5
            Assert.True(budgetQuality < premiumQuality);
        }

        [Fact]
        public void SwitchingToCheaperIngredients_RaisesMarginAndLowersSatisfactionTogether()
        {
            // The tradeoff the whole sourcing system exists to create: you cannot cheapen
            // the plate without the guest noticing.
            var restaurant = BuildRestaurant(out var company, slotsPerStation: 4, supplier: "premium-harvest");
            var premiumNight = RunDinner(restaurant);
            var premiumMargin = restaurant.Costing.ContributionMargin("margherita");

            var cheapRestaurant = BuildRestaurant(out var cheapCompany, slotsPerStation: 4, supplier: "budget-wholesale");
            var cheapNight = RunDinner(cheapRestaurant);
            var cheapMargin = cheapRestaurant.Costing.ContributionMargin("margherita");

            Assert.True(cheapMargin > premiumMargin);                                   // better on paper
            Assert.True(cheapNight.AverageSatisfaction < premiumNight.AverageSatisfaction); // worse in the room
        }

        // ---- The service ----

        [Fact]
        public void TheSameSeedAlwaysProducesTheSameNight()
        {
            // Determinism is load-bearing: tests, save/load, and being able to explain a
            // night after the fact all depend on it. Asserted exactly, on purpose.
            var first = RunDinner(BuildRestaurant(out _));
            var second = RunDinner(BuildRestaurant(out _));

            Assert.Equal(first.Revenue, second.Revenue);
            Assert.Equal(first.CoversServed, second.CoversServed);
            Assert.Equal(first.Walkouts, second.Walkouts);
            Assert.Equal(first.AverageSatisfaction, second.AverageSatisfaction);
            Assert.Equal(first.LongestWaitMinutes, second.LongestWaitMinutes);
            Assert.Equal(first.UnitsSoldByRecipeId, second.UnitsSoldByRecipeId);
        }

        [Fact]
        public void ADifferentSeedProducesADifferentNight()
        {
            var quiet = ServiceSimulation.Run(BuildRestaurant(out _), 0, 180, new DemandModel(12, 4242), 99);
            var other = ServiceSimulation.Run(BuildRestaurant(out _), 0, 180, new DemandModel(12, 777), 99);

            Assert.NotEqual(quiet.Revenue, other.Revenue);
        }

        [Fact]
        public void AnUndersizedKitchenCostsRealMoney_AndABiggerOneRecoversIt()
        {
            // The design's dominant layout failure mode, now measurable rather than asserted.
            var cramped = RunDinner(BuildRestaurant(out _, slotsPerStation: 1));
            var roomy = RunDinner(BuildRestaurant(out _, slotsPerStation: 4));

            Assert.True(cramped.Walkouts > 0);
            Assert.True(roomy.Walkouts < cramped.Walkouts);
            Assert.True(roomy.Revenue > cramped.Revenue);
            Assert.True(roomy.LongestWaitMinutes < cramped.LongestWaitMinutes);
            Assert.True(roomy.AverageSatisfaction > cramped.AverageSatisfaction);
        }

        [Fact]
        public void EveryComplaintNamesASpecificCause_NeverJustAScore()
        {
            // Phase 6.2's legibility contract, enforced.
            var night = RunDinner(BuildRestaurant(out _));

            Assert.NotEmpty(night.Diagnostics);
            Assert.All(night.Diagnostics, d => Assert.False(string.IsNullOrWhiteSpace(d)));

            // At least one complaint points at the station responsible.
            Assert.Contains(night.Diagnostics, d => d.Contains("station"));
        }

        [Fact]
        public void ASmallDiningRoomTurnsGuestsAway_EvenWhenTheKitchenCouldCope()
        {
            var restaurant = BuildRestaurant(out _, slotsPerStation: 4);
            restaurant.SeatingCapacity = 4;

            var night = RunDinner(restaurant);

            Assert.True(night.PartiesTurnedAway > 0);
            Assert.Contains(night.Diagnostics, d => d.Contains("dining room full"));
        }

        [Fact]
        public void AnEmptyWalkIn86sEverything_WithoutCrashingTheService()
        {
            var restaurant = BuildRestaurant(out _);

            foreach (var id in restaurant.Company.Definitions.IngredientIds)
                restaurant.Inventory.TryConsume(id, restaurant.Inventory.QuantityOf(id));

            var night = RunDinner(restaurant);

            Assert.True(night.EightySixed > 0);
            Assert.Equal(0, night.CoversServed);
            Assert.Equal(0m, night.Revenue);
            Assert.Contains(night.Diagnostics, d => d.Contains("86'd"));
        }

        // ---- The payoff: real sales driving the menu matrix ----

        [Fact]
        public void TheMenuMatrixNowClassifiesAgainstWhatTheRestaurantActuallySold()
        {
            // Until now, Kasavana-Smith was fed invented figures. This is the point where
            // the loop closes: the simulation sells dishes, and the menu grades itself on
            // its own results.
            var restaurant = BuildRestaurant(out _, slotsPerStation: 4);
            var night = RunDinner(restaurant);

            Assert.True(night.TotalUnitsSold > 0);

            var analysis = MenuEngineering.Analyze(
                restaurant,
                night.UnitsSoldByRecipeId.ToDictionary(p => p.Key, p => p.Value));

            Assert.Equal(night.TotalUnitsSold, analysis.TotalUnitsSold);
            Assert.All(analysis.Items, item => Assert.True(item.UnitsSold >= 0));

            // Every dish landed in a real quadrant, derived from live margins and real volume.
            Assert.Equal(4, analysis.Items.Count);
            Assert.All(analysis.Items, item =>
                Assert.True(System.Enum.IsDefined(typeof(MenuClassification), item.Classification)));
        }

        [Fact]
        public void RevenueMatchesTheDishesActuallyPaidFor()
        {
            var restaurant = BuildRestaurant(out _, slotsPerStation: 4);
            var night = RunDinner(restaurant);

            var expected = night.UnitsSoldByRecipeId.Sum(
                pair => restaurant.Company.Definitions.GetRecipe(pair.Key).MenuPrice * pair.Value);

            Assert.Equal(expected, night.Revenue);

            // Walkouts and 86'd dishes are not sales.
            Assert.Equal(night.CoversServed, night.TotalUnitsSold);
        }
    }
}
