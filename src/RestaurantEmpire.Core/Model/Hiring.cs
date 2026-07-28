using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// Somebody who has applied for a job, and what you can tell about them before they start.
    ///
    /// Aaron's framing, and the reason this exists: *"in this model, it was hire a cook. In the
    /// real game, there will be profiles of cooks with their own rates, you can hire good cooks
    /// or bad cooks, they can do a good job or bad job, things can go wrong."*
    ///
    /// THE GAP BETWEEN <see cref="Advertises"/> AND <see cref="Employee.Skill"/> IS THE POINT.
    /// A CV is a claim, not a measurement. You are shown what someone says they are worth and
    /// charged accordingly; what they turn out to be is revealed by them working. That is what
    /// makes hiring a decision with risk in it rather than a button that adds a unit — and it
    /// is the design doc's "scouting uncertainty" in its cheapest honest form.
    /// </summary>
    public sealed class Candidate
    {
        internal Candidate(string id, string name, StaffRole role, decimal advertises, decimal actual, decimal wage, decimal potential)
        {
            Id = id;
            Name = name;
            Role = role;
            Advertises = advertises;
            Actual = actual;
            HourlyWage = wage;
            Potential = potential;
        }

        public string Id { get; }
        public string Name { get; }
        public StaffRole Role { get; }

        /// <summary>What the CV claims, 0 to 1. This is all the player sees.</summary>
        public decimal Advertises { get; }

        /// <summary>What they can really do. Not shown — it comes out on the pass.</summary>
        internal decimal Actual { get; }

        /// <summary>What they could become. Not shown either, and only found out by keeping them.</summary>
        internal decimal Potential { get; }

        /// <summary>Priced off what they CLAIM, so a confident chancer is expensive and bad.</summary>
        public decimal HourlyWage { get; }

        /// <summary>How they read on paper, in words rather than a number.</summary>
        public string Reads
        {
            get
            {
                if (Advertises >= 0.85m) return "a serious CV — worked somewhere good";
                if (Advertises >= 0.65m) return "solid, several years on a line";
                if (Advertises >= 0.45m) return "competent, nothing remarkable";
                if (Advertises >= 0.25m) return "green, but cheap";
                return "no real experience";
            }
        }

        /// <summary>Take them on. What you get is what they ARE, not what they claimed.</summary>
        public Employee Accept()
        {
            return new Employee(Id, Name, Role, HourlyWage, Actual, Potential);
        }

        public override string ToString()
        {
            return Name + " (" + Role + ", " + HourlyWage.ToString("N0") + "/hr, " + Reads + ")";
        }
    }

    /// <summary>
    /// Who is available this month. Deterministic from a seed, like everything else that has
    /// to be reproducible — the same restaurant on the same day always sees the same people.
    /// </summary>
    public static class HiringPool
    {
        /// <summary>Wage at the bottom of the market, before anything is claimed.</summary>
        public const decimal CookFloorWage = 12m;
        public const decimal ServerFloorWage = 9m;

        /// <summary>What a perfect CV adds to the floor wage.</summary>
        public const decimal CookSkillPremium = 16m;
        public const decimal ServerSkillPremium = 9m;

        /// <summary>
        /// How far the truth can sit from the claim, either way. Wide enough that a dear hire
        /// can disappoint and a cheap one can be a find, which is the whole risk.
        /// </summary>
        public const decimal ScoutingError = 0.22m;

        private static readonly string[] FirstNames =
        {
            "Marco", "Dani", "Priya", "Tomas", "Nell", "Iris", "Kwame", "Sofia", "Ren", "Aoife",
            "Yusuf", "Marta", "Elias", "Nadia", "Bruno", "Lena", "Hugo", "Clara", "Otto", "Mei"
        };

        private static readonly string[] LastNames =
        {
            "Alvarez", "Bright", "Costa", "Duran", "Engel", "Faber", "Greco", "Halloran",
            "Ito", "Jansen", "Keller", "Lund", "Moreau", "Novak", "Okafor", "Pike"
        };

        /// <summary>
        /// The people who applied. Roughly half cooks, half servers, spread across the market
        /// so there is always something cheap and something serious to choose between.
        /// </summary>
        public static IList<Candidate> Applicants(long seed, int howMany = 6)
        {
            var rng = new DeterministicRandom(seed);
            var pool = new List<Candidate>();

            for (var i = 0; i < howMany; i++)
            {
                var role = i % 2 == 0 ? StaffRole.Cook : StaffRole.Server;

                // Claimed ability, spread across the whole market rather than clustered.
                var advertises = Round(0.15m + ((decimal)rng.NextDouble() * 0.8m));

                // The truth, somewhere either side of the claim. Clamped so nobody is a
                // complete fabrication — a CV is optimistic, not fiction.
                var drift = ((decimal)rng.NextDouble() * 2m - 1m) * ScoutingError;
                var actual = advertises + drift;
                if (actual < 0.05m) actual = 0.05m;
                if (actual > 1m) actual = 1m;

                var floor = role == StaffRole.Cook ? CookFloorWage : ServerFloorWage;
                var premium = role == StaffRole.Cook ? CookSkillPremium : ServerSkillPremium;

                var name = FirstNames[(int)(rng.NextDouble() * FirstNames.Length) % FirstNames.Length]
                    + " " + LastNames[(int)(rng.NextDouble() * LastNames.Length) % LastNames.Length];

                // Room to grow, and the green have the most of it. That is what makes a cheap
                // hire a bet rather than only a risk: somebody on the floor wage who is not
                // much use yet may be worth considerably more in six months, and there is no
                // way to tell them apart from somebody who is simply not much use.
                var headroom = (decimal)rng.NextDouble() * 0.45m * (1m - actual);
                var potential = actual + headroom;
                if (potential > 1m) potential = 1m;

                pool.Add(new Candidate(
                    "hire-" + seed + "-" + i, name, role,
                    advertises, Round(actual),
                    Math.Round(floor + (advertises * premium), 2),
                    Round(potential)));
            }

            return pool;
        }

        private static decimal Round(decimal v) { return Math.Round(v, 3); }
    }
}
