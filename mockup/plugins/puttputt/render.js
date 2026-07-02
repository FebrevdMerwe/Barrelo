/* Darts.Games.PuttPutt — a mini-golf green rendered like Kickoff's pitch.
   Every dart is a putt: segment sets direction (reusing the dartboard's own
   angle order, same convention as Kickoff), ring sets how far the ball
   travels. Unlike Kickoff's one shared ball, each player putts their own
   ball on the same green — totals are compared per player, lowest wins.
   The green has no interior obstacles; the ball simply bounces off the four
   outer walls (see reflect01 below), and hole variety comes purely from
   each hole's tee/cup placement. */
(function (global) {
  "use strict";

  /* Self-contained: pulls in its own stylesheet, so the shell never needs to
     know this plugin's CSS filename. Relies on document.currentScript, which
     only resolves during synchronous <script src> execution — fine here
     since match.html loads plugins as plain, non-deferred scripts. */
  (function loadStyles() {
    var href = document.currentScript.src.replace(/render\.js(\?.*)?$/, "style.css");
    if (document.querySelector('link[href="' + href + '"]')) return;
    var link = document.createElement("link");
    link.rel = "stylesheet";
    link.href = href;
    document.head.appendChild(link);
  })();

  var HOLES_COUNT = 9;
  var MAX_PUTTS = 6;
  var CAPTURE_RADIUS = 0.055;
  var TRAIL_MAX = 6;

  /* Same magnitude table as Kickoff — one learned mental model of "how hard
     each ring hits" across every dart-driven plugin. */
  var MAGNITUDE = { InnerBull: 0.08, OuterBull: 0.08, Inner: 0.16, Outer: 0.26, Triple: 0.38, Double: 0.5 };

  var HOLES = [
    { tee: { x: 0.50, y: 0.88 }, cup: { x: 0.50, y: 0.12 } },
    { tee: { x: 0.12, y: 0.50 }, cup: { x: 0.88, y: 0.50 } },
    { tee: { x: 0.15, y: 0.85 }, cup: { x: 0.85, y: 0.15 } },
    { tee: { x: 0.85, y: 0.15 }, cup: { x: 0.15, y: 0.85 } },
    { tee: { x: 0.30, y: 0.70 }, cup: { x: 0.60, y: 0.25 } },
    { tee: { x: 0.20, y: 0.20 }, cup: { x: 0.80, y: 0.80 } },
    { tee: { x: 0.75, y: 0.30 }, cup: { x: 0.20, y: 0.75 } },
    { tee: { x: 0.50, y: 0.85 }, cup: { x: 0.10, y: 0.10 } },
    { tee: { x: 0.85, y: 0.50 }, cup: { x: 0.50, y: 0.50 } }
  ];

  /* Closed-form reflection off walls at 0 and 1 ("tent map") — handles any
     number of bounces in one line, no iterative collision detection. */
  function reflect01(v) {
    var m = v % 2;
    if (m < 0) m += 2;
    return m <= 1 ? m : 2 - m;
  }

  function resetHoleState(state) {
    var tee = HOLES[state.currentHole].tee;
    state.balls = [{ x: tee.x, y: tee.y }, { x: tee.x, y: tee.y }];
    state.trails = [[{ x: tee.x, y: tee.y }], [{ x: tee.x, y: tee.y }]];
    state.strokes = [0, 0];
    state.visit = [];
    state.currentPlayer = 0; /* fixed tee order — no honors reordering */
    state.meta = "Hole " + (state.currentHole + 1) + " of " + HOLES_COUNT;
  }

  function freshState() {
    var state = {
      currentHole: 0,
      currentPlayer: 0,
      balls: null,
      trails: null,
      strokes: null,
      scores: [new Array(HOLES_COUNT).fill(null), new Array(HOLES_COUNT).fill(null)],
      visit: [],
      complete: false,
      winners: [],
      meta: null,
      winTitle: null,
      winText: null,
      lastEvent: null,
      deadTargets: []
    };
    resetHoleState(state);
    return state;
  }

  function finishRound(state) {
    var totals = state.scores.map(function (holes) {
      return holes.reduce(function (a, b) { return a + b; }, 0);
    });
    var min = Math.min(totals[0], totals[1]);
    state.winners = [0, 1].filter(function (i) { return totals[i] === min; });
    state.complete = true;
    state.meta = "Match complete";
    state.winTitle = state.winners.length === 2 ? "It's a tie!" : "Game shot!";
    state.winText = state.winners.length === 2
      ? "Tied at " + totals[0] + " putts each — co-champions, no tiebreaker."
      : totals[state.winners[0]] + " putts to " + totals[1 - state.winners[0]] + " — lowest total takes the match.";
  }

  function advanceHole(state) {
    if (state.currentHole >= HOLES_COUNT - 1) {
      finishRound(state);
      return;
    }
    state.currentHole += 1;
    resetHoleState(state);
  }

  /* Called once a player's shot either locks their hole (holed out / hit the
     cap) or their 3-dart visit runs out. A player who already locked this
     hole is skipped — the other player keeps taking consecutive visits
     until they finish too, then the hole advances for both. */
  function afterLockOrVisit(state, p) {
    state.visit = [];
    var other = 1 - p;
    var pLocked = state.scores[p][state.currentHole] !== null;
    var otherLocked = state.scores[other][state.currentHole] !== null;
    if (pLocked && otherLocked) {
      advanceHole(state);
      return;
    }
    state.currentPlayer = otherLocked ? p : other;
  }

  function applyEvent(state, ev) {
    if (state.complete) return state;
    if (ev.type === "endturn") {
      state.visit = [];
      state.currentPlayer = 1 - state.currentPlayer;
      state.lastEvent = null;
      return state;
    }

    var ring = ev.ring, segment = ev.segment;
    var p = state.currentPlayer, h = state.currentHole;
    var notation = DartScoring.notationFor(ring, segment);
    state.lastEvent = null;

    var order = DartScoring.NUMBERS.indexOf(segment);
    var angle = order >= 0 ? order * 18 : 0;
    var rad = (angle * Math.PI) / 180;
    var magnitude = MAGNITUDE[ring] || 0; /* "Miss" isn't in the table — 0 distance, still a stroke */
    var rawX = state.balls[p].x + Math.sin(rad) * magnitude;
    var rawY = state.balls[p].y - Math.cos(rad) * magnitude;
    var newBall = { x: reflect01(rawX), y: reflect01(rawY) };

    state.strokes[p] += 1;
    state.balls[p] = newBall;
    state.trails[p].push(newBall);
    if (state.trails[p].length > TRAIL_MAX) state.trails[p].shift();
    state.visit.push({ ring: ring, segment: segment, notation: notation });

    var dx = newBall.x - HOLES[h].cup.x, dy = newBall.y - HOLES[h].cup.y;
    var holed = Math.sqrt(dx * dx + dy * dy) <= CAPTURE_RADIUS;

    if (holed) {
      state.scores[p][h] = state.strokes[p];
      state.lastEvent = state.strokes[p] === 1 ? { text: "HOLE IN ONE!", tone: "good" } : { text: "HOLED OUT!", tone: "good" };
      afterLockOrVisit(state, p);
      return state;
    }
    if (state.strokes[p] >= MAX_PUTTS) {
      state.scores[p][h] = MAX_PUTTS;
      state.lastEvent = { text: "AUTO-LOCKED AT " + MAX_PUTTS, tone: "bad" };
      afterLockOrVisit(state, p);
      return state;
    }
    if (state.visit.length >= 3) {
      afterLockOrVisit(state, p);
    }
    return state;
  }

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }

  var GREEN_X0 = 20, GREEN_Y0 = 30, GREEN_W = 180, GREEN_H = 180;

  function px(pt) {
    return { x: GREEN_X0 + pt.x * GREEN_W, y: GREEN_Y0 + pt.y * GREEN_H };
  }

  function trailPoints(trail) {
    return trail.map(function (pt) {
      var p = px(pt);
      return p.x.toFixed(1) + "," + p.y.toFixed(1);
    }).join(" ");
  }

  function holeProgressDots(state) {
    var out = "";
    for (var h = 0; h < HOLES_COUNT; h++) {
      var x = GREEN_X0 + (h / (HOLES_COUNT - 1)) * GREEN_W;
      var bothLocked = state.scores[0][h] !== null && state.scores[1][h] !== null;
      var isCurrent = h === state.currentHole && !state.complete;
      var cls = bothLocked ? "done" : (isCurrent ? "current" : "future");
      out += '<circle cx="' + x.toFixed(1) + '" cy="18" r="3" class="hole-dot ' + cls + '"/>';
    }
    return out;
  }

  function greenSvg(state) {
    var cup = px(HOLES[state.currentHole].cup);
    var tee = px(HOLES[state.currentHole].tee);
    var b0 = px(state.balls[0]), b1 = px(state.balls[1]);

    return (
      '<svg viewBox="0 0 220 240" class="green-diagram" aria-hidden="true">' +
      '<text x="110" y="10" class="green-label">' +
        (state.complete ? "MATCH COMPLETE" : "HOLE " + (state.currentHole + 1) + " OF " + HOLES_COUNT) +
      "</text>" +
      holeProgressDots(state) +
      '<rect x="' + GREEN_X0 + '" y="' + GREEN_Y0 + '" width="' + GREEN_W + '" height="' + GREEN_H + '" class="green-outline"/>' +
      '<circle cx="' + tee.x.toFixed(1) + '" cy="' + tee.y.toFixed(1) + '" r="3" class="tee-marker"/>' +
      '<g class="cup-marker" transform="translate(' + cup.x.toFixed(1) + "," + cup.y.toFixed(1) + ')">' +
        '<circle r="4" class="cup-hole"/>' +
        '<line x1="0" y1="0" x2="0" y2="-14" class="flag-pole"/>' +
        '<path d="M0,-14 L8,-10 L0,-6 Z" class="flag-flag"/>' +
      "</g>" +
      (state.trails[0].length > 1 ? '<polyline points="' + trailPoints(state.trails[0]) + '" class="ball-trail p0"/>' : "") +
      (state.trails[1].length > 1 ? '<polyline points="' + trailPoints(state.trails[1]) + '" class="ball-trail p1"/>' : "") +
      '<circle cx="' + b0.x.toFixed(1) + '" cy="' + b0.y.toFixed(1) + '" r="5" class="ball p0' + (!state.complete && state.currentPlayer === 0 ? " active" : "") + '"/>' +
      '<circle cx="' + b1.x.toFixed(1) + '" cy="' + b1.y.toFixed(1) + '" r="5" class="ball p1' + (!state.complete && state.currentPlayer === 1 ? " active" : "") + '"/>' +
      "</svg>"
    );
  }

  function holeTotal(scoresRow) {
    return scoresRow.reduce(function (sum, v) { return sum + (v === null ? 0 : v); }, 0);
  }

  function cellFor(state, p, h) {
    var locked = state.scores[p][h];
    var isCurrentHole = h === state.currentHole && !state.complete;
    if (locked !== null) {
      return { text: String(locked), cls: "done" + (isCurrentHole ? " current-hole" : "") };
    }
    if (isCurrentHole) {
      var val = state.strokes[p];
      var whoCls = p === state.currentPlayer ? "live" : "pending";
      return { text: val > 0 ? String(val) : "–", cls: whoCls + " current-hole" };
    }
    return { text: "–", cls: "future" };
  }

  function scorecardHtml(state, players) {
    var thead = "<tr><th></th>";
    for (var h = 0; h < HOLES_COUNT; h++) {
      var cls = h === state.currentHole && !state.complete ? "current-hole" : "";
      thead += '<th class="' + cls + '">' + (h + 1) + "</th>";
    }
    thead += "<th>Tot</th></tr>";

    var rows = "";
    players.forEach(function (name, p) {
      var rowCls = (!state.complete && p === state.currentPlayer ? "active-row" : "") +
        (state.complete && state.winners.indexOf(p) !== -1 ? " winner-row" : "");
      var tr = '<tr class="' + rowCls + '">';
      tr += '<td class="pp-name">' + escapeHtml(name) + "</td>";
      for (var h = 0; h < HOLES_COUNT; h++) {
        var cell = cellFor(state, p, h);
        tr += '<td class="' + cell.cls + '">' + cell.text + "</td>";
      }
      tr += '<td class="pp-total">' + holeTotal(state.scores[p]) + "</td>";
      tr += "</tr>";
      rows += tr;
    });

    return '<table class="pp-grid"><thead>' + thead + "</thead><tbody>" + rows + "</tbody></table>";
  }

  function renderGameBoard(container, state, players) {
    container.innerHTML = "";
    var wrap = document.createElement("div");
    wrap.className = "green-board";
    wrap.innerHTML = greenSvg(state) + scorecardHtml(state, players);
    container.appendChild(wrap);
  }

  global.GamePlugins = global.GamePlugins || {};
  global.GamePlugins.puttputt = {
    freshState: freshState,
    applyEvent: applyEvent,
    renderGameBoard: renderGameBoard
  };
})(window);
