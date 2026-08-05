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


            // These tests are about pricing, not about being the new place in town. Open the
            // doors on a restaurant the neighborhood already knows, so the awareness ramp
            // does not quietly halve the sample.
            restaurant.Reputation.Restore(Reputation.Neutral, Reputation.MealsToBecomeKnown);
            return restaurant;
        }


        /// <summary>
        /// Margin computed from the CONTENT rather than pinned to it. Literals here encode the
        /// menu, so repricing a dish in a data file fails tests whose claims are still true —
        /// hostile to Architecture Rule 2, which this suite exists to protect.
        /// </summary>
        private static decimal MarginOf(string recipeId, string supplierId = "valley-produce")
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var recipe = definitions.GetRecipe(recipeId);
            var supplier = definitions.GetSupplier(supplierId);

            var cost = 0m;
            foreach (var line in recipe.Ingredients)
                cost += supplier.UnitPriceFor(line.IngredientId) * line.Quantity;

            return recipe.MenuPrice - cost;
        }

        [Fact]
        public void WithNoPricesSet_TheDishChargesWhatItShippedWith()
        {
            var flagship = Build(out _);

            // ASSERTED AGAINST THE DEFINITION, NOT A LITERAL. The claim is "with nothing set,
            // a dish charges what it shipped with" — pinning 14.00 encodes the CONTENT, so
            // repricing the menu in a data file failed eighteen tests that were all still true.
            var shipped = flagship.Company.Definitions.GetRecipe("margherita").MenuPrice;
            Assert.Equal(shipped, flagship.Costing.MenuPrice("margherita"));
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
            flagship.Pricing.SetPrice("margherita", 19.00m);   // smarter neighborhood

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
            Assert.Equal(MarginOf("margherita"), flagship.Costing.ContributionMargin("margherita"));
            var recipe = flagship.Company.Definitions.GetRecipe("margherita");
            Assert.Equal(decimal.Round(flagship.Costing.PlateCost("margherita") / recipe.MenuPrice, 3),
                         decimal.Round(flagship.Costing.FoodCostRatio("margherita"), 3));

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

            var shipped = flagship.Company.Definitions.GetRecipe("margherita").MenuPrice;

            company.Pricing.AdjustPrice("margherita", 1.25m);
            Assert.Equal(shipped * 1.25m, flagship.Costing.MenuPrice("margherita"));

            flagship.Pricing.AdjustPrice("margherita", 1.20m);
            Assert.Equal(shipped * 1.25m * 1.20m, flagship.Costing.MenuPrice("margherita"));   // set locally
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
            foreach (var id in repriced.Menu.RecipeIds) repricedCo.Pricing.AdjustPrice(id, 1.2m);
            var charged = Dinner.Run(repriced, 25, 99);
            repricedCo.Economy.RecordService(repriced, charged, 0);

            // Same ingredients, same guests, same kitchen — only the prices moved.
            //
            // Judged against the industry bands rather than hand-picked thresholds, so this
            // keeps testing the claim rather than a particular balance pass: book each
            // night's takings with the same labor and read what the books say.
            // Labor is a realistic share of the mid-market night's takings, and the SAME
            // absolute figure is booked against both — so this stays a comparison of pricing
            // rather than of a hardcoded number that goes stale whenever demand is retuned.
            // Food is paid on delivery now, so book what each night consumed explicitly to
            // keep this a comparison of PRICING rather than of when the invoices landed.
            cheapCo.Economy.Record(0, LedgerCategory.FoodCost, asIs.FoodCost, "Ingredients", cheapPrices.Id);
            repricedCo.Economy.Record(0, LedgerCategory.FoodCost, charged.FoodCost, "Ingredients", repriced.Id);

            var labor = decimal.Round(asIs.Revenue * 0.20m, 2);
            cheapCo.Economy.Record(0, LedgerCategory.LaborCost, labor, "Brigade", cheapPrices.Id);
            repricedCo.Economy.Record(0, LedgerCategory.LaborCost, labor, "Brigade", repriced.Id);

            var asIsBooks = cheapCo.Economy.Summarize(0, 0, cheapPrices.Id);
            var chargedBooks = repricedCo.Economy.Summarize(0, 0, repriced.Id);

            // Bands run Excellent(1) .. Unsustainable(4), so better is lower.
            //
            // No longer pinned to Unsustainable. Premium stock at mid-market prices used to
            // book a hopeless prime cost; now that price steers what people ORDER, fewer of
            // the dear dishes sell and the ratio lands merely bad rather than fatal. The claim
            // that survives — and it is the one the test is named for — is that the food cost
            // is unhealthy until you charge for what you bought, and charging fixes it.
            Assert.True(asIsBooks.FoodCostRatio > 0.38m,
                "premium stock at mid-market prices should read badly: " + asIsBooks.FoodCostRatio.ToString("P0"));
            Assert.True(chargedBooks.FoodCostRatio < asIsBooks.FoodCostRatio,
                "charged " + chargedBooks.FoodCostRatio.ToString("P1") + " vs asIs " + asIsBooks.FoodCostRatio.ToString("P1"));
            Assert.True(chargedBooks.Band <= asIsBooks.Band, "bands " + chargedBooks.Band + " vs " + asIsBooks.Band);

            // The same night earns appreciably more once the plate is priced for what went
            // into it. Not "twice" any more — that figure came from a model where raising
            // prices cost nothing until you doubled them. Price now decides who turns up, so
            // charging properly is a real gain with a real price attached rather than free.
            // Measured here: 935.81 gross at the designed prices against 995.76 at 1.2x — a
            // 6% gain, where it used to be over 100%. That collapse in headroom IS the change:
            // when price decides who turns up, most of what you gain per cover you give back
            // in covers. The clear win is the food-cost ratio above (41.9% to 35.5%), which is
            // the number the claim is really about — charging for what you bought is what
            // makes premium sourcing survivable, not what makes it lucrative.
            Assert.True(charged.Revenue - charged.FoodCost > (asIs.Revenue - asIs.FoodCost) * 1.05m,
                "gross charged " + (charged.Revenue - charged.FoodCost) + " vs asIs " + (asIs.Revenue - asIs.FoodCost));
        }

        [Fact]
        public void RaisingPricesCostsGoodwill_SoItIsATradeoffRatherThanFreeMoney()
        {
            // Charge more for the same plate and guests notice — and now MOST of that shows
            // up as people who never come, rather than as a crowd that arrives and storms off.
            var modest = Build(out _);
            var modestNight = Dinner.Run(modest, 25, 99);

            var dear = Build(out var dearCo);
            foreach (var id in dear.Menu.RecipeIds) dearCo.Pricing.AdjustPrice(id, 1.25m);
            var dearNight = Dinner.Run(dear, 25, 99);

            // A modest rise is still worth making...
            Assert.True(dearNight.Revenue > modestNight.Revenue);
            Assert.True(dearNight.AverageSatisfaction < modestNight.AverageSatisfaction);

            // ...and it already costs custom, which is the point. Under the old model nothing
            // at all happened until double the designed price.
            Assert.True(dearNight.PartiesPutOffByThePrices > 0);
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

            // No diagnostic line any more, and that is the change: at triple the price people
            // do not turn up to be disappointed. Most of the loss is now guests who never set
            // off, which is how it works in life — you know roughly what a place costs before
            // you go. Reading a menu at the door and leaving still happens, but it is a city
            // behaviour and a small remainder (Neighborhood.MenuReadAtTheDoor).
        }

        [Fact]
        public void AModestMarkupIsStillWorthDoing()
        {
            // Multipliers here came down from 1.5x when price began deciding who turns up at
            // all. The profitable band now peaks around 1.4x rather than 2.5x, so 1.5x is past
            // the top rather than comfortably inside it — the claims are unchanged, the
            // numbers that express them are not.
            // The threshold has to be forgiving, or pricing stops being a lever at all.
            var fair = Build(out _);
            var modest = Build(out var modestCo);
            foreach (var id in modest.Menu.RecipeIds) modestCo.Pricing.AdjustPrice(id, 1.25m);

            var fairNight = Dinner.Run(fair, 25, 4242);
            var modestNight = Dinner.Run(modest, 25, 4242);

            // Resistance is no longer a wall you either hit or do not — it is a chance that
            // climbs with the price, so half again over the designed price costs you a few
            // parties rather than none. What has to stay true is that it is WORTH doing.
            Assert.True(modestNight.Revenue > fairNight.Revenue);
            Assert.True(modestNight.PartiesPutOffByThePrices < modestNight.CoversServed / 4,
                "a modest markup should cost a few covers, not the room");
        }
    }
}
