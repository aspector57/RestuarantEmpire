using System;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// The books: cash, the ledger, and prime cost — the metric the design says should
    /// always be visible, because unlike rent it is controllable week to week.
    /// </summary>
    public class EconomyTests
    {
        private static Restaurant BuildTradingRestaurant(out Company company, decimal openingCash = 20000m)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme", "Acme Restaurant Group", definitions, openingCash);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Menu.Add("margherita", "caprese-salad", "truffle-risotto", "house-focaccia");
            company.SupplierPolicy.AssignAll("valley-produce");

            restaurant.Kitchen.Install("oven", "Wood Oven", 4);
            restaurant.Kitchen.Install("garde-manger", "Garde Manger", 4);
            restaurant.Kitchen.Install("saute", "Saute", 4);

            foreach (var id in definitions.IngredientIds) restaurant.Inventory.Receive(id, 10000m);

            return restaurant;
        }

        [Fact]
        public void OpeningCashIsRecordedAsAnEntry_NotJustSetAsANumber()
        {
            var company = new Company("acme", "Acme Restaurant Group",
                JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory), 20000m);

            Assert.Equal(20000m, company.Economy.CashOnHand);
            Assert.Single(company.Economy.Entries);
            Assert.Equal(LedgerCategory.CapitalContribution, company.Economy.Entries[0].Category);
        }

        [Fact]
        public void CategoryDecidesDirection_SoACostCanNeverBeBookedAsIncome()
        {
            var restaurant = BuildTradingRestaurant(out var company, openingCash: 1000m);

            company.Economy.Record(0, LedgerCategory.Revenue, 250m, "Dinner service", restaurant.Id);
            company.Economy.Record(0, LedgerCategory.FoodCost, 80m, "Ingredients", restaurant.Id);
            company.Economy.Record(0, LedgerCategory.LaborCost, 120m, "Payroll", restaurant.Id);

            Assert.Equal(1050m, company.Economy.CashOnHand); // 1000 + 250 - 80 - 120

            // Negative amounts are rejected outright — direction is the category's job.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => company.Economy.Record(0, LedgerCategory.Revenue, -50m, "Nonsense"));
        }

        [Fact]
        public void PrimeCostIsFoodPlusLaborOverRevenue_AndBandsAgainstTheRealIndustryRanges()
        {
            var restaurant = BuildTradingRestaurant(out var company, openingCash: 0m);

            company.Economy.Record(0, LedgerCategory.Revenue, 1000m, "Service", restaurant.Id);
            company.Economy.Record(0, LedgerCategory.FoodCost, 300m, "Ingredients", restaurant.Id);
            company.Economy.Record(0, LedgerCategory.LaborCost, 320m, "Payroll", restaurant.Id);
            company.Economy.Record(0, LedgerCategory.Overhead, 150m, "Rent", restaurant.Id);

            var books = company.Economy.SummarizeAll();

            Assert.Equal(620m, books.PrimeCost);
            Assert.Equal(0.62m, books.PrimeCostRatio);
            Assert.Equal(0.30m, books.FoodCostRatio);
            Assert.Equal(0.32m, books.LaborCostRatio);
            Assert.Equal(PrimeCostBand.Healthy, books.Band);   // 55-65% is where a healthy kitchen lives

            // Overhead is real but sits outside prime cost, exactly as the industry treats it.
            Assert.Equal(230m, books.NetProfit);               // 1000 - 620 - 150
        }

        [Theory]
        [InlineData(500, PrimeCostBand.Excellent)]      // 50%
        [InlineData(620, PrimeCostBand.Healthy)]        // 62%
        [InlineData(680, PrimeCostBand.Tight)]          // 68% — fine dining territory
        [InlineData(750, PrimeCostBand.Unsustainable)]  // 75% — losing on every cover
        public void PrimeCostBandsMatchThePhase2Research(int primeCost, PrimeCostBand expected)
        {
            var restaurant = BuildTradingRestaurant(out var company, openingCash: 0m);

            company.Economy.Record(0, LedgerCategory.Revenue, 1000m, "Service", restaurant.Id);
            company.Economy.Record(0, LedgerCategory.FoodCost, primeCost, "Ingredients", restaurant.Id);

            Assert.Equal(expected, company.Economy.SummarizeAll().Band);
        }

        [Fact]
        public void NoRevenueMeansNoJudgment_RatherThanADivideByZero()
        {
            var restaurant = BuildTradingRestaurant(out var company, openingCash: 5000m);
            company.Economy.Record(0, LedgerCategory.FoodCost, 400m, "Opening stock order", restaurant.Id);

            var books = company.Economy.SummarizeAll();

            Assert.Equal(0m, books.PrimeCostRatio);
            Assert.Equal(PrimeCostBand.NoData, books.Band);
            Assert.Equal(-400m, books.NetProfit);
        }

        [Fact]
        public void AFinishedServiceBooksItself_RevenueInAndFoodCostOut()
        {
            var restaurant = BuildTradingRestaurant(out var company);
            var cashBefore = company.Economy.CashOnHand;

            var night = Dinner.Run(restaurant, 12, 99);
            company.Economy.RecordService(restaurant, night, 0);

            var books = company.Economy.Summarize(0, 0, restaurant.Id);

            Assert.Equal(night.Revenue, books.Revenue);
            Assert.Equal(night.FoodCost, books.FoodCost);
            Assert.Equal(cashBefore + night.Revenue - night.FoodCost, company.Economy.CashOnHand);

            // A real trading night should be comfortably profitable on food alone,
            // before any labor is booked against it.
            Assert.True(books.Revenue > books.FoodCost);
        }

        [Fact]
        public void FoodCostIncludesPlatesCookedForGuestsWhoWalkedOut()
        {
            // Prime cost must not be gameable by hiding a cost. A walkout is expensive twice:
            // no revenue, and the plate still went in the bin.
            var restaurant = BuildTradingRestaurant(out _, openingCash: 0m);

            // Choke the OVEN, not the saute. Saute cooks the risotto, and since quality and
            // price entered the ordering decision guests order that rarely — so choking it
            // no longer backs anything up and produced no walkouts to account for. The oven
            // cooks what people actually want, which is what makes a queue.
            restaurant.Kitchen.Install("oven", "Oven", 1);

            var night = Dinner.Run(restaurant, 14, 99);

            Assert.True(night.Walkouts > 0);
            Assert.True(night.WastedFoodCost > 0m);
            Assert.True(night.FoodCost > night.WastedFoodCost); // waste is a share of the total, not all of it
        }

        [Fact]
        public void CheaperSuppliersImproveFoodCostRatio_WhichIsPreciselyWhyItIsATradeoff()
        {
            var premium = BuildTradingRestaurant(out var premiumCompany);
            premiumCompany.SupplierPolicy.AssignAll("premium-harvest");
            var premiumNight = Dinner.Run(premium, 12, 99);
            premiumCompany.Economy.RecordService(premium, premiumNight, 0);

            var budget = BuildTradingRestaurant(out var budgetCompany);
            budgetCompany.SupplierPolicy.AssignAll("budget-wholesale");
            var budgetNight = Dinner.Run(budget, 12, 99);
            budgetCompany.Economy.RecordService(budget, budgetNight, 0);

            var premiumBooks = premiumCompany.Economy.Summarize(0, 0, premium.Id);
            var budgetBooks = budgetCompany.Economy.Summarize(0, 0, budget.Id);

            // Cheaper ingredients read better on the books...
            Assert.True(budgetBooks.FoodCostRatio < premiumBooks.FoodCostRatio);

            // ...while the guests were measurably less happy. That is the whole tradeoff,
            // now visible on both sides of the same decision.
            Assert.True(budgetNight.AverageSatisfaction < premiumNight.AverageSatisfaction);
        }

        [Fact]
        public void OneLedgerAnswersBothPerLocationAndEmpireWideQuestions()
        {
            // The Phase 9 rollup requirement, working: no second system for group accounts.
            var flagship = BuildTradingRestaurant(out var company, openingCash: 0m);
            var truck = company.OpenRestaurant("truck", "Acme Truck", LocationType.FoodTruck);

            company.Economy.Record(0, LedgerCategory.Revenue, 1000m, "Dinner", flagship.Id);
            company.Economy.Record(0, LedgerCategory.FoodCost, 300m, "Ingredients", flagship.Id);
            company.Economy.Record(0, LedgerCategory.Revenue, 400m, "Lunch pitch", truck.Id);
            company.Economy.Record(0, LedgerCategory.FoodCost, 200m, "Ingredients", truck.Id);

            Assert.Equal(1000m, company.Economy.Summarize(0, 0, flagship.Id).Revenue);
            Assert.Equal(400m, company.Economy.Summarize(0, 0, truck.Id).Revenue);
            Assert.Equal(1400m, company.Economy.SummarizeAll().Revenue);

            // The truck is running a much worse food cost ratio than the flagship —
            // visible per location, which a single blended number would have hidden.
            Assert.Equal(0.30m, company.Economy.Summarize(0, 0, flagship.Id).FoodCostRatio);
            Assert.Equal(0.50m, company.Economy.Summarize(0, 0, truck.Id).FoodCostRatio);
        }

        [Fact]
        public void SummariesAreScopedToAPeriod_SoWeeklyReviewsAreJustAQuery()
        {
            var restaurant = BuildTradingRestaurant(out var company, openingCash: 0m);
            var clock = new GameClock();

            company.Economy.Record(clock.Tick, LedgerCategory.Revenue, 500m, "Monday", restaurant.Id);
            clock.AdvanceDays(3);
            company.Economy.Record(clock.Tick, LedgerCategory.Revenue, 700m, "Thursday", restaurant.Id);
            clock.AdvanceDays(10);
            company.Economy.Record(clock.Tick, LedgerCategory.Revenue, 900m, "A fortnight in", restaurant.Id);

            var openingWeek = company.Economy.Summarize(0, GameClock.TicksPerWeek - 1);

            Assert.Equal(1200m, openingWeek.Revenue);   // Monday + Thursday only
            Assert.Equal(2100m, company.Economy.SummarizeAll().Revenue);
        }

        [Fact]
        public void SpendingMoneyYouDoNotHave_ShowsAsInsolvent_RatherThanBeingBlocked()
        {
            // Failure has to be legible and reachable, never silently prevented.
            var restaurant = BuildTradingRestaurant(out var company, openingCash: 100m);

            company.Economy.Record(0, LedgerCategory.CapitalExpenditure, 5000m, "Second wood oven", restaurant.Id);

            Assert.True(company.Economy.IsInsolvent);
            Assert.Equal(-4900m, company.Economy.CashOnHand);

            // And it is explicable — the entry naming the cause is right there.
            Assert.Contains(company.Economy.Entries, e => e.Description.Contains("Second wood oven"));
        }
    }
}
