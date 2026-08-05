using System;
using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>What someone does. M1 keeps this to the two roles that change the simulation.</summary>
    public enum StaffRole
    {
        /// <summary>Works the stations. Cooks are what let installed equipment actually run.</summary>
        Cook = 0,

        /// <summary>Works the floor. Servers are what let seats actually be sat in.</summary>
        Server = 1
    }

    /// <summary>
    /// Somebody on the payroll.
    ///
    /// Deliberately thin for M1, per the roadmap: a role, a wage and a single skill number.
    /// Hiring profiles (Smart / Loyal / Hardworking / Experience), scouting uncertainty,
    /// morale, turnover and the promotion ladder are all M2 — building them now would be
    /// building ahead, and none of them are needed for hiring to be a real decision.
    /// </summary>
    public sealed class Employee
    {
        /// <summary>
        /// How fast someone closes the gap to what they could become, per plate served.
        /// Slow: about half the gap over six months of steady trade, so a green hire is a
        /// season-long bet rather than a switch that flips.
        /// </summary>
        public const decimal LearningRate = 0.00005m;

        public Employee(string id, string name, StaffRole role, decimal hourlyWage, decimal skill = 0.5m,
            decimal potential = -1m)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Employee id is required.", nameof(id));
            if (hourlyWage < 0m) throw new ArgumentOutOfRangeException(nameof(hourlyWage));
            if (skill < 0m || skill > 1m) throw new ArgumentOutOfRangeException(nameof(skill), "Skill runs 0 to 1.");

            Id = id;
            Name = name ?? id;
            Role = role;
            HourlyWage = hourlyWage;
            Skill = skill;
            Potential = potential < 0m ? skill : (potential < skill ? skill : (potential > 1m ? 1m : potential));
        }

        public string Id { get; }
        public string Name { get; }
        public StaffRole Role { get; }
        public decimal HourlyWage { get; }

        /// <summary>
        /// 0 to 1. What they can actually do, as opposed to what their CV claimed — see
        /// <see cref="Candidate"/>. Read by the kitchen (how many plates they keep moving),
        /// by the satisfaction score (a good cook elevates what they are given, a poor one
        /// wastes it) and by the floor (how many covers a server can really hold).
        ///
        /// This property existed, validated and documented as "reserved for M2", from the
        /// first commit and was read by NOTHING for the whole of M1 — the fourth time that
        /// exact shape has turned up here, after PriceSensitivity, IngredientQuality and
        /// PartiesTurnedAway. It is worth assuming there is a fifth.
        /// </summary>
        public decimal Skill { get; private set; }

        /// <summary>
        /// How this person feels about working here, 0 to 1. Driven by what they are paid
        /// against the going rate for their skill, and by how long the doors are open.
        ///
        /// It only ever DRAGS what they can do — see <see cref="Payroll.MoraleFactor"/> — so
        /// this can never become a way to buy skill with money. It moves slowly, like a
        /// reputation, because a pay rise does not buy a good week overnight.
        /// </summary>
        public decimal Morale { get; private set; } = -1m;

        /// <summary>What the market pays somebody this good. The number a wage is judged against.</summary>
        public decimal MarketWage
        {
            get
            {
                return Role == StaffRole.Cook
                    ? Tuning.CookFloorWage + (Skill * Tuning.CookSkillPremium)
                    : Tuning.ServerFloorWage + (Skill * Tuning.ServerSkillPremium);
            }
        }

        /// <summary>
        /// Where their morale is heading, given today's pay and hours. Paying the going rate
        /// lands at 1.0 and nothing above it buys more; underpay 20% and it roughly halves.
        /// </summary>
        public decimal MoraleTarget(int openHours)
        {
            var market = MarketWage;
            var paidWell = market <= 0m ? 1m : HourlyWage / market;

            var pay = Clamp((paidWell - 0.55m) / 0.45m);

            // Long hours cost morale whatever you pay, which is the other half of the
            // opening-hours decision — until now trading late cost only wages.
            var strain = Clamp((openHours - Tuning.MoraleComfortableHours) / 6m) * 0.35m;

            var target = pay - strain;
            return Clamp(target < Tuning.MoraleFloor ? Tuning.MoraleFloor : target);
        }

        /// <summary>Move morale toward where the current pay and hours are taking it.</summary>
        public void SettleMorale(int openHours)
        {
            var target = MoraleTarget(openHours);

            // Start where they are heading rather than at an arbitrary number, so a restaurant
            // that pays properly is not dragged for its first months by a fixed starting value.
            if (Morale < 0m) { Morale = target; return; }

            Morale = Clamp(Morale + ((target - Morale) * Tuning.MoraleDrift));
        }

        public void RestoreMorale(decimal morale) { Morale = Clamp(morale); }

        private static decimal Clamp(decimal v) { return v < 0m ? 0m : v > 1m ? 1m : v; }

        /// <summary>
        /// What they could become with the hours in. Never shown, never below current skill.
        ///
        /// Aaron: *"cheap labor can also be good, like have high potential to learn but start
        /// off not great."* This is that — a green hire on the floor wage who is genuinely
        /// worth training, and indistinguishable at interview from one who is simply green.
        /// </summary>
        public decimal Potential { get; }

        /// <summary>Whether there is anything left to learn. Purely for the read surface.</summary>
        public bool StillLearning { get { return Potential - Skill > 0.02m; } }

        /// <summary>
        /// Time on the pass. Called once per plate the kitchen sends, so a busy restaurant
        /// trains people faster than a quiet one — which is true, and means the same hire is
        /// worth more to a place that is actually trading.
        /// </summary>
        public void Worked(int plates = 1)
        {
            if (plates < 1 || Skill >= Potential) return;

            for (var i = 0; i < plates; i++)
                Skill += (Potential - Skill) * LearningRate;

            if (Skill > Potential) Skill = Potential;
        }

        /// <summary>Restores a saved skill. Loading is the only reason to set this directly.</summary>
        public void RestoreSkill(decimal skill)
        {
            if (skill < 0m) skill = 0m;
            Skill = skill > 1m ? 1m : skill;
        }

        public override string ToString()
        {
            return Name + " (" + Role + ", " + HourlyWage.ToString("N0") + "/hr)";
        }
    }

    /// <summary>
    /// The payroll for one location.
    ///
    /// The point of hiring being a decision at all is that STAFF ARE WHAT MAKE YOUR ASSETS
    /// WORK. You can own six ovens and cook on two of them if you only employ two cooks;
    /// you can install forty covers and seat a dozen if you only employ one server. Firing
    /// people is therefore genuinely tempting and genuinely costly, which is the whole
    /// tradeoff — without it, payroll is just a number to minimise.
    /// </summary>
    public sealed class Payroll
    {
        private readonly List<Employee> _staff = new List<Employee>();

        internal Payroll() { }

        public IReadOnlyList<Employee> Staff { get { return _staff; } }

        /// <summary>Average morale of everyone in a role, 1.0 when nobody is employed.</summary>
        public decimal MoraleOf(StaffRole role)
        {
            var total = 0m;
            var n = 0;
            for (var i = 0; i < _staff.Count; i++)
                if (_staff[i].Role == role) { total += _staff[i].Morale < 0m ? 1m : _staff[i].Morale; n++; }

            return n == 0 ? 1m : total / n;
        }

        /// <summary>
        /// What a resentful team is worth against a contented one. It only ever DRAGS: full
        /// morale is exactly 1.0, never a bonus above what somebody can actually do.
        /// </summary>
        public decimal MoraleFactor(StaffRole role)
        {
            return 0.85m + (MoraleOf(role) * 0.15m);
        }

        /// <summary>Move everyone's morale toward where their pay and the hours are taking it.</summary>
        public void SettleMorale(int openHours)
        {
            for (var i = 0; i < _staff.Count; i++) _staff[i].SettleMorale(openHours);
        }
        public int Headcount { get { return _staff.Count; } }

        public int CountOf(StaffRole role)
        {
            var total = 0;
            foreach (var person in _staff) { if (person.Role == role) total++; }

            return total;
        }

        /// <summary>
        /// How good this half of the brigade is on average, 0 to 1. Half is the neutral
        /// point everything is calibrated against, so a payroll of average people behaves
        /// exactly as it did before skill was consulted at all.
        /// </summary>
        public decimal AverageSkill(StaffRole role)
        {
            var total = 0m;
            var counted = 0;

            foreach (var person in _staff)
            {
                if (person.Role != role) continue;
                total += person.Skill;
                counted++;
            }

            return counted == 0 ? 0.5m : total / counted;
        }

        /// <summary>
        /// Plates the kitchen can keep moving, counting who is on rather than how many.
        /// A strong cook is worth appreciably more than a weak one, but never two of them —
        /// bodies still matter, which is what keeps hiring a real cost rather than a puzzle
        /// solved once by finding the best person available.
        /// </summary>
        public int PlateCapacity(int platesPerCook)
        {
            var capacity = 0m;

            foreach (var person in _staff)
            {
                if (person.Role != StaffRole.Cook) continue;
                capacity += platesPerCook * (0.7m + (person.Skill * 0.6m));
            }

            return (int)Math.Floor(capacity);
        }

        /// <summary>Total wage bill per hour with everyone on shift.</summary>
        public decimal HourlyWageBill
        {
            get
            {
                var total = 0m;
                foreach (var person in _staff) total += person.HourlyWage;

                return total;
            }
        }

        /// <summary>Everybody on shift gets the hours in.</summary>
        public void Worked(int plates = 1)
        {
            foreach (var person in _staff) person.Worked(plates);
        }

        public Employee Hire(Employee person)
        {
            if (person == null) throw new ArgumentNullException(nameof(person));

            foreach (var existing in _staff)
            {
                if (existing.Id == person.Id)
                    throw new InvalidOperationException("Someone with id '" + person.Id + "' already works here.");
            }

            _staff.Add(person);
            return person;
        }

        /// <summary>Lets someone go. Returns them, or null if nobody by that id works here.</summary>
        public Employee Fire(string employeeId)
        {
            for (var i = 0; i < _staff.Count; i++)
            {
                if (_staff[i].Id != employeeId) continue;

                var leaving = _staff[i];
                _staff.RemoveAt(i);

                return leaving;
            }

            return null;
        }

        /// <summary>Lets go of the most recently hired person in a role. Convenience for "fire a cook".</summary>
        public Employee FireOne(StaffRole role)
        {
            for (var i = _staff.Count - 1; i >= 0; i--)
            {
                if (_staff[i].Role != role) continue;

                var leaving = _staff[i];
                _staff.RemoveAt(i);

                return leaving;
            }

            return null;
        }
    }
}
