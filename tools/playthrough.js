/*
 * A WHOLE GAME, PLAYED BY THE ADVISOR, WITH EVERY READOUT CHECKED AGAINST EVERY OTHER ONE.
 *
 * Aaron: "we need a way for you to see everything and how it impacts the gameplay so we can
 * reduce testing time."
 *
 * Right, and the loop it replaces is expensive: he plays, pastes a screen, I find a defect. Six
 * of the last eight defects were not simulation bugs at all — they were two components
 * answering one question differently, or a readout confidently stating something false. Those
 * are mechanically checkable, so a machine should be finding them.
 *
 * This does three things:
 *   1. Plays a full run, acting ONLY on what the Advisor says, like AdvisedCampaign does in C#.
 *   2. Prints a transcript: what it saw, what it did, what happened.
 *   3. Asserts INVARIANTS every day. Any violation is a defect, printed with the day and both
 *      conflicting values.
 *
 *     python3 tools/headless.py tools/playthrough.js
 */

function pad(s,n){ s=String(s); while(s.length<n) s+=" "; return s; }
function pct(x){ return Math.round(x*100)+"%"; }

var DAYS = 240;
var violations = [];
function check(day, ok, what, detail){ if(!ok) violations.push({day:day, what:what, detail:detail}); }

/* ---- open a restaurant the way the game does ---- */
var sub = SITES.filter(function(s){ return s.id === "suburban-high-street"; })[0];
G = newGame(sub, 0x5adbfdbb);
G.seats = 12;
G.fittings = [{id:"t", name:"Standard tables and chairs", seats:12, comfort:0.55}];
G.seatSpend = 12*15;
G.servers = [{id:"s0",name:"Server",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.55}];
G.cooks   = [{id:"c0",name:"Cook",  role:"cook",  wage:16,skill:0.5,claim:0.5,potential:0.6}];
["oven","garde-manger"].forEach(function(st){
  var m = EQUIPMENT.filter(function(e){ return e.station === st; })[0];
  G.stations[st] = [{id:m.id, speed:m.speed, foot:m.foot, capacity:0}];
});
G.stations["cold-storage"] = [{id:"cold-underbar", speed:1, foot:16, capacity:600}];
G.stations["dry-storage"]  = [{id:"dry-shelving",  speed:1, foot:10, capacity:900}];
RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k, 60); });

console.log("A FULL RUN, PLAYED BY THE ADVISOR — " + DAYS + " days on the " + sub.name);
console.log("");
console.log(pad("day",6)+pad("cash",10)+pad("covers",8)+pad("walkout",9)+pad("bound",9)+pad("seats",7)+pad("cooks",7)+"what the Advisor said, and what was done");
console.log(new Array(112).join("-"));

var hiredCount = 0;

for(var day = 1; day <= DAYS; day++){
  /* ---- what every readout claims BEFORE the night ---- */
  var f     = forecastDay();
  var lim   = passLimit(daypartAt(G.windows[0].from));
  var note  = balanceNote();
  var list  = advise();
  var room  = servableSeats() * (60/dwellNow());

  /* ---- INVARIANTS. Each one is a bug this session actually shipped. ---- */

  // The forecast and the Build tab must not contradict each other about what is binding.
  var buildSaysRoom = note.indexOf("the room is the bottleneck") >= 0;
  check(day, !(f.constraint === "kitchen" && buildSaysRoom),
        "forecast and Build tab disagree", "forecast=" + f.constraint + " build=\"" + note + "\"");

  // Every button offered must actually work: affordable AND it must fit.
  list.forEach(function(a){
    if(a.buy)
      check(day, G.cash >= a.buy.cost && freeArea() >= a.buy.foot,
            "Advisor offers a purchase that cannot happen",
            a.buy.name + " $" + a.buy.cost + " needs " + a.buy.foot + " sq ft; cash " +
            Math.round(G.cash) + ", free " + freeArea().toFixed(0));
    if(a.seats)
      check(day, G.cash >= a.seats.perSeat*10 && freeArea() >= a.seats.foot*10,
            "Advisor offers seats that cannot be bought", a.seats.name);
  });

  // Never recommend equipment when the brigade is what is short.
  var offersKit = list.some(function(a){ return a.buy; });
  check(day, !(lim && lim.kind === "brigade" && offersKit),
        "recommends equipment while short of hands",
        "brigade allows " + Math.round(lim.allows) + "/hr, room turns " + Math.round(room) + "/hr");

  // Never recommend seats that would GROSSLY outrun the kitchen.
  //
  // THIS ASSERTION USED TO DEMAND THAT A WHOLE BLOCK FIT INSIDE THE HEADROOM, and that was
  // encoding a measurement the abandoned-plate fix has since reversed. When seats let people
  // sit down and then walk out while their food was still being cooked and binned, adding
  // tables genuinely reduced covers -- 12 seats gave 68.8 and 20 gave 56.8. With plates
  // coming back off the board the same sweep reads 12 -> 69.0 and 18 -> 76.1, and on Aaron's
  // day-128 save the seats this rule forbade were worth $6,659 against $3,414 for the oven
  // recommended instead.
  //
  // So a marginal overshoot is now NEUTRAL, not harmful, and forbidding it froze a
  // restaurant at twelve seats for 128 days. What is still worth catching is gross
  // overshoot -- seats above about 1.5x what the pass can send buy nothing and cost money,
  // which the lever sweep shows flattening hard past that point.
  //
  // Loosening an assertion to make a change pass is the trap; this is retiring one whose
  // premise was measured away. When a fix reverses the finding a guard was built on, the
  // guard is the next thing to go and read.
  var offersSeats = list.some(function(a){ return a.seats; });
  check(day, !(offersSeats && lim && (room + 10*(60/dwellNow())) > lim.allows * 1.5),
        "recommends seats that would grossly outrun the pass",
        "pass " + Math.round(lim.allows) + "/hr vs room " + Math.round(room) + "/hr");

  // Shares are shares.
  if(G.ledger.food > 500)
    check(day, G.spoiled / G.ledger.food <= 1.0001, "binned more food than was ever bought",
          "spoiled " + Math.round(G.spoiled) + " of " + Math.round(G.ledger.food));

  // A solvent, trading restaurant must not be told it is dying.
  var saysRunway = list.some(function(a){ return a.id === "runway"; });
  check(day, !(saysRunway && G.recent.length >= 14 && monthlyBurn() <= 0),
        "runway alarm while not losing money", "cash " + Math.round(G.cash));

  /* ---- act on the single most urgent thing, the way a player would ---- */
  var did = "—";
  var top = list[0];
  if(top){
    if(top.buy)                   { buyEquip(top.buy);            did = "bought " + top.buy.name; }
    else if(top.upgrade)          { sellEquip(top.upgrade.from); buyEquip(top.upgrade.to);
                                    did = "swapped in a " + top.upgrade.to.name; }
    else if(top.seats)            { buySeats(top.seats);          did = "added 10 seats"; }
    else if(top.extend){ extendBuilding();            did = "extended the building"; }
    // Acts on anything that asks for hands, not one hard-coded code — the harness must follow
    // the advice as given, or it is testing my memory of the Advisor rather than the Advisor.
    // ROLE COMES FROM THE ADVICE, never from here. This hardcoded "cook", so a suggestion
    // about the FLOOR hired seven cooks and drove prime cost to 94% -- the harness testing
    // its own assumption instead of the Advisor, which is the exact failure the comment
    // above warns about and did not prevent.
    else if(top.needsWage && G.cash > 9000 && hiredCount < 6){
      var role = top.role === "server" ? "server" : "cook";
      var who = {id:role[0]+(++hiredCount), name:role, role:role,
                 wage: role === "server" ? 12 : 16, skill:0.5, claim:0.5, potential:0.6};
      (role === "server" ? G.servers : G.cooks).push(who);
      did = "hired a " + role;
    }
    else if(top.id === "price")   { var s = suggestedPosition();
                                    RECIPES.forEach(function(r){ if(G.onMenu.has(r.id)) G.prices[r.id] = Math.round(r.base*s.position*100)/100; });
                                    did = "repriced the card to " + s.position.toFixed(2) + "x"; }
  }

  var d = runDay();

  if(day <= 3 || day % 20 === 0 || did !== "—")
    console.log(pad("d"+day,6)+pad("$"+Math.round(G.cash),10)+pad(d.covers,8)+pad(d.walkouts,9)+
                pad(f.constraint,9)+pad(G.seats,7)+pad(G.cooks.length,7)+
                (top ? "[" + top.id + "] " : "[quiet] ") + did);
}

/* ---- the verdict ---- */
console.log("");
console.log("FINISHED — cash $" + Math.round(G.cash) + ", " + G.seats + " seats, " + G.cooks.length +
            " cooks, standing " + Math.round(G.rep.standing*100) + "/100");
var last30 = G.recent.slice(-30);
var avgCov = last30.reduce(function(a,x){return a+x.covers;},0)/last30.length;
var avgRev = last30.reduce(function(a,x){return a+(x.revenue||0);},0)/last30.length;
var avgWalk= last30.reduce(function(a,x){return a+x.walkouts;},0)/last30.length;
console.log("  last 30 days: " + avgCov.toFixed(1) + " covers/day, $" + Math.round(avgRev) +
            "/day, " + avgWalk.toFixed(1) + " walkouts/day");
console.log("  prime cost " + (G.ledger.revenue>0 ? pct((G.ledger.food+G.ledger.labor)/G.ledger.revenue) : "—") +
            ", binned " + pct(G.ledger.food>0 ? G.spoiled/G.ledger.food : 0) + " of the food bill");
console.log("");

if(!violations.length){
  console.log("NO CONTRADICTIONS FOUND across " + DAYS + " days.");
} else {
  var byKind = {};
  violations.forEach(function(v){ (byKind[v.what] = byKind[v.what] || []).push(v); });
  console.log(violations.length + " CONTRADICTIONS across " + DAYS + " days:");
  for(var k in byKind){
    var days = byKind[k].map(function(v){ return "d"+v.day; });
    console.log("  " + byKind[k].length + "x  " + k);
    console.log("        first: " + byKind[k][0].detail);
    console.log("        days:  " + days.slice(0,10).join(", ") + (days.length>10 ? ", ..." : ""));
  }
}
