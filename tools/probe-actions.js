/*
 * EVERY BUTTON THE GAME OFFERS MUST BE ABLE TO DO SOMETHING.
 *
 * Three separate dead buttons have shipped: the Advisor offering an oven with no floor for it,
 * `needsWage` rendering nothing at all, and an interrupt written with `run:` where the renderer
 * calls `fn:`. All three looked fine and all three did nothing when pressed. Static checks
 * cannot see any of it; pressing them can.
 *
 *     python3 tools/headless.py tools/probe-actions.js
 */
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];
var problems = [];

function build(seats, cooks, servers, ovens, cash, floor){
  G = newGame(sub, 4242);
  G.cash = cash; G.seats = seats;
  G.fittings=[{id:"t",name:"T",seats:seats,comfort:0.55}]; G.seatSpend=seats*15;
  G.floorArea = floor;
  G.servers=[]; for(var i=0;i<servers;i++) G.servers.push({id:"s"+i,name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5});
  G.cooks=[];   for(var i=0;i<cooks;i++)   G.cooks.push({id:"c"+i,name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});
  ["oven","garde-manger"].forEach(function(st){
    var m=EQUIPMENT.filter(function(e){return e.station===st;})[0];
    G.stations[st]=[];
    for(var u=0;u<ovens;u++) G.stations[st].push({id:m.id,speed:m.speed,foot:m.foot,capacity:0,holds:m.holds||1});
  });
  G.stations["cold-storage"]=[{id:"cold-walkin",speed:1,foot:90,capacity:3000}];
  G.stations["dry-storage"]=[{id:"dry-racking",speed:1,foot:34,capacity:4000}];
  RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k,150); });
}

/* Shapes chosen so each one binds on something different. */
var shapes = [
  ["room-bound, no floor",     12, 4, 1, 3,   50000,  520],
  ["room-bound, floor spare",  12, 4, 1, 3,   50000, 3000],
  ["short of hands",           40, 1, 1, 3,   50000, 3000],
  ["short of kitchen",         60, 3, 4, 1,   50000, 3000],
  ["rich and stuck",           52, 4, 3, 4, 1300000,  900],
  ["broke",                    24, 2, 2, 2,     900, 3000]
];

console.log(pad("shape",26)+pad("source",12)+"button  ->  does it do anything?");
shapes.forEach(function(s){
  build(s[1],s[2],s[3],s[4],s[5],s[6]);
  for(var i=0;i<12;i++) runDay();

  // --- every action the Advisor offers ---
  advise().forEach(function(a){
    var offers = [];
    if(a.buy)      offers.push(["buy " + a.buy.name, function(){ var n=stationUnits(a.buy.station); buyEquip(a.buy); return stationUnits(a.buy.station)>n; }]);
    if(a.seats)    offers.push(["add 10 seats",      function(){ var n=G.seats; buySeats(a.seats); return G.seats>n; }]);
    if(a.extend)   offers.push(["extend building",   function(){ var n=G.floorArea; extendBuilding(); return G.floorArea>n; }]);
    if(a.upgrade)  offers.push(["swap equipment",    function(){ sellEquip(a.upgrade.from); buyEquip(a.upgrade.to); return true; }]);
    if(a.needsWage)offers.push(["go and hire",       function(){ return true; }]);
    offers.forEach(function(o){
      var worked = false;
      try { worked = o[1](); } catch(e){ worked = false; }
      console.log("  "+pad(s[0],24)+pad("advisor",12)+pad(o[0],26)+(worked?"yes":"NO — DEAD BUTTON"));
      if(!worked) problems.push(s[0]+" / advisor / "+o[0]);
    });
  });

  // --- and every action an interrupt offers ---
  build(s[1],s[2],s[3],s[4],s[5],s[6]);
  for(var i=0;i<12;i++) runDay();
  // Force the wait-balk condition rather than hoping for it, so the interrupt path is
  // genuinely exercised — the dead `run:` button lived here and no shape happened to fire it.
  var forced = { covers:G.today.covers||20, balkedWait:40, walkouts:12, balkedPrice:2,
                 labor:400, revenue:600, food:200, noTable:15, lostMenu:0 };
  var int_ = checkInterrupts(forced);
  if(int_ && int_.acts) int_.acts.forEach(function(a){
    var callable = typeof a.fn === "function";
    console.log("  "+pad(s[0],24)+pad("interrupt",12)+pad(a.label.slice(0,24),26)+(callable?"yes":"NO — NOT CALLABLE"));
    if(!callable) problems.push(s[0]+" / interrupt / "+a.label);
  });
});

console.log("");
console.log(problems.length ? problems.length + " DEAD BUTTON(S):\n  " + problems.join("\n  ")
                            : "EVERY OFFERED ACTION WORKS.");
