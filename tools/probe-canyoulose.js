/*
 * CAN YOU ACTUALLY LOSE? Sabotage an established restaurant for a year and see.
 *
 * Before the pressure systems existed, every deliberate mistake still made money -- the worst
 * possible play (one cook, one server, budget stock, gutted kitchen, a marketing lie, all at
 * once) netted $793 a day against a control of $1,532. The whole downside in the game was
 * halving your profit.
 *
 * NOTE the monthly hook. Rivals, events, staff and the lease all live in billTheMonth(), so a
 * probe that only calls runDay() reaches none of it -- the first run after adding four pressure
 * systems reported byte-identical numbers.
 *
 *     python3 tools/headless.py tools/probe-canyoulose.js
 */
/* Once you are established, can ANYTHING hurt you? */
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];

function established(){
  G = newGame(sub, 0x546c4736);
  G.cash = 28000; G.seats = 32;   // a working restaurant, not one insulated by half a million
  G.fittings=[{id:"t",name:"T",seats:32,comfort:1.0}]; G.seatSpend=640; G.floorArea=900;
  G.servers=[]; for(var i=0;i<2;i++) G.servers.push({id:"s"+i,name:"S",role:"server",wage:14,skill:0.55,claim:0.5,potential:0.6});
  G.cooks=[];   for(var i=0;i<3;i++) G.cooks.push({id:"c"+i,name:"C",role:"cook",wage:20,skill:0.72,claim:0.7,potential:0.8});
  var oven=EQUIPMENT.filter(function(e){return e.id==="oven-hearth";})[0];
  var gm=EQUIPMENT.filter(function(e){return e.station==="garde-manger";})[2];
  G.stations["oven"]=[]; for(var u=0;u<3;u++) G.stations["oven"].push({id:oven.id,speed:oven.speed,foot:oven.foot,capacity:0,holds:oven.holds});
  G.stations["garde-manger"]=[{id:gm.id,speed:gm.speed,foot:gm.foot,capacity:0,holds:gm.holds}];
  G.stations["cold-storage"]=[{id:"c",speed:1,foot:90,capacity:3000}];
  G.stations["dry-storage"]=[{id:"d",speed:1,foot:34,capacity:4000}];
  G.rep.standing=0.96; G.rep.meals=40000;
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k,400); });
}
function run(days){
  var start = G.cash;
  for(var n=0;n<days;n++){
    runDay(); G.day++;
    // The month is where rivals, events, staff and the lease live. A probe that only calls
    // runDay() never reaches any of it -- which is why the first run of this reported
    // byte-identical numbers after four new pressure systems had been added.
    if(G.day % 30 === 0) billTheMonth();
  }
  return { perDay: (G.cash-start)/days, standing: G.rep.standing, cash: G.cash,
           rivals: G.rivals||0, cooks: G.cooks.length, rent: G.site.rent };
}

console.log("An established restaurant, then one year of a DELIBERATE mistake:");
console.log("  " + pad("what you do wrong",34)+pad("profit/day",13)+pad("standing",15)+pad("rivals",8)+pad("rent",10)+"solvent?");

var cases = [
  ["nothing — the control",            function(){}],
  ["fire every cook but one",          function(){ G.cooks = G.cooks.slice(0,1); }],
  ["fire every server but one",        function(){ G.servers = G.servers.slice(0,1); }],
  ["switch to the cheapest supplier",  function(){ G.supplier = "budget-wholesale"; }],
  ["double every price",               function(){ RECIPES.forEach(function(r){ if(G.onMenu.has(r.id)) G.prices[r.id]*=2; }); }],
  ["sell the whole kitchen but one",   function(){ for(var s in G.stations){ if(s.indexOf("storage")>=0) continue;
                                                     G.stations[s] = G.stations[s].slice(0,1); } }],
  ["claim premium on budget stock",    function(){ G.supplier="budget-wholesale";
                                                   G.campaign={claim:"ingredients",channel:"influencer"}; }],
  ["all of the above at once",         function(){ G.cooks=G.cooks.slice(0,1); G.servers=G.servers.slice(0,1);
                                                   G.supplier="budget-wholesale";
                                                   for(var s in G.stations){ if(s.indexOf("storage")<0) G.stations[s]=G.stations[s].slice(0,1); }
                                                   G.campaign={claim:"ingredients",channel:"influencer"}; }]
];

cases.forEach(function(c){
  established(); c[1]();
  var r = run(365);
  console.log("  " + pad(c[0],34) + pad(money(Math.round(r.perDay)),13) +
              pad(Math.round(r.standing*100)+"/100",15) + pad(r.rivals,8) +
              pad("$"+r.rent.toLocaleString(),10) + (r.cash > 0 ? "yes" : "BUST"));
});

console.log("");
console.log("--- why can't the worst case lose? ---");
established();
G.cooks=G.cooks.slice(0,1); G.servers=G.servers.slice(0,1); G.supplier="budget-wholesale";
for(var s in G.stations){ if(s.indexOf("storage")<0) G.stations[s]=G.stations[s].slice(0,1); }
for(var n=0;n<60;n++){ runDay(); G.day++; if(G.day%30===0) billTheMonth(); }
var m = G.metrics.slice(-30);
var rev = m.reduce(function(a,x){return a+x.revenue;},0)/30;
var lab = m.reduce(function(a,x){return a+x.labor;},0)/30;
var fd  = m.reduce(function(a,x){return a+x.food;},0)/30;
var cov = m.reduce(function(a,x){return a+x.covers;},0)/30;
console.log("  " + cov.toFixed(0) + " covers/day on 1 cook and 1 server");
console.log("  revenue $" + rev.toFixed(0) + "  food " + Math.round(fd/rev*100) + "%  LABOUR " + Math.round(lab/rev*100) + "%  rent " + Math.round(G.site.rent/30/rev*100) + "%");
console.log("  the trade runs labour at 30-35% of revenue.");
