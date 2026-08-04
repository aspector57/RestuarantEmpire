/*
 * Does the full-run transcript contain what it needs to — state, advice, and every move?
 *
 *     python3 tools/headless.py tools/probe-export.js
 */
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];
G = newGame(sub, 0x540bf7bf);
G.cash=60000; G.seats=32;
G.fittings=[{id:"t",name:"T",seats:32,comfort:0.55}]; G.seatSpend=480;
G.servers=[{id:"s0",name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5}];
G.cooks=[]; for(var i=0;i<3;i++) G.cooks.push({id:"c"+i,name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});
["oven","garde-manger"].forEach(function(st){
  var m=EQUIPMENT.filter(function(e){return e.station===st;})[0];
  G.stations[st]=[{id:m.id,speed:m.speed,foot:m.foot,capacity:0,holds:m.holds||1},
                  {id:m.id,speed:m.speed,foot:m.foot,capacity:0,holds:m.holds||1}]; });
G.stations["cold-storage"]=[{id:"cold-walkin",speed:1,foot:90,capacity:3000}];
G.stations["dry-storage"]=[{id:"dry-racking",speed:1,foot:34,capacity:4000}];
RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k,200); });
G.campaign = { claim:"ingredients", channel:"press" };

for(var d=0; d<40; d++){
  var top = advise()[0];
  var head = top ? "["+top.id+"] "+top.head : "[quiet] Nothing needs you right now.";
  if(head !== G.lastAdvice){ say("advice", "Advisor: " + head); G.lastAdvice = head; }
  if(d===12){ G.prices["margherita"] = 19; noteDecision("margherita","price set to $19.00"); }
  if(d===25){ G.servers.push({id:"s1",name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5}); didThat("Hired a second server."); }
  runDay(); G.day++;   // the clock lives in advance(), which a bare runDay() bypasses
}

var text = fullTranscript();
var lines = text.split("\n");
console.log(lines.slice(0, 34).join("\n"));
console.log("...");
console.log(lines.filter(function(l){ return l.indexOf("YOU ")>=0 || l.indexOf("ADVISOR")>=0; }).slice(0,8).join("\n"));
console.log("");
console.log("(" + lines.length + " lines, " + text.length + " characters)");
