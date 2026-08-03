/*
 * THE SCOUTING REPORT — would this street be excited about this concept?
 *
 * Aaron, on NBA 2K's relocation screen: *"you can see if people would be excited about your
 * concept or team."* That is the right reference, and the useful half of it is the market
 * readout rather than the uniform designer.
 *
 * NEARLY ALL OF THIS ALREADY EXISTED. `menuAppealTo` scores a card against a sort of guest,
 * `likelyAt` says who is out on a given street at a given hour, and `wouldConsider` says
 * whether they would set off at that price. The browser build already prints the result as
 * "card suits the street 116%" — pointed at the site you have. A scouting report is the same
 * three functions pointed at a site you do NOT have yet, before you have spent anything.
 *
 * That matters beyond convenience: the forecast/commit/autopsy loop is the shape this project
 * already decided is good — you commit to a belief, then find out. Choosing a site is the
 * biggest commitment in the game and it is currently the one made with the least information.
 *
 * IT ALSO ANSWERS THE ANTI-PATTERN QUESTION. If every concept scores the same everywhere,
 * relocation is cosmetic and a new city is another arithmetic restaurant. The report below is
 * the evidence either way, which is why it is a measuring instrument and not a mock-up.
 *
 *     python3 tools/headless.py tools/scout.js
 */

function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
function rpad(s,n){ s=String(s); while(s.length<n) s=" "+s; return s; }

/* The concepts already live in StrategyDiversity as fixtures. Making them first-class is the
 * "select a concept or build your own" half of Aaron's idea; this is the data behind it. */
var CONCEPTS = [
  { id:"neighbourhood", name:"Neighborhood standard",
    menu:["margherita","house-focaccia","caprese-salad","sea-bass"], price:1.10, hours:[[18,23]] },
  { id:"pizzeria", name:"Pizza and sharing plates",
    menu:["margherita","house-focaccia"], price:0.95, hours:[[18,23]] },
  { id:"fine", name:"Fine dining",
    menu:["sea-bass","truffle-risotto","caprese-salad"], price:1.90, hours:[[18,23]] },
  { id:"counter", name:"Coffee and counter",
    menu:["flat-white","house-focaccia","eggs-benedict"], price:1.00, hours:[[7,11],[12,15]] },
  { id:"bar", name:"Wine bar and small plates",
    menu:["house-wine","cellar-wine","negroni","caprese-salad","house-focaccia"], price:1.20, hours:[[18,23],[23,26]] }
];

/* Sets up a HYPOTHETICAL restaurant on a site the player does not own, purely to read the
 * market off it. Nothing is bought and no money moves — this is a survey, not a build. */
function survey(site, concept){
  G = newGame(site, 20240802);
  G.onMenu = new Set(concept.menu);
  G.windows = concept.hours.map(function(h,i){ return {name:"S"+i, from:h[0], to:h[1]}; });
  RECIPES.forEach(function(r){ if(G.onMenu.has(r.id)) G.prices[r.id] = Math.round(r.base*concept.price*100)/100; });

  // A site you are scouting has never heard of you, so read the market at a neutral name
  // rather than flattering it with a reputation you have not earned there yet.
  G.rep.standing = 0.5; G.rep.meals = 0;

  var dps = [];
  G.windows.forEach(function(w){ var dp = daypartAt(w.from); if(dps.indexOf(dp) < 0) dps.push(dp); });

  // How much the card suits whoever is out, averaged across the hours actually served.
  var appeal = 0;
  dps.forEach(function(dp){ appeal += menuDrawAt(dp); });
  appeal = dps.length ? appeal/dps.length : 0;

  // And who those people ARE — the single most useful line, because it says WHY.
  var crowd = {};
  dps.forEach(function(dp){
    likelyAt(dp, site.id).forEach(function(a){
      crowd[a] = (crowd[a]||0) + menuAppealTo(a);
    });
  });
  var top = Object.keys(crowd).sort(function(a,b){ return crowd[b]-crowd[a]; });

  // Would they set off at this price, given they have never heard of you?
  var considered = 0, n = 0;
  dps.forEach(function(dp){
    likelyAt(dp, site.id).forEach(function(a){
      considered += wouldConsider(ARCHETYPES[a].sens, concept.price); n++;
    });
  });

  // Raw footfall the street offers over the hours served.
  var street = 0;
  G.windows.forEach(function(w){
    for(var h=w.from; h<w.to; h++) street += site.traffic[h % 24];
  });

  return { appeal: appeal, top: top[0], worst: top[top.length-1],
           consider: n ? considered/n : 1, street: street,
           key: site.key, rent: site.rent, cap: site.maxArea };
}

function verdict(a){
  if(a >= 1.20) return "they have been waiting for this";
  if(a >= 1.05) return "goes down well here";
  if(a >= 0.92) return "no strong feelings";
  if(a >= 0.80) return "a hard sell";
  return "wrong street for it";
}

console.log("SCOUTING REPORT — how each street would take each concept, before you spend a penny");
console.log("Appeal is the card against whoever is actually out at the hours you would open.");
console.log("");

SITES.forEach(function(site){
  console.log(site.name.toUpperCase() + "   key money $" + site.key.toLocaleString() +
              " · rent $" + site.rent.toLocaleString() + "/mo · " + site.maxArea + " sq ft ceiling");
  console.log("  " + pad("concept",28) + rpad("appeal",8) + rpad("would come",12) +
              rpad("footfall",10) + "  reads as");

  var rows = CONCEPTS.map(function(c){ return { c:c, s:survey(site, c) }; });
  rows.sort(function(x,y){ return y.s.appeal - x.s.appeal; });

  rows.forEach(function(row){
    console.log("  " + pad(row.c.name,28) + rpad(Math.round(row.s.appeal*100)+"%",8) +
                rpad(Math.round(row.s.consider*100)+"%",12) +
                rpad(Math.round(row.s.street),10) + "  " + verdict(row.s.appeal) +
                " (" + row.s.top + ")");
  });
  console.log("");
});

/* THE ANTI-PATTERN CHECK. If the best concept is the same everywhere, this screen is
 * decoration and a new city is another arithmetic restaurant. */
console.log("=== IS THE SITE CHOICE A REAL DECISION? ===");
var winners = {};
SITES.forEach(function(site){
  var best = null;
  CONCEPTS.forEach(function(c){
    var s = survey(site, c);
    if(!best || s.appeal > best.appeal) best = { name:c.name, appeal:s.appeal };
  });
  winners[best.name] = 1;
  console.log("  " + pad(site.name,26) + "-> " + best.name + " (" + Math.round(best.appeal*100) + "%)");
});

var distinct = Object.keys(winners).length;
console.log("");
console.log("  distinct winning concepts across " + SITES.length + " sites: " + distinct);
console.log(distinct >= 3
  ? "  A site is a real choice — different streets want different restaurants."
  : "  NOT ENOUGH SPREAD. One concept suits everywhere, so choosing a site is choosing a rent bill.");
