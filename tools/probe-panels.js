/*
 * Does every panel actually RENDER, licensed and unlicensed?
 *
 * RUN THIS AFTER ANY UI CHANGE. A brace-balanced file can still throw on the first click, and
 * that is not a theoretical risk: `wrap.appendChild(pc)` referenced a variable that never
 * existed, threw a ReferenceError right after the dish list, and silently killed the rest of
 * the Menu tab -- which is why Aaron could not find the liquor licence for two sessions. The
 * brace counter said the file was fine. It was not.
 *
 *     python3 tools/headless.py tools/probe-panels.js
 */
// Every panel must actually RENDER. A brace-balanced file can still throw on the first click,
// and a ReferenceError in panelMenu hid the liquor licence for two sessions.
var sub = SITES.filter(function(s){return s.id==="suburban-high-street";})[0];
G = newGame(sub, 4242);
G.cash = 40000; G.seats = 30;
G.fittings=[{id:"t",name:"T",seats:30,comfort:0.5}]; G.seatSpend=450;
G.servers.push({id:"s0",name:"S",role:"server",wage:12,skill:0.5,claim:0.5,potential:0.5});
G.cooks.push({id:"c0",name:"C",role:"cook",wage:16,skill:0.5,claim:0.5,potential:0.5});
["oven","garde-manger","bar"].forEach(function(st){
  var m=EQUIPMENT.filter(function(e){return e.station===st;})[0];
  G.stations[st]=[{id:m.id,speed:m.speed,foot:m.foot,capacity:0}]; });
G.stations["cold-storage"]=[{id:"c",speed:1,foot:90,capacity:3000}];
G.stations["dry-storage"]=[{id:"d",speed:1,foot:34,capacity:4000}];

var panels = ["panelService","panelMenu","panelPantry","panelBuild","panelTeam","panelBooks"];
var failed = 0;
[false, true].forEach(function(licensed){
  G.licence = licensed;
  panels.forEach(function(name){
    if(typeof this[name] !== "function") return;
    try { this[name](); console.log("  ok    " + name + (licensed ? " (licensed)" : " (unlicensed)")); }
    catch(e){ failed++; console.log("  THROW " + name + (licensed?" (licensed)":" (unlicensed)") + " -> " + e); }
  }, this);
});
try { renderAdvisor(); console.log("  ok    renderAdvisor"); } catch(e){ failed++; console.log("  THROW renderAdvisor -> " + e); }
try { renderForecast(); console.log("  ok    renderForecast"); } catch(e){ failed++; console.log("  THROW renderForecast -> " + e); }
console.log("");
console.log(failed === 0 ? "ALL PANELS RENDER" : failed + " PANEL(S) THROW");
