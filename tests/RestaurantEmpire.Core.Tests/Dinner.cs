using RestaurantEmpire.Core.Model;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// Test helper: runs one full dinner service on the continuously-running simulation and
    /// returns the snapshot, so tests that only care about "a night's trading" don't each
    /// have to set up a clock, a window and a runner.
    ///
    /// Interrupts are off here on purpose — a test measuring revenue wants the whole night,
    /// not a night that stopped halfway to ask a question. Interrupt behaviour has its own
    /// tests.
    /// </summary>
    internal static class Dinner
    {
        public static ServiceResult Run(Restaurant restaurant, double peakPartiesPerHour = 25, long seed = 99)
        {
            var runner = Runner(restaurant, peakPartiesPerHour, seed, InterruptPolicy.None());

            // Dinner runs 18:00-23:00; the extra hour lets the last tables finish eating.
            runner.Advance(6 * GameClock.TicksPerHour);

            return runner.Snapshot();
        }

        /// <summary>A runner parked at 18:00 with a single dinner window, ready to step.</summary>
        public static SimulationRunner Runner(
            Restaurant restaurant, double peakPartiesPerHour = 25, long seed = 99, InterruptPolicy? interrupts = null)
        {
            restaurant.ServiceWindows.Clear();
            restaurant.ServiceWindows.Add(new ServiceWindow("Dinner", 18, 23));

            // Demand comes from the street now, so a test that wants "a dinner rush of this
            // size" says so by putting the restaurant somewhere with that traffic.
            restaurant.Location = Neighbourhood.PeakedBetween("Test Dinner Strip", 18, 23, peakPartiesPerHour);

            var clock = new GameClock();
            clock.AdvanceHours(18);

            return new SimulationRunner(restaurant, clock, seed, interrupts ?? InterruptPolicy.None());
        }
    }
}
