/*
 * Does the browser build's forecast match the night the browser build actually runs?
 *
 * The C# equivalent (`ForecastTests`) lands within 12% of the C# service. This is the same
 * question asked of the port, and the two answers differing is the point of asking.
 *
 *     python3 tools/headless.py tools/probe-forecast.js
 */

function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
var suburban = SITES.filter(function(s){return s.id==="suburban-high-street";})[0]||SITES[SITES.length-1];
function build(seats,cooks,units,seed){
  G=newGame(suburban,seed); G.cash=400000; G.seats=seats;
  G.fittings=[{id:"t",name:"T",seats:seats,comfort:0.5}]; G.seatSpend=seats*15;
  for(var i=0;i<3;i++) G.servers.push({id:"s"+i,name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5});
  for(var i=0;i<cooks;i++) G.cooks.push({id:"c"+i,name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});
  var need={}; RECIPES.forEach(function(r){ if(G.onMenu.has(r.id)) need[r.station]=true; });
  for(var st in need){ var m=EQUIPMENT.filter(function(e){return e.station===st;})[0]; if(!m) continue;
    G.stations[st]=[]; for(var u=0;u<units;u++) G.stations[st].push({id:m.id,speed:m.speed,foot:m.foot,capacity:m.capacity||0}); }
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) G.pantry[k]=[{qty:400,day:G.day}]; });
}
var shapes=[[40,4,3,"balanced"],[12,5,4,"tiny room"],[60,2,1,"short kitchen"],[30,3,2,"modest"]];
var errs=[];
for(var s=0;s<shapes.length;s++) for(var seed=0;seed<3;seed++){
  build(shapes[s][0],shapes[s][1],shapes[s][2],4242+seed*977);
  var f=forecastDay(), d=runDay(), a=autopsy(f,d); errs.push(a.err);
  if(seed===0) console.log(pad(shapes[s][3],15)+"forecast "+pad(Math.round(f.covers),5)+" actual "+pad(d.covers,5)+
    "("+Math.round(a.err*100)+"% out, bound by "+f.constraint+")");
}
errs.sort(function(a,b){return a-b;});
console.log("");
console.log("median "+Math.round(errs[Math.floor(errs.length/2)]*100)+"%   worst "+Math.round(errs[errs.length-1]*100)+"%");
