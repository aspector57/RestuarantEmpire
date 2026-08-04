using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// Who walked in. Legible types with concrete wants, rather than one abstract
    /// preference vector — the design's Phase 4 position, on the grounds that a player can
    /// design around "the business lunch crowd wants to be out in forty minutes" and cannot
    /// design around a number.
    /// </summary>
    public enum CustomerArchetype
    {
        /// <summary>Out at lunch on the company card. In a hurry, barely looks at the price.</summary>
        BusinessLuncher = 0,

        /// <summary>Here for the evening. Patient, unhurried, wants the room to be nice.</summary>
        RomanticCouple = 1,

        /// <summary>Feeding people. Price-sensitive, wants things to share.</summary>
        Family = 2,

        /// <summary>Photographs it first. Impatient, chases whatever is refined or luxurious.</summary>
        Influencer = 3,

        /// <summary>Someone who just lives nearby. No strong opinions — the baseline.</summary>
        Local = 4
    }

    /// <summary>
    /// What an archetype actually wants, expressed as things the simulation already has:
    /// how long they will wait, how hard they judge price, and which dish tags pull them.
    ///
    /// This is what fixes the popularity axis. Before archetypes, guests picked dishes
    /// uniformly at random, so every dish landed near an equal share and a Puzzle — high
    /// margin, LOW volume — could not arise at all. Half the Kasavana-Smith matrix was
    /// measuring the RNG. Popularity now comes from who is in the room and what they like.
    /// </summary>
    public sealed class ArchetypeProfile
    {
        private readonly Dictionary<string, int> _pull;

        private ArchetypeProfile(CustomerArchetype archetype, string description,
            int patienceLow, int patienceHigh, decimal priceSensitivity, Dictionary<string, int> pull)
        {
            Archetype = archetype;
            Description = description;
            PatienceLow = patienceLow;
            PatienceHigh = patienceHigh;
            PriceSensitivity = priceSensitivity;
            _pull = pull;
        }

        public CustomerArchetype Archetype { get; }
        public string Description { get; }
        public int PatienceLow { get; }
        public int PatienceHigh { get; }

        /// <summary>1.0 is neutral; above 1.0 judges price harder.</summary>
        public decimal PriceSensitivity { get; }

        /// <summary>
        /// How much more likely this archetype is to order a dish carrying a given tag.
        /// Anything unlisted is neutral.
        /// </summary>
        public int PullToward(string tag)
        {
            int weight;
            return tag != null && _pull.TryGetValue(tag, out weight) ? weight : 0;
        }

        /// <summary>
        /// Whether somebody of this sort would consider eating here at all, given how dear the
        /// place looks. 0 to 1.
        ///
        /// Aaron, and he is right about how this works in life: *"people know the rough costs
        /// before going to a restaurant... typically they don't go somewhere and leave unless
        /// they are in a city and look at the menu on the door."* Price mostly decides WHO
        /// TURNS UP. An expensive restaurant does not get crowds storming out; it gets fewer
        /// of the wrong people walking in.
        ///
        /// That matters for more than realism. Punishing high prices with a crowd that arrives
        /// and leaves is weak, because the ones who stay pay the higher price and make up the
        /// difference — which is exactly why over-charging stayed profitable. If they never
        /// come, there is nobody left to make it up.
        ///
        /// The price-sensitive drop away first, so a dear menu quietly fills the room with
        /// couples and enthusiasts instead of families. **Still one price on the menu** — this
        /// decides who reads it, not what they are charged.
        /// </summary>
        /// <param name="standing">
        /// What the place is known for, 0 to 1. **The only quality signal available before you
        /// go** — you cannot see the ingredients from home, but you have heard whether it is
        /// any good, and a well-regarded restaurant is forgiven a dearer menu. This is what
        /// makes building a reputation the thing that BUYS the right to charge.
        /// </param>
        /// <summary>
        /// Would this sort of person eat here at these prices? <paramref name="valueOnOffer"/>
        /// is what the restaurant is actually putting in front of them — see
        /// <see cref="SatisfactionModel.ValueOnOffer"/>. Defaults to the neutral 0.5 so callers
        /// that do not know yet behave exactly as before.
        /// </summary>
        public decimal WouldConsider(decimal pricePosition, decimal valueOnOffer = 0.5m)
        {
            if (pricePosition <= 1m) return 1m;   // priced as designed or under: everybody is in

            // WHAT YOU CAN CHARGE IS WHAT YOU ARE ACTUALLY OFFERING, not just your name.
            //
            // Aaron: *"people should be willing to pay more for premium ingredients especially
            // if made by a great chef, but also happy with lower costs for worse food as long
            // as it isn't horrible."*
            //
            // This used to hang on reputation alone, which made sourcing a two-year bet — good
            // ingredients raised standing, standing slowly raised the price you could ask, and
            // a great chef got no credit for tonight's dinner. Measured across horizons, budget
            // sourcing beat mid-tier until about two years and premium never won at all.
            //
            // The 0.70 floor is the other half of his sentence: a plainly-sourced, competently
            // run room still carries a normal price without anyone objecting. Everything above
            // that has to be earned.
            var allowance = 0.70m + (Clamp(valueOnOffer) * 0.95m);

            var over = (pricePosition - 1m) / allowance;
            var chance = 1m - (over * PriceSensitivity * 0.85m);

            return chance < 0.03m ? 0.03m : chance;
        }

        private static decimal Clamp(decimal v) { return v < 0m ? 0m : v > 1m ? 1m : v; }

        public static ArchetypeProfile For(CustomerArchetype archetype)
        {
            switch (archetype)
            {
                case CustomerArchetype.BusinessLuncher:
                    return new ArchetypeProfile(archetype,
                        "wants to be out in forty minutes and is not paying",
                        14, 24, 0.7m,
                        new Dictionary<string, int> { { "quick", 3 }, { "light", 2 }, { "refined", 1 }, { "sharing", -1 } });

                case CustomerArchetype.RomanticCouple:
                    return new ArchetypeProfile(archetype,
                        "here for the evening, in no rush at all",
                        34, 52, 0.95m,
                        new Dictionary<string, int> { { "refined", 3 }, { "luxury", 2 }, { "rich", 2 }, { "quick", -2 } });

                case CustomerArchetype.Family:
                    return new ArchetypeProfile(archetype,
                        "feeding several people and watching the bill",
                        22, 36, 1.35m,
                        new Dictionary<string, int> { { "sharing", 3 }, { "classic", 2 }, { "pizza", 2 }, { "luxury", -3 } });

                case CustomerArchetype.Influencer:
                    return new ArchetypeProfile(archetype,
                        "photographs it before eating it, and tells everyone",
                        16, 26, 0.85m,
                        new Dictionary<string, int> { { "luxury", 4 }, { "refined", 3 }, { "seafood", 2 }, { "classic", -1 } });

                default:
                    return new ArchetypeProfile(CustomerArchetype.Local,
                        "lives round the corner, no strong opinions",
                        24, 38, 1.05m,
                        new Dictionary<string, int>());
            }
        }

        /// <summary>
        /// Who is likely to be out at this hour, in this sort of place. A business district
        /// at one o'clock is not a nightlife quarter at midnight, and neither is a suburban
        /// high street at seven.
        /// </summary>
        public static CustomerArchetype[] LikelyAt(Daypart daypart, string neighborhoodId)
        {
            var business = neighborhoodId == "business-district";
            var nightlife = neighborhoodId == "nightlife-quarter";
            var city = neighborhoodId == "city-center";

            switch (daypart)
            {
                case Daypart.Breakfast:
                    return business
                        ? new[] { CustomerArchetype.BusinessLuncher, CustomerArchetype.BusinessLuncher, CustomerArchetype.Local }
                        : new[] { CustomerArchetype.Local, CustomerArchetype.Local, CustomerArchetype.Family };

                case Daypart.Lunch:
                    if (business) return new[] { CustomerArchetype.BusinessLuncher, CustomerArchetype.BusinessLuncher, CustomerArchetype.BusinessLuncher, CustomerArchetype.Local };
                    if (city) return new[] { CustomerArchetype.BusinessLuncher, CustomerArchetype.BusinessLuncher, CustomerArchetype.Local, CustomerArchetype.Family };
                    return new[] { CustomerArchetype.Local, CustomerArchetype.Family, CustomerArchetype.Local };

                case Daypart.Dinner:
                    if (nightlife) return new[] { CustomerArchetype.Influencer, CustomerArchetype.RomanticCouple, CustomerArchetype.Local };
                    if (city) return new[] { CustomerArchetype.RomanticCouple, CustomerArchetype.Influencer, CustomerArchetype.Local, CustomerArchetype.Family };
                    return new[] { CustomerArchetype.Family, CustomerArchetype.RomanticCouple, CustomerArchetype.Local, CustomerArchetype.Local };

                default:   // late night
                    return nightlife
                        ? new[] { CustomerArchetype.Influencer, CustomerArchetype.Influencer, CustomerArchetype.Local }
                        : new[] { CustomerArchetype.Local, CustomerArchetype.Influencer };
            }
        }

        /// <summary>Tags a guest might personally love, on top of whatever their type wants.</summary>
        public static readonly string[] TastesWorthHaving =
        {
            "seafood", "vegetarian", "pizza", "luxury", "light", "rich", "classic"
        };
    }
}
