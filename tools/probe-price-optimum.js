/* Where is the TRUE price optimum per supplier? Fine sweep, not two points.
   The two-point comparison (1.0x vs 1.5x) said premium wanted 1.5x. That is only
   evidence that 1.5 beat 1.0, which is not the same claim. */
var DAYS = 240;
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
function rpad(s,n){ s=String(s); while(s.length<n) s=" "+s; return s; }
function cash(n){ return (n<0?"-$":"$") + Math.abs(Math.round(n)).toLocaleString(); }
var sub = SITES.filter(function(s){ return s.id === "suburban-high-street"; })[0];

function play(supplier, price, big){
  G = newGame(sub, 20240802);
  G.cash = 400000; G.seats = big ? 60 : 24;
  G.fittings = [{id:"t", name:"Tables", seats:G.seats, comfort:0.55}];
  G.seatSpend = G.seats*15; G.supplier = supplier;
  G.onMenu = new Set(["margherita","house-focaccia","caprese-salad"]);
  G.servers = []; for(var i=0;i<(big?4:1);i++) G.servers.push({id:"s"+i,name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5});
  G.cooks   = []; for(var j=0;j<(big?5:2);j++) G.cooks.push({id:"c"+j,name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});
  var om = EQUIPMENT.filter(function(e){ return e.id === "oven-secondhand"; })[0];
  G.stations = { oven:[] };
  for(var u=0;u<(big?6:2);u++) G.stations["oven"].push({id:om.id,speed:om.speed,foot:om.foot,capacity:0});
  var gm = EQUIPMENT.filter(function(e){ return e.station === "garde-manger"; })[1];
  G.stations["garde-manger"] = [];
  for(var v=0;v<(big?4:2);v++) G.stations["garde-manger"].push({id:gm.id,speed:gm.speed,foot:gm.foot,capacity:0});
  G.stations["cold-storage"] = [{id:"cold-walkin", speed:1, foot:90, capacity:6000}];
  G.stations["dry-storage"]  = [{id:"dry-racking", speed:1, foot:34, capacity:8000}];
  G.floorArea = 3000;
  RECIPES.forEach(function(r){ if(G.onMenu.has(r.id)) G.prices[r.id] = Math.round(r.base*price*100)/100; });
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 400); });
  var rev=0, food=0, cov=0;
  for(var d=0; d<DAYS; d++){ var r = runDay(); rev+=r.revenue; food+=r.food; cov+=r.covers; }
  return { net: rev-food-G.ledger.labor-sub.rent*(DAYS/30), covers: cov/DAYS, standing: G.rep.standing };
}

[["SMALL kitchen (2 ovens, 2 cooks, 24 seats) — capacity-bound", false],
 ["BIG kitchen (6 ovens, 5 cooks, 60 seats) — demand-bound", true]].forEach(function(mode){
  console.log("");
  console.log(mode[0] + ", " + DAYS + " days");
  var header = pad("supplier",10);
  var prices = [1.0,1.1,1.2,1.3,1.4,1.5,1.6,1.8];
  prices.forEach(function(p){ header += rpad(p.toFixed(1)+"x",11); });
  console.log(header + "   <- optimum");

  ["budget-wholesale","valley-produce","premium-harvest"].forEach(function(s){
    var line = pad(SUPPLIERS.filter(function(x){return x.id===s;})[0].name.split(" ")[0],10);
    var best = null;
    prices.forEach(function(p){
      var r = play(s, p, mode[1]);
      line += rpad(cash(r.net),11);
      if(!best || r.net > best.net) best = { p:p, net:r.net, standing:r.standing };
    });
    console.log(line + "   " + best.p.toFixed(1) + "x  (standing " + Math.round(best.standing*100) + ")");
  });
});
