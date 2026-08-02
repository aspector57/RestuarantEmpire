/*
 * Does adding tables help? Not if the kitchen cannot feed them.
 *
 * 12 seats -> 68.8 covers/day and 6.1 walkouts. 20 seats -> 56.8 covers and 26.2 walkouts.
 * A guest turned away at the door costs the sale; one who sits and gives up costs the plate,
 * the food, the table they held and your reputation.
 *
 *     python3 tools/headless.py tools/probe-seats-vs-kitchen.js
 */
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];

function build(seats){
  G = newGame(sub, 0x5ad27d92);
  G.cash = 52824; G.seats = seats;
  G.fittings=[{id:"t",name:"T",seats:seats,comfort:0.56}]; G.seatSpend=seats*15;
  G.servers=[]; for(var i=0;i<3;i++) G.servers.push({id:"s"+i,name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5});
  G.cooks=[]; for(var i=0;i<2;i++) G.cooks.push({id:"c"+i,name:"C",role:"cook",wage:16,skill:0.42,claim:0.5,potential:0.5});
  var oven=EQUIPMENT.filter(function(e){return e.id==="oven-secondhand";})[0];
  var prep=EQUIPMENT.filter(function(e){return e.station==="garde-manger";})[0];
  G.stations["oven"]=[]; for(var u=0;u<3;u++) G.stations["oven"].push({id:oven.id,speed:oven.speed,foot:oven.foot,capacity:0});
  G.stations["garde-manger"]=[{id:prep.id,speed:prep.speed,foot:prep.foot,capacity:0}];
  G.stations["cold-storage"]=[{id:"c",speed:1,foot:16,capacity:3000}];
  G.stations["dry-storage"]=[{id:"d",speed:1,foot:10,capacity:4000}];
  G.rep.standing = 0.50; G.rep.meals = 12000;
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 300); });
}

console.log("FRESH RUN PER CONFIGURATION, 30 days each, 2 cooks throughout");
console.log("  " + pad("seats",8)+pad("room/hr",9)+pad("pass/hr",9)+pad("covers/day",12)+pad("walkouts",10)+pad("turned away",13)+"revenue/day");
[12,16,20,30,40,60].forEach(function(s){
  build(s);
  var lim = passLimit("dinner");
  var room = Math.round(servableSeats()*(60/DWELL));
  var c=0,w=0,n=0,r=0;
  for(var i=0;i<30;i++){ var d=runDay(); c+=d.covers; w+=d.walkouts; n+=d.noTable; r+=d.revenue; }
  console.log("  " + pad(s,8)+pad(room,9)+pad(Math.round(lim.allows),9)+pad((c/30).toFixed(1),12)+
              pad((w/30).toFixed(1),10)+pad((n/30).toFixed(1),13)+"$"+Math.round(r/30));
});
