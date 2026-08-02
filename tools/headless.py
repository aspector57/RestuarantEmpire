#!/usr/bin/env python3
"""
Run the browser build's simulation WITHOUT a browser, using the JavaScriptCore CLI that ships
with macOS. No node, no install.

    python3 tools/headless.py <probe.js>

WHY THIS EXISTS. `pass.html` re-implements the C# core in JavaScript so the loop can be felt
rather than read, and it has drifted twice — invented equipment speeds, and a markup formula
ported by name instead of by definition. Until now the only way to check it was for a human to
play it, so drift was found by Aaron losing an evening to a broken game.

This runs the same file headlessly, so the port can be MEASURED against the engine instead of
trusted. It found a real divergence within minutes of existing.

The DOM is stubbed to a proxy that swallows everything: the simulation and the forecast never
read from the page, so the model half runs unmodified. Anything that touches rendering is a
no-op. Your probe script is appended after the model and can use G, RECIPES, EQUIPMENT, SITES,
newGame, runDay, forecastDay, autopsy — whatever the build defines.
"""
import pathlib
import subprocess
import sys

JSC = "/System/Library/Frameworks/JavaScriptCore.framework/Versions/A/Helpers/jsc"
ROOT = pathlib.Path(__file__).resolve().parent.parent

DOM_STUB = """
var __noop = function(){ return __el; };
var __el = new Proxy({}, {
  get: function(o, k){
    if(k === "style" || k === "classList" || k === "dataset") return __el;
    if(k === "innerHTML" || k === "textContent" || k === "value" || k === "className") return "";
    if(k === "hidden" || k === "checked" || k === "disabled") return false;
    if(k === "length") return 0;
    if(k === Symbol.iterator) return [][Symbol.iterator].bind([]);
    return __noop;
  },
  set: function(){ return true; }
});
var document = { querySelector:__noop, querySelectorAll:function(){ return []; },
  getElementById:__noop, createElement:__noop, addEventListener:__noop, body:__el,
  documentElement:__el };
var window = { addEventListener:__noop,
  matchMedia:function(){ return { matches:false, addEventListener:__noop }; },
  localStorage:{ getItem:function(){ return null; }, setItem:__noop } };
var localStorage = window.localStorage;
var requestAnimationFrame = __noop;
var console = { log: function(){
  var a = []; for (var i = 0; i < arguments.length; i++) a.push(String(arguments[i]));
  print(a.join(" "));
} };
"""


def find_build():
    """pass.html lives in the scratchpad, not the repo. Take a path or go looking."""
    for candidate in ROOT.rglob("pass.html"):
        return candidate
    raise SystemExit("pass.html not found — pass its path as the second argument.")


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)

    probe = pathlib.Path(sys.argv[1]).read_text()
    build = pathlib.Path(sys.argv[2]) if len(sys.argv) > 2 else find_build()

    html = build.read_text()
    js = html[html.index("<script>") + 8:html.rindex("</script>")]

    bundle = ROOT / "tools" / ".headless-bundle.js"
    bundle.write_text(DOM_STUB + js + "\n" + probe)

    if not pathlib.Path(JSC).exists():
        raise SystemExit("JavaScriptCore CLI not found at " + JSC)

    result = subprocess.run([JSC, str(bundle)], capture_output=True, text=True)
    sys.stdout.write(result.stdout)
    sys.stderr.write(result.stderr)
    return result.returncode


if __name__ == "__main__":
    sys.exit(main())
