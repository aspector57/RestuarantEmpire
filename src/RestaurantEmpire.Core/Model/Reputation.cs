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
        /// caught me out: at ten times this rate a busy restaurant moved a third of the way
        /// to its new standing in a SINGLE DAY, which is a status effect wearing a
        /// reputation's clothes. At this rate a hundred-cover night shifts standing about 4%
        /// of the way, and a month of trading roughly 70% — slow enough that you feel it
        /// arrive, fast enough that a season of cutting corners genuinely costs you.
        /// </summary>
        public const decimal GoodNewsRate = 0.0004m;

        /// <summary>
        /// How fast complaints accumulate. Bad news travels roughly two and a half times
        /// faster than good, which is both true to life and what gives cutting corners a cost
        /// that outlasts the saving.
        /// </summary>
        public const decimal BadNewsRate = 0.0010m;

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

        /// <summary>Whether standing has run into that ceiling rather than merely being low.</summary>
        public bool AtCeiling { get { return Standing >= Ceiling - 0.01m; } }

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

            // A CEILING, not a cliff. Aaron's point: a cheap decent dish satisfies the person
            // eating it — they got what they paid for — but nobody loves a restaurant for it,
            // so the two ratings are connected without being the same number. Serving budget
            // food competently will carry you to "well thought of locally" and stop there,
            // and that is a real strategy rather than a punishment: you can run a profitable
            // neighbourhood place forever. You simply cannot become the best in the world
            // doing it, because being the best is a thing you have to actually attempt.
            if (Standing > Ceiling) Standing = Ceiling;

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
