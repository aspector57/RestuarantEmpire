var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];
G = newGame(sub, 0x5adbfdbb);
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
RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 120); });

// A handful of real decisions, the kind that never used to appear.
for(var i=0;i<3;i++) runDay();
G.prices["margherita"] = 17.5; noteDecision("margherita", "price set to $17.50");
buyEquip(EQUIPMENT.filter(function(e){return e.id==="oven-secondhand";})[0]);
for(var i=0;i<2;i++) runDay();
G.onMenu.add("sea-bass"); noteDecision("sea-bass", "put on the menu");
G.supplier = "premium-harvest";
didThat("Sourcing switched from Valley Produce Co. to Premium Harvest Partners (tier 5).");
G.standingOrder = false; didThat("Standing order switched off — ordering by hand now.");
for(var i=0;i<2;i++) runDay();

console.log("WHAT I DID (the filter that did not exist):");
G.log.filter(function(e){return e.kind==="you";}).forEach(function(e){
  console.log("  d" + String(e.day).padStart(3,"0") + "  " + e.text);
});
console.log("");
console.log("total log entries: " + G.log.length + " (cap was 90, now 400)");
