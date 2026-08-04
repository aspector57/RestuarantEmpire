/*
 * Do the forecast, the Build tab and the shared pass model all say the same thing?
 *
 * They did not: Aaron's day 35 read "kitchen-bound, the pass is the limit" in the forecast and
 * "the room is the bottleneck, buy tables before anything else" in Build, on one screen, with
 * one cook. Run this after touching anything that reasons about capacity.
 *
 *     python3 tools/headless.py tools/probe-agreement.js
 */
// Aaron's day 35: 3 ovens, 1 prep bench, 12 seats, ONE cook, ONE server, dinner 18-23.
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];
G = newGame(sub, 0x5ae48672);
G.cash = 19565; G.seats = 12;
G.fittings=[{id:"t",name:"T",seats:12,comfort:0.56}]; G.seatSpend=180;
G.servers=[{id:"s0",name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5}];
G.cooks=[{id:"c0",name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5}];
var oven=EQUIPMENT.filter(function(e){return e.id==="oven-secondhand";})[0];
var prep=EQUIPMENT.filter(function(e){return e.station==="garde-manger";})[0];
G.stations["oven"]=[]; for(var u=0;u<3;u++) G.stations["oven"].push({id:oven.id,speed:oven.speed,foot:oven.foot,capacity:0});
G.stations["garde-manger"]=[{id:prep.id,speed:prep.speed,foot:prep.foot,capacity:0}];
G.stations["cold-storage"]=[{id:"c",speed:1,foot:16,capacity:600}];
G.stations["dry-storage"]=[{id:"d",speed:1,foot:10,capacity:900}];
RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 120); });
for(var i=0;i<20;i++) runDay();

var f = forecastDay();
console.log("FORECAST : " + f.constraint + "-bound   (street " + Math.round(f.demand) + ", seats " + f.seatCeil + ", pass " + f.kitchenCeil + ")");
console.log("BUILD    : " + balanceNote());
// bindingConstraint answers "what limits the RESTAURANT". passLimit answers the narrower
// "what limits the PASS" and knows nothing about the room, so quoting it here compared the
// wrong two things — the probe reported a contradiction that was not one.
var bind = bindingConstraint("dinner");
console.log("SHARED   : limited by " + bind.kind.toUpperCase() +
            "  (room " + Math.round(bind.room) + "/hr, pass " + Math.round(bind.pass) + "/hr)");
console.log("");

// A CONTRADICTION IS THEM POINTING OPPOSITE WAYS, not one of them saying "about right".
// The old test failed whenever the two halves were near parity — which is the healthiest a
// restaurant can be — because it treated "neither dominates" as disagreement.
var note = balanceNote();
var buildSaysRoom    = note.indexOf("the room is the bottleneck") >= 0;
var buildSaysKitchen = note.indexOf("is the bottleneck") >= 0 && !buildSaysRoom;
var clash = (f.constraint === "kitchen" && buildSaysRoom) ||
            (f.constraint === "seats"   && buildSaysKitchen);
console.log("AGREE? " + (clash ? "NO — STILL CONTRADICTING" : "yes"));
console.log("");
var burn = monthlyBurn();
var takings = G.recent.slice(-14).reduce(function(a,x){return a+(x.revenue||0);},0)/14*30;
console.log("RUNWAY: cash " + Math.round(G.cash) + ", taking ~$" + Math.round(takings) + "/mo, net burn $" + Math.round(burn) + "/mo");
console.log("  -> " + (burn > 0 ? (G.cash/burn).toFixed(1) + " months" : "not losing money — no runway problem"));
console.log("");
console.log("ADVISOR:");
advise().forEach(function(a){ console.log("  [" + a.id + "] " + a.head); });
