/*
 * IS BEING UNDER-BUILT PUNISHED AT ALL, AND DOES GROWING PAY?
 *
 * Aaron, after a day-64 run: "we attracted people too quickly and filled the whole place up
 * right away, then we extended to the point we couldn't extend any more and maxxed out seats
 * and barely needed to hire more people. it was very easy."
 *
 * His hypothesis, and mine, was that demand is unbounded -- that no restaurant can ever
 * satisfy its street, so nothing except adding capacity matters. probe-demandceiling.js
 * REFUTES that: a built-out restaurant serves 100% of every street. So the easiness is
 * somewhere else, and this is where.
 *
 *     python3 tools/headless.py tools/probe-underbuilt.js
 */
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
function rpad(s,n){ s=String(s); while(s.length<n) s=" "+s; return s; }
function unit(id){ var e=EQUIPMENT.filter(function(x){return x.id===id;})[0];
  return {id:e.id,speed:e.speed,foot:e.foot,capacity:(e.capacity||0),holds:e.holds}; }

var site = SITES.filter(function(s){return s.id==="nightlife-quarter";})[0];
var concept = conceptById("fine-dining");

function build(seats, cooks, servers){
  G = newGame(site, 4242); G.concept=concept.id; fitOutOpening(concept);
  G.cash = 300000; G.floorArea = site.maxArea;
  G.stations = { grill:[unit("grill-chargrill"),unit("grill-chargrill")],
                 saute:[unit("saute-induction"),unit("saute-induction")],
                 "garde-manger":[unit("gm-pass")],
                 "cold-storage":[unit("cold-walkin"),unit("cold-walkin")],
                 "dry-storage":[unit("dry-stockroom")] };
  G.seats=seats; G.fittings=[{id:"t",name:"T",seats:seats,comfort:0.85}]; G.seatSpend=seats*260;
  G.cooks=[]; for(var i=0;i<cooks;i++) G.cooks.push(makeStaff("cook",0.75));
  G.servers=[]; for(var i=0;i<servers;i++) G.servers.push(makeStaff("server",0.7));
  G.rep.standing=0.6; G.rep.meals=8000; G.rep.word=8000;
  var want={cooks:cooks,servers:servers};
  for(var n=0;n<45;n++){
    runDay(); G.day++;
    if(G.day%30===0){ billTheMonth();
      while(G.cooks.length<want.cooks) G.cooks.push(makeStaff("cook",0.75));
      while(G.servers.length<want.servers) G.servers.push(makeStaff("server",0.7)); }
  }
  var m=G.metrics.slice(-14), avg=function(k){return m.reduce(function(a,x){return a+(x[k]||0);},0)/m.length;};
  var served=avg("covers"), lost=avg("noTable")+avg("balkWait")+avg("walkouts");
  return { profit:avg("profit")*30, covers:served, lost:lost,
           share: (served+lost)>0 ? served/(served+lost) : 1 };
}

print("NIGHTLIFE QUARTER, fine dining — does under-building cost you anything?");
print("  " + pad("build",22)+rpad("covers/dy",11)+rpad("turned away",13)+rpad("share met",11)+rpad("profit/mo",12)+rpad("per seat",10));
[[10,2,1],[20,3,2],[30,4,3],[50,5,4],[72,8,6],[100,11,8]].forEach(function(b){
  var r = build(b[0],b[1],b[2]);
  print("  " + pad(b[0]+" seats "+b[1]+"c/"+b[2]+"s",22)
    + rpad(r.covers.toFixed(0),11) + rpad(r.lost.toFixed(0),13)
    + rpad(Math.round(r.share*100)+"%",11)
    + rpad("$"+Math.round(r.profit).toLocaleString(),12)
    + rpad("$"+Math.round(r.profit/b[0]),10));
});
