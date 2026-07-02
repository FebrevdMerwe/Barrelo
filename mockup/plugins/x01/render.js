/* Darts.Games.X01 — stand-in for the reference game plugin.
   Owns its own rules (bust/checkout/leg/set) and its own #game-board content.
   The shell never looks inside `state.payload`-equivalent fields below. */
(function (global) {
  "use strict";

  var STARTING_SCORE = 501;
  var LEGS_TO_WIN = 3;
  var lastRemaining = [null, null];

  function freshState() {
    return {
      remaining: [STARTING_SCORE, STARTING_SCORE],
      legs: [0, 0],
      sets: [0, 0],
      visit: [],
      currentPlayer: 0,
      startPlayerThisLeg: 0,
      complete: false,
      winner: null,
      lastEvent: null,
      deadTargets: []
    };
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
    var score = DartScoring.scoreFor(ring, segment);
    var p = state.currentPlayer;
    state.visit.push({ ring: ring, segment: segment, score: score, notation: DartScoring.notationFor(ring, segment) });

    var prospective = state.remaining[p] - score;
    var bust = prospective < 0 || prospective === 1 || (prospective === 0 && !DartScoring.isValidCheckoutRing(ring));

    if (bust) {
      state.lastEvent = { text: "BUST — score stands", tone: "bad" };
      state.visit = [];
      state.currentPlayer = 1 - p;
      return state;
    }

    state.remaining[p] = prospective;

    if (prospective === 0) {
      state.legs[p] += 1;
      state.lastEvent = { text: "CHECKOUT!", tone: "good" };
      if (state.legs[p] >= LEGS_TO_WIN) {
        state.sets[p] += 1;
        state.complete = true;
        state.winner = p;
        return state;
      }
      state.remaining = [STARTING_SCORE, STARTING_SCORE];
      state.visit = [];
      state.startPlayerThisLeg = 1 - state.startPlayerThisLeg;
      state.currentPlayer = state.startPlayerThisLeg;
      return state;
    }

    state.lastEvent = null;
    if (state.visit.length >= 3) {
      state.visit = [];
      state.currentPlayer = 1 - p;
    }
    return state;
  }

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

  function renderGameBoard(container, state, players) {
    container.innerHTML = "";
    var wrap = document.createElement("div");
    wrap.className = "x01-board";
    players.forEach(function (name, idx) {
      var isActive = state.currentPlayer === idx && !state.complete;
      var pulse = lastRemaining[idx] !== null && lastRemaining[idx] !== state.remaining[idx];
      lastRemaining[idx] = state.remaining[idx];

      var panel = document.createElement("div");
      panel.className = "x01-panel" + (isActive ? " active" : "");
      panel.innerHTML =
        '<div class="x01-name">' + escapeHtml(name) + "</div>" +
        '<div class="x01-remaining' + (pulse ? " pulse" : "") + '">' + state.remaining[idx] + "</div>" +
        '<div class="tallies">' +
          '<span class="tally-group">legs' + tallyMarks(state.legs[idx], "chalk-tick") + "</span>" +
          '<span class="tally-group">sets' + tallyMarks(state.sets[idx], "brass-pip") + "</span>" +
        "</div>";
      wrap.appendChild(panel);
    });
    container.appendChild(wrap);
  }

  global.GamePlugins = global.GamePlugins || {};
  global.GamePlugins.x01 = {
    freshState: freshState,
    applyEvent: applyEvent,
    renderGameBoard: renderGameBoard
  };
})(window);
