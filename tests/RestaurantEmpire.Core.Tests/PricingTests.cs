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

            Assert.Equal(14.00m, flagship.Costing.MenuPrice("margherita"));
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

            // margherita costs 2.597 at Valley Produce, and ships at 14.00.
            Assert.Equal(11.403m, flagship.Costing.ContributionMargin("margherita"));
            Assert.Equal(0.186m, decimal.Round(flagship.Costing.FoodCostRatio("margherita"), 3));

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
            Assert.Equal(17.50m, flagship.Costing.MenuPrice("margherita"));   // 14.00 * 1.25

            flagship.Pricing.AdjustPrice("margherita", 1.20m);
            Assert.Equal(21.00m, flagship.Costing.MenuPrice("margherita"));   // 17.50 * 1.20, set locally
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
            var asIs = Dinner.Run(cheapPrices, 25, 99);
            cheapCo.Economy.RecordService(cheapPrices, asIs, 0);

            var repriced = Build(out var repricedCo, "premium-harvest");
            foreach (var id in repriced.Menu.RecipeIds) repricedCo.Pricing.AdjustPrice(id, 1.5m);
            var charged = Dinner.Run(repriced, 25, 99);
            repricedCo.Economy.RecordService(repriced, charged, 0);

            // Same ingredients, same guests, same kitchen — only the prices moved.
            //
            // Judged against the industry bands rather than hand-picked thresholds, so this
            // keeps testing the claim rather than a particular balance pass: book each
            // night's takings with the same labour and read what the books say.
            // Labour is a realistic share of the mid-market night's takings, and the SAME
            // absolute figure is booked against both — so this stays a comparison of pricing
            // rather than of a hardcoded number that goes stale whenever demand is retuned.
            var labour = decimal.Round(asIs.Revenue * 0.20m, 2);
            cheapCo.Economy.Record(0, LedgerCategory.LaborCost, labour, "Brigade", cheapPrices.Id);
            repricedCo.Economy.Record(0, LedgerCategory.LaborCost, labour, "Brigade", repriced.Id);

            var asIsBooks = cheapCo.Economy.Summarize(0, 0, cheapPrices.Id);
            var chargedBooks = repricedCo.Economy.Summarize(0, 0, repriced.Id);

            // Bands run Excellent(1) .. Unsustainable(4), so better is lower.
            Assert.Equal(PrimeCostBand.Unsustainable, asIsBooks.Band);   // losing on every cover
            Assert.True(chargedBooks.Band < asIsBooks.Band);             // charging for it moves you up the bands

            // The same night earns more than twice as much once the plate is priced for
            // what went into it.
            Assert.True(charged.Revenue - charged.FoodCost > (asIs.Revenue - asIs.FoodCost) * 2m);
        }

        [Fact]
        public void RaisingPricesCostsGoodwill_SoItIsATradeoffRatherThanFreeMoney()
        {
            // Value perception is driven by food cost ratio against the industry's fair
            // third. Charge more for the same plate and guests notice.
            var modest = Build(out _);
            var modestNight = Dinner.Run(modest, 25, 99);

            var dear = Build(out var dearCo);
            foreach (var id in dear.Menu.RecipeIds) dearCo.Pricing.AdjustPrice(id, 1.75m);
            var dearNight = Dinner.Run(dear, 25, 99);

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
            var midNight = Dinner.Run(mid, 25, 99);

            var premium = Build(out var premiumCo, "premium-harvest");
            foreach (var id in premium.Menu.RecipeIds) premiumCo.Pricing.AdjustPrice(id, 1.5m);
            var premiumNight = Dinner.Run(premium, 25, 99);

            Assert.True(midNight.FoodCost < premiumNight.FoodCost);                       // mid-tier buys cheaper
            Assert.True(premiumNight.AverageSatisfaction > midNight.AverageSatisfaction); // premium pleases more

            // AND premium now SELLS more, which is the part that used to be missing. This
            // assertion was previously Assert.Equal(midNight.Revenue, premiumNight.Revenue)
            // — "identical takings" — and it held only because ingredient quality fed the
            // satisfaction score and nothing else. Budget stock served exactly as many
            // covers as premium, so the cheapest supplier was strictly dominant and free.
            // The old comment here anticipated this: "once satisfaction converts into volume,
            // that is precisely the bet the player will be making." It converts now.
            Assert.True(premiumNight.Revenue > midNight.Revenue);

            // Neither is the right answer, which is the point: premium takes more at the
            // till and hands more of it to the supplier. That is a real bet, not a free lunch.
            Assert.True(midNight.FoodCost / midNight.Revenue
                      < premiumNight.FoodCost / premiumNight.Revenue);
        }

        [Fact]
        public void GougingEmptiesTheRoom_BecauseGuestsCanReadAMenu()
        {
            // Aaron's playtest finding: tripling every price tripled revenue, because
            // satisfaction had no consequence and unhappy guests came back anyway. A guest
            // can judge value from the menu in the window without eating anything, so
            // overpricing now costs trade rather than being free money.
            var fair = Build(out _);
            var fairNight = Dinner.Run(fair, 25, 4242);

            var greedy = Build(out var greedyCo);
            foreach (var id in greedy.Menu.RecipeIds) greedyCo.Pricing.AdjustPrice(id, 3m);
            var greedyNight = Dinner.Run(greedy, 25, 4242);

            Assert.Equal(0, fairNight.PartiesPutOffByThePrices);
            Assert.True(greedyNight.PartiesPutOffByThePrices > 0);
            Assert.True(greedyNight.CoversServed < fairNight.CoversServed / 2);
            Assert.Contains(greedyNight.Diagnostics, d => d.Contains("read the prices and left"));
        }

        [Fact]
        public void AModestMarkupIsStillWorthDoing()
        {
            // The threshold has to be forgiving, or pricing stops being a lever at all.
            var fair = Build(out _);
            var modest = Build(out var modestCo);
            foreach (var id in modest.Menu.RecipeIds) modestCo.Pricing.AdjustPrice(id, 1.5m);

            var fairNight = Dinner.Run(fair, 25, 4242);
            var modestNight = Dinner.Run(modest, 25, 4242);

            Assert.Equal(0, modestNight.PartiesPutOffByThePrices);
            Assert.True(modestNight.Revenue > fairNight.Revenue);
        }
    }
}
