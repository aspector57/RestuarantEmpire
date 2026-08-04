/*
 * Does marketing behave the way it should — awareness bought, standing earned, and a claim
 * that costs you when it is not true?
 *
 *     python3 tools/headless.py tools/probe-marketing.js
 */
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
function cash(n){ return (n<0?"-$":"$") + Math.abs(Math.round(n)).toLocaleString(); }
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];

function run(supplier, camp, days){
  G = newGame(sub, 20240802);
  G.cash=200000; G.seats=32; G.supplier=supplier;
  G.fittings=[{id:"t",name:"T",seats:32,comfort:0.55}]; G.seatSpend=480;
  G.servers=[]; for(var i=0;i<3;i++) G.servers.push({id:"s"+i,name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5});
  G.cooks=[];   for(var i=0;i<3;i++) G.cooks.push({id:"c"+i,name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});
  ["oven","garde-manger"].forEach(function(st){
    var m=EQUIPMENT.filter(function(e){return e.station===st;})[0];
    G.stations[st]=[{id:m.id,speed:m.speed,foot:m.foot,capacity:0,holds:m.holds||1},
                    {id:m.id,speed:m.speed,foot:m.foot,capacity:0,holds:m.holds||1}]; });
  G.stations["cold-storage"]=[{id:"cold-walkin",speed:1,foot:90,capacity:3000}];
  G.stations["dry-storage"]=[{id:"dry-racking",speed:1,foot:34,capacity:4000}];
  G.floorArea=3000; G.campaign = camp;
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k,200); });
  var rev=0, food=0, cov=0, sat=0, n=0;
  for(var d=0; d<days; d++){ var x=runDay(); rev+=x.revenue; food+=x.food; cov+=x.covers; if(x.covers){ sat+=(x.sat||0); n++; } }
  return { net: rev-food-G.ledger.labor-sub.rent*(days/30)-(camp?CHANNELS[camp.channel].cost*(days/30):0),
           covers: cov/days, standing:G.rep.standing, known:awareness() };
}

console.log("CLAIMING YOUR INGREDIENTS — 180 days, same everything but the supplier");
console.log("  " + pad("supplier",12)+pad("campaign",26)+pad("net",12)+pad("covers/day",12)+pad("standing",10)+"known");
[["budget-wholesale","Budget"],["premium-harvest","Premium"]].forEach(function(s){
  [[null,"no campaign"],
   [{claim:"ingredients",channel:"press"},"claims ingredients (press)"],
   [{claim:"value",channel:"coupons"},"claims value (coupons)"]].forEach(function(c){
    var r = run(s[0], c[0], 180);
    console.log("  " + pad(s[1],12)+pad(c[1],26)+pad(cash(r.net),12)+pad(r.covers.toFixed(1),12)+
                pad(Math.round(r.standing*100)+"/100",10)+Math.round(r.known*100)+"%");
  });
});

console.log("");
console.log("DOES LYING EVENTUALLY COST YOU? budget stock, claiming its ingredients");
console.log("  " + pad("horizon",12)+pad("quiet",22)+pad("claims ingredients",22)+"verdict");
[180, 360, 720, 1080].forEach(function(days){
  var quiet = run("budget-wholesale", null, days);
  var loud  = run("budget-wholesale", {claim:"ingredients",channel:"press"}, days);
  console.log("  " + pad(days+" days",12) +
    pad(cash(quiet.net)+" ("+Math.round(quiet.standing*100)+")",22) +
    pad(cash(loud.net)+" ("+Math.round(loud.standing*100)+")",22) +
    (loud.net > quiet.net ? "lying still pays" : "LYING NOW COSTS"));
});

console.log("");
console.log("WHO EACH CHANNEL BRINGS (weighting applied to the crowd that is out)");
console.log("  " + pad("channel",20) + ["Family","RomanticCouple","Influencer","BusinessLuncher"].map(function(a){return pad(a,17);}).join(""));
for(var ch in CHANNELS){
  G.campaign = { claim:"ingredients", channel:ch };
  console.log("  " + pad(CHANNELS[ch].name,20) +
    ["Family","RomanticCouple","Influencer","BusinessLuncher"].map(function(a){
      return pad(marketingPull(a).toFixed(2)+"x", 17); }).join(""));
}
