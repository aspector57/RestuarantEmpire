// Do the three chefs actually play differently over a year, doing nothing?
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
function rpad(s,n){ s=String(s); while(s.length<n) s=" "+s; return s; }
var site = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];
print("EACH CHEF, 300 DAYS, NO DECISIONS AFTER OPENING");
print("  " + pad("who you are",24) + rpad("start",9) + rpad("cook",7) + rpad("covers/dy",11)
      + rpad("satisfaction",14) + rpad("standing",10) + rpad("cash",11));
CHEFS.forEach(function(c){
  chosenChef = c.id;
  G = newGame(site, 0x4aa16f77); fitOutOpening(conceptById("neighborhood-standard"));
  var start = G.cash;
  // Replacing somebody who quit is not a strategy, it is opening the doors. Without this the
  // comparison measures which archetype got unlucky with departures, not the archetypes.
  var want = { cooks:G.cooks.length, servers:G.servers.length };
  for(var n=0;n<300;n++){
    runDay(); G.day++;
    if(G.day%30===0){ billTheMonth();
      while(G.cooks.length<want.cooks) G.cooks.push(makeStaff("cook",0.55));
      while(G.servers.length<want.servers) G.servers.push(makeStaff("server",0.55)); }
  }
  var m=G.metrics.slice(-30), avg=function(k){return m.reduce(function(a,x){return a+(x[k]||0);},0)/30;};
  print("  " + pad(c.name,24) + rpad("$"+Math.round(start/1000)+"k",9)
        + rpad((c.skill*5).toFixed(1)+"/5",7) + rpad(avg("covers").toFixed(0),11)
        + rpad(avg("sat").toFixed(3),14) + rpad(Math.round(G.rep.standing*100),10)
        + rpad("$"+Math.round(G.cash).toLocaleString(),11));
});
