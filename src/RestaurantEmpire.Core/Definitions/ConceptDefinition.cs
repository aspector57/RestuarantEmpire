using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Definitions
{
    /// <summary>
    /// A restaurant concept: a card, a price position, and the hours it trades.
    ///
    /// THESE WERE FIXTURES IN A TEST FILE. `StrategyDiversity` hardcoded six of them in C# to
    /// answer "are there several ways to run a restaurant?", and they were doing real work —
    /// the whole distinct-winners measurement runs on them — while being invisible to the
    /// game and unmoddable. Architecture Rule 2 says content lives in data, and a concept is
    /// content by any reading of it.
    ///
    /// Making them first-class is also what Aaron's *"build your own concept or select one"*
    /// needs: the select-one half is this list, and the build-your-own half is the player
    /// writing the same fields by hand.
    ///
    /// NOTE WHAT IS NOT HERE: no staffing, no equipment, no floor plan. A concept says what
    /// you are TRYING TO DO, not how well you execute it — "you should be able to win with any
    /// concept anywhere if you run the restaurant properly" only means anything if running it
    /// properly is still the player's job. Bundling a build in would make picking a concept
    /// pick the whole restaurant, which is Binding Principle 5's "must not solve strategy for
    /// them".
    /// </summary>
    public sealed class ConceptDefinition
    {
        public ConceptDefinition(string id, string name, string description,
            IEnumerable<string> recipeIds, decimal pricePosition, IEnumerable<ConceptService> services)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Concept id is required.", nameof(id));

            Id = id;
            Name = name ?? id;
            Description = description ?? string.Empty;
            PricePosition = pricePosition <= 0m ? 1m : pricePosition;

            var recipes = new List<string>();
            if (recipeIds != null) recipes.AddRange(recipeIds);
            RecipeIds = recipes;

            var hours = new List<ConceptService>();
            if (services != null) hours.AddRange(services);
            Services = hours;
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>One line the player reads when choosing. Plain language, never jargon.</summary>
        public string Description { get; }

        /// <summary>The card, by recipe id. Ids, never cached prices — Architecture Rule 1.</summary>
        public IReadOnlyList<string> RecipeIds { get; }

        /// <summary>Where the card sits against what the dishes are designed to sell for.</summary>
        public decimal PricePosition { get; }

        /// <summary>The hours this concept trades. A coffee counter is not a dinner house.</summary>
        public IReadOnlyList<ConceptService> Services { get; }
    }

    /// <summary>One service window in a concept — a name and the hours it runs.</summary>
    public sealed class ConceptService
    {
        public ConceptService(string name, int from, int to)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Service" : name;
            From = from;
            To = to;
        }

        public string Name { get; }
        public int From { get; }
        public int To { get; }
    }
}
