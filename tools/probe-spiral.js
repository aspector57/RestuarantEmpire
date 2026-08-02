// Aaron's day 149: many stations, few cooks, walkouts every night, buying more equipment.
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];
G = newGame(sub, 0x5af4c5cc);
G.cash = 10535; G.seats = 34;
G.fittings=[{id:"t",name:"T",seats:34,comfort:0.5}]; G.seatSpend=510;
G.servers=[{id:"s0",name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5},
           {id:"s1",name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5}];
G.cooks=[{id:"c0",name:"C",role:"cook",wage:14,skill:0.25,claim:0.4,potential:0.4},
         {id:"c1",name:"C",role:"cook",wage:14,skill:0.25,claim:0.4,potential:0.4}];
// The seven purchases from his log.
["oven","garde-manger"].forEach(function(st){
  var m=EQUIPMENT.filter(function(e){return e.station===st;})[0];
  G.stations[st]=[]; for(var u=0;u<4;u++) G.stations[st].push({id:m.id,speed:m.speed,foot:m.foot,capacity:0}); });
G.stations["cold-storage"]=[{id:"c",speed:1,foot:90,capacity:3000}];
G.stations["dry-storage"]=[{id:"d",speed:1,foot:34,capacity:4000}];
RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 150); });

var lim = passLimit("dinner");
console.log("4 ovens, 4 cold sections, 2 cooks at 1.2/5:");
console.log("  the pass is limited by: " + lim.kind.toUpperCase() + " — about " + Math.round(lim.allows) + " covers/hour");
console.log("");
for(var i=0;i<3;i++) runDay();
var d = G.recent[G.recent.length-1];
var int_ = checkInterrupts(G.today);
console.log("INTERRUPT NOW SAYS:");
if(int_){ console.log("  " + int_.title); console.log("  " + int_.body); console.log("  " + int_.why);
  console.log("  actions: " + int_.acts.map(function(a){return a.label;}).join(" | ")); }
else console.log("  (none fired)");
console.log("");
console.log("ADVISOR NOW SAYS:");
advise().forEach(function(a){ console.log("  [" + a.id + "] " + a.head); console.log("        " + a.body.replace(/\*\*/g,"")); });

console.log("");
console.log("--- the interrupt, on a night like his (39 walkouts, 21 covers) ---");
var fake = { covers:21, balkedWait:12, walkouts:39, balkedPrice:3, labor:400, revenue:287, food:80, noTable:20, lostMenu:0 };
var i2 = checkInterrupts(fake);
if(i2){
  console.log("  " + i2.title);
  console.log("  " + i2.body);
  console.log("  " + i2.why);
  console.log("  actions: " + i2.acts.map(function(a){return a.label;}).join(" | "));
} else console.log("  (none)");

console.log("");
console.log("--- and with a properly staffed kitchen, the same night ---");
for(var i=0;i<5;i++) G.cooks.push({id:"x"+i,name:"C",role:"cook",wage:20,skill:0.8,claim:0.8,potential:0.9});
var lim2 = passLimit("dinner");
console.log("  limited by: " + lim2.kind.toUpperCase() + " — " + Math.round(lim2.allows) + " covers/hour");
var i3 = checkInterrupts(fake);
if(i3){ console.log("  " + i3.body); }
