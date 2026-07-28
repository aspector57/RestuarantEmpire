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
