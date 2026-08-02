/*
 * Does a dish's sales trend show what the player's decision did to it?
 *
 *     python3 tools/headless.py tools/probe-trends.js
 */
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];
G = newGame(sub, 2024);
G.cash = 60000; G.seats = 34;
G.fittings=[{id:"t",name:"T",seats:34,comfort:0.5}]; G.seatSpend=510;
for(var i=0;i<2;i++) G.servers.push({id:"s"+i,name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5});
for(var i=0;i<3;i++) G.cooks.push({id:"c"+i,name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});
["oven","garde-manger","grill"].forEach(function(st){
  var m=EQUIPMENT.filter(function(e){return e.station===st;})[1];
  G.stations[st]=[]; for(var u=0;u<3;u++) G.stations[st].push({id:m.id,speed:m.speed,foot:m.foot,capacity:0}); });
G.stations["cold-storage"]=[{id:"c",speed:1,foot:90,capacity:3000}];
G.stations["dry-storage"]=[{id:"d",speed:1,foot:34,capacity:4000}];
RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 200); });

for(var i=0;i<20;i++) runDay();
var t1 = dishTrend("margherita");
console.log("Margherita, first 20 days: " + t1.now.toFixed(1) + "/day  (was " + t1.was.toFixed(1) + ")");

// Now double its price and note the decision, exactly as the UI does.
G.prices["margherita"] = G.prices["margherita"] * 2;
noteDecision("margherita", "price set to $" + G.prices["margherita"].toFixed(2));
for(var i=0;i<20;i++) runDay();

var t2 = dishTrend("margherita");
var dir = Math.abs(t2.change) < 0.10 ? "steady" : (t2.change > 0 ? "up " : "down ") + Math.round(Math.abs(t2.change)*100) + "%";
console.log("After doubling the price:  " + t2.now.toFixed(1) + "/day  " + dir);
console.log("  cause shown: \"after you " + (t2.since.length ? t2.since[t2.since.length-1].what + ", day " + t2.since[t2.since.length-1].day : "(none)") + "\"");
console.log("");
console.log("A dish nobody touched, for contrast:");
var t3 = dishTrend("caprese-salad");
console.log("  Caprese " + t3.now.toFixed(1) + "/day, " + (t3.since.length ? "changed" : "no changes lately — this is the market moving"));
console.log("");
console.log("sparkline renders: " + (sparkline(t2.line, 96, 22).indexOf("<svg") === 0 ? "yes" : "NO"));
console.log("history length: " + G.dishHistory["margherita"].length + " days");
