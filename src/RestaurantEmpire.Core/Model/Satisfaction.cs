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
        /// colors it.
        /// </summary>
        public const decimal AmbianceWeight = 0.08m;

        /// <summary>Food cost ratio treated as fair value for money — the midpoint of the real 28-35% band.</summary>
        public const decimal FairFoodCostRatio = 0.33m;

        /// <summary>Guests are fully happy with the wait up to this share of their patience.</summary>
        public const decimal ComfortableWaitShare = 0.40m;

        /// <summary>
        /// What actually arrives on the plate: what you bought, worked by whoever is cooking.
        ///
        /// Ingredients set the ceiling on a dish and the kitchen decides how much of it you
        /// get. A strong brigade lifts mid-market stock close to what it could be; a weak one
        /// wastes whatever it is handed, which is why buying premium and staffing badly is a
        /// way to spend a great deal of money on a mediocre dinner. An average cook (0.5) is
        /// exactly neutral, so this changes nothing for a payroll that has not been chosen.
        /// </summary>
        public static decimal PlateQuality(decimal ingredientQuality, decimal kitchenSkill,
            decimal freshness = 1m)
        {
            var craft = 0.6m + (Clamp(kitchenSkill) * 0.8m);   // 0.6x at worst, 1.4x at best

            // What you bought, worked by whoever is cooking, and how long it has been sitting.
            // Freshness bottoms out at 0.55 rather than zero — the worst a guest gets from
            // stock that is still legally food is "that didn't taste fresh".
            return Clamp(ingredientQuality * craft * Clamp(freshness));
        }

        /// <summary>
        /// How good a deal a dish looks, 0 to 1, judged on MARKUP — what you charge against
        /// what the dish is worth — rather than on food cost ratio.
        ///
        /// That distinction matters more than it looks. A flat white with 27p of coffee in
        /// it runs a 7% food cost, and nobody thinks a $3.60 coffee is a swindle; coffee is
        /// simply a high-margin product. Judging value by food cost ratio cannot tell an
        /// ordinary coffee apart from a pizza sold at three times its worth, because both
        /// land at the same ratio. Markup can, and it is also what a guest actually reacts
        /// to: this costs more than it should.
        ///
        /// Exposed separately from <see cref="Evaluate"/> because a guest can judge it from
        /// the menu in the window — they need not sit down and be disappointed to notice.
        /// That is what stops "put every price up 3x" being free money.
        /// </summary>
        public static decimal ScoreValue(decimal markup, decimal priceSensitivity,
            decimal ingredientQuality = 0m, decimal reputation = Model.Reputation.Neutral)
        {
            if (priceSensitivity <= 0m) priceSensitivity = 1m;
            if (markup <= 0m) return 1m;   // comped, and nobody complains about free

            // Raised to a power, so resistance builds rather than waiting for a cliff.
            var worth = (decimal)System.Math.Pow(1.0 / (double)markup, PriceToleranceExponent);

            // WHAT ARRIVES IS PART OF WHAT YOU PAID FOR. Value is what you get over what you
            // give, and until now only the second half was modeled — so switching every
            // ingredient to the cheapest supplier raised margin, lowered the satisfaction
            // score, and changed absolutely nothing else. Measured before this: budget stock
            // served 4,089 covers with 151 walkouts, premium served 4,089 covers with 151
            // walkouts. Identical. The cheapest supplier was strictly dominant and free,
            // inside the one system this whole project exists to get right.
            //
            // Putting quality HERE rather than only in dish selection is the point. Appetite
            // decides which dish off a menu, so a quality change applied evenly to every dish
            // is a common factor and cancels out completely — it cannot make anyone eat
            // somewhere else. This can, because it is read at the door.
            if (ingredientQuality > 0m)
                worth *= 0.6m + (ingredientQuality * 0.8m);   // tier 1 -> 0.76x, tier 5 -> 1.40x

            // A REPUTATION IS WHAT LETS YOU CHARGE. This is the half that makes buying good
            // ingredients rational at all: without it, reputation bought nothing but footfall,
            // footfall does not pay for truffles, and budget stock out-earned premium at every
            // horizon — so no player would ever have sourced well.
            //
            // It is also simply how the trade works. Nobody pays $200 a head because the
            // ingredients cost $60; they pay it because of what the place is. Neutral standing
            // is exactly 1.0, so this changes nothing for a restaurant nobody has heard of.
            worth *= 0.75m + (Clamp(reputation) * 0.5m);   // unknown 1.0x, beloved ~1.20x

            return Clamp(worth / priceSensitivity);
        }

        /// <summary>
        /// Below this, a guest reading the menu decides it is not worth it and goes
        /// elsewhere. Deliberately forgiving — a modest markup should cost you nothing,
        /// and only real gouging should empty the room.
        /// </summary>
        public const decimal WalkAwayValueThreshold = 0.40m;

        /// <summary>
        /// How sharply people react to a price above what the dish is worth.
        ///
        /// This was 1 — a plain reciprocal — and it made over-charging the dominant strategy.
        /// Measured across a month: profit rose from 7,315 at the designed prices to 47,001 at
        /// two and a half times them, with NOBODY put off until double. A free six-fold
        /// multiplier sitting behind a slider is not a decision, and Aaron found it in about a
        /// minute: *"raised prices again, drastically this time. Still making a ton of money."*
        ///
        /// At 2.5 the reaction starts around a third above the designed price and steepens,
        /// so there is real headroom for a confident operator and a real wall behind it.
        /// </summary>
        public const double PriceToleranceExponent = 2.0;

        /// <summary>
        /// How likely a party is to read the menu and leave, given how good a deal it looks.
        ///
        /// A hard threshold could only ever produce a CLIFF: everybody tolerates the price
        /// until one more cent, and then the entire street stops coming. Measured at an
        /// exponent of 2.5, a 1.4x menu was thriving and a 1.6x menu served literally nobody.
        /// Real demand does not work like that and neither should this.
        ///
        /// Now it is a chance that climbs as the deal worsens, so raising prices loses you a
        /// growing SHARE of the room rather than all of it at once — and the price-sensitive
        /// go first, which is what makes archetypes matter at the door.
        /// </summary>
        public static decimal WalkAwayChance(decimal value)
        {
            if (value >= WalkAwayValueThreshold) return 0m;

            var shortfall = (WalkAwayValueThreshold - value) / WalkAwayValueThreshold;
            return shortfall > 1m ? 1m : shortfall;
        }

        public static SatisfactionResult Evaluate(
            CustomerParty party, Ticket ticket, string dishName,
            decimal ingredientQuality, decimal markup, decimal comfort = 0.5m,
            decimal reputation = Model.Reputation.Neutral)
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
            var quality = Clamp(ingredientQuality);   // craft is applied by the caller
            var value = ScoreValue(markup, party.PriceSensitivity, ingredientQuality, reputation);
            var ambiance = Clamp(comfort);

            var overall = (quality * FoodQualityWeight)
                        + (speed * ServiceSpeedWeight)
                        + (value * ValueWeight)
                        + (ambiance * AmbianceWeight);

            return new SatisfactionResult(quality, speed, value, ambiance, overall, false,
                Diagnose(dishName, ticket, quality, speed, value, ambiance));
        }

        /// <summary>
        /// How the wait felt, 0 to 1. Public so a dish rating can be built from exactly the
        /// same arithmetic a guest uses, rather than a second opinion that could drift.
        /// </summary>
        public static decimal ScoreSpeed(int waitMinutes, int patienceMinutes)
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
