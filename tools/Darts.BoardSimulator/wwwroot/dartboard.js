/* Shared dartboard geometry + scoring helpers.
   Copied verbatim from Darts.Api/wwwroot/dartboard.js — this app deliberately has zero references to the
   platform, so it carries its own copy rather than a shared assembly/static-file link. */
(function (global) {
  "use strict";

  var NUMBERS = [20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5];
  var R = { bullIn: 6, bullOut: 15, tripleIn: 58, tripleOut: 64, doubleIn: 94, doubleOut: 100, numRing: 109 };

  function polar(r, deg) {
    var rad = (deg * Math.PI) / 180;
    return { x: r * Math.sin(rad), y: -r * Math.cos(rad) };
  }
  function fmt(n) { return Math.round(n * 100) / 100; }
  function annularSector(r1, r2, a1, a2) {
    var p1 = polar(r1, a1), p2 = polar(r2, a1), p3 = polar(r2, a2), p4 = polar(r1, a2);
    return "M " + fmt(p1.x) + " " + fmt(p1.y) +
      " L " + fmt(p2.x) + " " + fmt(p2.y) +
      " A " + r2 + " " + r2 + " 0 0 1 " + fmt(p3.x) + " " + fmt(p3.y) +
      " L " + fmt(p4.x) + " " + fmt(p4.y) +
      " A " + r1 + " " + r1 + " 0 0 0 " + fmt(p1.x) + " " + fmt(p1.y) + " Z";
  }

  var svgNS = "http://www.w3.org/2000/svg";
  function el(name, attrs) {
    var e = document.createElementNS(svgNS, name);
    for (var k in attrs) e.setAttribute(k, attrs[k]);
    return e;
  }

  function scoreFor(ring, segment) {
    switch (ring) {
      case "Miss": return 0;
      case "InnerBull": return 50;
      case "OuterBull": return 25;
      case "Double": return segment * 2;
      case "Triple": return segment * 3;
      default: return segment;
    }
  }
  function notationFor(ring, segment) {
    switch (ring) {
      case "Miss": return "MISS";
      case "InnerBull": return "BULL";
      case "OuterBull": return "25";
      case "Double": return "D" + segment;
      case "Triple": return "T" + segment;
      default: return String(segment);
    }
  }
  function isValidCheckoutRing(ring) { return ring === "Double" || ring === "InnerBull"; }

  /**
   * Builds the interactive SVG dartboard inside `svgEl` and returns a controller.
   * This is the one chrome asset both plugins share as an input device —
   * neither game defines its own board, only its own score display.
   */
  function createDartboard(svgEl) {
    var gWedges = el("g", { id: "wedges" });
    var gBulls = el("g", { id: "bulls" });
    var gNumbers = el("g", { class: "num-ring" });
    var activateCb = null;

    function addWedge(r1, r2, a1, a2, fill, segment, ring, label) {
      var p = el("path", {
        d: annularSector(r1, r2, a1, a2),
        fill: fill,
        stroke: "#6b4d20",
        "stroke-width": "0.6",
        "data-segment": segment,
        "data-ring": ring,
        tabindex: "0",
        role: "button",
        "aria-label": label
      });
      gWedges.appendChild(p);
    }

    for (var i = 0; i < 20; i++) {
      var seg = NUMBERS[i];
      var a1 = i * 18 - 9, a2 = i * 18 + 9;
      var isA = i % 2 === 0;
      var singleFill = isA ? "var(--chalk)" : "var(--slate-2)";
      var accentFill = isA ? "var(--board-red)" : "var(--board-green)";

      addWedge(R.bullOut, R.tripleIn, a1, a2, singleFill, seg, "Inner", "Segment " + seg + " single");
      addWedge(R.tripleIn, R.tripleOut, a1, a2, accentFill, seg, "Triple", "Segment " + seg + " triple, scores " + (seg * 3));
      addWedge(R.tripleOut, R.doubleIn, a1, a2, singleFill, seg, "Outer", "Segment " + seg + " single");
      addWedge(R.doubleIn, R.doubleOut, a1, a2, accentFill, seg, "Double", "Segment " + seg + " double, scores " + (seg * 2));

      var np = polar(R.numRing, i * 18);
      var t = el("text", { x: fmt(np.x), y: fmt(np.y), "font-size": "12", "text-anchor": "middle", "dominant-baseline": "middle" });
      t.textContent = seg;
      gNumbers.appendChild(t);
    }

    var bullOuter = el("circle", { class: "wedge-hit", cx: 0, cy: 0, r: R.bullOut, fill: "var(--board-green)", stroke: "#6b4d20", "stroke-width": "0.6", "data-segment": 25, "data-ring": "OuterBull", tabindex: "0", role: "button", "aria-label": "Outer bull, scores 25" });
    var bullInner = el("circle", { class: "wedge-hit", cx: 0, cy: 0, r: R.bullIn, fill: "var(--board-red)", stroke: "#6b4d20", "stroke-width": "0.6", "data-segment": 50, "data-ring": "InnerBull", tabindex: "0", role: "button", "aria-label": "Inner bull, scores 50" });
    gBulls.appendChild(bullOuter);
    gBulls.appendChild(bullInner);

    var rim = el("circle", { cx: 0, cy: 0, r: R.doubleOut + 0.6, fill: "none", stroke: "var(--brass-dim)", "stroke-width": "1.4", "pointer-events": "none" });

    svgEl.setAttribute("viewBox", "-118 -118 236 236");
    svgEl.appendChild(gWedges);
    svgEl.appendChild(gBulls);
    svgEl.appendChild(rim);
    svgEl.appendChild(gNumbers);

    function handleActivate(target) {
      var ring = target.getAttribute("data-ring");
      var segment = parseInt(target.getAttribute("data-segment"), 10);
      if (!ring || !activateCb) return;
      activateCb(ring, segment);
    }

    svgEl.addEventListener("click", function (e) {
      var t = e.target;
      if (t.hasAttribute && t.hasAttribute("data-ring")) handleActivate(t);
    });
    svgEl.addEventListener("keydown", function (e) {
      if (e.key !== "Enter" && e.key !== " ") return;
      var t = e.target;
      if (t.hasAttribute && t.hasAttribute("data-ring")) {
        e.preventDefault();
        handleActivate(t);
      }
    });

    return {
      onThrow: function (cb) { activateCb = cb; },
      setDisabled: function (disabled) { svgEl.classList.toggle("disabled", !!disabled); },
      /** deadTargets: Set/array of segment numbers (15-20) and/or "BULL" that are closed to scoring. */
      setDeadTargets: function (deadTargets) {
        var dead = new Set(deadTargets || []);
        var all = svgEl.querySelectorAll("[data-ring]");
        all.forEach(function (node) {
          var ring = node.getAttribute("data-ring");
          var isBull = ring === "InnerBull" || ring === "OuterBull";
          var segment = parseInt(node.getAttribute("data-segment"), 10);
          var isDead = isBull ? dead.has("BULL") : dead.has(segment);
          node.classList.toggle("dead", isDead);
        });
      }
    };
  }

  global.DartScoring = { NUMBERS: NUMBERS, scoreFor: scoreFor, notationFor: notationFor, isValidCheckoutRing: isValidCheckoutRing };
  global.createDartboard = createDartboard;
})(window);
