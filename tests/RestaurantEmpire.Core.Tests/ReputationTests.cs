using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// The loop that makes bad food cost something: meals are remembered, and what the
    /// neighborhood remembers decides how many people turn up and what they will pay.
    ///
    /// Aaron's framing, which is the design: a dish and a restaurant are rated separately.
    /// A cheap decent plate satisfies the person eating it — they got what they paid for —
    /// but nobody loves a restaurant for it. So serving budget food competently makes money
    /// and caps how well regarded you can ever be. "You can be moderately successful but not
    /// like the best in the world."
    /// </summary>
    public class ReputationTests
    {
        private static Restaurant Build(out Company company, string supplierId, decimal priceMultiplier = 1m)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);

            company = new Company("acme", "Acme", definitions, 300000m);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Location = Neighborhood.SuburbanHighStreet();
            restaurant.FloorArea = 2150m;
            restaurant.Menu.Add("margherita", "caprese-salad", "truffle-risotto", "house-focaccia");
            company.SupplierPolicy.AssignAll(supplierId);

            if (priceMultiplier != 1m)
                foreach (var id in restaurant.Menu.RecipeIds) company.Pricing.AdjustPrice(id, priceMultiplier);

            restaurant.ServiceWindows.Clear();
            restaurant.ServiceWindows.Add(new ServiceWindow("Dinner", 18, 23));

            restaurant.BuyEquipment(definitions.GetEquipment("oven-commercial"), 4);
            restaurant.BuyEquipment(definitions.GetEquipment("gm-refrigerated"), 3);
            restaurant.BuyEquipment(definitions.GetEquipment("saute-commercial"), 3);

            foreach (var id in definitions.IngredientIds)
            {
                // Modest, and restocked as it trades. Nine thousand units was a way of saying
                // "stock is not what this test is about" — but a walk-in that deep is never
                // fresh, freshness now reaches the plate, and these tests would have been
                // measuring the age of the pantry rather than the standing of the restaurant.
                restaurant.Inventory.SetPar(id, 30m, 400m);
                restaurant.Inventory.Receive(id, 60m);
            }

            restaurant.BuyTables("t", "Tables", 4000m, 32);
            for (var i = 0; i < 8; i++) restaurant.Payroll.Hire(new Employee("c" + i, "Cook", StaffRole.Cook, 16m));
            for (var i = 0; i < 3; i++) restaurant.Payroll.Hire(new Employee("s" + i, "Server", StaffRole.Server, 12m));

            return restaurant;
        }

        private static ServiceResult Trade(Restaurant restaurant, int days)
        {
            var clock = new GameClock();
            clock.AdvanceHours(18);

            var runner = new SimulationRunner(restaurant, clock, 99, InterruptPolicy.None());

            for (var day = 0; day < days; day++)
            {
                runner.AdvanceDays(1);
                foreach (var stock in restaurant.Inventory.Items.ToList())
                    if (stock.IsBelowPar) restaurant.Inventory.Receive(stock.IngredientId, stock.SuggestedReorderQuantity);
            }

            return runner.Snapshot();
        }

        // ---- The ceiling: cheap food is viable, and capped ----

        [Fact]
        public void BudgetIngredientsCapHowWellRegardedYouCanEverBe()
        {
            var cheap = Build(out _, "budget-wholesale");
            var fine = Build(out _, "premium-harvest");

            // A YEAR each. Reputation moves over months by design — a name you could build
            // or lose in a fortnight would be a status effect, not a reputation — so a
            // fixture that only trades for a couple of months has not finished settling.
            Trade(cheap, 360);
            Trade(fine, 360);

            // Both are run competently. Only what they are made of differs.
            Assert.True(cheap.ReputationCeiling < 0.65m);
            Assert.True(fine.ReputationCeiling > 0.85m);

            Assert.True(cheap.Reputation.Standing < fine.Reputation.Standing);

            // The cheap place never even reaches its own ceiling — measured at 0.551 against
            // a cap of 0.570. That is a better outcome than the cap binding: what holds it
            // back is the food people are actually eating, not an artificial limit. The
            // ceiling exists for the case above it, where competent execution would otherwise
            // carry a restaurant somewhere its ingredients do not deserve.
            Assert.True(cheap.Reputation.Standing < 0.65m, "budget food should never be well thought of");

            // NOT asserting either hits its ceiling, because measurement says neither does —
            // and that is the honest finding. What holds a restaurant back is the FOOD, not
            // the cap: standing converges toward what its meals actually score, and that sits
            // below the ceiling at every tier (budget 0.45 against a 0.56 cap, premium 0.75
            // against 0.96). The ceiling is a backstop for a kitchen executing far better than
            // it sources, which is rarer than it sounds.
            Assert.True(cheap.ReputationCeiling < fine.ReputationCeiling);
            Assert.True(cheap.Reputation.Standing <= cheap.ReputationCeiling + 0.01m);
            Assert.True(fine.Reputation.Standing <= fine.ReputationCeiling + 0.01m);
        }

        [Fact]
        public void HittingTheCeilingIsSaidOutLoud_NotLeftAsAPlateau()
        {
            // Binding Principle 2. "Stuck at 57" is a number; "as well liked as these
            // ingredients allow" is a decision about the supplier.
            // Mid-tier rather than budget, because mid-tier is where the ceiling actually
            // BINDS: its meals score around 0.78 and its ingredients only justify 0.73, so
            // it is genuinely being held back by what it sources. Budget never reaches its
            // own cap — the food is simply mediocre — so a plateau message there would be
            // blaming the ceiling for something the plates are doing.
            // Forced against the cap rather than waiting for trade to reach it: a kitchen
            // cooking far better than it sources. That is the case the ceiling exists for, and
            // ordinary trading does not reach it — the food caps you first.
            var midTier = Build(out _, "valley-produce");
            Trade(midTier, 90);

            midTier.Reputation.Restore(midTier.ReputationCeiling, Reputation.MealsToBecomeKnown);
            midTier.Reputation.RecordMeal(1m, midTier.ReputationCeiling);

            Assert.True(midTier.Reputation.AtCeiling);
            Assert.Contains("ingredients", midTier.Reputation.Verdict);
        }

        [Fact]
        public void CompetenceAloneGetsYouToTheMiddleAndNoFurther()
        {
            Assert.Equal(Reputation.CompetenceCeiling, Reputation.CeilingFor(0m, 0m));
            Assert.True(Reputation.CeilingFor(1m, 1m) > 0.9m);

            // Ingredients dominate the room, exactly as they do in a single meal.
            Assert.True(Reputation.AmbitionFromIngredients > Reputation.AmbitionFromRoom * 4m);
        }

        // ---- What a reputation is worth ----

        [Fact]
        public void AGoodNameBringsPeopleIn_AndABadOneKeepsThemAway()
        {
            var loved = new Reputation();
            var loathed = new Reputation();

            for (var i = 0; i < 20000; i++) loved.RecordMeal(1m);
            for (var i = 0; i < 20000; i++) loathed.RecordMeal(0m);

            // Both are long since KNOWN, so footfall is down to opinion alone.
            Assert.True(loved.TrafficMultiplier > 1.3m);
            Assert.True(loathed.TrafficMultiplier < 0.7m);
        }

        [Fact]
        public void AReputationIsWhatLetsYouCharge()
        {
            // The half that makes sourcing well rational at all. Without it, reputation buys
            // footfall, footfall does not pay for truffles, and budget stock out-earns premium
            // at every horizon — so nobody would ever buy good ingredients.
            var unknown = SatisfactionModel.ScoreValue(4m, 1m, 1m, Reputation.Neutral);
            var beloved = SatisfactionModel.ScoreValue(4m, 1m, 1m, 0.9m);

            Assert.True(beloved > unknown);
        }

        [Fact]
        public void PricingPastWhatCheapFoodCanCarryCostsYouTheRoom()
        {
            // Budget stock at a fair price is honest trade and nobody objects. The same food
            // at a fine-dining price empties the room, because the reputation is not there to
            // carry it.
            var fair = Build(out _, "budget-wholesale");
            var greedy = Build(out _, "budget-wholesale", priceMultiplier: 1.8m);

            var fairTrade = Trade(fair, 60);
            var greedyTrade = Trade(greedy, 60);

            Assert.Equal(0, fairTrade.PartiesPutOffByThePrices);
            Assert.True(greedyTrade.PartiesPutOffByThePrices > 500);
            Assert.True(greedyTrade.CoversServed < fairTrade.CoversServed);
        }

        // ---- How it moves ----

        [Fact]
        public void BadNewsTravelsFasterThanGood()
        {
            var falling = new Reputation();
            var rising = new Reputation();

            for (var i = 0; i < 500; i++) falling.RecordMeal(0m);
            for (var i = 0; i < 500; i++) rising.RecordMeal(1m);

            var lost = Reputation.Neutral - falling.Standing;
            var gained = rising.Standing - Reputation.Neutral;

            Assert.True(lost > gained * 2m);
        }

        [Fact]
        public void AReputationCannotBeRebuiltInAnEvening()
        {
            // The calibration that caught me out: at ten times these rates a busy restaurant
            // moved a third of the way to a new standing in a single day, which is a status
            // effect wearing a reputation's clothes.
            var rep = new Reputation();
            for (var i = 0; i < 120; i++) rep.RecordMeal(1m);   // one very good night

            Assert.True(rep.Standing < 0.53m, "one night moved standing to " + rep.Standing);
        }

        [Fact]
        public void ANewRestaurantIsUnknownRatherThanDisliked()
        {
            var fresh = new Reputation();

            Assert.Equal(Reputation.Neutral, fresh.Standing);
            Assert.Contains("finding its feet", fresh.Verdict);

            // Nobody DISLIKES it — opinion is exactly neutral.
            Assert.Equal(1m, fresh.OpinionMultiplier);

            // But hardly anybody has heard of it, so hardly anybody comes. Those are two
            // different problems: being undiscovered is fixed by trading (and later by
            // marketing), being disliked is fixed by cooking better.
            Assert.Equal(Reputation.UnknownTrafficShare, fresh.Awareness);
            Assert.Equal(Reputation.UnknownTrafficShare, fresh.TrafficMultiplier);

            // And being unknown is strictly better than being loathed.
            var loathed = new Reputation();
            for (var i = 0; i < 20000; i++) loathed.RecordMeal(0m);
            Assert.True(fresh.OpinionMultiplier > loathed.OpinionMultiplier);
        }

        [Fact]
        public void WalkingOutIsRememberedToo()
        {
            var rep = new Reputation();
            var before = rep.Standing;

            rep.RecordWalkout();

            Assert.True(rep.Standing < before);
        }

        // ---- It is state, so it has to survive a save ----

        [Fact]
        public void AReputationSurvivesSavingAndLoading()
        {
            var restaurant = Build(out var company, "premium-harvest");
            var clock = new GameClock();
            Trade(restaurant, 30);

            var standing = restaurant.Reputation.Standing;
            var meals = restaurant.Reputation.MealsRemembered;
            Assert.True(meals > 0, "the fixture should have served somebody");

            var loaded = SaveGameSerializer.FromJson(
                SaveGameSerializer.ToJson(company, clock),
                JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory));

            var restored = loaded.Company.GetRestaurant("flagship").Reputation;

            Assert.Equal(standing, restored.Standing);
            Assert.Equal(meals, restored.MealsRemembered);
        }

        // ---- Losing a name takes as long as losing a name takes (Aaron) ----

        [Fact]
        public void CuttingCornersDoesNotCraterYouOvernight()
        {
            // Aaron: "it shouldn't be instant unless there is a critic or blogger who catches
            // it quickly... it should deteriorate over weeks or months."
            //
            // The bug this pins: the ceiling used to CLAMP rather than pull, so the first
            // service after switching supplier snapped standing straight down to it —
            // measured at 0.890 to 0.568 in a single day. Six months of work, gone over one
            // dinner, with no window in which to notice or undo it.
            var restaurant = Build(out var company, "premium-harvest");
            Trade(restaurant, 360);

            var earned = restaurant.Reputation.Standing;
            Assert.True(earned > 0.85m, "the fixture should have earned a real name first");

            company.SupplierPolicy.AssignAll("budget-wholesale");
            Assert.True(restaurant.ReputationCeiling < 0.60m, "the ceiling drops at once — that part is correct");

            // One day of trading on the cheap stuff barely registers.
            //
            // ASSERTED AS A RATIO, NOT A CONSTANT. Reputation moves per MEAL, so how far it
            // travels in a day depends on how busy you are — and when equipment gained a batch
            // size, throughput roughly doubled and a fixed 0.02 threshold started failing at
            // 0.021 for a restaurant behaving exactly as designed. The claim was never about a
            // number; it is that a day is a rounding error against a month.
            Trade(restaurant, 1);
            var oneDay = earned - restaurant.Reputation.Standing;
            var afterADay = restaurant.Reputation.Standing;
            Assert.True(oneDay < 0.05m,
                "one day cost " + oneDay + " of standing, which is not 'barely registers'");

            // A month in, it is visibly going — and by an order more than the single day did.
            Trade(restaurant, 30);
            var oneMonth = afterADay - restaurant.Reputation.Standing;
            Assert.True(oneMonth > oneDay * 8m,
                "a month should cost far more than a day: day " + oneDay + ", month " + oneMonth);
            Assert.True(restaurant.Reputation.Standing < earned - 0.15m, "a month should show real damage");

            // And it takes months to actually arrive.
            Trade(restaurant, 150);
            Assert.True(restaurant.Reputation.Standing < 0.60m);
        }

        [Fact]
        public void WhileTheNameIsStillFallingTheGameSaysSo()
        {
            // The one window where the damage is visible and not yet done. Getting this
            // message wrong would waste it: before the fix the game reported "as well liked
            // as these ingredients allow" while standing was 0.884 and its ceiling 0.570,
            // which is not a plateau — it is a slide, and it can still be undone.
            var restaurant = Build(out var company, "premium-harvest");
            Trade(restaurant, 360);

            company.SupplierPolicy.AssignAll("budget-wholesale");
            Trade(restaurant, 3);

            Assert.True(restaurant.Reputation.LivingOnPastGlory);
            Assert.False(restaurant.Reputation.AtCeiling, "sliding is not the same as settled");
            Assert.Contains("no longer justify", restaurant.Reputation.Verdict);
        }

        [Fact]
        public void ARealNameTakesMonthsToBuild()
        {
            var restaurant = Build(out _, "premium-harvest");

            Trade(restaurant, 30);
            var afterAMonth = restaurant.Reputation.Standing;
            Assert.True(afterAMonth < 0.70m, "a month of good food should not make you famous");

            Trade(restaurant, 150);
            var afterHalfAYear = restaurant.Reputation.Standing;

            // Asserted as a SHAPE rather than an absolute, because the absolute moves whenever
            // the plate model gets more honest — freshness landing lowered a well-run premium
            // kitchen's meals from near-perfect to 0.88, so standing now converges there
            // instead of to the ceiling. The claim is "months, not weeks", and that holds.
            Assert.True(afterHalfAYear > afterAMonth + 0.15m,
                "half a year should move it a long way: " + afterAMonth + " to " + afterHalfAYear);
            Assert.True(afterHalfAYear > 0.70m, "and should be well regarded by then");
        }

        [Fact]
        public void BeingFoundTakesAboutASeasonOfTrading()
        {
            // Aaron: "perhaps I had too much traffic right away?" A restaurant that opened
            // this morning used to draw the full footfall of the street, because standing
            // began at neutral and neutral meant 1.0. Its first job should be to be found.
            // AND HOW LONG THAT SEASON IS NOW DEPENDS ON THE FOOD, which is the half this
            // test could not say before. Awareness used to be a pure meal COUNTER, so a
            // budget kitchen became famous on exactly the same schedule as a good one —
            // measured over 400 days, the two reached 100% known two days apart. Word of
            // mouth is earned per meal now, in proportion to how much it pleased.
            var rep = new Reputation();
            Assert.Equal(Reputation.UnknownTrafficShare, rep.Awareness);

            for (var i = 0; i < Reputation.MealsToBecomeKnown / 2; i++) rep.RecordMeal(0.7m);
            Assert.InRange(rep.Awareness, 0.55m, 0.70m);

            // Enough good trading and you are known. The multiplier is what a merely decent
            // dinner costs you in reach: it takes more of them.
            for (var i = 0; i < Reputation.MealsToBecomeKnown * 2; i++) rep.RecordMeal(0.7m);
            Assert.Equal(1m, rep.Awareness);
        }

        /// <summary>
        /// THE SAME NUMBER OF DINNERS, TWO DIFFERENT REPUTATIONS — this is the claim the old
        /// counter could not make. Serving people is not the same as being worth talking
        /// about, and Restaurant Empire 2's manual puts the rule plainly: *"the more
        /// completely satisfied customers there are, the higher your customer awareness"*,
        /// and *"100% satisfied customers are your best source of advertising."*
        ///
        /// The floor matters as much as the slope: a forgettable meal still spreads SOME
        /// word, because you were there and you mentioned it. Being dull is slow, not silent.
        /// </summary>
        [Fact]
        public void GoodFoodGetsYourNameOutFasterThanMerelyFeedingPeople()
        {
            var delightful = new Reputation();
            var forgettable = new Reputation();

            for (var i = 0; i < Reputation.MealsToBecomeKnown; i++)
            {
                delightful.RecordMeal(0.90m);
                forgettable.RecordMeal(0.30m);
            }

            Assert.Equal(delightful.MealsRemembered, forgettable.MealsRemembered);
            Assert.True(delightful.Awareness > forgettable.Awareness + 0.25m,
                $"the same {Reputation.MealsToBecomeKnown} dinners should not make the two equally " +
                $"known: delightful {delightful.Awareness:P0} against forgettable {forgettable.Awareness:P0}");

            // Being dull is slow, not silent — it still moves off the opening share.
            Assert.True(forgettable.Awareness > Reputation.UnknownTrafficShare,
                "a forgettable meal still spreads some word; nobody is invisible for serving dinner");

            // And the ceiling: a delightful restaurant is not made to wait longer than the
            // old universal pace. Today's speed is the BEST case now, not everybody's.
            Assert.Equal(1m, delightful.Awareness);
        }

        [Fact]
        public void APerfectScoreIsReachable_ButNeedsTheBestOfEverything()
        {
            // Aaron: "this is the best supplier possible so would I never be able to reach
            // 100?" He could not — competence 0.45 plus ingredients 0.40 plus room 0.08 topped
            // out at 93, and the game gave no way to find that out. A scale whose top cannot
            // be reached is a wrong scale.
            Assert.Equal(1m, Reputation.CeilingFor(1m, 1m));

            // And it needs BOTH. The best sourcing in a plain room does not get there.
            Assert.True(Reputation.CeilingFor(1m, 0.5m) < 1m);
            Assert.True(Reputation.CeilingFor(0.6m, 1m) < 1m);

            // The ladder still runs the right way.
            Assert.True(Reputation.CeilingFor(0.2m, 0.55m) < Reputation.CeilingFor(0.6m, 0.55m));
            Assert.True(Reputation.CeilingFor(0.6m, 0.55m) < Reputation.CeilingFor(1m, 0.55m));
        }
    }
}
