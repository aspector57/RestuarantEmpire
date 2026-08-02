using System;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// Permission to sell alcohol, which you buy once and then keep paying for.
    ///
    /// **WHY DRINKS ARE THE RIGHT THING TO BUILD, and it is not "more content".** The strategy
    /// probe found that fine dining cannot run a late service because there is nothing on the
    /// card anyone wants at one in the morning — and that is TRUE, not a content gap. Nobody
    /// orders sea bass at midnight. They order drinks. Alcohol is what makes a late daypart
    /// exist at all, which turns opening hours from near-free upside into a decision tied to
    /// what kind of place you are.
    ///
    /// It also repairs the premium concept honestly. Fine dining runs about a 41% food cost
    /// here, which is why it struggles, and real fine dining survives on a wine program at
    /// 70-80% margin blending that down. Without drinks the only way to make an expensive
    /// concept pay is to charge more for food, which drives the guests away — measured, the
    /// cliff past the optimum is brutal. Wine is how the trade actually solves this.
    ///
    /// **The license is a capital gate, which is Binding Principle 4 in its cleanest form:**
    /// expansion is gated by what you can genuinely afford, never by a quest flag. You pay real
    /// money up front, you keep paying to renew, and it unlocks a whole revenue line. Aaron's
    /// instinct — *"maybe you need to buy a liquor license to sell it?"* — is exactly right, and
    /// the reason it works is that it is not a tax. It is the thing that makes the decision
    /// carry weight.
    /// </summary>
    public sealed class LiquorLicense
    {
        /// <summary>
        /// What the licence costs to obtain. Deliberately steep against an opening bankroll of
        /// 18,000-27,000 — it should be something you grow into rather than buy on day one,
        /// or the decision disappears.
        /// </summary>
        public const decimal ApplicationFee = 6500m;

        /// <summary>
        /// Paid every month whether you sell a drop. This is what stops the licence being a
        /// one-off cost you take and forget: a quiet restaurant with a licence is bleeding.
        /// </summary>
        public const decimal MonthlyRenewal = 340m;

        internal LiquorLicense() { }

        public bool Held { get; private set; }

        /// <summary>Which day it was granted, so renewals can be charged from then.</summary>
        public int HeldSince { get; private set; }

        internal void Grant(int onDay)
        {
            Held = true;
            HeldSince = onDay;
        }

        /// <summary>
        /// Given up. The money already spent does not come back — which is the point of a
        /// sunk cost, and worth the player feeling once.
        /// </summary>
        public void Surrender()
        {
            Held = false;
        }

        public override string ToString()
        {
            return Held ? "licensed to sell alcohol" : "no alcohol licence";
        }
    }
}
