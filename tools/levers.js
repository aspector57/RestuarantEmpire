/*
 * EVERY LEVER, SWEPT ACROSS ITS WHOLE RANGE, ONE AT A TIME.
 *
 * Aaron: "we need you to be able to test like everything — price changes, hiring, adding seats,
 * lowering costs, upgrading and downgrading our ingredients, etc."
 *
 * The point is not just "what does this do". It is whether each lever is a REAL DECISION. A
 * lever whose best setting is at the top of its range is not a choice, it is a purchase you
 * make whenever you can afford it. One whose best setting is at the bottom is a trap. Only a
 * lever with an interior optimum asks the player anything — and "flat scaling: bigger numbers
 * are not new decisions" is on this project's own anti-pattern list.
 *
 * Everything else is held fixed while one thing moves, and every run starts from identical
 * state. Confounded comparisons have produced two false findings on this project already.
 *
 *     python3 tools/headless.py tools/levers.js
 */

var DAYS = 120;
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
function cash(n){ return (n<0?"-$":"$") + Math.abs(Math.round(n)).toLocaleString(); }

var sub = SITES.filter(function(s){ return s.id === "suburban-high-street"; })[0];

/* The baseline every sweep varies from: a sensible small restaurant. */
var BASE = {
  price: 1.0, cooks: 2, servers: 2, seats: 24, supplier: "valley-produce",
  oven: "oven-secondhand", menu: ["margherita","house-focaccia","caprese-salad"],
  licence: false, ovens: 2
};

function play(over){
  var cfg = {}; for(var k in BASE) cfg[k] = BASE[k];
  for(var k2 in over) cfg[k2] = over[k2];

  G = newGame(sub, 20240802);
  G.cash = 200000;                    // capital is not the variable here
  G.seats = cfg.seats;
  G.fittings = [{id:"t", name:"Tables", seats:cfg.seats, comfort:0.55}];
  G.seatSpend = cfg.seats*15;
  G.supplier = cfg.supplier;
  G.licence = cfg.licence;
  G.onMenu = new Set(cfg.menu);

  G.servers = []; for(var i=0;i<cfg.servers;i++) G.servers.push({id:"s"+i,name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5});
  G.cooks   = []; for(var j=0;j<cfg.cooks;  j++) G.cooks.push({id:"c"+j,name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});

  var ovenModel = EQUIPMENT.filter(function(e){ return e.id === cfg.oven; })[0];
  G.stations = {};
  G.stations["oven"] = [];
  for(var u=0;u<cfg.ovens;u++) G.stations["oven"].push({id:ovenModel.id, speed:ovenModel.speed, foot:ovenModel.foot, capacity:0});
  ["garde-manger","grill","bar","saute","coffee"].forEach(function(st){
    var need = RECIPES.some(function(r){ return G.onMenu.has(r.id) && r.station === st; });
    if(!need) return;
    var m = EQUIPMENT.filter(function(e){ return e.station === st; })[1] || EQUIPMENT.filter(function(e){ return e.station === st; })[0];
    G.stations[st] = [{id:m.id, speed:m.speed, foot:m.foot, capacity:0}, {id:m.id, speed:m.speed, foot:m.foot, capacity:0}];
  });
  G.stations["cold-storage"] = [{id:"cold-walkin", speed:1, foot:90, capacity:3000}];
  G.stations["dry-storage"]  = [{id:"dry-racking", speed:1, foot:34, capacity:4000}];
  G.floorArea = 3000;                 // floor is not the variable either

  RECIPES.forEach(function(r){ if(G.onMenu.has(r.id)) G.prices[r.id] = Math.round(r.base*cfg.price*100)/100; });
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 150); });
  G.rep.standing = 0.5; G.rep.meals = 12000;     // established, so awareness is not the variable

  var rev=0, food=0, cov=0, walk=0;
  for(var d=0; d<DAYS; d++){
    var r = runDay();
    rev += r.revenue; food += r.food; cov += r.covers; walk += r.walkouts;
  }
  // Take the labour the SIMULATION actually booked, never recompute it. Recomputing it at an
  // eight-hour shift against a five-hour service inflated wages 60% and turned every staffing
  // lever into a fake trap — a harness that recalculates what the game already knows is just
  // a second implementation waiting to disagree.
  var labor = G.ledger.labor;
  var rent  = sub.rent * (DAYS/30) + (cfg.licence ? LICENCE_RENEWAL*(DAYS/30) : 0);
  return { net: rev - food - labor - rent, covers: cov/DAYS, walk: walk/DAYS,
           rev: rev/DAYS, foodPct: rev>0 ? food/rev : 0 };
}

/* A lever is only a decision if its best setting is not at either end. */
function sweep(name, key, values, label){
  console.log("");
  console.log(name.toUpperCase());
  console.log("  " + pad("setting",22)+pad("net /"+DAYS+"d",13)+pad("covers/day",12)+pad("walkouts",10)+pad("food%",8)+"revenue/day");

  var results = values.map(function(v){
    var over = {}; over[key] = v;
    var r = play(over);
    console.log("  " + pad(label ? label(v) : v, 22) + pad(cash(r.net),13) +
                pad(r.covers.toFixed(1),12) + pad(r.walk.toFixed(1),10) +
                pad(Math.round(r.foodPct*100)+"%",8) + "$"+Math.round(r.rev));
    return { v:v, net:r.net };
  });

  var best = 0;
  for(var i=1;i<results.length;i++) if(results[i].net > results[best].net) best = i;
  var verdict = best === 0 ? "TRAP — the lowest setting wins; using this lever at all costs you money"
              : best === results.length-1 ? "NOT A DECISION — more is always better, so it is a purchase not a choice"
              : "a real decision — best in the middle at " + (label ? label(results[best].v) : results[best].v);
  var spread = Math.round(results[best].net - Math.min.apply(null, results.map(function(r){return r.net;})));
  console.log("  -> " + verdict + "  (worth " + cash(spread) + " over " + DAYS + " days)");
}

console.log("EVERY LEVER, " + DAYS + " DAYS EACH, ONE THING MOVED AT A TIME");
console.log("Baseline: " + BASE.seats + " seats, " + BASE.cooks + " cooks, " + BASE.servers +
            " servers, " + BASE.ovens + " second-hand ovens, valley produce, 3 dishes, prices as designed.");

sweep("Price",              "price",    [0.8,1.0,1.2,1.4,1.6,1.8,2.0], function(v){ return v.toFixed(1)+"x designed"; });
sweep("Cooks",              "cooks",    [1,2,3,4,5,6],                 function(v){ return v + " cook" + (v===1?"":"s"); });
sweep("Servers",            "servers",  [1,2,3,4],                     function(v){ return v + " server" + (v===1?"":"s"); });
sweep("Seats",              "seats",    [12,18,24,32,40,60],           function(v){ return v + " seats"; });
/* SOURCING CANNOT BE COMPARED AT ONE PRICE, and doing so is how this harness reported
   "ingredients are a trap" while probe-price-optimum showed the opposite. Better ingredients
   buy standing, standing is what lets you charge more, and judging them all at 1.0x measures
   the cost and none of the benefit. Each supplier is swept at ITS OWN best price. */
console.log("");
console.log("INGREDIENTS — each supplier at its own best price, because comparing at one price");
console.log("measures what they cost and not what they buy you.");
console.log("  " + pad("supplier",22)+pad("best price",13)+pad("net /"+DAYS+"d",13)+pad("covers/day",12)+pad("standing",10)+"food%");
var sourcing = ["budget-wholesale","valley-produce","premium-harvest"].map(function(sup){
  var best = null;
  [1.0,1.1,1.2,1.3,1.4,1.5,1.6].forEach(function(pr){
    var r = play({ supplier:sup, price:pr });
    if(!best || r.net > best.net) best = { net:r.net, price:pr, covers:r.covers, foodPct:r.foodPct, standing:G.rep.standing };
  });
  console.log("  " + pad(SUPPLIERS.filter(function(s){return s.id===sup;})[0].name,22) +
              pad(best.price.toFixed(1)+"x",13) + pad(cash(best.net),13) +
              pad(best.covers.toFixed(1),12) + pad(Math.round(best.standing*100)+"/100",10) +
              Math.round(best.foodPct*100)+"%");
  return { sup:sup, net:best.net };
});
var bestSup = 0;
for(var si=1; si<sourcing.length; si++) if(sourcing[si].net > sourcing[bestSup].net) bestSup = si;
console.log("  -> " + (bestSup === 0
  ? "budget wins even when everyone is priced properly — sourcing well does not pay"
  : bestSup === sourcing.length-1
    ? "premium wins outright"
    : "a real decision — the middle tier wins once each is priced for what it is"));
sweep("Oven",               "oven",     ["oven-secondhand","oven-commercial","oven-hearth"],
      function(v){ var m = EQUIPMENT.filter(function(e){return e.id===v;})[0]; return m ? m.name : v; });
sweep("How many ovens",     "ovens",    [1,2,3,4,5],                   function(v){ return v + " oven" + (v===1?"":"s"); });
sweep("Menu size",          "menu",     [
        ["margherita","house-focaccia"],
        ["margherita","house-focaccia","caprese-salad"],
        ["margherita","house-focaccia","caprese-salad","sea-bass"],
        ["margherita","house-focaccia","caprese-salad","sea-bass","truffle-risotto"],
        ["margherita","house-focaccia","caprese-salad","sea-bass","truffle-risotto","eggs-benedict"]
      ], function(v){ return v.length + " dishes"; });

console.log("");
console.log("DRINKS (needs both the licence and a bar on the card)");
["dry","licensed"].forEach(function(mode){
  var over = mode === "dry" ? {}
    : { licence:true, menu:["margherita","house-focaccia","caprese-salad","house-wine","negroni"] };
  var r = play(over);
  console.log("  " + pad(mode === "dry" ? "no bar" : "licensed + bar", 22) + pad(cash(r.net),13) +
              pad(r.covers.toFixed(1),12) + pad(r.walk.toFixed(1),10) +
              pad(Math.round(r.foodPct*100)+"%",8) + "$"+Math.round(r.rev));
});
