/*
 * CAN EVERY CONCEPT WIN SOMEWHERE, IF IT IS RUN PROPERLY?
 *
 * Aaron's balance bar, and the only question that decides whether concepts are a real choice:
 *
 *     "You should be able to win with any concept anywhere if you run the restaurant
 *      properly, unless the concept just totally sucks."
 *
 * WHY THE EXISTING SWEEP DOES NOT ANSWER IT. `probe-concepts.js` opens each concept and then
 * touches nothing for ninety days, so it measures the SHAPE a concept opens with. That is a
 * fair question and a different one. Handing every concept a fixed price and a fixed brigade
 * compares concepts run at arbitrary settings rather than concepts run well -- the exact error
 * already recorded twice in this project, once against StrategyDiversity and once against the
 * supplier comparison that read premium as a trap because it was measured at budget's optimum.
 *
 * So: each concept, on each street, swept to ITS OWN best price position and brigade, and the
 * hours it should have kept rather than the ones it shipped with. Then compared.
 *
 * This is slow on purpose -- 7 concepts x 4 sites x price x brigade x hours, each 120 days.
 * Run it and go and do something else.
 *
 *     python3 tools/headless.py tools/probe-conceptbar.js
 */
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
function rpad(s,n){ s=String(s); while(s.length<n) s=" "+s; return s; }

var PRICES  = [0.85, 1.0, 1.15, 1.3, 1.5, 1.7, 1.9];
var BRIGADE = [[1,1],[2,1],[2,2],[3,2],[3,3],[4,3]];

function trial(concept, site, priceMul, cooks, servers, windows, days){
  G = newGame(site, 20260805);
  G.concept = concept.id;
  fitOutOpening(concept);

  if(windows) G.windows = windows.map(function(w){ return {name:w.name, from:w.from, to:w.to}; });

  // The concept's own price position, moved by the sweep multiplier.
  for(var i=0;i<concept.card.length;i++){
    var r = RECIPES.filter(function(x){ return x.id === concept.card[i]; })[0];
    if(r) G.prices[r.id] = +(designedPrice(r) * concept.pricePosition * priceMul).toFixed(2);
  }

  // Staff to the shape being tested, at the going rate -- underpaying is a different
  // experiment and would confound this one now that morale exists.
  G.cooks = []; G.servers = [];
  for(var c=0;c<cooks;c++)   G.cooks.push(makeStaff("cook", 0.6));
  for(var s=0;s<servers;s++) G.servers.push(makeStaff("server", 0.55));
  var want = { cooks: cooks, servers: servers };

  for(var n=0;n<days;n++){
    runDay(); G.day++;
    if(G.day % 30 === 0){
      billTheMonth();
      while(G.cooks.length   < want.cooks)   G.cooks.push(makeStaff("cook", 0.6));
      while(G.servers.length < want.servers) G.servers.push(makeStaff("server", 0.55));
    }
  }

  var m = G.metrics.slice(-30);
  var avg = function(k){ return m.reduce(function(a,x){ return a + x[k]; }, 0) / 30; };
  return { profit: avg("profit") * 30, covers: avg("covers"), solvent: G.cash > 0,
           check: avg("covers") ? avg("revenue") / avg("covers") : 0 };
}

/* The best this concept can do on this street, run properly. */
function bestOf(concept, site){
  // Hours it SHOULD keep here, not the ones it shipped with -- keeping the wrong hours is an
  // operating mistake, and this measures the concept rather than the mistake.
  G = newGame(site, 1);
  var best = bestHoursForThisStreet();
  var options = [null, best.preset.w];

  var top = null;
  for(var h=0; h<options.length; h++)
    for(var p=0; p<PRICES.length; p++)
      for(var b=0; b<BRIGADE.length; b++){
        var r = trial(concept, site, PRICES[p], BRIGADE[b][0], BRIGADE[b][1], options[h], 120);
        if(!r.solvent) continue;
        if(top === null || r.profit > top.profit)
          top = { profit:r.profit, covers:r.covers, check:r.check,
                  price:PRICES[p], cooks:BRIGADE[b][0], servers:BRIGADE[b][1],
                  hours: options[h] ? best.preset.name : "as designed" };
      }
  return top;
}

console.log("EVERY CONCEPT, RUN PROPERLY, ON EVERY STREET");
console.log("Each swept to its own best price, brigade and hours. 120 days. Profit per month.");
console.log("");
console.log("  " + pad("concept", 26) + SITES.map(function(s){ return rpad(s.name.split(" ")[0], 13); }).join("") + rpad("best street", 20));

var wins = {}, rows = [];
CONCEPTS.forEach(function(c){
  var line = "  " + pad(c.name, 26), best = null, bestSite = "", cells = [];
  SITES.forEach(function(site){
    var r = bestOf(c, site);
    cells.push({ site: site, r: r });
    line += rpad(r ? "$" + Math.round(r.profit).toLocaleString() : "NEVER", 13);
    if(r && (best === null || r.profit > best)){ best = r.profit; bestSite = site.name; }
  });
  console.log(line + rpad(bestSite || "nowhere", 20));
  rows.push({ concept: c, cells: cells, best: best, bestSite: bestSite });
});

console.log("");
console.log("HOW EACH CONCEPT WANTS TO BE RUN, in its own best market:");
console.log("  " + pad("concept",26) + pad("street",22) + pad("price",8) + pad("brigade",10) + pad("hours",22) + pad("covers",9) + "$/cover");
rows.forEach(function(row){
  var cell = null;
  for(var i=0;i<row.cells.length;i++)
    if(row.cells[i].r && row.cells[i].site.name === row.bestSite) cell = row.cells[i];
  if(!cell) { console.log("  " + pad(row.concept.name,26) + "never solvent anywhere"); return; }
  var r = cell.r;
  console.log("  " + pad(row.concept.name,26) + pad(cell.site.name,22)
    + pad(r.price.toFixed(2)+"x",8) + pad(r.cooks+" cooks, "+r.servers+" srv",10)
    + pad(r.hours,22) + pad(r.covers.toFixed(0),9) + "$" + r.check.toFixed(2));
});

console.log("");
SITES.forEach(function(site){
  var best = null, name = "";
  rows.forEach(function(row){
    for(var i=0;i<row.cells.length;i++){
      var cell = row.cells[i];
      if(cell.site.id !== site.id || !cell.r) continue;
      if(best === null || cell.r.profit > best){ best = cell.r.profit; name = row.concept.name; }
    }
  });
  wins[name] = (wins[name] || 0) + 1;
  console.log("  " + pad(site.name, 26) + "won by " + pad(name, 28) + "$" + Math.round(best).toLocaleString() + "/mo");
});

var distinct = Object.keys(wins).length;
console.log("");
console.log("  DISTINCT WINNERS ACROSS 4 STREETS, RUN PROPERLY: " + distinct + "/4");
var dead = rows.filter(function(r){ return r.best === null; });
console.log("  Concepts that cannot turn a profit anywhere, however run: " + (dead.length ? dead.map(function(r){return r.concept.name;}).join(", ") : "none"));
console.log("");
console.log("  Aaron's bar: you should be able to win with any concept anywhere if you run it");
console.log("  properly, unless the concept just totally sucks.");
