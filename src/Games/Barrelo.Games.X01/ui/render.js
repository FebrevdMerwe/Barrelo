/* Barrelo.Games.X01's own board region. Reads snapshot.payload (X01StatePayload:
   { groups: [{groupIndex, playerIds, remainingScore, legsWon, setsWon}], currentVisitThrows: [...],
   justEndedVisitThrows: [...] }) and the shell-provided playerId -> name map. A group shares one
   cumulative score (team play); an ungrouped/solo player is just a group of one. The shell never looks
   inside payload itself — this file owns everything about how X01's state renders.

   Also owns a read-only dartboard visualization centered in the board region: it shows each dart
   landing (with a short flight animation) as currentVisitThrows/justEndedVisitThrows grow, then
   clears when a visit ends. This is a separate, non-interactive board from the shared click-to-score
   input board in wwwroot/dartboard.js (used by the match screen's input drawer) — the wedge-drawing
   geometry below is intentionally duplicated rather than shared, so this plugin stays self-contained. */
(function (global) {
  "use strict";

  /* Self-contained: pulls in its own stylesheet, so the shell never needs to
     know this plugin's CSS filename. Relies on document.currentScript, which
     resolves correctly during a script's synchronous top-level execution even
     when the shell injects this script dynamically. */
  (function loadStyles() {
    var href = document.currentScript.src.replace(/render\.js(\?.*)?$/, "style.css");
    if (document.querySelector('link[href="' + href + '"]')) return;
    var link = document.createElement("link");
    link.rel = "stylesheet";
    link.href = href;
    document.head.appendChild(link);
  })();

  var lastRemaining = {};

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }
  function tallyMarks(count, cls) {
    var out = "";
    for (var i = 0; i < count; i++) out += '<span class="' + cls + '"></span>';
    return '<span class="tally-marks">' + out + "</span>";
  }

  /* ---------- read-only dartboard visualization ----------
     Geometry mirrors wwwroot/dartboard.js (same NUMBERS order, same R radii, same polar/annularSector
     math) but only draws the static board art — no tabindex/role/click handling/data-ring attributes,
     no setDeadTargets/onThrow. */
  var NUMBERS = [20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5];
  var R = { bullIn: 6, bullOut: 15, tripleIn: 58, tripleOut: 64, doubleIn: 94, doubleOut: 100, numRing: 109 };
  var LAUNCH = { x: 0, y: 112 };

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

  function buildDartboardSvg() {
    var svg = el("svg", { viewBox: "-118 -118 236 236", class: "x01-dartboard-svg", "aria-hidden": "true" });
    var gWedges = el("g", {});
    var gBulls = el("g", {});
    var gNumbers = el("g", { class: "x01-num-ring" });

    function addWedge(r1, r2, a1, a2, fill) {
      gWedges.appendChild(el("path", {
        d: annularSector(r1, r2, a1, a2), fill: fill, stroke: "#6b4d20", "stroke-width": "0.6"
      }));
    }

    for (var i = 0; i < 20; i++) {
      var seg = NUMBERS[i];
      var a1 = i * 18 - 9, a2 = i * 18 + 9;
      var isA = i % 2 === 0;
      var singleFill = isA ? "var(--chalk)" : "var(--slate-2)";
      var accentFill = isA ? "var(--board-red)" : "var(--board-green)";

      addWedge(R.bullOut, R.tripleIn, a1, a2, singleFill);
      addWedge(R.tripleIn, R.tripleOut, a1, a2, accentFill);
      addWedge(R.tripleOut, R.doubleIn, a1, a2, singleFill);
      addWedge(R.doubleIn, R.doubleOut, a1, a2, accentFill);

      var np = polar(R.numRing, i * 18);
      var t = el("text", { x: fmt(np.x), y: fmt(np.y), "font-size": "12", "text-anchor": "middle", "dominant-baseline": "middle" });
      t.textContent = seg;
      gNumbers.appendChild(t);
    }

    gBulls.appendChild(el("circle", { cx: 0, cy: 0, r: R.bullOut, fill: "var(--board-green)", stroke: "#6b4d20", "stroke-width": "0.6" }));
    gBulls.appendChild(el("circle", { cx: 0, cy: 0, r: R.bullIn, fill: "var(--board-red)", stroke: "#6b4d20", "stroke-width": "0.6" }));

    var rim = el("circle", { cx: 0, cy: 0, r: R.doubleOut + 0.6, fill: "none", stroke: "var(--brass-dim)", "stroke-width": "1.4" });

    svg.appendChild(gWedges);
    svg.appendChild(gBulls);
    svg.appendChild(rim);
    svg.appendChild(gNumbers);
    svg.appendChild(el("g", { class: "x01-darts-layer" }));

    return svg;
  }

  function ensureBoardSkeleton(container) {
    var wrap = container.querySelector(".x01-board");
    if (wrap) return wrap;

    container.innerHTML = "";
    wrap = document.createElement("div");
    wrap.className = "x01-board";

    var stage = document.createElement("div");
    stage.className = "x01-dartboard-stage";
    stage.appendChild(buildDartboardSvg());
    wrap.appendChild(stage);

    container.appendChild(wrap);
    return wrap;
  }

  /* A dart marker's local (0,0) is its tip/impact point — the shaft and flight extend outward from
     it — so translating the whole <g> to a throw's board position lands the tip exactly there.
     Sized well beyond a real dart's proportions (board radius is 100 units) so it actually reads at
     a glance on a TV-distance display, not just up close. */
  function buildDartMarker() {
    var g = el("g", { class: "x01-dart" });
    g.appendChild(el("line", { class: "x01-dart-shaft", x1: 0, y1: 0, x2: 0, y2: 17 }));
    g.appendChild(el("path", { class: "x01-dart-flight", d: "M -8 17 L 0 30 L 8 17 L 0 23 Z" }));
    g.appendChild(el("circle", { class: "x01-dart-tip", cx: 0, cy: 0, r: 4.5 }));
    return g;
  }

  /* BoardPosition (Barrelo.GameSdk) uses standard math orientation (+Y up); SVG/this board's polar()
     is Y-down, so the Y sign must flip or every dart renders vertically mirrored. Magnitude 1.0 in
     BoardPosition is the double-ring outer edge, i.e. R.doubleOut (100) in this board's local units. */
  function throwPosition(t) {
    return { x: t.position.x * R.doubleOut, y: -t.position.y * R.doubleOut };
  }

  // ---- per-throw animation state (module-level; persists across renderGameBoard calls) ----
  var hasRenderedBoardOnce = false;
  var stuckThrowIds = [];
  var dartNodesByThrowId = {};

  function placeDart(dartsLayer, t, animate) {
    var node = buildDartMarker();
    var pos = throwPosition(t);
    node.style.setProperty("--land-x", fmt(pos.x) + "px");
    node.style.setProperty("--land-y", fmt(pos.y) + "px");

    if (animate) {
      // Commit a "launch" pose first so the later reset to the CSS-driven landed
      // transform is a genuine change the browser will transition, not a no-op.
      node.style.transform = "translate(" + LAUNCH.x + "px, " + LAUNCH.y + "px) rotate(-18deg) scale(.85)";
      node.style.opacity = "0.85";
    }

    dartsLayer.appendChild(node);

    if (animate) {
      void node.getBoundingClientRect();
      node.style.transform = "";
      node.style.opacity = "";
    }

    dartNodesByThrowId[t.throwId] = node;
  }

  function fadeOutAllStuckDarts() {
    Object.keys(dartNodesByThrowId).forEach(function (id) {
      var node = dartNodesByThrowId[id];
      delete dartNodesByThrowId[id];
      node.classList.add("x01-dart--fading");
      node.addEventListener("transitionend", function onEnd(e) {
        if (e.propertyName !== "opacity") return;
        node.removeEventListener("transitionend", onEnd);
        if (node.parentNode) node.parentNode.removeChild(node);
      });
    });
  }

  /* True when `next` is `prev` with zero or more throws appended — i.e. the same visit continuing,
     not a new one starting. Used (as a safety net alongside justEndedVisitThrows, e.g. for Undo) to
     detect a visit resetting without an explicit end-of-visit snapshot. */
  function extendsPrevious(prev, next) {
    if (next.length < prev.length) return false;
    for (var i = 0; i < prev.length; i++) {
      if (prev[i] !== next[i]) return false;
    }
    return true;
  }

  function updateDartboard(dartsLayer, snapshot) {
    var payload = snapshot.payload || {};
    var currentThrows = Array.isArray(payload.currentVisitThrows) ? payload.currentVisitThrows : [];
    var endedThrows = Array.isArray(payload.justEndedVisitThrows) ? payload.justEndedVisitThrows : [];

    if (endedThrows.length > 0) {
      endedThrows.forEach(function (t) {
        if (!dartNodesByThrowId[t.throwId]) placeDart(dartsLayer, t, hasRenderedBoardOnce);
      });
      fadeOutAllStuckDarts();
      stuckThrowIds = [];
      hasRenderedBoardOnce = true;
      return;
    }

    var nextIds = currentThrows.map(function (t) { return t.throwId; });
    if (stuckThrowIds.length > 0 && !extendsPrevious(stuckThrowIds, nextIds)) {
      fadeOutAllStuckDarts();
    }

    currentThrows.forEach(function (t) {
      if (!dartNodesByThrowId[t.throwId]) placeDart(dartsLayer, t, hasRenderedBoardOnce);
    });

    stuckThrowIds = nextIds;
    hasRenderedBoardOnce = true;
  }

  /* ---------- score panels + wiring ---------- */

  function renderGameBoard(container, snapshot, playerNames) {
    var groups = (snapshot.payload && snapshot.payload.groups) || [];
    var wrap = ensureBoardSkeleton(container);

    wrap.querySelectorAll(".x01-panel").forEach(function (p) { p.remove(); });

    groups.forEach(function (g, index) {
      var isActive = g.playerIds.indexOf(snapshot.currentPlayerId) !== -1 && !snapshot.isComplete;
      var pulse = lastRemaining[g.groupIndex] !== undefined && lastRemaining[g.groupIndex] !== g.remainingScore;
      lastRemaining[g.groupIndex] = g.remainingScore;

      var names = g.playerIds.map(function (id) {
        var isThrower = id === snapshot.currentPlayerId;
        return (isThrower ? "› " : "") + escapeHtml((playerNames && playerNames[id]) || "Player");
      }).join(" & ");

      var panel = document.createElement("div");
      panel.className = "x01-panel" + (isActive ? " active" : "");
      panel.style.order = index < Math.ceil(groups.length / 2) ? "-1" : "1";
      panel.innerHTML =
        '<div class="x01-name">' + names + "</div>" +
        '<div class="x01-remaining' + (pulse ? " pulse" : "") + '">' + g.remainingScore + "</div>" +
        '<div class="tallies">' +
          '<span class="tally-group">legs' + tallyMarks(g.legsWon, "chalk-tick") + "</span>" +
          '<span class="tally-group">sets' + tallyMarks(g.setsWon, "brass-pip") + "</span>" +
        "</div>";
      wrap.appendChild(panel);
    });

    updateDartboard(wrap.querySelector(".x01-darts-layer"), snapshot);
  }

  global.renderGameBoard = renderGameBoard;
})(window);
