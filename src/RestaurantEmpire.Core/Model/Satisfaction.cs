using System;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// How one guest felt, and — required by the design's legibility contract — exactly why.
    ///
    /// The three components line up with three of Reputation's four sub-scores (food
    /// quality, service speed, value). The fourth, ambiance, arrives with Furniture at M1.
    /// </summary>
    public sealed class SatisfactionResult
    {
        internal SatisfactionResult(
            decimal foodQuality, decimal serviceSpeed, decimal value, decimal ambiance,
            decimal overall, bool walkedOut, string diagnosis)
        {
            FoodQuality = foodQuality;
            ServiceSpeed = serviceSpeed;
            Value = value;
            Ambiance = ambiance;
            Overall = overall;
            WalkedOut = walkedOut;
            Diagnosis = diagnosis;
        }

        /// <summary>0 to 1, driven by the quality tier of the suppliers actually feeding this dish.</summary>
        public decimal FoodQuality { get; }

        /// <summary>0 to 1, driven by wait time against this guest's patience.</summary>
        public decimal ServiceSpeed { get; }

        /// <summary>0 to 1, driven by how well the plate justifies its price.</summary>
        public decimal Value { get; }

        /// <summary>
        /// 0 to 1, from how the room is furnished. Carries the smallest weight of the four
        /// by design — bare walls should mildly disappoint, never sink a restaurant.
        /// </summary>
        public decimal Ambiance { get; }

        /// <summary>0 to 1 overall. Zero when they walked out.</summary>
        public decimal Overall { get; }

        public bool WalkedOut { get; }

        /// <summary>
        /// One line naming the specific cause, never just a score. Phase 6.2 makes this a
        /// contract: "Grill station backed up during Friday 7-9pm" beats "service was slow".
        /// </summary>
        public string Diagnosis { get; }

        public override string ToString()
        {
            return Overall.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " — " + Diagnosis;
        }
    }

    /// <summary>
    /// The satisfaction formula.
    ///
    /// Every input is something M0 already computes, which is the point — this is not a
    /// placeholder with magic numbers, it is the existing simulation being read back:
    ///
    ///   FOOD QUALITY comes from the quality tier of whichever suppliers are currently
    ///   assigned. Buy better, guests notice.
    ///
    ///   SERVICE SPEED comes from the kitchen ticket's real wait, judged against this
    ///   guest's own patience. An overloaded station shows up here directly.
    ///
    ///   VALUE comes from food cost ratio — how much plate the guest got for the price.
    ///   The industry benchmark is 28-35% (design doc Phase 2), so a third is treated as
    ///   fair. This is what makes pricing a genuine tradeoff instead of free money:
    ///   raising a price lifts margin and lowers perceived value at the same time, and
    ///   cheapening ingredients lifts margin while lowering BOTH quality and value.
    /// </summary>
    public static class SatisfactionModel
    {
        public const decimal FoodQualityWeight = 0.42m;
        public const decimal ServiceSpeedWeight = 0.33m;
        public const decimal ValueWeight = 0.17m;

        /// <summary>
        /// Deliberately the smallest of the four. The design is explicit that furniture is a
        /// "small, bounded modifier... capped low enough that it's forgiving, not a trap",
        /// because cutting corners on decor while broke is the intended early-game
        /// experience. Food and service decide whether a night was good; the room only
        /// colours it.
        /// </summary>
        public const decimal AmbianceWeight = 0.08m;

        /// <summary>Food cost ratio treated as fair value for money — the midpoint of the real 28-35% band.</summary>
        public const decimal FairFoodCostRatio = 0.33m;

        /// <summary>Guests are fully happy with the wait up to this share of their patience.</summary>
        public const decimal ComfortableWaitShare = 0.40m;

        public static SatisfactionResult Evaluate(
            CustomerParty party, Ticket ticket, string dishName,
            decimal ingredientQuality, decimal foodCostRatio, decimal comfort = 0.5m)
        {
            if (party == null) throw new ArgumentNullException(nameof(party));
            if (ticket == null) throw new ArgumentNullException(nameof(ticket));

            // A dish that never arrived is its own outcome — no scoring needed.
            if (!ticket.WasServed)
                return new SatisfactionResult(0m, 0m, 0m, 0m, 0m, true, ticket.FailureReason);

            // Waited past their limit and gave up.
            if (ticket.WaitMinutes > party.PatienceMinutes)
            {
                return new SatisfactionResult(0m, 0m, 0m, 0m, 0m, true,
                    "Walked out after " + ticket.WaitMinutes + " min waiting for " + dishName +
                    " (patience " + party.PatienceMinutes + " min; " + ticket.QueuedMinutes +
                    " of that was the " + ticket.StationId + " station backed up).");
            }

            var speed = ScoreSpeed(ticket.WaitMinutes, party.PatienceMinutes);
            var quality = Clamp(ingredientQuality);
            var value = Clamp((foodCostRatio / FairFoodCostRatio) / party.PriceSensitivity);
            var ambiance = Clamp(comfort);

            var overall = (quality * FoodQualityWeight)
                        + (speed * ServiceSpeedWeight)
                        + (value * ValueWeight)
                        + (ambiance * AmbianceWeight);

            return new SatisfactionResult(quality, speed, value, ambiance, overall, false,
                Diagnose(dishName, ticket, quality, speed, value, ambiance));
        }

        private static decimal ScoreSpeed(int waitMinutes, int patienceMinutes)
        {
            var comfortable = patienceMinutes * ComfortableWaitShare;

            if (waitMinutes <= comfortable) return 1m;

            // Degrades linearly from "fine" to "nearly walked out".
            var overrun = waitMinutes - comfortable;
            var window = patienceMinutes - comfortable;

            return window <= 0m ? 0m : Clamp(1m - (overrun / window));
        }

        /// <summary>Names the weakest component, with the number that caused it.</summary>
        private static string Diagnose(string dishName, Ticket ticket, decimal quality, decimal speed, decimal value, decimal ambiance)
        {
            // Ambiance is only ever named when it is both the worst thing AND genuinely
            // poor — a bare room should not become the headline complaint about a slow,
            // overpriced dinner.
            if (ambiance < 0.3m && ambiance < quality && ambiance < speed && ambiance < value)
                return dishName + " was fine, but the room is bleak.";

            if (speed <= quality && speed <= value)
            {
                if (speed >= 0.95m)
                    return dishName + " arrived in " + ticket.WaitMinutes + " min. No complaints.";

                return dishName + " took " + ticket.WaitMinutes + " min (" + ticket.QueuedMinutes +
                       " min queued at the " + ticket.StationId + " station).";
            }

            if (quality <= value)
                return dishName + " was fine, but the ingredients tasted cheap.";

            return dishName + " was good, but felt expensive for what it was.";
        }

        private static decimal Clamp(decimal value)
        {
            if (value < 0m) return 0m;
            return value > 1m ? 1m : value;
        }
    }
}
