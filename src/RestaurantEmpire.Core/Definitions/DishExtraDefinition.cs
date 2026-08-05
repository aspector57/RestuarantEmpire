using System.Collections.Generic;

namespace RestaurantEmpire.Core.Definitions
{
    /// <summary>
    /// SOMETHING YOU CHOOSE TO PUT ON A DISH — Aaron's idea, and the ceiling is the mechanic.
    ///
    /// *"What if we can select additional ingredients to put into dishes, raising your cost, it
    /// should raise what you can charge but not infinitely?"* Without the "not infinitely" this
    /// is a slider that always says yes.
    ///
    /// An extra costs real ingredients and raises the dish's DESIGNED price — so a plate with
    /// buffalo mozzarella and prosciutto on it is judged against a higher bar, and charging
    /// more for it is not gouging. The first version raised only quality, which reaches money
    /// solely through the slow reputation chain, so profit fell at every step and the honest
    /// answer was always "add nothing".
    ///
    /// Each further extra counts for <see cref="Model.Tuning.ExtraDiminishing"/> of the one
    /// before it, and the total is capped by what the dish IS — a focaccia cannot be dressed
    /// into a main course. Measured on a margherita, the optimum is interior: bare $744/day,
    /// buffalo $827, plus prosciutto $920, plus basil $868.
    ///
    /// Data-driven per Architecture Rule 2 — new extras are a JSON edit, not a code change.
    /// </summary>
    public sealed class DishExtraDefinition
    {
        public string RecipeId { get; }
        public string Id { get; }
        public string Name { get; }

        /// <summary>How much this raises what the dish is worth, before diminishing and the cap.</summary>
        public decimal Lift { get; }

        public IReadOnlyList<RecipeIngredient> Ingredients { get; }

        public DishExtraDefinition(string recipeId, string id, string name, decimal lift,
                                   IEnumerable<RecipeIngredient> ingredients)
        {
            RecipeId = recipeId;
            Id = id;
            Name = name;
            Lift = lift < 0m ? 0m : lift;

            var list = new List<RecipeIngredient>();
            if (ingredients != null) list.AddRange(ingredients);
            Ingredients = list;
        }

        public override string ToString()
        {
            return Name + " (+" + System.Math.Round(Lift * 100m) + "% on " + RecipeId + ")";
        }
    }
}
