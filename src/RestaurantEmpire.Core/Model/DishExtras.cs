using System.Collections.Generic;

namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// WHAT THE PLAYER HAS DECIDED TO PUT ON EACH DISH.
    ///
    /// This is genuine state — a choice nothing else can re-derive — so it lives on the
    /// Restaurant and is saved. <see cref="MenuCosting"/> READS it and never owns it, because
    /// `Restaurant.Costing` builds a fresh lens on every read (Architecture Rule 1: policy
    /// propagates, nothing is cached). Anything stored on that lens would be discarded between
    /// two consecutive reads, which is a very quiet way to lose a decision.
    /// </summary>
    public sealed class DishExtras
    {
        private readonly Dictionary<string, List<string>> _on = new Dictionary<string, List<string>>();

        public IReadOnlyList<string> On(string recipeId)
        {
            List<string> chosen;
            if (recipeId != null && _on.TryGetValue(recipeId, out chosen)) return chosen;
            return new List<string>();
        }

        public void Set(string recipeId, string extraId, bool on)
        {
            if (string.IsNullOrWhiteSpace(recipeId) || string.IsNullOrWhiteSpace(extraId)) return;

            List<string> chosen;
            if (!_on.TryGetValue(recipeId, out chosen))
            {
                if (!on) return;
                chosen = new List<string>();
                _on[recipeId] = chosen;
            }

            var already = chosen.Contains(extraId);
            if (on && !already) chosen.Add(extraId);
            else if (!on && already) chosen.Remove(extraId);
        }

        /// <summary>Every dish that currently carries something, for saving.</summary>
        public IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> All()
        {
            foreach (var kv in _on)
                yield return new KeyValuePair<string, IReadOnlyList<string>>(kv.Key, kv.Value);
        }
    }
}
