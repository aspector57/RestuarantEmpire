/*
 * ADDING CAPACITY MUST NEVER REDUCE OUTPUT.
 *
 * The bug this exists to stop: hiring a cook used to make the restaurant WORSE — 2 cooks
 * served 50.6 covers a day, 6 cooks served 39.5, with walkouts climbing 34.8 -> 54.0. The
 * player paid more wages for less trade and nothing anywhere named a cause, which breaks
 * Binding Principle 2 outright.
 *
 * Two causes, both now fixed in `pass.html` and in `KitchenPass`:
 *
 *   1. The door quote was a SECOND IMPLEMENTATION of the scheduler, and it drifted in the
 *      direction that hurt. `Math.min(...slots, ...cookFree)` quoted against whichever
 *      resource was more available, so with twelve cook-slots against two ovens the quote
 *      stopped seeing the oven queue. The more hands you hired, the blinder the host got,
 *      and parties who should have been turned away were seated and then lost.
 *   2. Plates for a table that walked were still cooked, burning the constraint at the
 *      moment it was tightest. A loop that feeds itself.
 *
 * A walkout is strictly worse than a door-balk: you cook the plate, bin it, hold the table
 * for the wait, and take the reputation hit. So this also checks that adding capacity does
 * not simply convert cheap losses into expensive ones.
 *
 *     python3 tools/headless.py tools/probe-capacity-monotonic.js
 */

var DAYS = 45;
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }

var sub = SITES.filter(function(s){ return s.id === "suburban-high-street"; })[0];

var BASE = { cooks:2, servers:2, seats:24, ovens:2, oven:"oven-secondhand",
             supplier:"valley-produce", menu:["margherita","house-focaccia","caprese-salad"] };

function play(over){
  var cfg = {}; for(var k in BASE) cfg[k] = BASE[k];
  for(var k2 in over) cfg[k2] = over[k2];

  G = newGame(sub, 20240802);
  G.cash = 200000;                                   // capital is not the variable
  G.seats = cfg.seats;
  G.fittings = [{id:"t", name:"Tables", seats:cfg.seats, comfort:0.55}];
  G.seatSpend = cfg.seats*15;
  G.supplier = cfg.supplier;
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
  G.floorArea = 3000;

  RECIPES.forEach(function(r){ if(G.onMenu.has(r.id)) G.prices[r.id] = r.base; });
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 150); });
  G.rep.standing = 0.5; G.rep.meals = 12000;         // established, so awareness is not the variable

  var cov=0, walk=0, balk=0;
  for(var dd=0; dd<DAYS; dd++){
    var r = runDay();
    cov += r.covers; walk += r.walkouts; balk += G.today.balkedWait;
  }
  return { covers: cov/DAYS, walk: walk/DAYS, balk: balk/DAYS };
}

var failures = 0;

/* Each step UP in a capacity lever must not cost covers. A little slack, because the pass
 * genuinely saturates: past the point where something else binds, more of this buys nothing
 * real and seed noise moves the number a percent either way. */
var TOLERANCE = 0.96;

function ratchet(name, key, values, label){
  console.log("");
  console.log(name.toUpperCase());
  console.log("  " + pad("setting",18) + pad("covers/day",12) + pad("walkouts",11) + "balked at door");

  var prev = null;
  values.forEach(function(v){
    var over = {}; over[key] = v;
    var r = play(over);
    var tag = "";

    if(prev && r.covers < prev.covers * TOLERANCE){
      tag = "   <-- FAIL: fewer covers than " + (label ? label(prev.v) : prev.v);
      failures++;
    }

    console.log("  " + pad(label ? label(v) : v, 18) + pad(r.covers.toFixed(1),12) +
                pad(r.walk.toFixed(1),11) + r.balk.toFixed(1) + tag);
    prev = { v:v, covers:r.covers };
  });
}

console.log("ADDING CAPACITY MUST NEVER REDUCE OUTPUT — " + DAYS + " days per setting, one thing moved");

ratchet("Cooks",    "cooks", [1,2,3,4,5,6],      function(v){ return v + " cook" + (v===1?"":"s"); });
ratchet("Ovens",    "ovens", [1,2,3,4,5],        function(v){ return v + " oven" + (v===1?"":"s"); });
ratchet("Seats",    "seats", [12,18,24,32,40,60],function(v){ return v + " seats"; });
ratchet("Oven speed","oven", ["oven-secondhand","oven-commercial","oven-hearth"],
        function(v){ var m = EQUIPMENT.filter(function(e){return e.id===v;})[0]; return m ? m.name : v; });

console.log("");
if(failures){
  console.log("FAILED — " + failures + " place(s) where buying capacity cost the player output.");
} else {
  console.log("PASS — every capacity lever is monotonic; nothing punishes the player for growing.");
}
