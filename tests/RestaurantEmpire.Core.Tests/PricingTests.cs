using System;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Definitions;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Menu pricing as a player decision rather than content.
    ///
    /// Resolution walks Restaurant -> Company -> the price the recipe shipped with, the
    /// same chain pattern as sourcing. This is what makes expensive ingredients a strategy
    /// instead of a trap: you cannot buy premium and charge mid-market, but you can buy
    /// premium and charge accordingly.
    /// </summary>
    public class PricingTests
    {
        private static Restaurant Build(out Company company, string supplier = "valley-produce", int slots = 6)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme", "Acme Restaurant Group", definitions, 50000m);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Menu.Add("margherita", "caprese-salad", "truffle-risotto", "house-focaccia");
            company.SupplierPolicy.AssignAll(supplier);

            restaurant.Kitchen.Install("oven", "Wood Oven", slots);
            restaurant.Kitchen.Install("garde-manger", "Garde Manger", slots);
            restaurant.Kitchen.Install("saute", "Saute", slots);

            foreach (var id in definitions.IngredientIds) restaurant.Inventory.Receive(id, 100000m);

            return restaurant;
        }

        [Fact]
        public void WithNoPricesSet_TheDishChargesWhatItShippedWith()
        {
            var flagship = Build(out _);

            Assert.Equal(12.00m, flagship.Costing.MenuPrice("margherita"));
            Assert.Equal("menu default", flagship.Pricing.ResolvedFromScopeName("margherita"));
            Assert.Empty(flagship.Pricing.LocalPrices);
        }

        [Fact]
        public void ACompanyPrice_ReachesEveryLocationThatHasNotOverriddenIt()
        {
            var flagship = Build(out var company);
            var truck = company.OpenRestaurant("truck", "Acme Truck", LocationType.FoodTruck);
            truck.Menu.Add("margherita");

            company.Pricing.SetPrice("margherita", 15.00m);

            Assert.Equal(15.00m, flagship.Costing.MenuPrice("margherita"));
            Assert.Equal(15.00m, truck.Costing.MenuPrice("margherita"));
            Assert.Equal("Acme Restaurant Group", flagship.Pricing.ResolvedFromScopeName("margherita"));
        }

        [Fact]
        public void ALocationCanChargeItsOwn_WithoutMovingTheRestOfTheGroup()
        {
            // Phase 9's franchise requirement: per-instance overrides for local pricing.
            var flagship = Build(out var company);
            var truck = company.OpenRestaurant("truck", "Acme Truck", LocationType.FoodTruck);
            truck.Menu.Add("margherita");

            company.Pricing.SetPrice("margherita", 15.00m);
            flagship.Pricing.SetPrice("margherita", 19.00m);   // smarter neighbourhood

            Assert.Equal(19.00m, flagship.Costing.MenuPrice("margherita"));
            Assert.Equal(15.00m, truck.Costing.MenuPrice("margherita"));

            Assert.Equal("The Flagship", flagship.Pricing.ResolvedFromScopeName("margherita"));
            Assert.Equal("Acme Restaurant Group", truck.Pricing.ResolvedFromScopeName("margherita"));
        }

        [Fact]
        public void ClearingALocalPrice_FallsBackToWhateverTheCompanyChargesNow()
        {
            var flagship = Build(out var company);

            flagship.Pricing.SetPrice("margherita", 19.00m);
            company.Pricing.SetPrice("margherita", 15.00m);

            Assert.Equal(19.00m, flagship.Costing.MenuPrice("margherita"));

            Assert.True(flagship.Pricing.ClearOverride("margherita"));
            Assert.Equal(15.00m, flagship.Costing.MenuPrice("margherita"));   // current company price, not a snapshot

            Assert.False(flagship.Pricing.ClearOverride("margherita"));
        }

        [Fact]
        public void RepricingMovesMarginRatioAndClassification_Live()
        {
            var flagship = Build(out var company);

            // margherita costs 2.597 at Valley Produce.
            Assert.Equal(9.403m, flagship.Costing.ContributionMargin("margherita"));
            Assert.Equal(0.216m, decimal.Round(flagship.Costing.FoodCostRatio("margherita"), 3));

            company.Pricing.SetPrice("margherita", 18.00m);

            Assert.Equal(15.403m, flagship.Costing.ContributionMargin("margherita"));
            Assert.Equal(0.144m, decimal.Round(flagship.Costing.FoodCostRatio("margherita"), 3));

            // Nothing was recalculated by hand and no recipe was edited — same live-read
            // rule the sourcing chain runs on.
        }

        [Fact]
        public void AdjustPrice_AppliesAMultiplierToWhateverIsCurrentlyResolved()
        {
            var flagship = Build(out var company);

            company.Pricing.AdjustPrice("margherita", 1.25m);
            Assert.Equal(15.00m, flagship.Costing.MenuPrice("margherita"));   // 12.00 * 1.25

            flagship.Pricing.AdjustPrice("margherita", 1.20m);
            Assert.Equal(18.00m, flagship.Costing.MenuPrice("margherita"));   // 15.00 * 1.20, set locally
            Assert.True(flagship.Pricing.HasLocalOverride("margherita"));
        }

        [Fact]
        public void NegativePricesAreRejected_ButComplimentaryDishesAreAllowed()
        {
            var flagship = Build(out _);

            Assert.Throws<ArgumentOutOfRangeException>(() => flagship.Pricing.SetPrice("margherita", -1m));
            Assert.Throws<DefinitionNotFoundException>(() => flagship.Pricing.SetPrice("unicorn-steak", 10m));

            flagship.Pricing.SetPrice("house-focaccia", 0m);   // comped bread service
            Assert.Equal(0m, flagship.Costing.MenuPrice("house-focaccia"));
            Assert.Equal(0m, flagship.Costing.FoodCostRatio("house-focaccia")); // no divide-by-zero
        }

        // ---- The payoff ----

        [Fact]
        public void PremiumSourcingIsUnviableAtMidMarketPrices_AndViableOnceYouChargeForIt()
        {
            // Before pricing existed, buying the best ingredients was simply a losing move.
            // That was not a balance problem, it was a missing lever.
            var cheapPrices = Build(out var cheapCo, "premium-harvest");
            var asIs = ServiceSimulation.Run(cheapPrices, 0, 180, new DemandModel(25, 4242), 99);
            cheapCo.Economy.RecordService(cheapPrices, asIs, 0);

            var repriced = Build(out var repricedCo, "premium-harvest");
            foreach (var id in repriced.Menu.RecipeIds) repricedCo.Pricing.AdjustPrice(id, 1.5m);
            var charged = ServiceSimulation.Run(repriced, 0, 180, new DemandModel(25, 4242), 99);
            repricedCo.Economy.RecordService(repriced, charged, 0);

            // Same ingredients, same guests, same kitchen — only the prices moved.
            Assert.True(asIs.FoodCost / asIs.Revenue > 0.70m);        // ruinous
            Assert.True(charged.FoodCost / charged.Revenue < 0.55m);  // survivable

            var asIsProfit = asIs.Revenue - asIs.FoodCost - 324m - 300m;
            var chargedProfit = charged.Revenue - charged.FoodCost - 324m - 300m;

            Assert.True(asIsProfit < 0m);       // was losing money
            Assert.True(chargedProfit > 0m);    // now making it
        }

        [Fact]
        public void RaisingPricesCostsGoodwill_SoItIsATradeoffRatherThanFreeMoney()
        {
            // Value perception is driven by food cost ratio against the industry's fair
            // third. Charge more for the same plate and guests notice.
            var modest = Build(out _);
            var modestNight = ServiceSimulation.Run(modest, 0, 180, new DemandModel(25, 4242), 99);

            var dear = Build(out var dearCo);
            foreach (var id in dear.Menu.RecipeIds) dearCo.Pricing.AdjustPrice(id, 1.75m);
            var dearNight = ServiceSimulation.Run(dear, 0, 180, new DemandModel(25, 4242), 99);

            Assert.True(dearNight.Revenue > modestNight.Revenue);                       // more money in
            Assert.True(dearNight.AverageSatisfaction < modestNight.AverageSatisfaction); // less goodwill
        }

        [Fact]
        public void AtTheSamePrices_PremiumBuysHappinessAndMidTierBuysProfit()
        {
            // The tradeoff stated plainly, with price held constant so it is genuinely
            // sourcing being compared and not pricing.
            var mid = Build(out var midCo, "valley-produce");
            foreach (var id in mid.Menu.RecipeIds) midCo.Pricing.AdjustPrice(id, 1.5m);
            var midNight = ServiceSimulation.Run(mid, 0, 180, new DemandModel(25, 4242), 99);

            var premium = Build(out var premiumCo, "premium-harvest");
            foreach (var id in premium.Menu.RecipeIds) premiumCo.Pricing.AdjustPrice(id, 1.5m);
            var premiumNight = ServiceSimulation.Run(premium, 0, 180, new DemandModel(25, 4242), 99);

            Assert.Equal(midNight.Revenue, premiumNight.Revenue);                             // identical takings
            Assert.True(midNight.FoodCost < premiumNight.FoodCost);                           // mid-tier keeps more
            Assert.True(premiumNight.AverageSatisfaction > midNight.AverageSatisfaction);     // premium pleases more

            // Neither is the right answer. Once Reputation converts satisfaction into
            // volume at M1, that is precisely the bet the player will be making.
        }
    }
}
