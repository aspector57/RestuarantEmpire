using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// The Advisor — and mostly, the things it is forbidden from doing.
    ///
    /// The design's sharpest worry (Phase 10, finding 2.1) is that an Advisor which says
    /// "feature this dish, it's a Puzzle" has PERFORMED the menu engineering and handed over
    /// the conclusion, dissolving one of the three mechanics called central. These tests
    /// pin the line: it may say what it sees at any tier, but it may only say what to do
    /// about chores and tactical proposals.
    /// </summary>
    public class AdvisorTests
    {
        private static Restaurant Build(out Company company, decimal cash = 60000m, bool trading = false)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme", "Acme", definitions, cash);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Location = Neighborhood.SuburbanHighStreet();
            restaurant.FloorArea = 90m;
            restaurant.Menu.Add("margherita", "caprese-salad", "truffle-risotto", "house-focaccia");
            company.SupplierPolicy.AssignAll("valley-produce");

            // Bought from the catalogue so the equipment has a real footprint — installing
            // with a zero footprint would leave the floor never filling up.
            restaurant.BuyEquipment(definitions.GetEquipment("oven-commercial"), 4);
            restaurant.BuyEquipment(definitions.GetEquipment("gm-refrigerated"), 4);
            restaurant.BuyEquipment(definitions.GetEquipment("saute-commercial"), 4);

            foreach (var id in definitions.IngredientIds)
            {
                restaurant.Inventory.SetPar(id, 100m, 1000m);
                restaurant.Inventory.Receive(id, 1000m);
            }

            // Most of these tests are about what the Advisor notices in a STATIC restaurant,
            // so it is deliberately unstaffed and unfurnished by default. The proposals tier
            // needs a place that actually trades.
            if (trading)
            {
                restaurant.BuyTables("t", "Tables", 3600m, 30);
                for (var i = 0; i < 6; i++)
                    restaurant.Payroll.Hire(new Employee("c" + i, "Cook " + i, StaffRole.Cook, 16m));
                for (var i = 0; i < 3; i++)
                    restaurant.Payroll.Hire(new Employee("s" + i, "Server " + i, StaffRole.Server, 12m));
            }

            return restaurant;
        }

        // ---- The line it must not cross ----

        [Fact]
        public void StrategicObservationsAreNeverAYesNoQuestion()
        {
            // The protection against the game becoming a notification inbox: menu strategy,
            // pricing, expansion and hiring direction get named, never proposed.
            var restaurant = Build(out var company, cash: 500m);
            company.Economy.Record(0, LedgerCategory.Revenue, 1000m, "Trading", restaurant.Id);
            company.Economy.Record(0, LedgerCategory.FoodCost, 900m, "Ingredients", restaurant.Id);

            var strategic = new Advisor(restaurant).Review().Where(s => s.Tier == AdvisorTier.Strategic).ToList();

            Assert.NotEmpty(strategic);
            Assert.All(strategic, s => Assert.Null(s.Question));
        }

        [Fact]
        public void ItNeverHandsOverTheMenuEngineeringVerdict()
        {
            // It may say "this earns most and nobody orders it" — the observation. It may
            // not say "this is a Puzzle", which is the game doing the analysis for you.
            var restaurant = Build(out _, trading: true);
            var trading = Dinner.Run(restaurant, 25, 4242);

            var suggestions = new Advisor(restaurant).Review(trading);

            Assert.All(suggestions, s =>
            {
                var text = (s.Headline + " " + s.Reasoning + " " + s.Question).ToLowerInvariant();
                Assert.DoesNotContain("puzzle", text);
                Assert.DoesNotContain("plowhorse", text);
                Assert.DoesNotContain("kasavana", text);
            });
        }

        [Fact]
        public void EveryProposalCarriesItsReasoningAndItsQuestion()
        {
            var restaurant = Build(out _, trading: true);
            var trading = Dinner.Run(restaurant, 25, 4242);

            var proposals = new Advisor(restaurant).Review(trading)
                .Where(s => s.Tier == AdvisorTier.Proposal).ToList();

            Assert.All(proposals, s =>
            {
                Assert.False(string.IsNullOrWhiteSpace(s.Question));
                Assert.False(string.IsNullOrWhiteSpace(s.Reasoning));
                Assert.True(s.Reasoning.Any(char.IsDigit), "reasoning should cite numbers, not just assert");
            });
        }

        [Fact]
        public void ChoresAreStatedFlatly_NotAsked()
        {
            // If the right answer is nearly always the same, it does not deserve a question.
            var restaurant = Build(out _);
            restaurant.Inventory.TryConsume("tomato", 950m);   // now below par

            var chores = new Advisor(restaurant).Review().Where(s => s.Tier == AdvisorTier.Chore).ToList();

            Assert.NotEmpty(chores);
            Assert.All(chores, s => Assert.Null(s.Question));
            Assert.Contains(chores, s => s.SubjectId == "tomato");
        }

        // ---- That it notices the right things ----

        [Fact]
        public void ItNoticesEquipmentYouHaveNotStaffed()
        {
            var restaurant = Build(out _);
            restaurant.Payroll.Hire(new Employee("c1", "Cook", StaffRole.Cook, 16m));   // 1 cook, 12 units

            var suggestions = new Advisor(restaurant).Review();

            var idle = suggestions.Single(s => s.Id == "understaffed:kitchen");
            Assert.Equal(AdvisorTier.Chore, idle.Tier);
            Assert.Contains("12 units", idle.Reasoning);
            Assert.Contains("sits idle", idle.Reasoning);
        }

        [Fact]
        public void ItNoticesTablesYouCannotServe()
        {
            var restaurant = Build(out _);
            restaurant.BuyTables("t", "Tables", 3360m, 28);
            restaurant.Payroll.Hire(new Employee("s1", "Server", StaffRole.Server, 12m));   // covers 14 of 28

            var suggestion = new Advisor(restaurant).Review().Single(s => s.Id == "understaffed:floor");

            Assert.Contains("28 covers", suggestion.Reasoning);
            Assert.Contains("14", suggestion.Reasoning);
        }

        [Fact]
        public void ItAsksAboutAProfitableDishNobodyOrders_AndNamesWhatFeaturingWouldCost()
        {
            var restaurant = Build(out _, trading: true);

            // Featuring one dish is what pushes the others' share below the popularity bar,
            // which is how a genuine Puzzle arises. (Without it, guests order uniformly at
            // random and every dish lands near an equal share — see CLAUDE.md on why dish
            // appeal is the M2 gap that makes the popularity axis mean something.)
            restaurant.Menu.Feature("margherita");
            var trading = Dinner.Run(restaurant, 25, 4242);

            var suggestions = new Advisor(restaurant).Review(trading);
            var feature = suggestions.FirstOrDefault(s => s.Id.StartsWith("feature:"));

            Assert.NotNull(feature);
            Assert.Equal(AdvisorTier.Proposal, feature.Tier);
            Assert.Contains("Want it featured?", feature.Question);
            Assert.Contains("a plate against a menu average of", feature.Reasoning);

            // And once it IS featured, it stops asking.
            restaurant.Menu.Feature(feature.SubjectId);
            Assert.DoesNotContain(new Advisor(restaurant).Review(trading),
                s => s.Id == "feature:" + feature.SubjectId);
        }

        [Fact]
        public void ItSurfacesOpportunities_NotOnlyProblems()
        {
            // Phase 10's finding 1.1: an Advisor made purely of warnings turns the game into
            // a maintenance exercise, which is the wrong shape for a fantasy about building
            // something.
            var restaurant = Build(out _);
            restaurant.BuyTables("t", "Tables", 4800m, 35);   // 49.0m2 of the 49.6m2 left

            var suggestions = new Advisor(restaurant).Review();
            var opportunity = suggestions.FirstOrDefault(s => s.Id == "opportunity:space");

            Assert.NotNull(opportunity);
            Assert.Equal(AdvisorTier.Strategic, opportunity.Tier);
            Assert.Contains("would allow another", opportunity.Reasoning);
            Assert.Null(opportunity.Question);   // named, not proposed
        }

        [Fact]
        public void OnASiteWithNoHeadroom_ItPointsAtUpgradingInstead()
        {
            var restaurant = Build(out _);
            restaurant.Location = Neighborhood.CityCenter();
            restaurant.FloorArea = restaurant.Location.MaxFloorArea;
            restaurant.BuyTables("t", "Tables", 9000m, 63);   // fill it

            var suggestions = new Advisor(restaurant).Review();

            Assert.Contains(suggestions, s => s.Id == "opportunity:upgrade");
        }

        [Fact]
        public void AHealthyQuietRestaurantIsNotNaggedAtAll()
        {
            // The Advisor must be capable of saying nothing. If it always has an opinion,
            // the player stops reading it.
            var restaurant = Build(out _);
            restaurant.BuyTables("t", "Tables", 2400m, 20);
            restaurant.Payroll.Hire(new Employee("c1", "Cook", StaffRole.Cook, 16m));
            restaurant.Payroll.Hire(new Employee("c2", "Cook", StaffRole.Cook, 16m));
            restaurant.Payroll.Hire(new Employee("c3", "Cook", StaffRole.Cook, 16m));
            restaurant.Payroll.Hire(new Employee("c4", "Cook", StaffRole.Cook, 16m));
            restaurant.Payroll.Hire(new Employee("c5", "Cook", StaffRole.Cook, 16m));
            restaurant.Payroll.Hire(new Employee("c6", "Cook", StaffRole.Cook, 16m));
            restaurant.Payroll.Hire(new Employee("s1", "Server", StaffRole.Server, 12m));
            restaurant.Payroll.Hire(new Employee("s2", "Server", StaffRole.Server, 12m));

            Assert.Empty(new Advisor(restaurant).Review());
        }
    }
}
