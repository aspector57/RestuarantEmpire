/*
 * Does putting more on the plate pay -- and does it stop paying?
 *
 * Aaron: "raising your cost, it should raise what you can charge but not infinitely."
 * The ceiling is the mechanic. Without it this is a slider that says yes.
 *
 *     python3 tools/headless.py tools/probe-extras.js
 */
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];

function run(extras, price, days){
  G = newGame(sub, 31337);
  G.cash=120000; G.seats=40; G.supplier="valley-produce";
  G.fittings=[{id:"t",name:"T",seats:40,comfort:0.6}]; G.seatSpend=600; G.floorArea=3000;
  G.servers=[]; for(var i=0;i<3;i++) G.servers.push({id:"s"+i,name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5});
  G.cooks=[];   for(var i=0;i<3;i++) G.cooks.push({id:"c"+i,name:"C",role:"cook",wage:16,skill:0.6,claim:0.6,potential:0.7});
  ["oven","garde-manger"].forEach(function(st){
    var m=EQUIPMENT.filter(function(e){return e.station===st;})[1];
    G.stations[st]=[{id:m.id,speed:m.speed,foot:m.foot,capacity:0,holds:m.holds},
                    {id:m.id,speed:m.speed,foot:m.foot,capacity:0,holds:m.holds}]; });
  G.stations["cold-storage"]=[{id:"c",speed:1,foot:90,capacity:3000}];
  G.stations["dry-storage"]=[{id:"d",speed:1,foot:34,capacity:4000}];
  G.extras = { "margherita": extras };
  RECIPES.forEach(function(r){ if(G.onMenu.has(r.id)) G.prices[r.id] = Math.round(r.base*price*100)/100; });
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k,400); });
  for(var id in G.extras) for(var i=0;i<G.extras[id].length;i++){
    var x = EXTRAS[id].filter(function(e){return e.id===G.extras[id][i];})[0];
    for(var k in x.ing) orderStock(k, 400);
  }
  var rev=0, food=0, sat=0, n=0;
  for(var day=0; day<days; day++){
    var d = runDay(); G.day++;
    rev += d.revenue; food += d.food;
    if(d.covers){ sat += (d.satTotal||0)/d.covers; n++; }
  }
  return { profit: (rev - food - G.ledger.labor - sub.rent*(days/30))/days,
           sat: n ? sat/n : 0, lift: extrasLift("margherita"), cost: extrasCost("margherita"),
           over: extrasOverdone("margherita") };
}

console.log("A MARGHERITA, PROGRESSIVELY DRESSED UP — 120 days each, priced to match");
console.log("  " + pad("what is on it",34)+pad("lift",8)+pad("+cost",8)+pad("satisfaction",14)+pad("profit/day",12)+"worth it?");
[[[], "just the recipe"],
 [["buffalo"], "buffalo mozzarella"],
 [["buffalo","parma"], "+ prosciutto"],
 [["buffalo","parma","basil"], "+ fresh basil"]].forEach(function(c, i){
  var plain = run([], 1.0, 120);
  var r = run(c[0], 1.0 + i*0.12, 120);   // dress it up, charge more for it
  console.log("  " + pad(c[1],34) + pad(r.lift.toFixed(2),8) + pad("$"+r.cost.toFixed(2),8) +
              pad(r.sat.toFixed(3),14) + pad(money(Math.round(r.profit)),12) +
              (r.over ? "PAST THE CEILING" : (r.profit > plain.profit ? "yes" : "no")));
});
