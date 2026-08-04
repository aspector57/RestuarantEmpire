/*
 * Does the Advisor suggest a second restaurant only when this one is genuinely finished?
 *
 * The first version asked "is demand high", which is true of every under-built restaurant --
 * so it told a twelve-seat opening to go and buy a second site. A pitch is finished when there
 * is nowhere left for a table, a unit, or another wall.
 *
 *     python3 tools/headless.py tools/probe-expand.js
 */
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];
function build(cash, seats, cooks, servers, ovens){
  G = newGame(sub, 555);
  G.cash=cash; G.seats=seats;
  G.fittings=[{id:"t",name:"T",seats:seats,comfort:0.7}]; G.seatSpend=seats*18; G.floorArea=900;
  G.servers=[]; for(var i=0;i<servers;i++) G.servers.push({id:"s"+i,name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5});
  G.cooks=[];   for(var i=0;i<cooks;i++)   G.cooks.push({id:"c"+i,name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});
  var oven=EQUIPMENT.filter(function(e){return e.id==="oven-secondhand";})[0];
  var gm=EQUIPMENT.filter(function(e){return e.station==="garde-manger";})[0];
  G.stations["oven"]=[]; for(var u=0;u<ovens;u++) G.stations["oven"].push({id:oven.id,speed:oven.speed,foot:oven.foot,capacity:0,holds:oven.holds});
  G.stations["garde-manger"]=[{id:gm.id,speed:gm.speed,foot:gm.foot,capacity:0,holds:gm.holds}];
  G.stations["cold-storage"]=[{id:"c",speed:1,foot:90,capacity:3000}];
  G.stations["dry-storage"]=[{id:"d",speed:1,foot:34,capacity:4000}];
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k,300); });
  for(var n=0;n<20;n++){ runDay(); G.day++; }
}
console.log(pad("situation",34)+pad("cash",12)+"does it say expand?");
[["packed and rich",        460000, 32, 3, 2, 3],
 ["packed but only just solvent", 9000, 32, 3, 2, 3],
 ["rich but the room is empty",  460000, 90, 3, 6, 4],
 ["rich, brand new, tiny",       460000, 12, 1, 1, 1],
 ["MAXED OUT: full site, packed", 460000, 150, 6, 8, 8]].forEach(function(c){
  build(c[1],c[2],c[3],c[4],c[5]);
  // The maxed case: the building is as big as the site allows and every foot is spoken for.
  if(c[0].indexOf("MAXED") === 0){
    G.floorArea = G.site.maxArea;
    G.seatSpend = G.floorArea - usedArea() + G.seatSpend - 4;   // leave 4 sq ft free
    for(var n=0;n<10;n++){ runDay(); G.day++; }
  }
  var has = advise().some(function(a){ return a.id === "expand"; });
  console.log("  "+pad(c[0],32)+pad(money(G.cash),12)+(has?"YES":"no"));
});

console.log("");
console.log("--- why is the maxed case not firing? ---");
console.log("  floorArea " + G.floorArea + " of site max " + G.site.maxArea + ", free " + freeArea().toFixed(0));
console.log("  canExtend " + canExtend());
console.log("  smallest table block needs " + (FURNITURE[0].foot*10) + " sq ft");
var smallestKit = EQUIPMENT.filter(function(e){ return !STORAGE_STATIONS_JS[e.station]; })
                  .sort(function(a,b){return a.foot-b.foot;})[0];
console.log("  smallest kit needs " + smallestKit.foot + " sq ft (" + smallestKit.name + ")");
var fc = forecastDay();
console.log("  demand " + Math.round(fc.demand) + " vs covers " + Math.round(fc.covers) +
            "  -> packed " + (fc.demand > fc.covers*1.15));
console.log("  sites left: " + SITES.filter(function(s){ return !G.sites.some(function(x){return x.site.id===s.id;}); }).length);
