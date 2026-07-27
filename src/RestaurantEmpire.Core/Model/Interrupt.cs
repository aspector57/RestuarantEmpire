using System;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// The M1 interrupt set. Deliberately three, deliberately hardcoded.
    ///
    /// M1's rhythm bar asks only whether the fast-forward-with-interrupts loop has a pulse.
    /// Interrupt VARIETY and "was that worth stopping for?" are M2 questions, answerable
    /// once the Advisor exists and can generate them properly. Growing this list at M1
    /// would be answering the wrong question expensively.
    ///
    /// All three read state the simulation already produces.
    /// </summary>
    public enum InterruptKind
    {
        /// <summary>A dish was 86'd mid-service because the walk-in ran dry.</summary>
        IngredientStockout = 0,

        /// <summary>Guests have started giving up and leaving, repeatedly.</summary>
        WalkoutStreak = 1,

        /// <summary>Cash fell through a threshold the player asked to be warned about.</summary>
        CashThreshold = 2
    }

    /// <summary>
    /// Something that stopped the simulation and needs the player. Carries the moment it
    /// happened and a plain-language reason, per the "every outcome traces to a named
    /// cause" contract.
    /// </summary>
    public sealed class Interrupt
    {
        internal Interrupt(InterruptKind kind, long tick, DateTime at, string message, string subjectId)
        {
            Kind = kind;
            Tick = tick;
            At = at;
            Message = message;
            SubjectId = subjectId;
        }

        public InterruptKind Kind { get; }

        /// <summary>The exact tick this fired. The sim is paused here and resumes from here.</summary>
        public long Tick { get; }

        public DateTime At { get; }

        public string Message { get; }

        /// <summary>Whatever the interrupt is about — an ingredient id, a restaurant id.</summary>
        public string SubjectId { get; }

        public override string ToString()
        {
            return At.ToString("ddd HH:mm") + "  " + Message;
        }
    }

    /// <summary>
    /// When the three M1 interrupts fire. Tunable so playtesting can find the pulse without
    /// a rebuild — interrupt frequency is explicitly flagged in the design as needing
    /// playtest data rather than more design.
    /// </summary>
    public sealed class InterruptPolicy
    {
        /// <summary>Consecutive walkouts before the player is pulled in. Reset by any served cover.</summary>
        public int WalkoutStreakThreshold { get; set; }

        /// <summary>Cash level that, once crossed downward, stops the sim. Null disables it.</summary>
        public decimal? CashFloor { get; set; }

        /// <summary>Whether an 86'd dish stops the sim.</summary>
        public bool StopOnStockout { get; set; }

        /// <summary>
        /// How far back above the floor cash must climb before it can raise the alarm
        /// again. Without this, a restaurant hovering around zero trips the same alarm
        /// every few minutes, which is noise rather than information.
        /// </summary>
        public decimal CashRearmMargin { get; set; }

        public InterruptPolicy()
        {
            WalkoutStreakThreshold = 3;
            CashFloor = 0m;
            CashRearmMargin = 1000m;
            StopOnStockout = true;
        }

        /// <summary>Nothing interrupts. Useful for measuring an uninterrupted baseline.</summary>
        public static InterruptPolicy None()
        {
            return new InterruptPolicy { WalkoutStreakThreshold = 0, CashFloor = null, StopOnStockout = false };
        }
    }
}
