/*
 * EVERY LEVER AGAINST EVERY OTHER LEVER, SHORT TERM AND LONG TERM.
 *
 * Aaron: "try simulations of every scenario possible while scanning the impact of the
 * changes in the short term and long term... we're looking for stupid patterns, for
 * example, its better to stay with 12 seats rather than grow."
 *
 * WHY THIS EXISTS AND `levers.js` IS NOT ENOUGH. That harness moves ONE thing at a time,
 * which cannot see an interaction — and the most important decision in the game is one.
 * Premium ingredients LOSE at the designed price and WIN once you charge for them, because
 * sourcing pays through reputation and reputation pays through what you can charge. Swept
 * one lever at a time, sourcing reads as a flat trap: "always buy the cheapest". Swept
 * against price, it is the arc the whole game is built around.
 *
 * So a lever is only judged a trap here if it is STILL a trap when the other lever is
 * played well. That is Aaron's own bar — "you should be able to win with any concept
 * anywhere if you run the restaurant properly" — turned into a measurement.
 *
 * TWO HORIZONS, ALWAYS. Reputation moves over months, so the first answer and the eventual
 * answer are often different, and the short one is the one that lies. Anything whose sign
 * flips between them is called out, because that is a decision the player cannot evaluate
 * by trying it for a fortnight.
 *
 *     python3 tools/headless.py tools/matrix.js
 */

var SHORT = 30, LONG = 240;

function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
function rpad(s,n){ s=String(s); while(s.length<n) s=" "+s; return s; }
function cash(n){ return (n<0?"-$":"$") + Math.abs(Math.round(n)).toLocaleString(); }

var sub = SITES.filter(function(s){ return s.id === "suburban-high-street"; })[0];

var BASE = {
  price:1.0, cooks:2, servers:1, seats:24, supplier:"valley-produce",
  oven:"oven-secondhand", ovens:2, licence:false,
  menu:["margherita","house-focaccia","caprese-salad"], site:sub
};

/* One restaurant, traded for `days`. Everything not named in `over` is the baseline, and
 * every run starts from identical state — confounded comparisons have produced two false
 * findings on this project already. */
function play(over, days){
  var cfg = {}; for(var k in BASE) cfg[k] = BASE[k];
  for(var k2 in over) cfg[k2] = over[k2];

  G = newGame(cfg.site, 20240802);
  G.cash = 200000;                                   // capital is not the variable here
  G.seats = cfg.seats;
  G.fittings = [{id:"t", name:"Tables", seats:cfg.seats, comfort:0.55}];
  G.seatSpend = cfg.seats*15;
  G.supplier = cfg.supplier;
  G.licence = cfg.licence;
  G.onMenu = new Set(cfg.menu);

  G.servers = []; for(var i=0;i<cfg.servers;i++) G.servers.push({id:"s"+i,name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5});
  G.cooks   = []; for(var j=0;j<cfg.cooks;  j++) G.cooks.push({id:"c"+j,name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});

  var ovenModel = EQUIPMENT.filter(function(e){ return e.id === cfg.oven; })[0];
  G.stations = {}; G.stations["oven"] = [];
  for(var u=0;u<cfg.ovens;u++) G.stations["oven"].push({id:ovenModel.id, speed:ovenModel.speed, foot:ovenModel.foot, capacity:0});
  ["garde-manger","grill","bar","saute","coffee"].forEach(function(st){
    var need = RECIPES.some(function(r){ return G.onMenu.has(r.id) && r.station === st; });
    if(!need) return;
    var m = EQUIPMENT.filter(function(e){ return e.station === st; })[1] || EQUIPMENT.filter(function(e){ return e.station === st; })[0];
    G.stations[st] = [{id:m.id, speed:m.speed, foot:m.foot, capacity:0}, {id:m.id, speed:m.speed, foot:m.foot, capacity:0}];
  });
  G.stations["cold-storage"] = [{id:"cold-walkin", speed:1, foot:90, capacity:3000}];
  G.stations["dry-storage"]  = [{id:"dry-racking", speed:1, foot:34, capacity:4000}];
  G.floorArea = 3000;                                // floor is not the variable either

  RECIPES.forEach(function(r){ if(G.onMenu.has(r.id)) G.prices[r.id] = Math.round(r.base*cfg.price*100)/100; });
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 150); });

  // A NEW restaurant, so reputation and awareness are free to move. That is the whole point
  // of the long horizon: sourcing pays through standing, and standing takes months.
  var rev=0, food=0, cov=0, walk=0, balkP=0, balkW=0, noTable=0, lostMenu=0, binned=0;
  for(var dd=0; dd<days; dd++){
    var r = runDay();
    rev += r.revenue; food += r.food; cov += r.covers; walk += r.walkouts;
    balkP += G.today.balkedPrice; balkW += G.today.balkedWait;
    noTable += G.today.noTable; lostMenu += G.today.lostMenu;
    binned += (G.today.wasted||0);
  }

  var labor = G.ledger.labor;
  var rent  = cfg.site.rent*(days/30) + (cfg.licence ? LICENCE_RENEWAL*(days/30) : 0);

  return {
    net: rev - food - labor - rent,
    perDay: (rev - food - labor - rent)/days,
    covers: cov/days, walk: walk/days, rev: rev/days,
    foodPct: rev>0 ? food/rev : 0,
    primePct: rev>0 ? (food+labor)/rev : 0,
    standing: G.rep.standing,
    lostPrice: balkP/days, lostWait: balkW/days, noTable: noTable/days, lostMenu: lostMenu/days,
    binnedPct: food>0 ? binned/food : 0
  };
}

var notes = [];

/*
 * A GRID OF TWO LEVERS. Reported at both horizons, and judged on the LONG one — but the
 * short one is printed beside it, because a decision whose sign flips is a decision the
 * player cannot evaluate by trying it for a fortnight.
 */
function grid(title, rowKey, rowVals, rowLabel, colKey, colVals, colLabel, extra){
  console.log("");
  console.log("=== " + title.toUpperCase() + " ===");
  console.log("net over " + LONG + " days (and over " + SHORT + " days in brackets)");
  console.log("");

  var header = pad("", 22);
  colVals.forEach(function(c){ header += rpad(colLabel(c), 20); });
  console.log(header);

  var results = [];

  rowVals.forEach(function(rv){
    var line = pad(rowLabel(rv), 22);
    var row = [];

    colVals.forEach(function(cv){
      var over = extra ? JSON.parse(JSON.stringify(extra)) : {};
      over[rowKey] = rv; over[colKey] = cv;
      if(extra && extra.menu) over.menu = extra.menu;   // Sets/arrays survive the clone badly

      var lng = play(over, LONG);
      var srt = play(over, SHORT);

      row.push({ rv:rv, cv:cv, lng:lng, srt:srt });
      line += rpad(cash(lng.net) + " (" + cash(srt.net) + ")", 20);
    });

    results.push(row);
    console.log(line);
  });

  /* THE VERDICT. A lever is only a trap if it is still a trap when the OTHER lever is
   * played well — otherwise the sweep is measuring a badly-run restaurant, not a bad
   * decision. */
  var flat = [];
  results.forEach(function(row){ row.forEach(function(cell){ flat.push(cell); }); });

  var best = flat[0];
  flat.forEach(function(c){ if(c.lng.net > best.lng.net) best = c; });

  console.log("");
  console.log("  best long-run: " + rowLabel(best.rv) + " x " + colLabel(best.cv) +
              "  -> " + cash(best.lng.net) +
              "   (covers/day " + best.lng.covers.toFixed(1) +
              ", food " + Math.round(best.lng.foodPct*100) + "%" +
              ", standing " + Math.round(best.lng.standing*100) + ")");

  // Conditioned on the winning column, is the row lever a real decision?
  var colOfBest = results.map(function(row){
    return row.filter(function(c){ return c.cv === best.cv; })[0];
  });
  verdict("  " + rowLabel(best.rv).replace(/[0-9.]+/g,"").trim() || "row", colOfBest, rowLabel,
          "held at " + colLabel(best.cv));

  var rowOfBest = results.filter(function(row){ return row[0].rv === best.rv; })[0];
  verdict("  column", rowOfBest, colLabel, "held at " + rowLabel(best.rv));

  // Anything whose sign flips between the fortnight and the year.
  flat.forEach(function(c){
    if((c.srt.net > 0) !== (c.lng.net > 0)){
      notes.push("SIGN FLIPS: " + title + " at " + rowLabel(c.rv) + " x " + colLabel(c.cv) +
                 " reads " + cash(c.srt.net) + " over " + SHORT + " days and " +
                 cash(c.lng.net) + " over " + LONG + ". The fortnight lies.");
    }
  });
}

function verdict(what, cells, label, condition){
  if(cells.length < 2 || cells.some(function(c){ return !c; })) return;

  var bi = 0;
  for(var i=1;i<cells.length;i++) if(cells[i].lng.net > cells[bi].lng.net) bi = i;

  var v = bi === 0 ? "TRAP — the lowest setting wins even when everything else is played well"
        : bi === cells.length-1 ? "NOT A DECISION — more is always better, so it is a purchase and not a choice"
        : "a real decision — best in the middle";

  var pick = label(cells[bi].rv !== undefined && cells[0].rv !== cells[1].rv ? cells[bi].rv : cells[bi].cv);
  console.log(what + ", " + condition + ": " + v + " (" + pick + ")");

  if(bi === 0 || bi === cells.length-1){
    notes.push(v.split(" —")[0] + ": " + what.trim() + " " + condition + " — best is " + pick);
  }
}

console.log("EVERY LEVER AGAINST EVERY OTHER, " + SHORT + " DAYS AND " + LONG + " DAYS");
console.log("Suburban High Street, one dinner service, starting from an unknown new restaurant.");

/* THE ONE THAT MATTERS MOST: sourcing only pays if you charge for it. */
grid("Ingredients against price",
     "supplier", ["budget-wholesale","valley-produce","premium-harvest"],
     function(v){ return SUPPLIERS.filter(function(s){return s.id===v;})[0].name.split(" ")[0]; },
     "price", [1.0,1.2,1.4,1.6],
     function(v){ return v.toFixed(1)+"x"; });

/* Hands against machines — the two halves of the pass. */
grid("Cooks against ovens",
     "cooks", [1,2,3,4],  function(v){ return v+" cook"+(v===1?"":"s"); },
     "ovens", [1,2,3,4],  function(v){ return v+" oven"+(v===1?"":"s"); });

/* The one Aaron named: is it better to stay small? */
grid("Seats against kitchen",
     "seats", [12,18,24,32,48], function(v){ return v+" seats"; },
     "ovens", [1,2,3,4],        function(v){ return v+" oven"+(v===1?"":"s"); });

/* Breadth costs ticket time and mistakes; does it still pay? */
grid("Menu size against price",
     "menu", [
       ["margherita","house-focaccia"],
       ["margherita","house-focaccia","caprese-salad"],
       ["margherita","house-focaccia","caprese-salad","sea-bass"],
       ["margherita","house-focaccia","caprese-salad","sea-bass","truffle-risotto"]
     ], function(v){ return v.length+" dishes"; },
     "price", [1.0,1.2,1.4,1.6], function(v){ return v.toFixed(1)+"x"; });

console.log("");
console.log("");
console.log("=== STUPID PATTERNS ===");
if(!notes.length){
  console.log("None found — every lever swept has an interior optimum once the others are played well.");
} else {
  var seen = {};
  notes.forEach(function(n){ if(!seen[n]){ seen[n] = 1; console.log("  * " + n); } });
}
