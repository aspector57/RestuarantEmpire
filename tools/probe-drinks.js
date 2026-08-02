/*
 * Do drinks behave in the browser build the way they do in the engine?
 *
 * The C# equivalent (`DrinksTests`) measures spend per cover $13.43 -> $22.39 and food cost
 * 37% -> 28%. This asks the same of the port.
 *
 *     python3 tools/headless.py tools/probe-drinks.js
 */
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
var night = SITES.filter(function(s){return s.id==="nightlife-quarter";})[0];

function build(menu, licensed, windows, seed){
  G = newGame(night, seed);
  G.cash = 400000; G.seats = 36;
  G.fittings=[{id:"t",name:"T",seats:36,comfort:0.5}]; G.seatSpend=540;
  G.onMenu = new Set(menu);
  G.licence = licensed;
  G.windows = windows;
  for(var i=0;i<3;i++) G.servers.push({id:"s"+i,name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5});
  for(var i=0;i<4;i++) G.cooks.push({id:"c"+i,name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});
  var need={}; RECIPES.forEach(function(r){ if(G.onMenu.has(r.id)) need[r.station]=true; });
  for(var st in need){
    var m=EQUIPMENT.filter(function(e){return e.station===st;})[0]; if(!m) continue;
    G.stations[st]=[]; for(var u=0;u<3;u++) G.stations[st].push({id:m.id,speed:m.speed,foot:m.foot,capacity:0});
  }
  // Somewhere to PUT the stock, or the standing order silently refuses every delivery and
  // the restaurant quietly starves. Cost a false finding once already.
  G.stations["cold-storage"] = [{id:"cold-walkin", speed:1, foot:90, capacity:3000}];
  G.stations["dry-storage"]  = [{id:"dry-racking", speed:1, foot:34, capacity:4000}];
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) G.pantry[k]=[{qty:300,day:G.day}]; });
}
function run(days){
  var tot={covers:0,revenue:0,food:0,lostMenu:0,drinks:0};
  for(var i=0;i<days;i++){ var d=runDay();
    tot.covers+=d.covers; tot.revenue+=d.revenue; tot.food+=d.food;
    tot.lostMenu+=d.lostMenu; tot.drinks+=(d.drinks||0); }
  return tot;
}

var dinner=[{name:"Dinner",from:18,to:23}];
var dinnerLate=[{name:"Dinner",from:18,to:23},{name:"Late",from:23,to:2}];
var food=["sea-bass","caprese-salad"];
var withBar=["sea-bass","caprese-salad","house-wine","negroni","draught-pint"];

console.log(pad("setup",22)+pad("covers",8)+pad("revenue",10)+pad("$/cover",9)+pad("food%",7)+pad("drinks",8)+"found nothing");
build(food,false,dinner,4242); var a=run(30);
console.log(pad("dinner, no bar",22)+pad(a.covers,8)+pad("$"+Math.round(a.revenue),10)+pad("$"+(a.revenue/a.covers).toFixed(2),9)+pad(Math.round(a.food/a.revenue*100)+"%",7)+pad(a.drinks,8)+a.lostMenu);
build(withBar,true,dinner,4242); var b=run(30);
console.log(pad("dinner, licensed",22)+pad(b.covers,8)+pad("$"+Math.round(b.revenue),10)+pad("$"+(b.revenue/b.covers).toFixed(2),9)+pad(Math.round(b.food/b.revenue*100)+"%",7)+pad(b.drinks,8)+b.lostMenu);
build(food,false,dinnerLate,4242); var c=run(30);
console.log(pad("dinner+late, no bar",22)+pad(c.covers,8)+pad("$"+Math.round(c.revenue),10)+pad("$"+(c.revenue/c.covers).toFixed(2),9)+pad(Math.round(c.food/c.revenue*100)+"%",7)+pad(c.drinks,8)+c.lostMenu);
build(withBar,true,dinnerLate,4242); var e=run(30);
console.log(pad("dinner+late, licensed",22)+pad(e.covers,8)+pad("$"+Math.round(e.revenue),10)+pad("$"+(e.revenue/e.covers).toFixed(2),9)+pad(Math.round(e.food/e.revenue*100)+"%",7)+pad(e.drinks,8)+e.lostMenu);

build(withBar,false,dinner,4242); var f=run(10);
console.log("");
console.log("unlicensed with a drinks list: "+(G.sold["house-wine"]||0)+" wine, "+(G.sold["negroni"]||0)+" negroni sold (must be 0)");

console.log("");
console.log("--- is the food-only case running out of stock? ---");
build(food,false,dinner,4242);
for(var day=0; day<30; day++){
  var d = runDay();
  if(day===0 || day===4 || day===29)
    console.log("  day "+pad(day+1,4)+" covers "+pad(d.covers,5)+" foundNothing "+pad(d.lostMenu,5)+
                " sea-bass stock "+stockOf("sea-bass").toFixed(1)+"  mozzarella "+stockOf("mozzarella").toFixed(1));
}
