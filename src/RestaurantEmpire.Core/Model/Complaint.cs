using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// WHAT A GUEST WOULD ACTUALLY SAY, and how much it mattered.
    ///
    /// Satisfaction is one number, and one number cannot be acted on: 0.61 does not tell the
    /// player whether to hire a cook, drop a price, or replace the chairs — and those are three
    /// different bills. Both Restaurant Empire games put an itemised complaint list in front of
    /// the player for exactly this reason.
    ///
    /// This project's own notes predicted needing it. The marketing lie was measured as taking
    /// about two years to reach the books, and the recorded conclusion was that the chain from
    /// a broken promise to lost trade is long by construction — *"if it needs to bite harder
    /// the honest fix is a visible consequence (a complaint, a bad review) rather than a bigger
    /// divisor."* This is that consequence: overclaim tonight and fourteen tables say so
    /// tonight.
    ///
    /// It is Binding Principle 2 in its most legible form — every outcome traces to a specific
    /// named cause, in the guest's words rather than as a score movement.
    /// </summary>
    public sealed class Complaint
    {
        public string Code { get; }
        public string Said { get; }

        /// <summary>1 to 3. How far short the thing fell, not how many people said it.</summary>
        public int Severity { get; }

        public Complaint(string code, string said, int severity)
        {
            Code = code;
            Said = said;
            Severity = severity < 1 ? 1 : severity > 3 ? 3 : severity;
        }

        public override string ToString()
        {
            return Said + " (" + new string('*', Severity) + ")";
        }
    }

    /// <summary>What one guest thought, component by component, so a complaint can name a cause.</summary>
    public struct MealVerdict
    {
        public decimal Food;
        public decimal Speed;
        public decimal Value;
        public decimal Room;

        /// <summary>Weakest ingredient's freshness, 0 to 1. Separates "stale" from "badly cooked".</summary>
        public decimal Freshness;

        /// <summary>How the floor staff feel. Separates "rude" from "slow".</summary>
        public decimal FloorMorale;

        /// <summary>Whether the restaurant is currently claiming its ingredients are exceptional.</summary>
        public bool ClaimsItsIngredients;

        /// <summary>
        /// How full the room looked when they sat down, 0 to 1. Separates "the room is tired"
        /// from "the room is empty" — decor and overbuilding are different mistakes.
        /// </summary>
        public decimal Occupancy;
    }

    public static class Complaints
    {
        private static int SeverityOf(decimal score)
        {
            var shortfall = Tuning.GrumbleThreshold - score;
            if (shortfall >= 0.30m) return 3;
            return shortfall >= 0.15m ? 2 : 1;
        }

        /// <summary>
        /// Nobody complains about a merely average dinner — a component has to fall below
        /// <see cref="Tuning.GrumbleThreshold"/> before it is worth mentioning.
        /// </summary>
        public static IReadOnlyList<Complaint> From(MealVerdict meal)
        {
            var said = new List<Complaint>();

            if (meal.Speed < Tuning.GrumbleThreshold)
                said.Add(new Complaint("wait", "the food took too long", SeverityOf(meal.Speed)));

            if (meal.Value < Tuning.GrumbleThreshold)
                said.Add(new Complaint("price", "dear for what it was", SeverityOf(meal.Value)));

            if (meal.Room < Tuning.GrumbleThreshold)
            {
                // A tired room and an empty one are different complaints with different fixes:
                // one is decor, the other is having bought more seats than the street can fill.
                if (meal.Occupancy <= Tuning.RoomFeelsThin)
                    said.Add(new Complaint("empty",
                        "the place was half empty — it felt like nobody wanted to be there",
                        SeverityOf(meal.Room)));
                else
                    said.Add(new Complaint("room", "the room is tired", SeverityOf(meal.Room)));
            }

            // The food line SPLITS, because "the food was poor" is not actionable and the three
            // things underneath it are: old stock, a claim the kitchen cannot live up to, and
            // plain bad cooking. Three different fixes at three different prices.
            if (meal.Food < Tuning.GrumbleThreshold)
            {
                if (meal.Freshness < 0.75m)
                    said.Add(new Complaint("stale", "it did not taste fresh", SeverityOf(meal.Food)));
                else if (meal.ClaimsItsIngredients)
                    said.Add(new Complaint("claim", "not what the advertising promised", SeverityOf(meal.Food)));
                else
                    said.Add(new Complaint("food", "the cooking was not up to much", SeverityOf(meal.Food)));
            }

            // Morale is its OWN complaint rather than a hidden multiplier, so an underpaid
            // dining room is something the player sees rather than infers from a worse score.
            if (meal.FloorMorale < 0.45m)
                said.Add(new Complaint("service", "the staff seemed like they would rather be elsewhere",
                                       SeverityOf(meal.FloorMorale)));

            return said;
        }
    }
}
