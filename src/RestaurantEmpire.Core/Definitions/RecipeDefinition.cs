using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Definitions
{
    /// <summary>One ingredient line on a recipe: which ingredient, and how much of it.</summary>
    public sealed class RecipeIngredient
    {
        /// <summary>Reference by stable string ID — never a cached cost (Architecture Rule 1).</summary>
        public string IngredientId { get; }

        /// <summary>Amount used per plate, in the ingredient's own unit.</summary>
        public decimal Quantity { get; }

        public RecipeIngredient(string ingredientId, decimal quantity)
        {
            if (string.IsNullOrWhiteSpace(ingredientId))
                throw new ArgumentException("Recipe ingredient id is required.", nameof(ingredientId));
            if (quantity <= 0m)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Recipe ingredient quantity must be positive.");

            IngredientId = ingredientId;
            Quantity = quantity;
        }

        public override string ToString()
        {
            return Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture) + " x " + IngredientId;
        }
    }

    /// <summary>
    /// A sellable dish. Loaded from a file in data/recipes/.
    ///
    /// DELIBERATELY HAS NO COST OR MARGIN PROPERTY. This is the architectural heart of
    /// the project: Restaurant Empire II stored cost on the recipe, so a supplier change
    /// left every recipe stale until the player hand-edited each one. Here there is
    /// nowhere to put a stale number — cost is computed on demand by
    /// <see cref="Model.MenuCosting"/> from whichever supplier is assigned right now.
    ///
    /// If you are ever tempted to add a `PlateCost` property here, that is the bug the
    /// whole design exists to prevent.
    /// </summary>
    public sealed class RecipeDefinition
    {
        /// <summary>Station a dish defaults to when its data file doesn't name one.</summary>
        public const string DefaultStationId = "line";

        /// <summary>Prep time a dish defaults to when its data file doesn't give one.</summary>
        public const int DefaultPrepMinutes = 5;

        public string Id { get; }
        public string Name { get; }

        /// <summary>What the guest pays. The one money figure that genuinely belongs to the dish.</summary>
        public decimal MenuPrice { get; }

        /// <summary>
        /// Which brigade station cooks this (oven, saute, garde-manger...). A string rather
        /// than an enum so a content pack can add a station type without a code change.
        /// </summary>
        public string StationId { get; }

        /// <summary>
        /// Hands-on minutes at that station for one plate, before any equipment or (from M1)
        /// staff-skill multiplier. This is what makes two dishes sharing one station
        /// contend for it under load.
        /// </summary>
        public int PrepMinutes { get; }

        public IReadOnlyList<RecipeIngredient> Ingredients { get; }

        /// <summary>
        /// When guests actually want this. Empty means "any time" — a coffee or a bread
        /// basket sells all day.
        ///
        /// This is what makes opening a service a real decision rather than free money. You
        /// may absolutely offer truffle risotto at 8am; nobody will order it, so you will
        /// have paid a morning's labour to serve an empty room. Wanting the breakfast trade
        /// means having breakfast dishes — which in turn means the equipment they need.
        /// </summary>
        public IReadOnlyList<Model.Daypart> Dayparts { get; }

        public RecipeDefinition(
            string id, string name, decimal menuPrice, IList<RecipeIngredient> ingredients,
            string stationId = DefaultStationId, int prepMinutes = DefaultPrepMinutes,
            IList<Model.Daypart> dayparts = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Recipe id is required.", nameof(id));
            if (menuPrice < 0m) throw new ArgumentOutOfRangeException(nameof(menuPrice), "Menu price cannot be negative.");
            if (prepMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(prepMinutes), "Prep time must be positive.");

            Id = id;
            Name = name ?? id;
            MenuPrice = menuPrice;
            StationId = string.IsNullOrWhiteSpace(stationId) ? DefaultStationId : stationId;
            PrepMinutes = prepMinutes;
            Ingredients = new List<RecipeIngredient>(ingredients ?? new List<RecipeIngredient>()).AsReadOnly();
            Dayparts = new List<Model.Daypart>(dayparts ?? new List<Model.Daypart>()).AsReadOnly();
        }

        /// <summary>Whether a guest would want this at the given time of day.</summary>
        public bool SuitsDaypart(Model.Daypart daypart)
        {
            if (Dayparts.Count == 0) return true;   // untagged sells all day

            for (var i = 0; i < Dayparts.Count; i++)
            {
                if (Dayparts[i] == daypart) return true;
            }

            return false;
        }

        public bool Uses(string ingredientId)
        {
            for (var i = 0; i < Ingredients.Count; i++)
            {
                if (Ingredients[i].IngredientId == ingredientId) return true;
            }

            return false;
        }

        public override string ToString()
        {
            return Name + " (" + Id + ")";
        }
    }
}
