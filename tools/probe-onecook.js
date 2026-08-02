/*
 * Can one cook keep up with twelve seats? Aaron asked, reasonably, why a tiny room could not be
 * served by a small kitchen.
 *
 *     python3 tools/headless.py tools/probe-onecook.js
 */
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];

function build(seats, cooks, ovens, ovenId){
  G = newGame(sub, 0x5ae48672);
  G.cash = 19565; G.seats = seats;
  G.fittings=[{id:"t",name:"T",seats:seats,comfort:0.56}]; G.seatSpend=seats*15;
  G.servers=[{id:"s0",name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5},
             {id:"s1",name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5}];
  G.cooks=[]; for(var i=0;i<cooks;i++) G.cooks.push({id:"c"+i,name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});
  var oven=EQUIPMENT.filter(function(e){return e.id===(ovenId||"oven-secondhand");})[0];
  var prep=EQUIPMENT.filter(function(e){return e.station==="garde-manger";})[0];
  G.stations["oven"]=[]; for(var u=0;u<ovens;u++) G.stations["oven"].push({id:oven.id,speed:oven.speed,foot:oven.foot,capacity:0});
  G.stations["garde-manger"]=[{id:prep.id,speed:prep.speed,foot:prep.foot,capacity:0}];
  G.stations["cold-storage"]=[{id:"c",speed:1,foot:16,capacity:3000}];
  G.stations["dry-storage"]=[{id:"d",speed:1,foot:10,capacity:4000}];
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 200); });
}
function run(days){
  var c=0,w=0,b=0;
  for(var i=0;i<days;i++){ var d=runDay(); c+=d.covers; w+=d.walkouts; b+=d.balkedWait; }
  return {covers:c/days, walkouts:w/days, balked:b/days};
}

console.log("THE ARITHMETIC, 12 seats, one 5-hour dinner service:");
console.log("  a seat turns every 45 min -> 12 x (300/45) = 80 seatings a night");
console.log("  one cook holds 2 plates at once; a margherita on a 0.75x oven takes 12 min");
console.log("  -> 2 plates / 12 min = 10 plates an hour = 50 a night");
console.log("  so the room offers 80 and the cook can make 50. The cook is short.");
console.log("");
console.log(pad("setup",30)+pad("covers/day",12)+pad("walkouts",11)+"put off by wait");
[["12 seats, 1 cook, 3 old ovens",12,1,3,null],
 ["12 seats, 2 cooks, 3 old ovens",12,2,3,null],
 ["12 seats, 1 cook, 1 old oven",12,1,1,null],
 ["12 seats, 1 cook, FAST oven",12,1,1,"oven-hearth"],
 ["40 seats, 3 cooks, 3 old ovens",40,3,3,null]].forEach(function(row){
  build(row[1],row[2],row[3],row[4]);
  var r = run(20);
  console.log(pad(row[0],30)+pad(r.covers.toFixed(1),12)+pad(r.walkouts.toFixed(1),11)+r.balked.toFixed(1));
});
