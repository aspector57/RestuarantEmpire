using System;
using System.Collections.Generic;
using System.Linq;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// How much authority a suggestion carries. The three tiers are the design's central
    /// protection against the Advisor quietly becoming the player.
    /// </summary>
    public enum AdvisorTier
    {
        /// <summary>
        /// Hygiene. The right answer is nearly always the same, so do not spend the player's
        /// attention on it — state it flatly, or let standing policy handle it. Rarely an
        /// interrupt.
        /// </summary>
        Chore = 0,

        /// <summary>
        /// A question, with its reasoning visible, answered yes or no.
        ///
        /// The rule that keeps this honest: ONLY ask where "no" is genuinely defensible. If
        /// yes is always right it is a chore, not a proposal. This tier should be the bulk
        /// of what the player sees.
        /// </summary>
        Proposal = 1,

        /// <summary>
        /// Never a yes/no. The Advisor may name an opportunity or an observation; the player
        /// decides what to do and initiates it themselves.
        ///
        /// This tier exists because a game made entirely of yes/no prompts is not a strategy
        /// game, it is a notification inbox. The player must keep a category of decisions
        /// they reach for unprompted.
        /// </summary>
        Strategic = 2
    }

    /// <summary>One thing the Advisor has noticed.</summary>
    public sealed class Suggestion
    {
        internal Suggestion(string id, AdvisorTier tier, string headline, string reasoning,
            string question = null, string subjectId = null, decimal? price = null)
        {
            Id = id;
            Tier = tier;
            Headline = headline;
            Reasoning = reasoning;
            Question = question;
            SubjectId = subjectId;
            Price = price;
        }

        /// <summary>Stable, so the same observation can be recognized across days.</summary>
        public string Id { get; }

        public AdvisorTier Tier { get; }

        /// <summary>What was noticed, in the voice of someone who works here.</summary>
        public string Headline { get; }

        /// <summary>The concrete numbers behind it. Never "trust me".</summary>
        public string Reasoning { get; }

        /// <summary>The yes/no being asked. Proposals only — null at every other tier.</summary>
        public string Question { get; }

        /// <summary>Whatever this is about: a recipe, an ingredient, a station.</summary>
        public string SubjectId { get; }

        /// <summary>What acting would cost, when that is knowable.</summary>
        public decimal? Price { get; }

        public override string ToString()
        {
            return Headline + " " + Reasoning + (Question == null ? "" : "  " + Question);
        }
    }

    /// <summary>
    /// The recommendation layer — your sous chef, not an oracle.
    ///
    /// The distinction matters more than it sounds. "We have too much fish, want to feature
    /// it?" is someone noticing something concrete on the ground and proposing a response.
    /// "Feature this dish, it's a Puzzle" is the game handing down its own strategic verdict
    /// and dissolving one of the three mechanics the design calls central. Same information,
    /// entirely different relationship — and only the first leaves the player running the
    /// restaurant.
    ///
    /// So: the Advisor may say what it SEES at any tier. It may only say what to DO about
    /// chores and tactical proposals. Menu strategy, pricing, expansion and hiring direction
    /// are surfaced as opportunities and then left alone.
    ///
    /// It also has to surface OPPORTUNITIES, not only problems. An Advisor made purely of
    /// warnings turns the game into a maintenance exercise, which is badly mismatched to a
    /// fantasy about building an empire.
    /// </summary>
    public sealed class Advisor
    {
        private readonly Restaurant _restaurant;

        public Advisor(Restaurant restaurant)
        {
            if (restaurant == null) throw new ArgumentNullException(nameof(restaurant));
            _restaurant = restaurant;
        }

        /// <summary>A dish sitting profitable-but-ignored this long is worth mentioning.</summary>
        public int PuzzleDaysBeforeMentioning { get; set; } = 3;

        /// <summary>
        /// Reads the current state and says what it notices. Pass recent trading when there
        /// is any — several observations only exist once the restaurant has sold something.
        /// </summary>
        public IReadOnlyList<Suggestion> Review(ServiceResult trading = null)
        {
            var found = new List<Suggestion>();

            AddChores(found);
            AddProposals(found, trading);
            AddOpportunities(found, trading);

            return Ordered(found);
        }

        /// <summary>
        /// ADVICE HAS TO SAY WHAT MATTERS FIRST, or it is only a list.
        ///
        /// An Advisor-guided campaign went bust on all four sites doing exactly what it was
        /// told. Every suggestion was individually correct and the sequence was ruinous: it
        /// staffed the kitchen it had, bought more kitchen because the queue was long, and
        /// paid eleven units and five staff to serve twelve covers. Nothing in a flat list
        /// says "the room is why you are poor, deal with that first".
        ///
        /// So the order carries meaning. Restocking comes first because it costs nothing and
        /// unblocks revenue you have already paid for. Seats come before kitchen, because a
        /// guest who cannot sit down never reaches the queue. And anything that spends money
        /// is dropped entirely when there is not two months of rent in the bank — a
        /// restaurant with three weeks of runway should not be told to buy an oven.
        /// </summary>
        private IReadOnlyList<Suggestion> Ordered(List<Suggestion> found)
        {
            var rent = _restaurant.Location == null ? 0m : _restaurant.Location.MonthlyRent;
            var broke = rent > 0m && _restaurant.Company.Economy.CashOnHand < rent * 2m;

            var kept = new List<Suggestion>();
            for (var i = 0; i < found.Count; i++)
            {
                if (broke && Spends(found[i].Id)) continue;
                kept.Add(found[i]);
            }

            kept.Sort((a, b) => Urgency(a.Id).CompareTo(Urgency(b.Id)));
            return kept;
        }

        private static bool Spends(string id)
        {
            return id == "opportunity:room" || id == "opportunity:capacity"
                || id == "opportunity:space" || id == "opportunity:upgrade"
                || id.StartsWith("understaffed:");
        }

        private static int Urgency(string id)
        {
            if (id.StartsWith("restock:")) return 0;          // free, and it unblocks sales
            if (id == "risk:runway") return 1;                 // know before you spend
            if (id == "opportunity:room") return 2;            // nobody queues who cannot sit
            if (id == "understaffed:floor") return 3;          // seats you cannot serve
            if (id == "opportunity:capacity") return 4;        // then the kitchen behind them
            if (id == "understaffed:kitchen") return 5;
            if (id.StartsWith("feature:")) return 6;
            return 7;
        }

        // ---- Tier 1: chores. Flatly stated, never a question. ----

        private void AddChores(List<Suggestion> found)
        {
            foreach (var stock in _restaurant.Inventory.BelowPar)
            {
                found.Add(new Suggestion(
                    "restock:" + stock.IngredientId, AdvisorTier.Chore,
                    "We're low on " + stock.IngredientId + ".",
                    "Down to " + stock.Quantity.ToString("0.#") + ", par is " + stock.ParMin.ToString("0.#") +
                    "-" + stock.ParMax.ToString("0.#") + ".",
                    subjectId: stock.IngredientId));
            }

            var units = _restaurant.Kitchen.Stations.Sum(s => s.ConcurrentCapacity);
            var manned = _restaurant.Payroll.CountOf(StaffRole.Cook) * KitchenPass.PlatesPerCook;

            if (_restaurant.Payroll.Headcount > 0 && manned < units)
            {
                found.Add(new Suggestion(
                    "understaffed:kitchen", AdvisorTier.Chore,
                    "We've got more kitchen than hands.",
                    "You own " + units + " units of equipment; " + _restaurant.Payroll.CountOf(StaffRole.Cook) +
                    " cooks can work " + manned + " of them at once. The rest sits idle.",
                    subjectId: "cook"));
            }

            if (_restaurant.Payroll.Headcount > 0 && _restaurant.ServableSeats < _restaurant.SeatingCapacity)
            {
                found.Add(new Suggestion(
                    "understaffed:floor", AdvisorTier.Chore,
                    "We can't serve every table we own.",
                    "You have " + _restaurant.SeatingCapacity + " covers but the floor staff can only look after " +
                    _restaurant.ServableSeats + ".",
                    subjectId: "server"));
            }
        }

        // ---- Tier 2: proposals. A question, and only where "no" is defensible. ----

        private void AddProposals(List<Suggestion> found, ServiceResult trading)
        {
            if (trading == null || trading.TotalUnitsSold == 0) return;

            var analysis = MenuEngineering.Analyze(_restaurant,
                trading.UnitsSoldByRecipeId.ToDictionary(p => p.Key, p => p.Value));

            foreach (var item in analysis.OfClass(MenuClassification.Puzzle))
            {
                if (_restaurant.Menu.IsFeatured(item.RecipeId)) continue;

                // NOTE what this does and does not say. It reports what the kitchen can see —
                // this earns well and nobody orders it — and asks. It does NOT say "this is a
                // Puzzle, feature it", which would be the game performing the menu
                // engineering and handing over the answer.
                //
                // "No" is genuinely defensible here: featured slots are scarce, and the dish
                // you would displace might be earning more overall.
                var displaced = _restaurant.Menu.Featured.Count >= _restaurant.Menu.FeaturedSlots
                    ? " We'd have to drop " + _restaurant.Menu.Featured[0] + " to make room."
                    : "";

                found.Add(new Suggestion(
                    "feature:" + item.RecipeId, AdvisorTier.Proposal,
                    "The " + item.Name.ToLowerInvariant() + " earns the most of anything we sell and nobody orders it.",
                    "It clears " + item.ContributionMargin.ToString("N2") + " a plate against a menu average of " +
                    analysis.AverageContributionMargin.ToString("N2") + ", but it's only " +
                    item.PopularityShare.ToString("P0") + " of covers." + displaced,
                    question: "Want it featured?",
                    subjectId: item.RecipeId));
            }

            // Surplus stock is a concrete thing on the ground, which is exactly the kind of
            // observation a sous chef makes.
            foreach (var stock in _restaurant.Inventory.Items)
            {
                if (!stock.IsAbovePar) continue;

                var dishes = _restaurant.Menu.Recipes.Where(r => r.Uses(stock.IngredientId)).ToList();
                if (dishes.Count == 0) continue;

                var pick = dishes[0];
                if (_restaurant.Menu.IsFeatured(pick.Id)) continue;

                found.Add(new Suggestion(
                    "surplus:" + stock.IngredientId, AdvisorTier.Proposal,
                    "We're sitting on a lot of " + stock.IngredientId + ".",
                    stock.Quantity.ToString("0.#") + " against a par of " + stock.ParMax.ToString("0.#") +
                    ", and it won't keep forever.",
                    question: "Feature the " + pick.Name.ToLowerInvariant() + " tonight?",
                    subjectId: pick.Id));
            }
        }

        // ---- Tier 3: opportunities and observations. Never a yes/no. ----

        private void AddOpportunities(List<Suggestion> found, ServiceResult trading)
        {
            var books = _restaurant.Company.Economy.SummarizeAll(_restaurant.Id);
            var cash = _restaurant.Company.Economy.CashOnHand;

            if (books.Revenue > 0m && books.Band == PrimeCostBand.Unsustainable)
            {
                found.Add(new Suggestion(
                    "risk:primecost", AdvisorTier.Strategic,
                    "We're losing money on every cover.",
                    "Prime cost is " + books.PrimeCostRatio.ToString("P0") + " of revenue — food " +
                    books.FoodCostRatio.ToString("P0") + ", wages " + books.LaborCostRatio.ToString("P0") +
                    ". Anything over 70% is unsustainable."));
            }

            if (_restaurant.Location != null && cash > 0m)
            {
                var rent = _restaurant.Location.MonthlyRent;
                if (rent > 0m && cash < rent * 2m)
                {
                    found.Add(new Suggestion(
                        "risk:runway", AdvisorTier.Strategic,
                        "Cash is getting thin.",
                        cash.ToString("N0") + " on hand against " + rent.ToString("N0") +
                        " a month in rent — under two months of runway."));
                }
            }

            // The opposite of a warning, and the reason this tier exists at all: something
            // worth doing, named, with the player left to decide whether and how.
            if (trading != null && trading.PartiesPutOffByTheWait > trading.CoversServed / 4 && trading.CoversServed > 0)
            {
                var busiest = trading.BusiestStationId;
                found.Add(new Suggestion(
                    "opportunity:capacity", AdvisorTier.Strategic,
                    "We're turning away more trade than we're keeping.",
                    trading.PartiesPutOffByTheWait + " parties looked at the wait and left, against " +
                    trading.CoversServed + " covers served" +
                    (busiest == null ? "." : ". The " + busiest + " is where the queue builds."),
                    subjectId: busiest));
            }

            // THE ROOM, which the Advisor was silent about entirely until an Advisor-guided
            // campaign went bust on all four sites with twelve covers after a year. It kept
            // buying kitchen — five to eleven units — because the kitchen is what it could
            // see, and never once said the dining room was the thing stopping the money.
            // `PartiesTurnedAway` was counted by the simulation from the start and read by
            // nothing, which is the same judged-but-never-consulted shape as PriceSensitivity
            // and IngredientQuality before it. Third time.
            if (trading != null && trading.PartiesTurnedAway > trading.CoversServed / 3
                && trading.PartiesTurnedAway > 5)
            {
                var room = _restaurant.FreeFloorArea;
                var couldSeat = (int)(room / 15m);

                found.Add(new Suggestion(
                    "opportunity:room", AdvisorTier.Strategic,
                    "We are turning people away at the door.",
                    trading.PartiesTurnedAway + " parties left because every table was full, against " +
                    trading.CoversServed + " covers served. " +
                    (couldSeat >= 4
                        ? "There is floor for about " + couldSeat + " more covers without building anything."
                        : "There is no floor left to put a table on."),
                    subjectId: couldSeat >= 4 ? "seats" : null));
            }

            if (_restaurant.FloorArea > 0m && _restaurant.FreeFloorArea < 32m && _restaurant.ExpansionHeadroom > 110m)
            {
                var perSqFt = _restaurant.Location.ExtensionCostPerSquareFoot;
                found.Add(new Suggestion(
                    "opportunity:space", AdvisorTier.Strategic,
                    "The building is full.",
                    "Nothing more fits, but " + _restaurant.Location.Name + " would allow another " +
                    _restaurant.ExpansionHeadroom.ToString("0") + " sq ft at " + perSqFt.ToString("N0") + " a foot.",
                    price: perSqFt));
            }

            if (_restaurant.FloorArea > 0m && _restaurant.FreeFloorArea < 32m && _restaurant.ExpansionHeadroom <= 0m)
            {
                found.Add(new Suggestion(
                    "opportunity:upgrade", AdvisorTier.Strategic,
                    "We've run out of building.",
                    "The site is built out to its limit, so the only way left to add throughput is " +
                    "better equipment in the same space."));
            }
        }
    }
}
