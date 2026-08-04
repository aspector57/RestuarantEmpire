/*
 * Would the Advisor have caught Aaron's day-0 mistake?
 *
 * He opened on the Business District -- a breakfast-and-lunch street -- with the default dinner
 * hours, and traded sixty days at five to seventeen covers a night, losing money every one of
 * them, before changing the hours on day 61 and going straight to seventy covers. Cash bottomed
 * at -$4,784 on the way. The window he chose was worth 16 people an hour against 132.
 *
 *     python3 tools/headless.py tools/probe-hours.js
 */
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }

SITES.forEach(function(site){
  [["Dinner only",[{name:"Dinner",from:18,to:23}]],
   ["Breakfast + lunch",[{name:"Breakfast",from:7,to:11},{name:"Lunch",from:12,to:15}]],
   ["Lunch + dinner",[{name:"Lunch",from:12,to:15},{name:"Dinner",from:18,to:23}]]].forEach(function(h){
    G = newGame(site, 1);
    G.windows = h[1];
    G.seats=24; G.fittings=[{id:"t",name:"T",seats:24,comfort:0.55}]; G.seatSpend=360;
    G.servers=[{id:"s0",name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5}];
    G.cooks=[{id:"c0",name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5}];
    ["oven","garde-manger"].forEach(function(st){
      var m=EQUIPMENT.filter(function(e){return e.station===st;})[0];
      G.stations[st]=[{id:m.id,speed:m.speed,foot:m.foot,capacity:0,holds:m.holds||1}]; });
    G.stations["cold-storage"]=[{id:"c",speed:1,foot:16,capacity:600}];
    G.stations["dry-storage"]=[{id:"d",speed:1,foot:10,capacity:900}];
    RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k,80); });

    var warned = advise().filter(function(a){ return a.id === "hours"; })[0];
    var best = bestHoursForThisStreet();
    console.log("  " + pad(site.name,24) + pad(h[0],20) +
                pad(Math.round(hoursAreWorth(G.windows)) + " vs " + Math.round(best.worth), 12) +
                (warned ? "WARNS -> " + warned.hours.name : "quiet"));
  });
});
