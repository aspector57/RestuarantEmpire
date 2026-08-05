/*
 * CAN A RESTAURANT EVER SATISFY ITS OWN STREET?
 *
 * Aaron, after a day-64 run: "we were able to attract people too quickly and fill the whole
 * place up right away, then we extended to the point we couldn't extend any more and maxxed
 * out seats and barely needed to hire more people. it was very easy."
 *
 * He had maxed the Nightlife Quarter -- 1545 sq ft against a 1550 cap -- and was STILL turning
 * away 154 covers a night. If a fully built restaurant on its best day cannot serve the street
 * it sits on, then demand is not a constraint anywhere in the game, and every decision except
 * "add capacity" is optional: you never compete for a customer, marketing cannot matter, and a
 * reputation past the point of filling the room buys nothing.
 *
 * This measures the ratio directly. Build each site OUT to its floor cap, staff it properly,
 * and ask what share of the people who want a table can get one.
 *
 *     python3 tools/headless.py tools/probe-demandceiling.js
 */
function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
function rpad(s,n){ s=String(s); while(s.length<n) s=" "+s; return s; }

/*
 * CARRY THE EQUIPMENT'S REAL CAPACITY ACROSS. Hardcoding capacity:0 here gave the walk-ins no
 * storage at all, so the standing order could not accept a delivery: the pantry emptied on day
 * one and every day after served ZERO covers with five million in the bank. The probe then
 * reported "0 wanted, 0 served, 100% satisfied" for all four streets, which read as a finding
 * and was a famine.
 *
 * Third time this exact fixture bug has been made here -- the missing fridge, the storage-less
 * starvation, and now this. A fixture that skips the game's own economy measures something
 * that is not the game.
 */
function unit(id){
  var e = EQUIPMENT.filter(function(x){ return x.id === id; })[0];
  return { id:e.id, speed:e.speed, foot:e.foot, capacity:(e.capacity || 0), holds:e.holds };
}

/* Build the biggest restaurant this site can legally hold, and staff it to match. */
function maxedOut(site, concept){
  G = newGame(site, 4242);
  G.concept = concept.id;
  fitOutOpening(concept);

  G.cash = 5000000;                       // capital is not the question here
  G.floorArea = site.maxArea;

  // A serious kitchen: four of the best on each station the card needs, plus storage.
  var need = {};
  concept.card.forEach(function(id){
    var r = RECIPES.filter(function(x){ return x.id === id; })[0];
    if(r) need[r.station] = 1;
  });
  G.stations = {};
  for(var st in need){
    var best = EQUIPMENT.filter(function(e){ return e.station === st; })
                        .sort(function(a,b){ return (b.speed*(b.holds||1)) - (a.speed*(a.holds||1)); })[0];
    if(!best) continue;
    G.stations[st] = [];
    for(var i=0;i<4;i++) G.stations[st].push(unit(best.id));
  }
  G.stations["cold-storage"] = [unit("cold-walkin"), unit("cold-walkin")];
  G.stations["dry-storage"]  = [unit("dry-stockroom"), unit("dry-stockroom")];

  // Fill whatever floor is left with seats, then staff to cover them.
  var usedByKitchen = 0;
  for(var s in G.stations) G.stations[s].forEach(function(u){ usedByKitchen += u.foot; });
  var seatArea = Math.max(0, site.maxArea - usedByKitchen);
  var seats = Math.max(10, Math.floor(seatArea / 13) * 1);   // ~13 sq ft a cover, dining side
  G.seats = seats;
  G.fittings = [{ id:"t", name:"T", seats:seats, comfort:0.85 }];
  G.seatSpend = seats * 260;

  var serversNeeded = Math.ceil(seats / 10) + 2;
  var cooksNeeded   = Math.ceil(seats / 6) + 2;
  G.cooks = []; G.servers = [];
  for(var c=0;c<cooksNeeded;c++)   G.cooks.push(makeStaff("cook", 0.85));
  for(var v=0;v<serversNeeded;v++) G.servers.push(makeStaff("server", 0.85));

  G.rep.standing = 0.85; G.rep.meals = 40000; G.rep.word = 40000;   // a known, liked place
  return { cooks:cooksNeeded, servers:serversNeeded, seats:seats };
}

console.log("A FULLY BUILT RESTAURANT AGAINST ITS OWN STREET");
console.log("Site built to its floor cap, staffed to match, well known and well liked.");
console.log("30 days. 'served' is the share of everyone who wanted a table and got one.");
console.log("");
console.log("  " + pad("street",24) + rpad("seats",7) + rpad("brigade",9) + rpad("wanted",9)
            + rpad("served",9) + rpad("turned away",13) + rpad("share served",14));

var anyDemandBound = false;
SITES.forEach(function(site){
  var built = maxedOut(site, conceptById("neighborhood-standard"));
  var want = { cooks:G.cooks.length, servers:G.servers.length };

  for(var n=0;n<30;n++){
    runDay(); G.day++;
    if(G.day % 30 === 0){
      billTheMonth();
      while(G.cooks.length   < want.cooks)   G.cooks.push(makeStaff("cook", 0.85));
      while(G.servers.length < want.servers) G.servers.push(makeStaff("server", 0.85));
    }
  }

  var m = G.metrics.slice(-14);
  var avg = function(k){ return m.reduce(function(a,x){ return a + (x[k]||0); }, 0) / m.length; };
  var served = avg("covers");
  var lost = avg("noTable") + avg("balkWait") + avg("walkouts");
  var wanted = served + lost;
  var share = wanted > 0 ? served / wanted : 1;
  if(share > 0.95) anyDemandBound = true;

  console.log("  " + pad(site.name,24) + rpad(built.seats,7)
    + rpad(built.cooks + "c/" + built.servers + "s",9)
    + rpad(Math.round(wanted),9) + rpad(Math.round(served),9)
    + rpad(Math.round(lost),13) + rpad(Math.round(share*100) + "%",14));
});

console.log("");
console.log(anyDemandBound
  ? "  At least one street CAN be satisfied — demand is a real ceiling somewhere."
  : "  NO STREET CAN BE SATISFIED, even fully built. Demand is not a constraint anywhere:");
if(!anyDemandBound){
  console.log("  you never compete for a customer, marketing cannot change anything that matters,");
  console.log("  and reputation past the point of filling the room buys nothing. Every decision");
  console.log("  except 'add capacity' is optional, which is what makes it feel easy.");
}
