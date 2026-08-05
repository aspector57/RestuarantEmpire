/*
 * EVERY CONCEPT ON EVERY STREET, opened and then left alone for 90 days.
 *
 * The one-site version of this said the wine bar out-earns the pizzeria 6.5x and looked like a
 * balance emergency. This project has twice been caught reading a single row as a finding --
 * "when comparing concepts, sweep each one to its own optimum before reporting a winner", and
 * one site is not a sweep. A concept that runs away on the suburban high street may be a
 * disaster in the business district at breakfast.
 *
 * NO DECISIONS AFTER DAY ONE, deliberately. This measures the SHAPE the concept opens with,
 * not how well it can be run -- Aaron's bar is that any concept should be winnable if you run
 * it properly, and that is a different question from whether they all open level.
 *
 *     python3 tools/headless.py tools/probe-concepts.js
 */
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
function rpad(s,n){ s=String(s); while(s.length<n) s=" "+s; return s; }

function run(concept, site, days){
  G = newGame(site, 20260804);
  G.concept = concept.id;
  fitOutOpening(concept);
  var want = { cooks:G.cooks.length, servers:G.servers.length };

  for(var n=0;n<days;n++){
    runDay(); G.day++;
    if(G.day%30===0){
      billTheMonth();
      // Replacing somebody who quit is not a strategy, it is opening the doors.
      while(G.cooks.length<want.cooks) G.cooks.push(makeStaff("cook",0.5));
      while(G.servers.length<want.servers) G.servers.push(makeStaff("server",0.5));
    }
  }
  var m = G.metrics.slice(-30), s = function(k){ return m.reduce(function(a,x){return a+x[k];},0)/30; };
  return { profit: s("profit")*30, covers: s("covers"), check: s("covers") ? s("revenue")/s("covers") : 0,
           solvent: G.cash > 0 };
}

console.log("EVERY CONCEPT ON EVERY STREET — 90 days, no decisions after opening");
console.log("Profit per month. BUST means the cash ran out.");
console.log("");
console.log("  " + pad("concept",26) + SITES.map(function(s){ return rpad(s.name.split(" ")[0],13); }).join("") + rpad("best on",16));

var wins = {};
CONCEPTS.forEach(function(c){
  var row = "  " + pad(c.name,26), best = null, bestSite = "";
  SITES.forEach(function(site){
    var r = run(c, site, 90);
    row += rpad(r.solvent ? "$"+Math.round(r.profit).toLocaleString() : "BUST", 13);
    if(best === null || r.profit > best){ best = r.profit; bestSite = site.name; }
  });
  console.log(row + rpad(bestSite, 16));
});

console.log("");
console.log("WHO WINS EACH STREET:");
SITES.forEach(function(site){
  var best = null, name = "";
  CONCEPTS.forEach(function(c){
    var r = run(c, site, 90);
    if(best === null || r.profit > best){ best = r.profit; name = c.name; }
    wins[c.name] = wins[c.name] || 0;
  });
  wins[name] = (wins[name]||0) + 1;
  console.log("  " + pad(site.name,26) + pad(name,28) + "$" + Math.round(best).toLocaleString() + "/mo");
});

var distinct = Object.keys(wins).filter(function(k){ return wins[k] > 0; }).length;
console.log("");
console.log("  distinct winners across 4 streets: " + distinct + "/4");
console.log("  A concept game where one concept wins everywhere is a spreadsheet with a theme.");
