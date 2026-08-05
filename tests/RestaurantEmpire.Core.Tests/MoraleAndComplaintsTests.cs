using System.Linq;
using RestaurantEmpire.Core.Content;
using RestaurantEmpire.Core.Model;
using Xunit;
using Xunit.Abstractions;

namespace RestaurantEmpire.Core.Tests
{
    /// <summary>
    /// WHAT IT IS LIKE TO WORK HERE, AND WHAT PEOPLE SAY ON THE WAY OUT.
    ///
    /// A satisfaction score of 0.61 cannot be acted on. "Eighteen people said the food took too
    /// long" can. This is Binding Principle 2 — every outcome traces to a named cause — in the
    /// guest's own words rather than as a score movement.
    /// </summary>
    public class MoraleAndComplaintsTests
    {
        private readonly ITestOutputHelper _out;
        public MoraleAndComplaintsTests(ITestOutputHelper o) { _out = o; }

        // Payroll's constructor is internal on purpose — a payroll belongs to a restaurant —
        // so the fixture opens one rather than working around the design.
        private static Payroll PayrollOf()
        {
            var definitions = JsonDefinitionLoader.LoadFromDirectory(TestData.DataDirectory);
            var company = new Company("acme-group", "Acme Restaurant Group", definitions);
            return company.OpenRestaurant("flagship", "The Flagship", LocationType.BrickAndMortar).Payroll;
        }

        private static Employee Cook(decimal wageAgainstMarket, decimal skill = 0.6m)
        {
            var market = Tuning.CookFloorWage + (skill * Tuning.CookSkillPremium);
            return new Employee("c1", "A Cook", StaffRole.Cook, market * wageAgainstMarket, skill);
        }

        [Fact]
        public void PayingTheGoingRateIsFineAndPayingOverBuysNothingExtra()
        {
            var fair = Cook(1.00m);
            var generous = Cook(1.60m);

            Assert.Equal(1m, fair.MoraleTarget(5));
            Assert.Equal(fair.MoraleTarget(5), generous.MoraleTarget(5));
        }

        [Fact]
        public void UnderpayingPeopleCostsYouTheirGoodwill()
        {
            var fair = Cook(1.00m);
            var mean = Cook(0.80m);
            var exploitative = Cook(0.60m);

            _out.WriteLine($"  market {fair.MoraleTarget(5):F2}  80% {mean.MoraleTarget(5):F2}  60% {exploitative.MoraleTarget(5):F2}");

            Assert.True(mean.MoraleTarget(5) < fair.MoraleTarget(5));
            Assert.True(exploitative.MoraleTarget(5) < mean.MoraleTarget(5));
            Assert.True(exploitative.MoraleTarget(5) <= 0.3m, "60% of the going rate should be plainly bad");
        }

        /// <summary>
        /// The other half of the opening-hours decision. Trading late used to cost only wages,
        /// so long hours were close to free once the wage bill was covered.
        /// </summary>
        [Fact]
        public void LongHoursCostMoraleWhateverYouPay()
        {
            var wellPaid = Cook(1.00m);
            Assert.True(wellPaid.MoraleTarget(14) < wellPaid.MoraleTarget(5),
                "a fourteen-hour day is worse to work than a five-hour one at the same wage");
        }

        /// <summary>Morale must not become a way to buy skill — it only ever drags.</summary>
        [Fact]
        public void MoraleNeverMakesSomebodyBetterThanTheyAre()
        {
            var payroll = PayrollOf();
            payroll.Hire(Cook(2.00m));
            payroll.SettleMorale(5);

            Assert.Equal(1m, payroll.MoraleOf(StaffRole.Cook));
            Assert.Equal(1m, payroll.MoraleFactor(StaffRole.Cook));
        }

        [Fact]
        public void MoraleMovesOverMonthsRatherThanOvernight()
        {
            var payroll = PayrollOf();
            payroll.Hire(Cook(0.60m));
            payroll.SettleMorale(5);

            var afterOne = payroll.MoraleOf(StaffRole.Cook);
            for (var i = 0; i < 12; i++) payroll.SettleMorale(5);

            _out.WriteLine($"  settles at {payroll.MoraleOf(StaffRole.Cook):F2} from {afterOne:F2}");
            Assert.True(payroll.MoraleOf(StaffRole.Cook) <= afterOne + 0.001m);
        }

        // ---- complaints ----

        private static MealVerdict Good()
        {
            return new MealVerdict
            {
                Food = 0.80m, Speed = 0.80m, Value = 0.80m, Room = 0.80m,
                Freshness = 1m, FloorMorale = 1m, ClaimsItsIngredients = false
            };
        }

        [Fact]
        public void NobodyComplainsAboutAGoodDinner()
        {
            Assert.Empty(Complaints.From(Good()));
        }

        [Fact]
        public void EachComplaintNamesTheThingThatWentWrong()
        {
            var slow = Good(); slow.Speed = 0.20m;
            Assert.Contains(Complaints.From(slow), c => c.Code == "wait");

            var dear = Good(); dear.Value = 0.20m;
            Assert.Contains(Complaints.From(dear), c => c.Code == "price");

            var shabby = Good(); shabby.Room = 0.20m;
            Assert.Contains(Complaints.From(shabby), c => c.Code == "room");

            var resentful = Good(); resentful.FloorMorale = 0.20m;
            Assert.Contains(Complaints.From(resentful), c => c.Code == "service");
        }

        /// <summary>
        /// "The food was poor" is not actionable; old stock, an overclaim and plain bad cooking
        /// are three different fixes at three different prices.
        /// </summary>
        [Fact]
        public void BadFoodSplitsIntoTheThreeThingsItCouldActuallyBe()
        {
            var stale = Good(); stale.Food = 0.20m; stale.Freshness = 0.40m;
            Assert.Contains(Complaints.From(stale), c => c.Code == "stale");

            var overclaimed = Good(); overclaimed.Food = 0.20m; overclaimed.ClaimsItsIngredients = true;
            Assert.Contains(Complaints.From(overclaimed), c => c.Code == "claim");

            var plainBad = Good(); plainBad.Food = 0.20m;
            Assert.Contains(Complaints.From(plainBad), c => c.Code == "food");
        }

        /// <summary>
        /// THE POINT OF THE WHOLE THING: overclaim tonight and you hear about it tonight. The
        /// marketing lie was measured as taking about two years to reach the books, and the
        /// recorded fix was a visible consequence rather than a bigger divisor.
        /// </summary>
        [Fact]
        public void AnOverclaimIsHeardAboutTheSameNight()
        {
            var quiet = Good(); quiet.Food = 0.20m;
            var loud = Good(); loud.Food = 0.20m; loud.ClaimsItsIngredients = true;

            Assert.DoesNotContain(Complaints.From(quiet), c => c.Code == "claim");
            Assert.Contains(Complaints.From(loud), c => c.Code == "claim");
        }

        [Fact]
        public void HowBadlySomethingWentIsSeparateFromHowManySaidIt()
        {
            var mild = Good(); mild.Speed = 0.50m;
            var dreadful = Good(); dreadful.Speed = 0.10m;

            var mildSeverity = Complaints.From(mild).Single(c => c.Code == "wait").Severity;
            var badSeverity = Complaints.From(dreadful).Single(c => c.Code == "wait").Severity;

            Assert.True(badSeverity > mildSeverity, "a twenty-minute wait and an hour are not the same complaint");
            Assert.InRange(badSeverity, 1, 3);
        }
    }
}
