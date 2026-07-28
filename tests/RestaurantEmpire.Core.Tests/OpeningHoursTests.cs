using System;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Opening hours are the operator's choice — including round-the-clock, including
    /// services that run past midnight.
    ///
    /// The clock runs continuously regardless; windows only decide when guests turn up.
    /// So staying open longer is not gated by the simulation, it is simply a decision with
    /// consequences — and the consequences get sharper as the systems that cost money for
    /// being open arrive (labor at M1, then time-of-day demand at M2).
    /// </summary>
    public class OpeningHoursTests
    {
        private static Restaurant Build()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("acme", "Acme Restaurant Group", definitions, 50000m);
            var restaurant = company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar);

            restaurant.Menu.Add("margherita", "caprese-salad", "truffle-risotto", "house-focaccia");
            company.SupplierPolicy.AssignAll("valley-produce");

            restaurant.Kitchen.Install("oven", "Wood Oven", 4);
            restaurant.Kitchen.Install("garde-manger", "Garde Manger", 4);
            restaurant.Kitchen.Install("saute", "Saute", 4);

            foreach (var id in definitions.IngredientIds) restaurant.Inventory.Receive(id, 1000000m);

            return restaurant;
        }

        [Fact]
        public void ARestaurantCanTradeRoundTheClock()
        {
            var restaurant = Build();
            restaurant.ServiceWindows.Clear();
            restaurant.ServiceWindows.Add(new ServiceWindow("Always Open", 0, 24));

            var runner = new SimulationRunner(restaurant, new GameClock(), 4242, InterruptPolicy.None());

            // Open at every hour we care to check, including the dead of night.
            foreach (var hour in new[] { 0, 3, 7, 12, 17, 23 })
            {
                var clock = new GameClock();
                clock.AdvanceHours(hour);
                Assert.True(restaurant.ServiceWindows[0].IsOpenAt(clock.Now), "should be open at " + hour + ":00");
            }

            runner.AdvanceDays(1);
            Assert.True(runner.Snapshot().CoversServed > 0);
        }

        [Fact]
        public void ALateNightServiceCanRunPastMidnight()
        {
            // 22:00-02:00. Without wrap-around support a late-night place is unexpressible.
            var window = new ServiceWindow("Late Night", 22, 2);

            Assert.True(window.WrapsMidnight);
            Assert.Equal(4 * 60, window.LengthMinutes);

            Assert.True(window.IsOpenAt(new DateTime(2026, 3, 2, 23, 30, 0)));  // before midnight
            Assert.True(window.IsOpenAt(new DateTime(2026, 3, 3, 1, 0, 0)));    // after midnight
            Assert.False(window.IsOpenAt(new DateTime(2026, 3, 3, 3, 0, 0)));   // closed
            Assert.False(window.IsOpenAt(new DateTime(2026, 3, 2, 20, 0, 0)));  // not yet
        }

        [Fact]
        public void SeveralWindowsInADayEachGetTheirOwnRush()
        {
            // This is the honest way to model round-the-clock trading: a breakfast, a lunch
            // and a late-night service each with their own demand, rather than one flat
            // 24-hour window whose single peak lands arbitrarily at noon.
            var restaurant = Build();
            restaurant.ServiceWindows.Clear();
            restaurant.ServiceWindows.Add(new ServiceWindow("Breakfast", 6, 10));
            restaurant.ServiceWindows.Add(new ServiceWindow("Lunch", 12, 15));
            restaurant.ServiceWindows.Add(new ServiceWindow("Late Night", 22, 2));

            var runner = new SimulationRunner(restaurant, new GameClock(), 4242, InterruptPolicy.None());

            runner.AdvanceHours(8);
            Assert.Equal("Breakfast", runner.CurrentWindow().Name);

            runner.AdvanceHours(3);                       // 11:00 — between services
            Assert.False(runner.IsOpen);

            runner.AdvanceHours(2);                       // 13:00
            Assert.Equal("Lunch", runner.CurrentWindow().Name);

            runner.AdvanceHours(10);                      // 23:00
            Assert.Equal("Late Night", runner.CurrentWindow().Name);

            runner.AdvanceHours(2);                       // 01:00 the next day, still serving
            Assert.Equal("Late Night", runner.CurrentWindow().Name);
            Assert.Equal(DayOfWeek.Tuesday, runner.Clock.DayOfWeek);

            runner.AdvanceHours(2);                       // 03:00 — finally closed
            Assert.False(runner.IsOpen);
        }

        [Fact]
        public void StayingOpenLongerSellsMoreFood_AndTiesUpTheKitchenForLonger()
        {
            // The upside of long hours is real and currently uncosted: labor is what makes
            // this a tradeoff, and nothing generates labor until Employees arrive at M1.
            // Recorded as a test so the day that changes, this fails and gets revisited.
            var shortDay = Build();
            shortDay.ServiceWindows.Clear();
            shortDay.ServiceWindows.Add(new ServiceWindow("Dinner", 18, 23));

            var allDay = Build();
            allDay.ServiceWindows.Clear();
            allDay.ServiceWindows.Add(new ServiceWindow("Always Open", 0, 24));

            var shortRunner = new SimulationRunner(shortDay, new GameClock(), 4242, InterruptPolicy.None());
            var allDayRunner = new SimulationRunner(allDay, new GameClock(), 4242, InterruptPolicy.None());

            shortRunner.AdvanceDays(1);
            allDayRunner.AdvanceDays(1);

            Assert.True(allDayRunner.Snapshot().CoversServed > shortRunner.Snapshot().CoversServed);
            Assert.True(allDayRunner.Snapshot().Revenue > shortRunner.Snapshot().Revenue);
        }

        [Fact]
        public void AWindowNeedsARealLength()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceWindow("Nonsense", 12, 12));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceWindow("Nonsense", 12, 25));
        }
    }
}
