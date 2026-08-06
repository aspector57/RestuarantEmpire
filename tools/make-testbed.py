#!/usr/bin/env python3
"""
Build the ONE-SCENARIO testbed from the full game.

Aaron: "we're trying to balance 28 combinations at once" -- 7 concepts x 4 streets -- which is
why nothing ever settles: fixing fine dining on the nightlife street knocks the pizzeria on the
suburban one out of shape. So this is the same game with the choice removed, locked to the one
setup we are actually trying to get right.

GENERATED, NOT FORKED. A hand-copied second file is exactly the drift that has cost this
project twenty-four commits and five bugs in a single session. Re-run this after any change to
web/pass.html and the testbed follows automatically:

    python3 tools/make-testbed.py
"""
import pathlib, re, sys

SITE = "suburban-high-street"
CONCEPT = "neighborhood-standard"

src = pathlib.Path("web/pass.html").read_text()
out = pathlib.Path("web/onestreet.html")

# 1. A different title, so the two are never confused in a browser tab or a gallery.
src = src.replace("<title>The Pass — Restaurant Empire</title>",
                  "<title>The Pass — One Street</title>", 1)

# 2. Replace the two-step picker with a single brief and one button.
setup_start = src.index('<div id="setup" class="setup">')
setup_end = src.index("</div>", src.index('<div class="sites" id="sites"></div>')) + len("</div>")
brief = '''<div id="setup" class="setup">
  <p class="lede">
    One street, one restaurant, one question: <b>can you make it pay?</b>
  </p>
  <div class="site" style="cursor:default">
    <span>
      <h3>Neighbourhood standard, on the Suburban High Street</h3>
      <p>A four-dish card at close to the price the dishes were designed for. Dinner only.
         Sixteen covers, one cook, one server, and a modest kitchen. The street is steady
         rather than busy — nobody is going to fill your room for you.</p>
    </span>
    <span class="fig"><b>$30,000</b>to your name<br>less key money and fit-out<br>rent every month regardless</span>
  </div>
  <p class="hint" style="margin-top:18px">
    This is a <b>testbed</b>, not the whole game — the concept and the street are fixed on
    purpose, so the numbers underneath can be got right one at a time. The full version lets
    you choose both.
  </p>
  <p class="hint">
    <b>What is useful to report:</b> the moment you were bored, the moment something felt
    unfair, and the moment you stopped having to think. Play until one of those happens, then
    open <b>Service &rarr; Copy the whole run</b> and send the transcript back — it carries
    every decision you made, everything the Advisor told you, and what happened next.
  </p>
  <div style="margin-top:20px">
    <button class="btn primary" id="begin">Open the doors</button>
  </div>
</div>'''
src = src[:setup_start] + brief + src[setup_end:]

# 3. Skip the pickers entirely and start the locked scenario.
old = re.search(r"function buildSetup\(\)\{.*?\n\}", src, re.S)
if not old:
    sys.exit("could not find buildSetup() — has web/pass.html changed shape?")
src = src[:old.start()] + '''function buildSetup(){
  // No choice to make here — the whole point of this build is that the setup is fixed.
  const go = $("#begin");
  if(go) go.onclick = () => start(
    SITES.filter(s => s.id === "%s")[0], "%s");
}''' % (SITE, CONCEPT) + src[old.end():]

out.write_text(src)

# Guard: the generated file must still contain the game, not just the shell.
for needed in ["function runDay(", "function advise(", "function fullTranscript("]:
    if needed not in src:
        sys.exit("generated testbed is missing " + needed)

print("wrote %s (%d lines) — %s / %s" % (out, src.count("\n") + 1, CONCEPT, SITE))
