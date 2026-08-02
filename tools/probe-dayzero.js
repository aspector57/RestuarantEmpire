/*
 * What does a restaurant see before it has traded a single day?
 *
 * It used to see a death warning. Run this after touching the Advisor or any capacity readout.
 *
 *     python3 tools/headless.py tools/probe-dayzero.js
 */
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];
G = newGame(sub, 0x5ad75861);
G.cash = 20808; G.seats = 12;
G.fittings=[{id:"t",name:"T",seats:12,comfort:0.56}]; G.seatSpend=180;
G.servers=[{id:"s0",name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5}];
G.cooks=[{id:"c0",name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5}];
var oven=EQUIPMENT.filter(function(e){return e.id==="oven-secondhand";})[0];
var prep=EQUIPMENT.filter(function(e){return e.station==="garde-manger";})[0];
G.stations["oven"]=[{id:oven.id,speed:oven.speed,foot:oven.foot,capacity:0}];
G.stations["garde-manger"]=[{id:prep.id,speed:prep.speed,foot:prep.foot,capacity:0}];
G.stations["cold-storage"]=[{id:"c",speed:1,foot:16,capacity:600}];
G.stations["dry-storage"]=[{id:"d",speed:1,foot:10,capacity:900}];

RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 100); });
console.log("DAY ZERO, stocked, nothing traded yet");
console.log("  Cooking stations: " + kitchenCapacity().units + "  (was 4 — it counted the fridge and the shelving)");
console.log("  Build says: " + balanceNote());
console.log("");
console.log("  ADVISOR:");
advise().forEach(function(a){ console.log("    [" + a.id + "] " + a.head); console.log("          " + a.body.replace(/\*\*/g,"")); });
