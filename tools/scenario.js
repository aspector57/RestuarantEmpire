/*
 * A YEAR, WITH CHANGES MADE ALONG THE WAY AND TIME TO SEE EACH ONE SETTLE.
 *
 * Aaron: "it would be great if you can simulate like 3 months or a year but be able to look at
 * daily logs, and a bunch of different changes but give the changes enough time to breathe to
 * see how they are doing."
 *
 * `levers.js` answers "what is this worth?" by holding everything else still. This answers the
 * different question: what happens to a RUNNING restaurant when you change something on day 90,
 * and how long before you can tell? Reputation moves over months, staff learn over weeks, and
 * spoilage compounds — none of that shows up in a snapshot.
 *
 * Each change gets a settling window, and the report compares the fortnight before it against
 * the fortnight after AND the month after, because the first answer and the eventual answer are
 * often different. A daily log is printed underneath so nothing is hidden behind an average.
 *
 *     python3 tools/headless.py tools/scenario.js
 */

var DAYS = 365;
var SETTLE = 45;           // how long each change gets before the next one
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
function cash(n){ return (n<0?"-$":"$") + Math.abs(Math.round(n)).toLocaleString(); }

/* ---- the changes to try, in order, each given room to breathe ---- */
var CHANGES = [
  { day: 60,  what: "raise the whole card to 1.4x",
    go: function(){ RECIPES.forEach(function(r){ if(G.onMenu.has(r.id)) G.prices[r.id] = Math.round(r.base*1.4*100)/100; }); } },
  { day: 105, what: "hire a second cook",
    go: function(){ G.cooks.push({id:"c9",name:"Cook",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.65}); } },
  { day: 150, what: "swap in a Stone Hearth Oven",
    go: function(){ var old_ = EQUIPMENT.filter(function(e){return e.id==="oven-secondhand";})[0];
                    var neu  = EQUIPMENT.filter(function(e){return e.id==="oven-hearth";})[0];
                    sellEquip(old_); buyEquip(neu); } },
  { day: 195, what: "add 10 seats",
    go: function(){ buySeats(FURNITURE.filter(function(f){return f.id==="standard";})[0]); } },
  { day: 240, what: "upgrade to premium ingredients",
    go: function(){ G.supplier = "premium-harvest"; } },
  { day: 285, what: "put sea bass on the card",
    go: function(){ G.onMenu.add("sea-bass");
                    for(var k in RECIPES.filter(function(r){return r.id==="sea-bass";})[0].ing) orderStock(k, 120); } },
  { day: 330, what: "drop back to budget ingredients",
    go: function(){ G.supplier = "budget-wholesale"; } }
];

/* ---- open ---- */
var sub = SITES.filter(function(s){ return s.id === "suburban-high-street"; })[0];
G = newGame(sub, 20240802);
G.cash = 60000; G.seats = 24;
G.fittings = [{id:"t", name:"Tables", seats:24, comfort:0.55}]; G.seatSpend = 360;
G.servers = [{id:"s0",name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.55}];
G.cooks   = [{id:"c0",name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.65}];
["oven","garde-manger"].forEach(function(st){
  var m = EQUIPMENT.filter(function(e){ return e.station === st; })[0];
  G.stations[st] = [{id:m.id, speed:m.speed, foot:m.foot, capacity:0},
                    {id:m.id, speed:m.speed, foot:m.foot, capacity:0}];
});
G.stations["cold-storage"] = [{id:"cold-walkin", speed:1, foot:90, capacity:3000}];
G.stations["dry-storage"]  = [{id:"dry-racking", speed:1, foot:34, capacity:4000}];
G.floorArea = 3000;
RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 150); });

/* ---- run, recording every single day ---- */
var daily = [];
for(var day = 1; day <= DAYS; day++){
  CHANGES.forEach(function(c){ if(c.day === day){ c.go(); c.at = day; } });
  var d = runDay();
  daily.push({ day:day, covers:d.covers, revenue:d.revenue, food:d.food, labor:d.labor,
               walkouts:d.walkouts, standing:G.rep.standing, cash:G.cash, spoiled:d.spoiled||0 });
}

function window_(from, to){
  var w = daily.filter(function(x){ return x.day > from && x.day <= to; });
  if(!w.length) return null;
  var n = w.length;
  return {
    covers:  w.reduce(function(a,x){return a+x.covers;},0)/n,
    revenue: w.reduce(function(a,x){return a+x.revenue;},0)/n,
    profit:  w.reduce(function(a,x){return a+(x.revenue-x.food-x.labor);},0)/n,
    walkouts:w.reduce(function(a,x){return a+x.walkouts;},0)/n,
    standing:w[w.length-1].standing
  };
}

console.log("A YEAR ON THE SUBURBAN HIGH STREET — seven changes, " + SETTLE + " days apart");
console.log("");
console.log(pad("change",34)+pad("when",7)+pad("covers",16)+pad("profit/day",22)+pad("walkouts",16)+"standing");
console.log(pad("",34)+pad("",7)+pad("before -> after",16)+pad("2wk -> 1mo after",22)+pad("before -> after",16));
console.log(new Array(112).join("-"));

CHANGES.forEach(function(c){
  var before = window_(c.day-14, c.day);
  var after  = window_(c.day, c.day+14);
  var later  = window_(c.day+14, c.day+42);
  if(!before || !after) return;
  console.log(pad(c.what,34)+pad("d"+c.day,7)+
    pad(before.covers.toFixed(0)+" -> "+after.covers.toFixed(0),16)+
    pad(cash(after.profit)+" -> "+(later?cash(later.profit):"—"),22)+
    pad(before.walkouts.toFixed(1)+" -> "+after.walkouts.toFixed(1),16)+
    Math.round(before.standing*100)+" -> "+(later?Math.round(later.standing*100):"—"));
});

console.log("");
console.log("THE SLOW ONES — reputation and skill move over months, so the fortnight lies:");
CHANGES.forEach(function(c){
  var quick = window_(c.day, c.day+14), slow = window_(c.day+60, c.day+90);
  if(!quick || !slow) return;
  var flip = (quick.profit < 0) !== (slow.profit < 0);
  var moved = Math.abs(slow.profit - quick.profit) > Math.abs(quick.profit)*0.25;
  if(flip || moved)
    console.log("  " + pad(c.what,34) + cash(quick.profit) + "/day after a fortnight, " +
                cash(slow.profit) + "/day two months on" + (flip ? "   ** SIGN FLIPPED **" : ""));
});

console.log("");
console.log("DAILY LOG — every tenth day, and every day around a change");
console.log("  " + pad("day",6)+pad("covers",8)+pad("revenue",10)+pad("profit",10)+pad("walk",7)+pad("standing",10)+"cash");
daily.forEach(function(x){
  var nearChange = CHANGES.some(function(c){ return Math.abs(x.day - c.day) <= 2; });
  if(x.day % 10 !== 0 && !nearChange) return;
  var mark = CHANGES.some(function(c){ return c.day === x.day; }) ? "  <-- change" : "";
  console.log("  " + pad("d"+x.day,6)+pad(x.covers,8)+pad("$"+Math.round(x.revenue),10)+
              pad(cash(x.revenue-x.food-x.labor),10)+pad(x.walkouts,7)+
              pad(Math.round(x.standing*100)+"/100",10)+cash(x.cash)+mark);
});
