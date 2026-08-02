/*
 * "Is a cover a seat? If so they should be able to serve 12 seats with 2 people, right?" (Aaron)
 *
 * Nearly. One cook does 14 covers an hour against a room that turns 16 — a small gap that
 * produces most of the walkouts. Two cooks clears it; a third adds nothing.
 *
 *     python3 tools/headless.py tools/probe-onecook-vs-room.js
 */
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];
function build(cooks, ovens){
  G = newGame(sub, 0x5ad75861);
  G.cash = 21793; G.seats = 12;
  G.fittings=[{id:"t",name:"T",seats:12,comfort:0.56}]; G.seatSpend=180;
  G.servers=[{id:"s0",name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5}];
  G.cooks=[]; for(var i=0;i<cooks;i++) G.cooks.push({id:"c"+i,name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});
  var oven=EQUIPMENT.filter(function(e){return e.id==="oven-secondhand";})[0];
  var prep=EQUIPMENT.filter(function(e){return e.station==="garde-manger";})[0];
  G.stations["oven"]=[]; for(var u=0;u<ovens;u++) G.stations["oven"].push({id:oven.id,speed:oven.speed,foot:oven.foot,capacity:0});
  G.stations["garde-manger"]=[{id:prep.id,speed:prep.speed,foot:prep.foot,capacity:0}];
  G.stations["cold-storage"]=[{id:"c",speed:1,foot:16,capacity:600}];
  G.stations["dry-storage"]=[{id:"d",speed:1,foot:10,capacity:900}];
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 200); });
}
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }

console.log("Can 12 seats be served by one cook?  (a cover = a seat = one person)");
console.log("  12 seats, a 45-minute sitting -> the room turns about 16 covers an hour");
console.log("");
console.log(pad("brigade",12)+pad("cook does/hr",14)+pad("room turns/hr",15)+pad("covers/day",12)+"walkouts/day");
[1,2,3].forEach(function(n){
  build(n, 3);
  var lim = passLimit("dinner");
  var room = Math.round(servableSeats()*(60/DWELL));
  var c=0,w=0; for(var i=0;i<20;i++){ var d=runDay(); c+=d.covers; w+=d.walkouts; }
  console.log(pad(n+" cook"+(n===1?"":"s"),12)+pad(Math.round(lim.allows),14)+pad(room,15)+pad((c/20).toFixed(1),12)+(w/20).toFixed(1));
});
console.log("");
build(1,3);
console.log("Interrupt and Build tab now agree:");
console.log("  interrupt: " + Math.round(passLimit("dinner").allows) + " covers/hour");
console.log("  build tab: " + balanceNote());
