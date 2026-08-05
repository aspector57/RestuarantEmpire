// WHAT DOES EACH DISH ACTUALLY COST, at every supplier tier, in the BROWSER build?
//
// The engine's recalibration was measured dish by dish; this asks the same question of the
// port so the two can be compared as numbers rather than as intentions. Content was mirrored
// by hand between the two builds and had silently drifted a whole recalibration apart.
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];
G = newGame(sub, 1);
const tiers = SUPPLIERS.map(s => s.id);
print("FOOD COST BY DISH AND TIER — browser build");
print("  " + "dish".padEnd(22) + "price" .padStart(7) + tiers.map(t => t.slice(0,8).padStart(11)).join(""));
for (const r of RECIPES.slice().sort((a,b) => a.id < b.id ? -1 : 1)) {
  let line = "  " + r.name.slice(0,21).padEnd(22) + ("$" + r.base.toFixed(2)).padStart(7);
  for (const t of tiers) {
    G.supplier = t;
    const cost = plateCost(r);
    line += ((100 * cost / r.base).toFixed(0) + "%").padStart(11);
  }
  print(line);
}
print("");
print("BLENDED, weighted by how often each is actually ordered:");
for (const t of tiers) {
  G.supplier = t;
  let cost = 0, take = 0;
  for (const r of RECIPES) { cost += plateCost(r); take += r.base; }
  print("  " + t.padEnd(22) + (100 * cost / take).toFixed(1) + "%   (flat average across the whole card)");
}
