/* Darts.Games.Cricket — the second reference plugin.
   Deliberately different from X01: no bust, no "remaining score", a shared
   marks-and-points grid instead of two big numbers — proves the game-board
   region isn't shaped around X01. */
(function (global) {
  "use strict";

  var TARGET_NUMBERS = [20, 19, 18, 17, 16, 15];
  var TARGET_LABELS = ["20", "19", "18", "17", "16", "15", "BULL"];
  var POINT_VALUES = [20, 19, 18, 17, 16, 15, 25];
  var LEGS_TO_WIN = 2;

  function freshState() {
    return {
      marks: [[0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0]],
      score: [0, 0],
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

  function targetIndexFor(ring, segment) {
    if (ring === "InnerBull" || ring === "OuterBull") return 6;
    if (ring === "Miss") return -1;
    return TARGET_NUMBERS.indexOf(segment);
  }
  function marksForRing(ring) {
    switch (ring) {
      case "Triple": return 3;
      case "Double": return 2;
      case "InnerBull": return 2;
      case "Inner": case "Outer": case "OuterBull": return 1;
      default: return 0;
    }
  }
  function recomputeDead(state) {
    state.deadTargets = TARGET_LABELS.filter(function (label, i) {
      return state.marks[0][i] >= 3 && state.marks[1][i] >= 3;
    }).map(function (label) { return label === "BULL" ? "BULL" : parseInt(label, 10); });
  }

  function applyEvent(state, ev) {
    if (state.complete) return state;
    if (ev.type === "endturn") {
      state.visit = [];
      state.currentPlayer = 1 - state.currentPlayer;
      state.lastEvent = null;
      recomputeDead(state);
      return state;
    }

    var ring = ev.ring, segment = ev.segment;
    var p = state.currentPlayer;
    var tIdx = targetIndexFor(ring, segment);
    var thrown = { ring: ring, segment: segment, notation: DartScoring.notationFor(ring, segment), score: 0 };
    state.lastEvent = null;

    if (tIdx >= 0) {
      var hitMarks = marksForRing(ring);
      var cur = state.marks[p][tIdx];
      var toClose = Math.min(3 - cur, hitMarks);
      var overflow = hitMarks - toClose;
      state.marks[p][tIdx] = cur + toClose;
      var opponent = 1 - p;
      if (overflow > 0 && state.marks[opponent][tIdx] < 3) {
        var pts = overflow * POINT_VALUES[tIdx];
        state.score[p] += pts;
        thrown.score = pts;
      }
    }
    state.visit.push(thrown);

    var closedAll = state.marks[p].every(function (m) { return m >= 3; });
    if (closedAll && state.score[p] >= state.score[1 - p]) {
      state.legs[p] += 1;
      state.lastEvent = { text: "CLOSED OUT!", tone: "good" };
      if (state.legs[p] >= LEGS_TO_WIN) {
        state.sets[p] += 1;
        state.complete = true;
        state.winner = p;
        recomputeDead(state);
        return state;
      }
      state.marks = [[0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0]];
      state.score = [0, 0];
      state.visit = [];
      state.startPlayerThisLeg = 1 - state.startPlayerThisLeg;
      state.currentPlayer = state.startPlayerThisLeg;
      recomputeDead(state);
      return state;
    }

    if (state.visit.length >= 3) {
      state.visit = [];
      state.currentPlayer = 1 - p;
    }
    recomputeDead(state);
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
  function markGlyph(count) {
    count = Math.min(count, 3);
    if (count === 0) return '<svg viewBox="0 0 24 24" class="mark-glyph" aria-hidden="true"></svg>';
    var parts = "";
    if (count >= 1) parts += '<line x1="5" y1="19" x2="19" y2="5" class="mark-stroke"/>';
    if (count >= 2) parts += '<line x1="5" y1="5" x2="19" y2="19" class="mark-stroke"/>';
    if (count >= 3) parts += '<circle cx="12" cy="12" r="10" class="mark-ring"/>';
    return '<svg viewBox="0 0 24 24" class="mark-glyph" aria-hidden="true">' + parts + "</svg>";
  }

  function renderGameBoard(container, state, players) {
    container.innerHTML = "";
    var board = document.createElement("div");
    board.className = "cricket-board";

    var head = document.createElement("div");
    head.className = "cricket-head";
    players.forEach(function (name, idx) {
      var isActive = state.currentPlayer === idx && !state.complete;
      var span = document.createElement("span");
      span.className = "cricket-player" + (isActive ? " active" : "");
      span.textContent = name;
      head.appendChild(span);
      if (idx === 0) {
        var title = document.createElement("span");
        title.className = "cricket-title";
        title.textContent = "Cricket";
        head.appendChild(title);
      }
    });
    board.appendChild(head);

    var table = document.createElement("table");
    table.className = "cricket-grid";
    var tbody = document.createElement("tbody");
    TARGET_LABELS.forEach(function (label, i) {
      var tr = document.createElement("tr");
      if (state.deadTargets.indexOf(i === 6 ? "BULL" : parseInt(label, 10)) !== -1) tr.classList.add("dead-row");
      tr.innerHTML =
        '<td class="mark-cell">' + markGlyph(state.marks[0][i]) + "</td>" +
        '<td class="target-cell">' + label + "</td>" +
        '<td class="mark-cell">' + markGlyph(state.marks[1][i]) + "</td>";
      tbody.appendChild(tr);
    });
    table.appendChild(tbody);
    var tfoot = document.createElement("tfoot");
    tfoot.innerHTML =
      '<tr class="cricket-score-row"><td>' + state.score[0] + '</td><td>Score</td><td>' + state.score[1] + "</td></tr>";
    table.appendChild(tfoot);
    board.appendChild(table);

    var tallies = document.createElement("div");
    tallies.className = "cricket-tallies";
    players.forEach(function (name, idx) {
      var span = document.createElement("span");
      span.innerHTML =
        escapeHtml(name) + " &nbsp;legs" + tallyMarks(state.legs[idx], "chalk-tick") +
        " &nbsp;sets" + tallyMarks(state.sets[idx], "brass-pip");
      tallies.appendChild(span);
    });
    board.appendChild(tallies);

    container.appendChild(board);
  }

  global.GamePlugins = global.GamePlugins || {};
  global.GamePlugins.cricket = {
    freshState: freshState,
    applyEvent: applyEvent,
    renderGameBoard: renderGameBoard
  };
})(window);
