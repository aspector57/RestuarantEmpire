using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// How a dish would land with a guest right now, out of five stars, BROKEN INTO ITS CAUSES.
    ///
    /// The breakdown is the whole point and is not decoration. Binding Principle 2 says every
    /// outcome must trace to a specific named cause and never to an opaque score, so a bare
    /// "2.4 stars" would be precisely the wrong shape: the player could not tell whether the
    /// risotto is dear for what it is, slow out of the kitchen, or made with budget cheese —
    /// and those are three completely different fixes costing three different amounts.
    ///
    /// So the star total is a DISPLAY of the four components. It is never what drives
    /// behavior; the components drive behavior on their own, exactly as they always did.
    /// Nothing here is stored — this is a lens over current state, computed live like
    /// everything else (Architecture Rule 1), so changing supplier or price moves it at once.
    /// </summary>
    public sealed class DishRating
    {
        internal DishRating(string recipeId, string name, decimal ingredients, decimal speed,
            decimal value, decimal room, int prepMinutes, decimal menuPrice)
        {
            RecipeId = recipeId;
            Name = name;
            Ingredients = ingredients;
            Speed = speed;
            Value = value;
            Room = room;
            PrepMinutes = prepMinutes;
            MenuPrice = menuPrice;
        }

        public string RecipeId { get; }
        public string Name { get; }
        public decimal MenuPrice { get; }
        public int PrepMinutes { get; }

        /// <summary>What it is made of — the assigned supplier's tier. 0 to 1.</summary>
        public decimal Ingredients { get; }

        /// <summary>How fast it reaches the table under the kitchen as it stands. 0 to 1.</summary>
        public decimal Speed { get; }

        /// <summary>Whether the price feels fair for what arrives. 0 to 1.</summary>
        public decimal Value { get; }

        /// <summary>The room it is eaten in. 0 to 1, and deliberately the smallest weight.</summary>
        public decimal Room { get; }

        /// <summary>The same weighting a guest actually applies to a finished meal.</summary>
        public decimal Overall
        {
            get
            {
                return (Ingredients * SatisfactionModel.FoodQualityWeight)
                     + (Speed * SatisfactionModel.ServiceSpeedWeight)
                     + (Value * SatisfactionModel.ValueWeight)
                     + (Room * SatisfactionModel.AmbianceWeight);
            }
        }

        public decimal Stars { get { return Overall * 5m; } }

        /// <summary>
        /// Which component is dragging hardest, weighted — so the answer is the one worth
        /// spending money on, not merely the lowest number. A poor room scores badly but is
        /// rarely the reason a dish is failing, because it carries an eighth of the weight
        /// that ingredients do.
        /// </summary>
        public string Weakest
        {
            get
            {
                var worst = "ingredients";
                var loss = (1m - Ingredients) * SatisfactionModel.FoodQualityWeight;

                var speedLoss = (1m - Speed) * SatisfactionModel.ServiceSpeedWeight;
                if (speedLoss > loss) { worst = "speed"; loss = speedLoss; }

                var valueLoss = (1m - Value) * SatisfactionModel.ValueWeight;
                if (valueLoss > loss) { worst = "value"; loss = valueLoss; }

                var roomLoss = (1m - Room) * SatisfactionModel.AmbianceWeight;
                if (roomLoss > loss) { worst = "room"; }

                return worst;
            }
        }

        /// <summary>Plain language, naming the cause rather than the score.</summary>
        public string Verdict
        {
            get
            {
                // Value below the walk-away threshold outranks a healthy-looking total,
                // because it is the one component that makes people leave WITHOUT ordering.
                // A $60 margherita on premium stock still scores four stars — food quality
                // carries 0.42 and value only 0.17 — so the aggregate would cheerfully
                // report "people are happy with this" about a dish nobody will buy.
                if (Value < SatisfactionModel.WalkAwayValueThreshold)
                    return "costs more than it looks worth";

                // Compared on the ROUNDED figure, because that is the one the player is
                // looking at. Judging on 3.96 while displaying "4.0" prints a complaint
                // directly underneath a score that looks fine, which reads as a bug.
                if (System.Math.Round(Stars, 1) >= 4m) return "people are happy with this";

                switch (Weakest)
                {
                    case "speed":
                        return "takes too long to reach the table";
                    case "value":
                        return "costs more than it looks worth";
                    case "room":
                        return "the room lets it down";
                    default:
                        return Value < 0.5m
                            ? "budget ingredients at a price that implies better"
                            : "made with cheaper ingredients than it could be";
                }
            }
        }

        public override string ToString()
        {
            return Name + " " + Stars.ToString("0.0") + "/5 (" + Verdict + ")";
        }
    }

    /// <summary>Builds <see cref="DishRating"/>s from a restaurant's current state.</summary>
    public static class DishRatings
    {
        /// <summary>
        /// The patience a rating is judged against, in minutes. Roughly the middle of the
        /// archetype range, so a rating reflects a typical guest rather than the fussiest or
        /// the most forgiving one.
        /// </summary>
        public const int NominalPatienceMinutes = 30;

        /// <summary>Neutral price sensitivity, so the rating is not skewed to one crowd.</summary>
        public const decimal NominalPriceSensitivity = 1m;

        public static IList<DishRating> For(Restaurant restaurant, long atTick = 0)
        {
            var ratings = new List<DishRating>();
            if (restaurant == null) return ratings;

            var costing = restaurant.Costing;
            var comfort = restaurant.DiningRoom.Comfort;

            // An unloaded pass, so the speed component answers "how fast can this kitchen
            // send this dish" rather than "how backed up is it this minute". That makes the
            // rating a property of what you have BUILT — buying an oven or hiring a cook
            // moves it — instead of a reading that swings around mid-service.
            var pass = restaurant.Kitchen.OpenPass(atTick, restaurant.Payroll.PlateCapacity(KitchenPass.PlatesPerCook));

            foreach (var recipe in restaurant.Menu.Recipes)
            {
                var wait = pass.EstimatedWaitMinutes(recipe, atTick);

                ratings.Add(new DishRating(
                    recipe.Id,
                    recipe.Name,
                    SatisfactionModel.PlateQuality(
                        costing.IngredientQuality(recipe.Id),
                        restaurant.Payroll.AverageSkill(StaffRole.Cook),
                        costing.Freshness(recipe.Id, restaurant.Inventory)),
                    SatisfactionModel.ScoreSpeed(wait, NominalPatienceMinutes),
                    SatisfactionModel.ScoreValue(costing.Markup(recipe.Id), NominalPriceSensitivity,
                        costing.IngredientQuality(recipe.Id)),
                    comfort,
                    recipe.PrepMinutes,
                    costing.MenuPrice(recipe.Id)));
            }

            return ratings;
        }
    }
}
