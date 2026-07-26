using System;

namespace RestaurantEmpire.Core.Definitions
{
    /// <summary>
    /// A raw input the kitchen buys and cooks with. Loaded from data/ingredients.json.
    ///
    /// Carries no price. Price is a property of whichever Supplier is currently assigned
    /// to this ingredient, never of the ingredient itself (Architecture Rule 1).
    /// </summary>
    public sealed class IngredientDefinition
    {
        public string Id { get; }
        public string Name { get; }

        /// <summary>Unit of measure prices and recipe quantities are expressed in (kg, g, bunch...).</summary>
        public string Unit { get; }

        public IngredientDefinition(string id, string name, string unit)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Ingredient id is required.", nameof(id));

            Id = id;
            Name = name ?? id;
            Unit = unit ?? "unit";
        }

        public override string ToString()
        {
            return Name + " (" + Id + ")";
        }
    }
}
