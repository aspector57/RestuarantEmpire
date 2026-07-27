using System;
using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// M1 EXIT TEST (a) — MECHANISM.
    ///
    ///   "Live play runs at 1x/2x/3x and jump-ahead by day/week/month pauses, resumes,
    ///    and returns control cleanly."
    ///
    /// This is a correctness bar, not a taste one. If the simulation cannot stop at an
    /// arbitrary minute and carry on from exactly there, then pausing, interrupting and
    /// fast-forwarding are all quietly lying, and everything built on top inherits the lie.
    ///
    /// The load-bearing property is chunk-size invariance: a month advanced in one call and
    /// a month advanced a minute at a time must produce the identical world. Get that right
    /// and pause/resume is free, because pausing is just a smaller chunk.
    /// </summary>
    public class TimeControlTests
    {
        private static Restaurant Build(int slots = 3, decimal openingCash = 20000m)
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("acme", "Acme Restaurant Group", definitions, openingCash);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Menu.Add("margherita", "caprese-salad", "truffle-risotto", "house-focaccia");
            company.SupplierPolicy.AssignAll("valley-produce");

            restaurant.Kitchen.Install("oven", "Wood Oven", slots);
            restaurant.Kitchen.Install("garde-manger", "Garde Manger", slots);
            restaurant.Kitchen.Install("saute", "Saute", slots);

            foreach (var id in definitions.IngredientIds) restaurant.Inventory.Receive(id, 100000m);

            return restaurant;
        }

        private static void AssertSameWorld(ServiceResult a, ServiceResult b)
        {
            Assert.Equal(a.Revenue, b.Revenue);
            Assert.Equal(a.FoodCost, b.FoodCost);
            Assert.Equal(a.WastedFoodCost, b.WastedFoodCost);
            Assert.Equal(a.CoversServed, b.CoversServed);
            Assert.Equal(a.Walkouts, b.Walkouts);
            Assert.Equal(a.EightySixed, b.EightySixed);
            Assert.Equal(a.PartiesArrived, b.PartiesArrived);
            Assert.Equal(a.PartiesTurnedAway, b.PartiesTurnedAway);
            Assert.Equal(a.LongestWaitMinutes, b.LongestWaitMinutes);
            Assert.Equal(a.AverageSatisfaction, b.AverageSatisfaction);
            Assert.Equal(a.UnitsSoldByRecipeId, b.UnitsSoldByRecipeId);
            Assert.Equal(a.Diagnostics.Count, b.Diagnostics.Count);
        }

        // ---- The load-bearing property ----

        [Fact]
        public void AdvancingInAnyChunkSizeWhatsoever_ProducesTheIdenticalNight()
        {
            const int span = 6 * 60;

            var oneCall = Dinner.Runner(Build(), 25, 4242);
            oneCall.Advance(span);

            var minuteByMinute = Dinner.Runner(Build(), 25, 4242);
            for (var i = 0; i < span; i++) minuteByMinute.Advance(1);

            // Deliberately ragged, because real pausing is not on tidy boundaries.
            var ragged = Dinner.Runner(Build(), 25, 4242);
            foreach (var chunk in new[] { 7, 113, 1, 60, 2, 90, 87 }) ragged.Advance(chunk);

            Assert.Equal(span, ragged.Clock.Tick - (18 * GameClock.TicksPerHour));

            AssertSameWorld(oneCall.Snapshot(), minuteByMinute.Snapshot());
            AssertSameWorld(oneCall.Snapshot(), ragged.Snapshot());
        }

        [Fact]
        public void PausingIsJustNotAdvancing_AndCostsNothing()
        {
            var straight = Dinner.Runner(Build(), 25, 4242);
            straight.Advance(300);

            var paused = Dinner.Runner(Build(), 25, 4242);
            paused.Advance(140);

            // "Paused" — the player wandered off mid-service. Time does not move.
            var tickWhilePaused = paused.Clock.Tick;
            var coversWhilePaused = paused.Snapshot().CoversServed;

            paused.Clock.Speed = GameSpeed.Paused;
            Assert.Equal(0, paused.Advance(0).TicksAdvanced);
            Assert.Equal(tickWhilePaused, paused.Clock.Tick);
            Assert.Equal(coversWhilePaused, paused.Snapshot().CoversServed);

            // Unpause and carry on. Nothing was lost, including guests mid-meal.
            paused.Clock.Speed = GameSpeed.Normal;
            paused.Advance(160);

            AssertSameWorld(straight.Snapshot(), paused.Snapshot());
        }

        [Fact]
        public void SpeedIsAComfortControl_NotSomethingThatChangesWhatHappens()
        {
            // 1x/2x/3x must alter how fast the player watches, never the outcome. If speed
            // changed results, fast-forwarding would be cheating and nobody could trust it.
            var slow = Dinner.Runner(Build(), 25, 4242);
            slow.Clock.Speed = GameSpeed.Normal;
            for (var i = 0; i < 300; i++) slow.Advance(1);

            var fast = Dinner.Runner(Build(), 25, 4242);
            fast.Clock.Speed = GameSpeed.Fastest;
            for (var i = 0; i < 100; i++) fast.Advance(3);

            AssertSameWorld(slow.Snapshot(), fast.Snapshot());
        }

        // ---- Jump-ahead, pause on interrupt, resume ----

        [Fact]
        public void AnInterruptStopsMidService_AndTheNextAdvanceResumesFromThatExactMinute()
        {
            var restaurant = Build(slots: 1);
            var runner = Dinner.Runner(restaurant, 30, 4242,
                new InterruptPolicy { WalkoutStreakThreshold = 3, CashFloor = null, StopOnStockout = false });

            var jump = runner.Advance(GameClock.TicksPerDay);   // "skip a day"

            Assert.True(jump.StoppedEarly);
            Assert.NotNull(jump.Interrupt);
            Assert.Equal(InterruptKind.WalkoutStreak, jump.Interrupt.Kind);

            // It stopped partway, and said exactly how far it got and what is still owed.
            Assert.True(jump.TicksAdvanced < GameClock.TicksPerDay);
            Assert.Equal(GameClock.TicksPerDay - jump.TicksAdvanced, jump.TicksRemaining);

            // The clock is parked on the interrupt, not somewhere near it.
            Assert.Equal(jump.Interrupt.Tick, runner.Clock.Tick);

            // And it is genuinely mid-service, with guests still in the room.
            Assert.True(runner.IsOpen);
            Assert.True(runner.GuestsInside > 0);

            // Carry on to where the player was originally headed.
            var resumed = runner.Advance(jump.TicksRemaining);
            Assert.True(resumed.TicksAdvanced > 0);
        }

        [Fact]
        public void StoppingForEveryInterrupt_EndsUpExactlyWhereAnUninterruptedRunWould()
        {
            // The real test of "returns control cleanly": being interrupted repeatedly must
            // not change the night, only punctuate it.
            var uninterrupted = Dinner.Runner(Build(slots: 1), 30, 4242, InterruptPolicy.None());
            uninterrupted.Advance(6 * 60);

            var interrupted = Dinner.Runner(Build(slots: 1), 30, 4242,
                new InterruptPolicy { WalkoutStreakThreshold = 2, CashFloor = null, StopOnStockout = true });

            long remaining = 6 * 60;
            var stops = 0;

            while (remaining > 0)
            {
                var step = interrupted.Advance(remaining);
                remaining -= step.TicksAdvanced;

                if (!step.StoppedEarly) break;
                stops++;

                Assert.True(stops < 500, "interrupts are not making progress");
            }

            Assert.True(stops > 0, "expected this night to be interrupted at least once");
            AssertSameWorld(uninterrupted.Snapshot(), interrupted.Snapshot());
        }

        // ---- The three M1 interrupts ----

        [Fact]
        public void RunningOutOfAnIngredientMidService_StopsTheSim_AndNamesIt()
        {
            var restaurant = Build();
            restaurant.Inventory.TryConsume("mozzarella", restaurant.Inventory.QuantityOf("mozzarella"));

            var runner = Dinner.Runner(restaurant, 25, 4242,
                new InterruptPolicy { StopOnStockout = true, WalkoutStreakThreshold = 0, CashFloor = null });

            var step = runner.Advance(6 * 60);

            Assert.True(step.StoppedEarly);
            Assert.Equal(InterruptKind.IngredientStockout, step.Interrupt.Kind);
            Assert.Contains("mozzarella", step.Interrupt.Message);
            Assert.True(runner.IsOpen);   // mid-service, not at a tidy boundary
        }

        [Fact]
        public void AStreakOfWalkouts_StopsTheSim()
        {
            var runner = Dinner.Runner(Build(slots: 1), 30, 4242,
                new InterruptPolicy { WalkoutStreakThreshold = 3, StopOnStockout = false, CashFloor = null });

            var step = runner.Advance(6 * 60);

            Assert.True(step.StoppedEarly);
            Assert.Equal(InterruptKind.WalkoutStreak, step.Interrupt.Kind);
            Assert.Contains("walked out", step.Interrupt.Message);
        }

        [Fact]
        public void CashFallingThroughTheFloor_StopsTheSim_WhileItIsStillHappening()
        {
            // Opening on a shoestring: ingredients are bought as plates are fired, and the
            // takings arrive minutes later. Start thin enough and you go under mid-service.
            var runner = Dinner.Runner(Build(openingCash: 5m), 25, 4242,
                new InterruptPolicy { CashFloor = 0m, WalkoutStreakThreshold = 0, StopOnStockout = false });

            var step = runner.Advance(6 * 60);

            Assert.True(step.StoppedEarly);
            Assert.Equal(InterruptKind.CashThreshold, step.Interrupt.Kind);
            Assert.True(runner.ProjectedCash < 0m);
        }

        [Fact]
        public void WithInterruptsOff_ADayRunsStraightThrough()
        {
            var runner = Dinner.Runner(Build(slots: 1), 30, 4242, InterruptPolicy.None());

            var step = runner.Advance(GameClock.TicksPerDay);

            Assert.False(step.StoppedEarly);
            Assert.Equal(GameClock.TicksPerDay, step.TicksAdvanced);
            Assert.Equal(0, step.TicksRemaining);
        }

        // ---- The clock genuinely runs continuously ----

        [Fact]
        public void TheClockRunsAllDayAndRollsIntoTheNext_WithGuestsOnlyDuringOpeningHours()
        {
            var restaurant = Build();
            restaurant.ServiceWindows.Clear();
            restaurant.ServiceWindows.Add(new ServiceWindow("Lunch", 12, 15));
            restaurant.ServiceWindows.Add(new ServiceWindow("Dinner", 18, 23));

            var clock = new GameClock();                 // Monday 00:00
            var runner = new SimulationRunner(restaurant, clock, 4242, InterruptPolicy.None());

            runner.AdvanceHours(9);                      // 09:00 — closed, prep time
            Assert.False(runner.IsOpen);
            Assert.Equal(0, runner.Snapshot().PartiesArrived);

            runner.AdvanceHours(4);                      // 13:00 — lunch is on
            Assert.True(runner.IsOpen);
            Assert.Equal("Lunch", runner.CurrentWindow().Name);
            var afterLunch = runner.Snapshot().PartiesArrived;
            Assert.True(afterLunch > 0);

            runner.AdvanceHours(3);                      // 16:00 — the afternoon lull
            Assert.False(runner.IsOpen);

            runner.AdvanceHours(4);                      // 20:00 — dinner
            Assert.Equal("Dinner", runner.CurrentWindow().Name);
            Assert.True(runner.Snapshot().PartiesArrived > afterLunch);

            // Straight through midnight into Tuesday without a seam.
            var overnight = runner.AdvanceHours(6);      // 02:00 Tuesday
            Assert.True(overnight.Elapsed.CrossedDay);
            Assert.Equal(DayOfWeek.Tuesday, runner.Clock.DayOfWeek);
            Assert.False(runner.IsOpen);
        }

        [Fact]
        public void ALongJumpStillReportsEveryBoundaryItPassedThrough()
        {
            // Skipping a month must not skip the payrolls inside it.
            var runner = Dinner.Runner(Build(), 25, 4242, InterruptPolicy.None());

            var step = runner.Advance(30L * GameClock.TicksPerDay);

            Assert.False(step.StoppedEarly);
            Assert.Equal(30, step.Elapsed.Days);
            Assert.True(step.Elapsed.Weeks >= 4);
            Assert.True(step.Elapsed.CrossedMonth);
        }

        [Fact]
        public void ThirtyNightsOfTradingAccumulate_RatherThanResettingEachDay()
        {
            var oneNight = Dinner.Runner(Build(), 25, 4242, InterruptPolicy.None());
            oneNight.Advance(GameClock.TicksPerDay);

            var aMonth = Dinner.Runner(Build(), 25, 4242, InterruptPolicy.None());
            aMonth.Advance(30L * GameClock.TicksPerDay);

            var single = oneNight.Snapshot();
            var month = aMonth.Snapshot();

            Assert.True(month.CoversServed > single.CoversServed * 20);
            Assert.True(month.Revenue > single.Revenue * 20m);
        }

        [Fact]
        public void TheSimulationCannotRunBackwards()
        {
            var runner = Dinner.Runner(Build(), 25, 4242);
            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Advance(-1));
        }

        [Fact]
        public void AWalkoutInterruptNamesTheBottleneckAndWhatFixingItCosts()
        {
            // Aaron's playtest verdict on the old message: "the kitchen is losing the room"
            // is true and useless. A stop has to carry WHY and WHAT CAN BE DONE, which is
            // the design's Tier-2 Advisor pattern. All of this was already known when the
            // alarm fired; it simply was not said.
            var runner = Dinner.Runner(Build(slots: 1), 30, 4242,
                new InterruptPolicy { WalkoutStreakThreshold = 3, CashFloor = null, StopOnStockout = false });

            var step = runner.Advance(6 * 60);

            Assert.True(step.StoppedEarly);
            Assert.Equal(InterruptKind.WalkoutStreak, step.Interrupt.Kind);

            // Names the station responsible, and blames it by number.
            Assert.NotNull(step.Interrupt.SubjectId);
            Assert.Contains(step.Interrupt.SubjectId, step.Interrupt.Message);
            Assert.Contains("bottleneck", step.Interrupt.Message);

            // And quotes the move available, with its price and your cash.
            Assert.Contains("Another", step.Interrupt.Message);
            Assert.Contains("you have", step.Interrupt.Message);
        }
    }
}
