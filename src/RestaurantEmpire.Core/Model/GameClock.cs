using System;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// Which period boundaries an <see cref="GameClock.Advance"/> call crossed.
    ///
    /// This is how recurring business rhythm gets driven without an event system: a caller
    /// advances the clock, is told plainly "that covered 3 days and crossed a week
    /// boundary," and runs payroll or the weekly business review accordingly. Returned as
    /// data rather than fired as events so it stays trivially testable and there is no
    /// subscription order to reason about.
    /// </summary>
    public sealed class ElapsedPeriods
    {
        internal ElapsedPeriods(long ticks, int days, int weeks, int months, int years)
        {
            Ticks = ticks;
            Days = days;
            Weeks = weeks;
            Months = months;
            Years = years;
        }

        public long Ticks { get; }

        /// <summary>Calendar days crossed. Advancing 23:00 -> 01:00 crosses one, despite being two hours.</summary>
        public int Days { get; }

        /// <summary>Week boundaries crossed. Weeks run from the campaign's start day.</summary>
        public int Weeks { get; }

        public int Months { get; }
        public int Years { get; }

        public bool CrossedDay { get { return Days > 0; } }

        /// <summary>The weekly business review beat — the game's one predictable anchor (design doc Phase 5).</summary>
        public bool CrossedWeek { get { return Weeks > 0; } }

        public bool CrossedMonth { get { return Months > 0; } }
        public bool CrossedYear { get { return Years > 0; } }
    }

    /// <summary>
    /// The campaign clock: ticks, calendar, and the currently selected speed.
    ///
    /// A tick is ONE GAME MINUTE. That granularity is chosen deliberately — the design
    /// talks in terms of "average ticket time 11 minutes against your usual 6" (Phase 6.2),
    /// customer patience, and service windows, all of which are minute-scale. Coarser ticks
    /// would make those unrepresentable; finer ticks would buy nothing.
    ///
    /// The calendar is a real one, so weekdays are real. That matters: a Friday night rush
    /// is a genuine design concept here, not decoration, and a 360-day fantasy calendar
    /// would throw it away for no benefit. A 40-year career is roughly 21 million ticks,
    /// which a long handles without strain.
    ///
    /// This class does NOT own the world or drive anything. It reports elapsed periods and
    /// lets callers decide what that means. Who owns the clock gets settled when Economy
    /// needs it — deciding now would be guessing.
    /// </summary>
    public sealed class GameClock
    {
        public const int TicksPerHour = 60;
        public const int TicksPerDay = 24 * TicksPerHour;    // 1,440
        public const int TicksPerWeek = 7 * TicksPerDay;     // 10,080

        /// <summary>Monday, 2 March 2026 — deliberately a Monday, so weeks line up with real weeks.</summary>
        public static readonly DateTime DefaultStartDate = new DateTime(2026, 3, 2);

        private readonly DateTime _startDate;

        public GameClock() : this(DefaultStartDate) { }

        public GameClock(DateTime startDate)
        {
            _startDate = startDate;
            Tick = 0;
            Speed = GameSpeed.Normal;
        }

        /// <summary>Game-minutes elapsed since the campaign began.</summary>
        public long Tick { get; private set; }

        public DateTime StartDate { get { return _startDate; } }

        /// <summary>The current in-game date and time.</summary>
        public DateTime Now { get { return _startDate.AddMinutes(Tick); } }

        /// <summary>Player's selected pace. Data only — this class never sleeps or polls.</summary>
        public GameSpeed Speed { get; set; }

        /// <summary>1 on opening day, 2 the next day, and so on.</summary>
        public int DayNumber { get { return (int)(Tick / TicksPerDay) + 1; } }

        /// <summary>1 during the opening week, 2 in the next, and so on.</summary>
        public int WeekNumber { get { return (int)(Tick / TicksPerWeek) + 1; } }

        public DayOfWeek DayOfWeek { get { return Now.DayOfWeek; } }
        public int HourOfDay { get { return Now.Hour; } }
        public int MinuteOfHour { get { return Now.Minute; } }

        /// <summary>Whole in-game years elapsed. The design caps a career at roughly 40.</summary>
        public int YearsElapsed
        {
            get
            {
                var now = Now;
                var years = now.Year - _startDate.Year;
                if (now < _startDate.AddYears(years)) years--;

                return years;
            }
        }

        /// <summary>
        /// Moves the clock forward and reports which period boundaries that crossed.
        /// Advancing by zero is legal and reports nothing; advancing backwards is not.
        /// </summary>
        public ElapsedPeriods Advance(long ticks)
        {
            if (ticks < 0)
                throw new ArgumentOutOfRangeException(nameof(ticks), "The clock cannot run backwards.");

            var beforeTick = Tick;
            var before = Now;

            Tick += ticks;

            var after = Now;

            var days = (int)(after.Date - before.Date).TotalDays;
            var weeks = (int)((Tick / TicksPerWeek) - (beforeTick / TicksPerWeek));
            var months = ((after.Year - before.Year) * 12) + after.Month - before.Month;
            var years = after.Year - before.Year;

            return new ElapsedPeriods(ticks, days, weeks, months, years);
        }

        public ElapsedPeriods AdvanceMinutes(int minutes) { return Advance(minutes); }

        public ElapsedPeriods AdvanceHours(int hours) { return Advance((long)hours * TicksPerHour); }

        /// <summary>Jump ahead a day — the smallest of the design's three jump-ahead granularities.</summary>
        public ElapsedPeriods AdvanceDays(int days) { return Advance((long)days * TicksPerDay); }

        public ElapsedPeriods AdvanceWeeks(int weeks) { return Advance((long)weeks * TicksPerWeek); }

        /// <summary>
        /// Jump ahead by calendar months, so this respects real month lengths rather than
        /// assuming 30 days. March 2nd plus one month is April 2nd, whatever that costs in ticks.
        /// </summary>
        public ElapsedPeriods AdvanceMonths(int months)
        {
            if (months < 0)
                throw new ArgumentOutOfRangeException(nameof(months), "The clock cannot run backwards.");

            var target = Now.AddMonths(months);
            return Advance((long)(target - Now).TotalMinutes);
        }

        /// <summary>Advances to the next midnight. Zero-length if already exactly at midnight.</summary>
        public ElapsedPeriods AdvanceToNextDay()
        {
            var remainder = Tick % TicksPerDay;
            return Advance(remainder == 0 ? 0 : TicksPerDay - remainder);
        }

        public override string ToString()
        {
            return Now.ToString("ddd dd MMM yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) +
                   " (day " + DayNumber + ", " + Speed + ")";
        }
    }
}
