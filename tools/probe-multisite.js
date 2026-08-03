/*
 * TWO RESTAURANTS IN THE BROWSER BUILD.
 *
 * The engine grew multi-location first, measured before anything was built for it: two
 * restaurants under one company earned 131,903 against 131,439 for the two of them run
 * separately, 0.4% apart. Pure arithmetic, which is the flat-scaling anti-pattern. What makes
 * it a decision is that a street is finite and your own second restaurant drinks from it.
 *
 * This is the port's proof. It checks the three things that could each be silently broken:
 *
 *   1. Both restaurants actually trade, and their books are separate.
 *   2. The company clock ticks ONCE a day and rent is billed ONCE per site per month —
 *      running three sites must not advance the calendar three times.
 *   3. Clustering costs you and spreading does not, which is the whole mechanic.
 *
 *     python3 tools/headless.py tools/probe-multisite.js
 */

var DAYS = 120;
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
function rpad(s,n){ s=String(s); while(s.length<n) s=" "+s; return s; }
function cash(n){ return (n<0?"-$":"$") + Math.abs(Math.round(n)).toLocaleString(); }

function siteBy(id){ return SITES.filter(function(s){ return s.id === id; })[0]; }

/* Fits out whichever site the pointer is on, identically every time. */
function fitOut(){
  G.seats = 24;
  G.fittings = [{id:"t", name:"Tables", seats:24, comfort:0.55}];
  G.seatSpend = 360;
  G.supplier = "valley-produce";
  G.onMenu = new Set(["margherita","house-focaccia","caprese-salad"]);
  G.servers = [{id:"s0",name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5},
               {id:"s1",name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5}];
  G.cooks   = [{id:"c0",name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5},
               {id:"c1",name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5}];

  var oven = EQUIPMENT.filter(function(e){ return e.id === "oven-secondhand"; })[0];
  G.stations = { oven: [{id:oven.id,speed:oven.speed,foot:oven.foot,capacity:0},
                        {id:oven.id,speed:oven.speed,foot:oven.foot,capacity:0}] };
  var gm = EQUIPMENT.filter(function(e){ return e.station === "garde-manger"; })[1];
  G.stations["garde-manger"] = [{id:gm.id,speed:gm.speed,foot:gm.foot,capacity:0},
                                {id:gm.id,speed:gm.speed,foot:gm.foot,capacity:0}];
  G.stations["cold-storage"] = [{id:"cold-walkin", speed:1, foot:90, capacity:3000}];
  G.stations["dry-storage"]  = [{id:"dry-racking", speed:1, foot:34, capacity:4000}];
  G.floorArea = 3000;

  RECIPES.forEach(function(r){ if(G.onMenu.has(r.id)) G.prices[r.id] = r.base; });
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 200); });
  G.rep.standing = 0.5; G.rep.meals = 12000;
}

/* Builds a portfolio, trades it, and reports the group AND each site. */
function portfolio(ids){
  G = newGame(siteBy(ids[0]), 20240802);
  G.cash = 500000;                       // capital is not the variable here
  fitOut();

  for(var i=1;i<ids.length;i++){
    var s = newSite(siteBy(ids[i]), "r"+i);
    G.sites.push(s);
    G.active = i;
    fitOut();
  }
  G.active = 0;

  var startCash = G.cash;
  advance(DAYS, false);

  var group = { net: G.cash - startCash, covers: 0, perSite: [] };
  for(var j=0;j<G.sites.length;j++){
    var L = G.sites[j].ledger;
    group.covers += G.sites[j].recent.reduce(function(a,x){ return a+x.covers; }, 0);
    group.perSite.push({
      name: G.sites[j].site.name,
      revenue: L.revenue, rent: L.rent,
      standing: G.sites[j].rep.standing
    });
  }
  group.days = G.day;
  return group;
}

/*
 * THE ONE THAT MATTERED MOST, AND WAS NOT HERE.
 *
 * `openSite` used to hand back a bare shell — no stations, no seats, nobody on the payroll,
 * an empty pantry. Aaron opened a City Center site for $12,000 and it served ZERO covers in
 * sixty-five days while paying $7,800 a month in rent. Every check below passed the whole
 * time, because they all fitted their sites out by hand and never used the button the player
 * uses.
 *
 * A HARNESS THAT SETS UP ITS OWN FIXTURE IS NOT TESTING THE THING THE PLAYER TOUCHES. That is
 * the same shape as the fixture bugs that produced a famine and a 669% food bill.
 */
function openSiteTheWayThePlayerDoes(){
  G = newGame(siteBy("suburban-high-street"), 20240802);
  fitOutOpening();
  G.cash = 200000;                       // affordability is not what is being tested here

  var opened = openSite(siteBy("city-center"));
  if(!opened){ console.log("  FAIL  openSite refused with $200,000 in the bank"); return null; }

  advance(30, false);
  return opened;
}

console.log("A RESTAURANT OPENED THE WAY THE PLAYER OPENS ONE");
var fresh = openSiteTheWayThePlayerDoes();
if(fresh){
  var covers = fresh.recent.reduce(function(a,x){ return a+x.covers; }, 0);
  console.log("  stations " + Object.keys(fresh.stations).length +
              ", seats " + fresh.seats +
              ", cooks " + fresh.cooks.length +
              ", servers " + fresh.servers.length +
              ", covers in 30 days " + covers);

  if(covers <= 0)
    console.log("  FAIL  a restaurant opened through the UI served NOTHING — it is a rent bill, not a restaurant");
  else
    console.log("  ok    it trades from the day it opens");
}
console.log("");

/*
 * SELLING UP, and the thing that makes it a decision rather than a bail-out button:
 * a restaurant is worth what it EARNS, so a good one is worth far more than a failing one.
 */
console.log("SELLING A RESTAURANT");
(function(){
  G = newGame(siteBy("suburban-high-street"), 20240802);
  fitOutOpening();
  G.cash = 200000;
  openSite(siteBy("city-center"));

  // Let the city site trade properly, and starve the suburban one of a kitchen so it loses.
  advance(60, false);

  var values = G.sites.map(function(_, i){ return restaurantValue(i); });
  G.sites.forEach(function(st, i){
    console.log("  " + pad(st.name, 24) +
                "fit-out " + rpad(cash(values[i].fitOut), 10) +
                "  goodwill " + rpad(values[i].goodwill ? cash(values[i].goodwill) : "—", 10) +
                "  worth " + rpad(cash(values[i].total), 11) +
                "   (" + (values[i].earns === null ? "too new"
                          : values[i].earns > 0 ? "earning " + cash(values[i].earns) + "/mo"
                          : "losing " + cash(-values[i].earns) + "/mo") + ")");
  });

  var before = G.cash, n = G.sites.length, worth = values[1].total;
  var sold = sellSite(1);

  console.log("");
  if(!sold) console.log("  FAIL  sellSite refused with two restaurants open");
  else if(G.sites.length !== n - 1) console.log("  FAIL  selling did not remove the restaurant");
  else if(Math.abs((G.cash - before) - worth) > 1) console.log("  FAIL  the money did not match the valuation");
  else console.log("  ok    sold, removed, and paid " + cash(worth));

  // ...and you cannot sell the last one. That is quitting, not a business decision.
  console.log(canSellSite(0) ? "  FAIL  it would let you sell your only restaurant"
                             : "  ok    refuses to sell the only restaurant left");

  // A restaurant is only worth its kit until it earns.
  G = newGame(siteBy("suburban-high-street"), 20240802);
  fitOutOpening();
  var fresh = restaurantValue(0);
  console.log(fresh.goodwill === 0 ? "  ok    a restaurant with no trading history carries no goodwill"
                                   : "  FAIL  a brand-new restaurant was credited with goodwill");

  // THE HALF THAT MAKES IT A DECISION: a place that earns is worth far more than its cookers,
  // which is the first time reputation and a full room have had a CAPITAL value rather than
  // only an income. Same fit-out spend either way — only the trading differs.
  G = newGame(siteBy("suburban-high-street"), 20240802);
  G.cash = 500000;
  fitOut();
  advance(60, false);
  var good = restaurantValue(0);

  G = newGame(siteBy("suburban-high-street"), 20240802);
  G.cash = 500000;
  fitOut();
  G.cooks = [];                       // nobody to cook: same building, no trade
  advance(60, false);
  var bad = restaurantValue(0);

  console.log("");
  console.log("  a restaurant that trades   worth " + rpad(cash(good.total), 11) +
              "  (" + cash(good.earns) + "/mo, goodwill " + cash(good.goodwill) + ")");
  console.log("  the same one that does not worth " + rpad(cash(bad.total), 11) +
              "  (goodwill " + (bad.goodwill ? cash(bad.goodwill) : "none") + ")");
  console.log(good.total > bad.total * 2
    ? "  ok    what it earns dominates what it cost — selling well is worth building well"
    : "  FAIL  a good restaurant is worth about the same as a dead one; goodwill is not biting");
})();
console.log("");

console.log("TWO RESTAURANTS IN THE BROWSER BUILD — " + DAYS + " days");
console.log("");

var one       = portfolio(["suburban-high-street"]);
var clustered = portfolio(["suburban-high-street","suburban-high-street"]);
var spread    = portfolio(["suburban-high-street","city-center"]);

console.log("  " + pad("portfolio",30) + rpad("net",14) + rpad("days elapsed",14));
console.log("  " + pad("one suburban",30) + rpad(cash(one.net),14) + rpad(one.days,14));
console.log("  " + pad("two, both suburban",30) + rpad(cash(clustered.net),14) + rpad(clustered.days,14));
console.log("  " + pad("two, suburban + city",30) + rpad(cash(spread.net),14) + rpad(spread.days,14));

var fails = 0;
function check(ok, what, detail){
  if(!ok){ fails++; console.log("  FAIL  " + what + (detail ? "  (" + detail + ")" : "")); }
  else console.log("  ok    " + what);
}

console.log("");
console.log("CHECKS");

/* 1. The clock ticks once a day however many restaurants there are. */
check(one.days === DAYS && clustered.days === DAYS && spread.days === DAYS,
      "the company clock ticks once a day, whatever the size of the group",
      "one " + one.days + ", clustered " + clustered.days + ", spread " + spread.days);

/* 2. Each restaurant keeps its own books, and both of them actually traded. */
var bothTraded = clustered.perSite.every(function(s){ return s.revenue > 0; });
check(bothTraded, "both restaurants trade and keep separate books",
      clustered.perSite.map(function(s){ return s.name + " " + cash(s.revenue); }).join(", "));

/* 3. Rent is billed once per site per month, not once per site per site. */
var months = Math.floor(DAYS / 30);
var expectedRent = siteBy("suburban-high-street").rent * months;
check(Math.abs(clustered.perSite[0].rent - expectedRent) < 1,
      "rent is billed once per site per month",
      "billed " + cash(clustered.perSite[0].rent) + ", expected " + cash(expectedRent));

/* 4. THE MECHANIC. A street is finite, so clustering costs you and spreading does not. */
check(clustered.net < one.net * 2,
      "clustering earns less than twice one restaurant — the street is finite",
      cash(clustered.net) + " against " + cash(one.net * 2));

check(spread.net > clustered.net,
      "spreading out beats clustering",
      cash(spread.net) + " against " + cash(clustered.net));

console.log("");
console.log(fails ? fails + " CHECK(S) FAILED" : "ALL CHECKS PASS — the browser build runs a portfolio.");
