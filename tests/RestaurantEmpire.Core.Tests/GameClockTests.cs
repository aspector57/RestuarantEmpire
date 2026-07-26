using System;
using RestaurantEmpire.Core.Model;
using Xunit;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// The campaign clock. M0 scope is "ticks, day/week/month advancement, speed
    /// multipliers as data only" — no real-time loop, which is a presentation concern.
    /// </summary>
    public class GameClockTests
    {
        [Fact]
        public void ATickIsOneGameMinute_AndTheCampaignStartsOnAMonday()
        {
            var clock = new GameClock();

            Assert.Equal(60, GameClock.TicksPerHour);
            Assert.Equal(1440, GameClock.TicksPerDay);
            Assert.Equal(10080, GameClock.TicksPerWeek);

            // Weeks line up with real weeks, so "the weekly business review" and
            // "the Friday night rush" mean the same thing they would to a real operator.
            Assert.Equal(DayOfWeek.Monday, clock.DayOfWeek);
            Assert.Equal(1, clock.DayNumber);
            Assert.Equal(1, clock.WeekNumber);
            Assert.Equal(0, clock.Tick);
        }

        [Fact]
        public void AdvancingCountsCalendarDaysCrossed_NotHoursElapsed()
        {
            var clock = new GameClock();

            var evening = clock.AdvanceHours(23);        // 23:00 on opening day
            Assert.Equal(0, evening.Days);
            Assert.Equal(23, clock.HourOfDay);

            var overnight = clock.AdvanceHours(2);       // 01:00 the next day
            Assert.Equal(1, overnight.Days);             // two hours, but one day boundary
            Assert.True(overnight.CrossedDay);
            Assert.Equal(2, clock.DayNumber);
            Assert.Equal(DayOfWeek.Tuesday, clock.DayOfWeek);
        }

        [Fact]
        public void WeekBoundaries_DriveTheWeeklyBusinessReviewBeat()
        {
            var clock = new GameClock();

            var sixDays = clock.AdvanceDays(6);
            Assert.False(sixDays.CrossedWeek);           // still in the opening week
            Assert.Equal(1, clock.WeekNumber);

            var seventhDay = clock.AdvanceDays(1);
            Assert.True(seventhDay.CrossedWeek);
            Assert.Equal(1, seventhDay.Weeks);
            Assert.Equal(2, clock.WeekNumber);
            Assert.Equal(DayOfWeek.Monday, clock.DayOfWeek);
        }

        [Fact]
        public void JumpingAheadAMonth_RespectsRealMonthLengths()
        {
            var clock = new GameClock();                 // Mon 2 March 2026

            var elapsed = clock.AdvanceMonths(1);

            Assert.Equal(new DateTime(2026, 4, 2), clock.Now);
            Assert.Equal(31, elapsed.Days);              // March has 31 days, not a flat 30
            Assert.True(elapsed.CrossedMonth);
            Assert.Equal(1, elapsed.Months);
        }

        [Fact]
        public void ALongJumpReportsEveryBoundaryItCrossed_AtOnce()
        {
            // "Choosing to skip a month does not skip the decisions inside it" — a caller
            // needs to know everything that fell due during the jump, not just where it landed.
            var clock = new GameClock();

            var elapsed = clock.AdvanceDays(70);

            Assert.Equal(70, elapsed.Days);
            Assert.Equal(10, elapsed.Weeks);
            Assert.Equal(2, elapsed.Months);             // March -> May
            Assert.False(elapsed.CrossedYear);
        }

        [Fact]
        public void ACareerSpansDecades_WithoutTheClockStruggling()
        {
            // The design caps a career at roughly 40 in-game years.
            var clock = new GameClock();

            clock.AdvanceDays(365 * 40);

            Assert.True(clock.YearsElapsed >= 39);
            Assert.True(clock.Tick > 20_000_000);        // ~21 million minutes, comfortably within a long
        }

        [Fact]
        public void SpeedIsDataOnly_ItNeverDrivesTheClockItself()
        {
            var clock = new GameClock();

            Assert.Equal(GameSpeed.Normal, clock.Speed);
            Assert.Equal(1, GameSpeed.Normal.Multiplier());
            Assert.Equal(2, GameSpeed.Fast.Multiplier());
            Assert.Equal(3, GameSpeed.Fastest.Multiplier());
            Assert.Equal(0, GameSpeed.Paused.Multiplier());
            Assert.True(GameSpeed.Paused.IsPaused());

            // Changing speed does not move the clock. Only Advance does.
            clock.Speed = GameSpeed.Fastest;
            Assert.Equal(0, clock.Tick);
        }

        [Fact]
        public void AdvanceToNextDay_LandsExactlyOnMidnight()
        {
            var clock = new GameClock();
            clock.AdvanceHours(19);                      // 19:00, mid-service

            var elapsed = clock.AdvanceToNextDay();

            Assert.Equal(5 * GameClock.TicksPerHour, elapsed.Ticks);
            Assert.Equal(0, clock.HourOfDay);
            Assert.Equal(2, clock.DayNumber);

            Assert.Equal(0, clock.AdvanceToNextDay().Ticks); // already at midnight: no-op
        }

        [Fact]
        public void TheClockCannotRunBackwards()
        {
            var clock = new GameClock();
            clock.AdvanceDays(3);

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceMonths(-1));

            Assert.Equal(0, clock.Advance(0).Ticks);     // standing still is fine
        }
    }
}
