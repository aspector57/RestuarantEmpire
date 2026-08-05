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

        // ---- Word of mouth: HOW you become known, not just how fast ----
        //
        // Awareness used to be a pure meal COUNTER, so a restaurant became famous on exactly
        // the same schedule whether it was any good or not. Measured over 400 days: a
        // well-run kitchen and one on budget stock with a single cook both hit 100% known
        // within two days of each other. Standing differentiated them (42 against 24);
        // awareness did not differentiate them at all.
        //
        // Restaurant Empire 2's manual states the rule this project should have had from the
        // start: "the more completely satisfied customers there are, the higher your customer
        // awareness", and "100% satisfied customers are your best source of advertising."
        //
        // So a meal now spreads word of mouth in proportion to how much it pleased. The floor
        // exists because being fed at all is worth SOMETHING — you were there, you told
        // someone. Reaching the delight mark scores a full 1.0, which means the old pace is
        // now the BEST case rather than everybody's case: a great restaurant becomes known
        // exactly as fast as before, and every worse one takes longer.
        //
        // This is a modelling fix, not a difficulty dial. It closes the same defect this
        // project keeps finding — a value that is computed, and then not read on the side of
        // the decision it exists to inform.
        /// <summary>
        /// What each further thing on a plate is worth against the one before it. The second
        /// good idea on a dish is worth less than the first, which is what stops "add
        /// everything" being the answer before the category ceiling is even reached.
        /// </summary>
        public const decimal ExtraDiminishing = 0.62m;

        // ---- What it is like to work here ----
        //
        // Both Restaurant Empire games tie morale to wages and hours, and let low morale both
        // slow people down AND generate complaints of its own. That second half is what makes
        // it a decision rather than a tax: an underpaid dining room is not merely cheaper, it
        // is visibly worse to sit in, and the player can see which it is because the complaint
        // says so.
        //
        // Paying the going rate lands at 1.0 and nothing above it buys more — deliberate, so
        // the lever is avoiding a bad wage rather than bidding for a bonus, and paying over
        // the odds never becomes a mechanical optimum. Underpay 20% and morale halves.
        public const decimal MoraleFloor = 0.15m;
        public const decimal MoraleDrift = 0.18m;
        public const int MoraleComfortableHours = 6;

        // ---- How full the room LOOKS ----
        //
        // Aaron's call, and it fixes a one-sided decision. Seats were a pure ratchet: too few
        // cost you covers, too many cost only the price of the chairs. Measured, profit peaked
        // at 30 seats on the nightlife pitch and fell to $164k at 100, and nothing ever said
        // why. *"Unless we can have customers say something like, this restaurant is so empty,
        // and it impacts their experience?"*
        //
        // Being quiet is forgiven up to a point — a new place is quiet because nobody has
        // heard of it, and punishing that would punish the opening. A full house gets a small
        // lift, far smaller than the penalty: this is a mistake to discover, not a bonus to
        // farm. Deliberately NOT warned about in advance, per Aaron: "discovering you
        // overbuilt should be part of the game."
        public const decimal RoomFeelsDead = 0.20m;
        public const decimal RoomFeelsThin = 0.45m;
        public const decimal RoomFeelsBuzzing = 0.70m;
        public const decimal RoomBuzzLift = 0.06m;

        /// <summary>Below this, a component of the meal is worth complaining about.</summary>
        public const decimal GrumbleThreshold = 0.55m;

        public const decimal WordOfMouthFloor = 0.25m;
        public const decimal WordOfMouthFrom = 0.40m;
        public const decimal WordOfMouthDelight = 0.85m;
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
        /// in clumps, so waits build long before utilization reaches 100%.
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
