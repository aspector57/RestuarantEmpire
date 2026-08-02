/*
 * What does the Advisor say to a struggling restaurant, and in what order?
 *
 *     python3 tools/headless.py tools/probe-advisor.js
 */
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];
G = newGame(sub, 7777);
G.cash = 6000; G.seats = 30;
G.fittings=[{id:"t",name:"T",seats:30,comfort:0.5}]; G.seatSpend=450;
for(var i=0;i<2;i++) G.servers.push({id:"s"+i,name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5});
for(var i=0;i<2;i++) G.cooks.push({id:"c"+i,name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});
["oven","garde-manger"].forEach(function(st){
  var m=EQUIPMENT.filter(function(e){return e.station===st;})[0];
  G.stations[st]=[{id:m.id,speed:m.speed,foot:m.foot,capacity:0}]; });
G.stations["cold-storage"]=[{id:"c",speed:1,foot:90,capacity:3000}];
G.stations["dry-storage"]=[{id:"d",speed:1,foot:34,capacity:4000}];
// BUY the opening stock rather than conjuring it, or the books show food binned that was
// never purchased -- which produced "669% of the food bill" once.
RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 300); });

for(var i=0;i<20;i++) runDay();
console.log("After 20 days — cash " + Math.round(G.cash) + ", covers/day ~" + G.recent[G.recent.length-1].covers);
console.log("");
var list = advise();
console.log("ADVISOR (" + list.length + " things, most urgent first):");
list.forEach(function(a){
  console.log("  [" + a.id + "] " + a.head);
  console.log("        " + a.body);
  if(a.buy) console.log("        -> offers: " + a.buy.name + " $" + a.buy.cost);
});
console.log("");
console.log("--- a healthy restaurant should get silence ---");
G.cash = 200000;
for(var st of ["oven","garde-manger"]) { var m=EQUIPMENT.filter(function(e){return e.station===st;})[1];
  G.stations[st]=[]; for(var u=0;u<4;u++) G.stations[st].push({id:m.id,speed:m.speed,foot:m.foot,capacity:0}); }
G.suggest = null;
console.log("  " + advise().length + " things to say");

console.log("");
console.log("--- the 669% ---");
console.log("  G.spoiled      = " + G.spoiled.toFixed(2) + "   (value of stock binned)");
console.log("  G.ledger.food  = " + G.ledger.food.toFixed(2) + "   (money spent buying food)");
console.log("  G.ledger.revenue = " + G.ledger.revenue.toFixed(2));
console.log("  G.ledger.labor   = " + G.ledger.labor.toFixed(2));
