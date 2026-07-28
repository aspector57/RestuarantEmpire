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
        public Employee(string id, string name, StaffRole role, decimal hourlyWage, decimal skill = 0.5m)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Employee id is required.", nameof(id));
            if (hourlyWage < 0m) throw new ArgumentOutOfRangeException(nameof(hourlyWage));
            if (skill < 0m || skill > 1m) throw new ArgumentOutOfRangeException(nameof(skill), "Skill runs 0 to 1.");

            Id = id;
            Name = name ?? id;
            Role = role;
            HourlyWage = hourlyWage;
            Skill = skill;
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
        public decimal Skill { get; }

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
