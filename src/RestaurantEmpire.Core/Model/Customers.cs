using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// A group of guests arriving together.
    ///
    /// Deliberately thin for M0/M1. Cuisine preferences, occasion, and the named archetypes
    /// (Influencer, Romantic Couple, Business Luncher) are M2 — building them now would be
    /// building ahead, and the satisfaction formula needs none of them to be meaningful.
    ///
    /// Parties are produced by <see cref="SimulationRunner"/> as the clock passes through a
    /// <see cref="ServiceWindow"/>; the window carries the demand rate, so there is no
    /// separate arrival model to keep in step with the running simulation.
    /// </summary>
    public sealed class CustomerParty
    {
        public CustomerParty(string id, int size, long arrivalTick, int patienceMinutes, decimal priceSensitivity,
            CustomerArchetype archetype = CustomerArchetype.Local, IList<string> tastes = null)
        {
            if (size < 1) throw new ArgumentOutOfRangeException(nameof(size), "A party has at least one guest.");
            if (patienceMinutes < 0) throw new ArgumentOutOfRangeException(nameof(patienceMinutes));

            Id = id;
            Size = size;
            ArrivalTick = arrivalTick;
            PatienceMinutes = patienceMinutes;
            PriceSensitivity = priceSensitivity;
            Archetype = archetype;
            Tastes = new List<string>(tastes ?? new List<string>()).AsReadOnly();
        }

        public string Id { get; }

        /// <summary>Covers in this party. Each orders one dish.</summary>
        public int Size { get; }

        public long ArrivalTick { get; }

        /// <summary>Minutes they will wait for food before giving up and walking out.</summary>
        public int PatienceMinutes { get; }

        /// <summary>1.0 is neutral. Above 1.0 means they judge price harder.</summary>
        public decimal PriceSensitivity { get; }

        /// <summary>What kind of guest this is, which decides most of what they want.</summary>
        public CustomerArchetype Archetype { get; }

        /// <summary>Tags this particular guest personally loves — someone who adores seafood.</summary>
        public IReadOnlyList<string> Tastes { get; }

        /// <summary>
        /// How much more likely this party is to order a given dish than a neutral one.
        ///
        /// TWO THINGS DECIDE THIS, and the first one is the important one.
        ///
        /// PRICE. A guest looks at what a dish costs before ordering it, judged against the
        /// rest of the menu and against how much they care. That is the actual real-world
        /// mechanism behind a high-margin, low-volume dish — the definition of a Puzzle on
        /// the Kasavana-Smith matrix — and its absence is why no Puzzle could form.
        /// `PriceSensitivity` existed on this class from the start but was only ever
        /// consulted on the way OUT, in the satisfaction score, to decide whether the meal
        /// felt like value after it had been eaten. It was judging, never choosing. Phase 4's
        /// Customers contract lists "budget/price sensitivity" among what a customer knows;
        /// this is that contract finally being honoured on the way in.
        ///
        /// TASTE. Then their type and their own preferences pull them around — a family
        /// toward something to share, someone who loves seafood toward the fish. This adds
        /// variety on top of the price signal rather than substituting for it.
        /// </summary>
        /// <param name="relativePrice">This dish's price over the menu's average.</param>
        /// <param name="ingredientQuality">0.2 (budget) to 1.0 (premium), from the assigned supplier.</param>
        public decimal AppetiteFor(Definitions.RecipeDefinition recipe, decimal relativePrice = 1m,
            decimal ingredientQuality = 0m)
        {
            if (recipe == null) return 1m;

            var profile = ArchetypeProfile.For(Archetype);
            var taste = 2m;   // everyone will eat most things

            for (var i = 0; i < recipe.Tags.Count; i++)
            {
                taste += profile.PullToward(recipe.Tags[i]);

                for (var j = 0; j < Tastes.Count; j++)
                    if (Tastes[j] == recipe.Tags[i]) taste += 3m;
            }

            if (taste < 1m) taste = 1m;   // never quite zero; people surprise you

            return taste * PriceAppeal(relativePrice) * QualityAppeal(ingredientQuality, relativePrice);
        }

        /// <summary>
        /// How much a price puts this guest off, relative to the rest of the menu.
        ///
        /// At the menu average this is 1. A dish at twice the average roughly halves a
        /// neutral guest's appetite for it, and does considerably worse with a family
        /// watching the bill than with someone on the company card.
        /// </summary>
        public decimal PriceAppeal(decimal relativePrice)
        {
            if (relativePrice <= 0m) return 1m;

            // Deliberately gentle and linear rather than a curve: dear dishes sell less,
            // they do not become unorderable. A floor keeps the priciest dish on the menu
            // rather than making it decoration.
            var appeal = 1m - ((relativePrice - 1m) * PriceSensitivity * 0.55m);

            if (appeal < 0.12m) return 0.12m;
            return appeal > 1.6m ? 1.6m : appeal;
        }

        /// <summary>
        /// Whether the cooking justifies the asking price.
        ///
        /// Budget ingredients are not a problem in themselves — a cheap dish made of cheap
        /// things is honest, and plenty of people want it. What guests object to is the
        /// MISMATCH: premium prices on budget cooking. So this compares what the ingredients
        /// actually are against what the price implies they should be.
        ///
        /// Before this, `MenuCosting.IngredientQuality` fed the satisfaction score and
        /// nothing else, so a night on budget stock scored 0.563 against 0.731 — and served
        /// exactly the same number of covers, with exactly the same walkouts. Quality was a
        /// number the game wrote down and never acted on, which made the cheapest supplier
        /// strictly dominant and free. That is the same "judging, never choosing" gap that
        /// `PriceSensitivity` had, in the one system this whole project is built around.
        /// </summary>
        public decimal QualityAppeal(decimal ingredientQuality, decimal relativePrice)
        {
            if (ingredientQuality <= 0m) return 1m;   // nothing sourced yet — no opinion

            // What the price implies. At the menu average a guest expects the house standard;
            // at twice the average they expect the best you have.
            var expected = relativePrice * 0.5m;
            if (expected < 0.2m) expected = 0.2m;
            if (expected > 1m) expected = 1m;

            var appeal = 1m + ((ingredientQuality - expected) * PriceSensitivity * 0.8m);

            if (appeal < 0.15m) return 0.15m;
            return appeal > 1.5m ? 1.5m : appeal;
        }

        public override string ToString()
        {
            return "party of " + Size + " at tick " + ArrivalTick;
        }
    }
}
