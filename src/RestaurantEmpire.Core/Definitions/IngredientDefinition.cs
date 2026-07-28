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

        /// <summary>
        /// Days before this goes off, from delivery. **Zero means it keeps** — flour, oil,
        /// rice and coffee do not rot on any timescale this game cares about.
        ///
        /// Aaron: *"maybe spoilage only happens on meats and produce?"* Yes, and it is what
        /// makes the mechanic survivable. Spoiling everything meant a restaurant binned 94%
        /// of what it bought and no site could be made to pay. Spoiling only what actually
        /// perishes keeps the lesson — order thoughtfully — without taxing the store cupboard.
        ///
        /// Lives are set with GRACE, also his call: *"give some grace so you don't need to be
        /// ordering every single day, but you should still be thoughtful."* Real sea bass is
        /// two days; here it is four, so a weekly-ish rhythm works and only genuine
        /// over-ordering is punished.
        /// </summary>
        public int ShelfLifeDays { get; }

        /// <summary>Whether this rots at all.</summary>
        public bool Perishable { get { return ShelfLifeDays > 0; } }

        public IngredientDefinition(string id, string name, string unit, int shelfLifeDays = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Ingredient id is required.", nameof(id));

            Id = id;
            Name = name ?? id;
            Unit = unit ?? "unit";
            ShelfLifeDays = shelfLifeDays < 0 ? 0 : shelfLifeDays;
        }

        public override string ToString()
        {
            return Name + " (" + Id + ")";
        }
    }
}
