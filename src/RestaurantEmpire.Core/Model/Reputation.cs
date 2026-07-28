using System;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// What the neighbourhood has come to think of this place, and therefore how many people
    /// turn up. The missing feedback loop: until this existed, a meal was judged and then
    /// forgotten, so serving bad food cost nothing at all.
    ///
    /// That absence was measurable and it was the last piece of a real exploit. Ingredient
    /// quality reached the satisfaction score and the door decision, but at HONEST prices the
    /// value score saturates — a cheap dish at a cheap price is fair, and no one walks out
    /// over it. So budget stock stayed marginally the most profitable way to run a restaurant
    /// for as long as nobody remembered eating there. Reputation is what remembers.
    ///
    /// UNLIKE almost everything else in this project, this is genuinely STATE and not a live
    /// computation. That is not a violation of Architecture Rule 1, which forbids caching
    /// values that are DERIVED from policy — a recipe's cost, a contribution margin. This is
    /// accumulated history: it cannot be recomputed from current state, because the whole
    /// point is that it remembers what you served last month. It therefore has to be saved.
    /// </summary>
    public sealed class Reputation
    {
        /// <summary>Where a new restaurant starts: unknown, neither liked nor disliked.</summary>
        public const decimal Neutral = 0.5m;

        /// <summary>
        /// How fast praise accumulates, PER MEAL SERVED. Deliberately slow — a reputation you
        /// can rebuild in one evening is not a reputation, it is a status effect.
        ///
        /// Calibrated against how many meals a night actually is, which is the thing that
        /// caught me out twice. A busy restaurant serves several thousand meals a month, so
        /// rates that look glacial per meal are fast per season — the first attempt moved a
        /// third of the way in a single DAY.
        ///
        /// At these rates, roughly: one night moves standing about 1%, a month about 27%,
        /// and building a real name takes half a year of consistently good food. Bad news
        /// runs 2.5x faster, so the same slide downward takes a couple of months. That is
        /// the pace Aaron asked for — deteriorating over weeks and months rather than
        /// overnight — and it is slow enough that a reputation feels like something you own
        /// rather than a meter that tracks last night.
        /// </summary>
        public const decimal GoodNewsRate = 0.00008m;

        /// <summary>
        /// How fast complaints accumulate. Bad news travels roughly two and a half times
        /// faster than good, which is both true to life and what gives cutting corners a cost
        /// that outlasts the saving.
        /// </summary>
        public const decimal BadNewsRate = 0.0002m;

        /// <summary>Trade at rock bottom, as a share of the street's normal traffic.</summary>
        public const decimal WorstTrafficMultiplier = 0.60m;

        /// <summary>Trade at the top. Bounded, because a reputation cannot conjure a queue
        /// out of an empty street — the neighbourhood still decides who walks past.</summary>
        public const decimal BestTrafficMultiplier = 1.40m;

        /// <summary>
        /// The standing any restaurant can reach on competence alone — turning food out fast,
        /// pricing it honestly, keeping the room decent. Aaron's framing: *"you can be
        /// moderately successful but not like the best in the world."* This is where
        /// "moderately successful" sits.
        /// </summary>
        public const decimal CompetenceCeiling = 0.45m;

        /// <summary>How much of the remaining headroom good ingredients unlock.</summary>
        public const decimal AmbitionFromIngredients = 0.40m;

        /// <summary>
        /// And how much the room unlocks — deliberately EQUAL to its weight in a single meal.
        /// The room counts for exactly as much in what you can become as it does in what a
        /// guest thinks of one dinner, which keeps decor the smallest lever everywhere rather
        /// than a nudge in one system and a decider in another. At 0.15 a set of walnut
        /// tables moved the ceiling from 0.69 to 0.84 on its own, which is furniture buying
        /// a reputation that the kitchen has not earned.
        /// </summary>
        public const decimal AmbitionFromRoom = SatisfactionModel.AmbianceWeight;

        public Reputation(decimal standing = Neutral)
        {
            Standing = Clamp(standing);
        }

        /// <summary>0 (notorious) to 1 (beloved). Starts at <see cref="Neutral"/>.</summary>
        public decimal Standing { get; private set; }

        /// <summary>
        /// The best this restaurant could ever be thought of, given what it is actually
        /// attempting. Recorded whenever a meal is, so the UI can explain a plateau.
        /// </summary>
        public decimal Ceiling { get; private set; } = 1m;

        /// <summary>
        /// Settled at the ceiling — held back by what the place sources rather than by how
        /// it is run. Deliberately NOT true while standing is above the ceiling and falling,
        /// which is a different situation entirely (see <see cref="LivingOnPastGlory"/>).
        /// </summary>
        public bool AtCeiling
        {
            get { return Standing >= Ceiling - 0.01m && Standing <= Ceiling + 0.02m; }
        }

        /// <summary>
        /// Trading on a name the current ingredients no longer justify — the window after
        /// cutting corners, while the reputation is still sliding toward what the place has
        /// become. This is the most useful thing the system can tell a player, because it is
        /// the only moment when the damage is visible and not yet done.
        /// </summary>
        public bool LivingOnPastGlory { get { return Standing > Ceiling + 0.02m; } }

        /// <summary>How many meals have contributed. Mostly so the UI can say "still finding its feet".</summary>
        public int MealsRemembered { get; private set; }

        /// <summary>
        /// Word of mouth from one meal.
        ///
        /// Per MEAL rather than per night, so a busy restaurant's reputation moves faster than
        /// a quiet one's — more people leave with an opinion. That also means a place nobody
        /// visits stays unknown for a long time, which is correct.
        /// </summary>
        public void RecordMeal(decimal satisfaction, decimal ceiling = 1m)
        {
            Ceiling = Clamp(ceiling);

            var heard = Clamp(satisfaction);
            var rate = heard < Standing ? BadNewsRate : GoodNewsRate;

            Standing = Clamp(Standing + ((heard - Standing) * rate));

            // THE CEILING PULLS, IT DOES NOT CLAMP.
            //
            // This was `if (Standing > Ceiling) Standing = Ceiling;` and that was wrong in a
            // way worth remembering. A ceiling derived from current state, applied as a hard
            // limit, means the instant you change supplier your reputation is already gone —
            // measured at 0.890 to 0.568 in ONE DAY of service. A name built over six months
            // cannot evaporate over one dinner. Aaron's correction: it should deteriorate
            // over weeks or months, and only a critic or an influencer who catches it should
            // be able to make it sudden (see the M5 addendum in docs/design.md — that is an
            // Event, and deliberately not built here).
            //
            // So a restaurant trading above what it now sources drifts downward at the
            // bad-news rate rather than falling off a wall. The reputation is still lost;
            // it just takes the time that losing a reputation actually takes.
            if (Standing > Ceiling) Standing -= (Standing - Ceiling) * BadNewsRate;

            MealsRemembered++;
        }

        /// <summary>
        /// How well regarded a restaurant could ever become, from what it is attempting.
        ///
        /// Competence is free and gets you to the middle. Past that you are buying it: better
        /// ingredients unlock most of the rest, the room a little, and there is no route to
        /// the top on cheap stock however well you run the pass.
        /// </summary>
        public static decimal CeilingFor(decimal averageIngredientQuality, decimal comfort)
        {
            var ceiling = CompetenceCeiling
                        + (Clamp(averageIngredientQuality) * AmbitionFromIngredients)
                        + (Clamp(comfort) * AmbitionFromRoom);

            return Clamp(ceiling);
        }

        /// <summary>
        /// A guest who left without eating. Counted as a bad experience — they tell people
        /// too, and a restaurant that seats you and then loses you is exactly the sort of
        /// thing that gets talked about.
        /// </summary>
        public void RecordWalkout(decimal ceiling = 1m)
        {
            RecordMeal(0m, ceiling);
        }

        /// <summary>
        /// What this does to footfall. Neutral standing is exactly 1.0, so a brand-new
        /// restaurant is neither rewarded nor punished for having no history.
        /// </summary>
        public decimal TrafficMultiplier
        {
            get
            {
                // Two straight segments meeting at neutral, so 0.5 maps to exactly 1.0 and
                // neither half is squashed against the other.
                if (Standing <= Neutral)
                    return WorstTrafficMultiplier + ((Standing / Neutral) * (1m - WorstTrafficMultiplier));

                return 1m + (((Standing - Neutral) / (1m - Neutral)) * (BestTrafficMultiplier - 1m));
            }
        }

        /// <summary>Plain language, because a bare number is not a reason (Binding Principle 2).</summary>
        public string Verdict
        {
            get
            {
                if (MealsRemembered < 50) return "still finding its feet — too new to have a reputation";

                // The warning, and it has to come before the plateau message. A restaurant
                // that has just switched to cheaper stock still LOOKS beloved for a while —
                // measured at 0.884 the day after a switch that dropped its ceiling to 0.570.
                // Saying "as well liked as these ingredients allow" there would be flatly
                // wrong and would waste the one window where the player can still undo it.
                if (LivingOnPastGlory)
                    return "still trading on a name these ingredients no longer justify";

                // Naming the ceiling matters more than naming the score. "Stuck at 61" is a
                // number; "as well regarded as budget ingredients allow" is a decision.
                if (AtCeiling && Ceiling < 0.80m)
                    return "as well liked as a place serving these ingredients can be";

                if (Standing >= 0.80m) return "people go out of their way to eat here";
                if (Standing >= 0.65m) return "well thought of locally";
                if (Standing >= 0.45m) return "no strong opinion either way";
                if (Standing >= 0.30m) return "word has got round that it is not worth it";
                return "people warn each other off this place";
            }
        }

        /// <summary>Restores a saved standing. Loading is the only reason to set this directly.</summary>
        public void Restore(decimal standing, int mealsRemembered)
        {
            Standing = Clamp(standing);
            MealsRemembered = mealsRemembered < 0 ? 0 : mealsRemembered;
        }

        private static decimal Clamp(decimal v)
        {
            if (v < 0m) return 0m;
            return v > 1m ? 1m : v;
        }

        public override string ToString()
        {
            return Math.Round(Standing * 100m) + "/100 — " + Verdict;
        }
    }
}
