/*
 * Does the transcript show what each decision actually DID?
 *
 * Marketing a claim you cannot back up: covers up, satisfaction DOWN. Then fixing the
 * ingredients: satisfaction up. That chain is the answer to "does marketing have an impact",
 * and it is invisible in a day log that only shows what happened.
 *
 *     python3 tools/headless.py tools/probe-impact.js
 */
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];
G = newGame(sub, 777);
G.cash=120000; G.seats=40;
G.fittings=[{id:"t",name:"T",seats:40,comfort:0.55}]; G.seatSpend=600; G.floorArea=3000;
G.servers=[]; for(var i=0;i<3;i++) G.servers.push({id:"s"+i,name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5});
G.cooks=[];   for(var i=0;i<3;i++) G.cooks.push({id:"c"+i,name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});
["oven","garde-manger"].forEach(function(st){
  var m=EQUIPMENT.filter(function(e){return e.station===st;})[0];
  G.stations[st]=[{id:m.id,speed:m.speed,foot:m.foot,capacity:0,holds:m.holds||1},
                  {id:m.id,speed:m.speed,foot:m.foot,capacity:0,holds:m.holds||1}]; });
G.stations["cold-storage"]=[{id:"c",speed:1,foot:90,capacity:3000}];
G.stations["dry-storage"]=[{id:"d",speed:1,foot:34,capacity:4000}];
RECIPES.forEach(function(r){ if(!G.onMenu.has(r.id)) return; for(var k in r.ing) orderStock(k,200); });

for(var n=0; n<120; n++){
  if(n===40){ G.campaign = {claim:"ingredients", channel:"press"}; didThat("Started marketing: the food press, claiming what we cook with."); }
  if(n===80){ G.supplier = "premium-harvest"; didThat("Sourcing switched to Premium Harvest Partners (tier 5)."); }
  runDay(); G.day++;
}
var lines = fullTranscript().split("\n");
var i = lines.indexOf("WHAT EACH DECISION DID — 14 days before against 14 days after");
console.log(lines.slice(i, i+12).join("\n"));
console.log("");
var j = lines.indexOf("EVERY METRIC, EVERY DAY (comma separated)");
console.log(lines[j+2].slice(0,120));
console.log(lines[j+3].slice(0,120));
console.log("... " + (lines.length - j) + " metric rows");
