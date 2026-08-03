namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// EVERY NUMBER THAT DECIDES HOW THE GAME FEELS, IN ONE PLACE.
    ///
    /// Why this exists, and it is not tidiness. These constants were scattered across the
    /// dozen classes that use them, and each one is duplicated again in the browser build's
    /// JavaScript. That is two copies with no mechanism keeping them equal, and the drift is
    /// not hypothetical — the port has diverged twice, once on equipment speeds and once on
    /// `Markup`, which was ported by NAME rather than by definition and made every guest balk
    /// at every price.
    ///
    /// Two independent sources landed on the same fix. The parallel implementation
    /// (`HSpector1/Restaurant`) keeps a single `Tuning` file shared by its simulator and its
    /// forecaster precisely so the two cannot disagree. And Aaron brought back a suggestion
    /// making the same point — put the balance knobs in one configuration file rather than
    /// scattering them through the code.
    ///
    /// **The guard is `TuningDriftTests`, not this file.** Centralising the C# side is only
    /// half of it; what actually catches drift is a test that reads `web/pass.html` and fails
    /// when the JavaScript disagrees with the numbers below. A constant that lives in one place
    /// but is copied by hand into another is still two constants.
    ///
    /// These stay `const` deliberately rather than loading from JSON. Architecture Rule 2 is
    /// about CONTENT — recipes, furniture, equipment, events — which must be addable by writing
    /// a data file. Tuning is not content: changing `PriceToleranceExponent` is a design
    /// decision that wants a commit and a measurement, not a config edit.
    /// </summary>
    public static class Tuning
    {
        // ---- What a guest weighs when they judge a meal. Sums to 1.0. ----
        public const decimal FoodQualityWeight = 0.42m;
        public const decimal ServiceSpeedWeight = 0.33m;
        public const decimal ValueWeight = 0.17m;
        public const decimal AmbianceWeight = 0.08m;

        /// <summary>Below this, a meal read as poor value. A CHANCE now, not a wall.</summary>
        public const decimal WalkAwayValueThreshold = 0.40m;

        /// <summary>
        /// Value is (1/markup) raised to this. At 1.0 there was a wide dead zone where raising
        /// prices cost nothing at all; squaring it starts resistance about a third over the
        /// designed price instead of waiting for double.
        /// </summary>
        public const double PriceToleranceExponent = 2.0;

        /// <summary>Share of their patience a guest is happy to spend waiting.</summary>
        public const decimal ComfortableWaitShare = 0.40m;

        /// <summary>A kitchen quotes a wait slightly under the truth, as kitchens do.</summary>
        public const int QuotedWaitOptimism = 95;

        // ---- The pass ----

        /// <summary>
        /// A cook works a LINE, not a pan. Modelling one cook as one plate forced a headcount
        /// that bankrupted every restaurant in the sweep — 0/100 profitable. Single biggest
        /// balance error this project has made.
        /// </summary>
        public const int PlatesPerCook = 2;

        /// <summary>How long a table is held for a sitting.</summary>
        public const int DwellMinutes = 35;

        // ---- Menu breadth ----
        public const int FreeMenuSize = 4;
        public const decimal ComplexityPerExtraDish = 0.09m;
        public const decimal MaxComplexityLoad = 1.65m;

        // ---- Hiring: what the market charges, and how wrong a CV can be ----
        public const decimal CookFloorWage = 12m;
        public const decimal ServerFloorWage = 9m;
        public const decimal CookSkillPremium = 16m;
        public const decimal ServerSkillPremium = 9m;
        public const decimal ScoutingError = 0.22m;

        // ---- Reputation. Rates are PER MEAL, and a night is a hundred-plus meals. ----
        // The first version was ten times too fast: a busy restaurant moved a third of the way
        // to a new standing in a single day, which is a status effect wearing a reputation's
        // clothes. Set these against meals, never against intuition about nights.
        public const decimal GoodNewsRate = 0.00008m;
        public const decimal BadNewsRate = 0.0002m;
        public const int MealsToBecomeKnown = 12000;
        public const decimal UnknownTrafficShare = 0.35m;
        public const decimal WorstTrafficMultiplier = 0.60m;
        public const decimal BestTrafficMultiplier = 1.40m;

        /// <summary>The three ceiling shares sum to exactly 1.0, so a perfect score is reachable.</summary>
        public const decimal CompetenceCeiling = 0.42m;
        public const decimal AmbitionFromIngredients = 0.50m;

        // ---- Forecasting ----

        /// <summary>
        /// Party sizes are 1/2/3/4/5-7 at 15/45/20/15/5 percent, which averages to this.
        /// </summary>
        public const decimal AveragePartySize = 2.55m;

        /// <summary>
        /// The share of a kitchen's theoretical throughput a real service gets. Guests arrive
        /// in clumps, so waits build long before utilisation reaches 100%.
        ///
        /// WAS 0.75, AND THAT NUMBER WAS BUNDLING TWO THINGS. Clumping is one; the other was
        /// the pass cooking plates for tables that had already walked out, which burned the
        /// constraint and never reached anybody. Once abandoned plates come back off the
        /// board (KitchenPass.Abandon), that second loss is gone, and charging for it twice
        /// made the forecast under-predict every kitchen-bound night by 17-30%.
        ///
        /// Measured rather than picked: the pass now converts about 94% of its theoretical
        /// station ceiling. This is deliberately set BELOW that. Sweeping the constant, error
        /// keeps falling to about 0.95 and then goes flat — flat because past that the
        /// kitchen stops being the binding ceiling at all, so a value in there would be
        /// fitted to the test rather than to the model. 0.90 keeps a real clumping haircut,
        /// lands median forecast error at 11% (it was 12% before any of this work), and
        /// leaves the projection slightly conservative, which is the right direction to be
        /// wrong in for a number the player plans against.
        /// </summary>
        public const decimal PracticalCapacity = 0.90m;
    }
}
