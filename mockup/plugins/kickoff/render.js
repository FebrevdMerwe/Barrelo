/* Darts.Games.Kickoff — third reference plugin.
   One shared pitch, one shared ball — not two independent panels. Every dart
   is a kick: segment sets the compass direction (reusing the dartboard's own
   angle order, so "up" on the board is always "toward the top goal"), ring
   sets how hard. Send the ball off the pitch and your turn ends on the spot;
   the other player picks it up exactly where it left the field. */
(function (global) {
  "use strict";

  var GOALS_TO_WIN_LEG = 3;
  var LEGS_TO_WIN = 2;
  var GOAL_X_MIN = 0.35;
  var GOAL_X_MAX = 0.65;
  var TRAIL_MAX = 8;

  var MAGNITUDE = { InnerBull: 0.08, OuterBull: 0.08, Inner: 0.16, Outer: 0.26, Triple: 0.38, Double: 0.5 };

  function clamp01(v) { return Math.max(0, Math.min(1, v)); }

  function freshState() {
    return {
      ball: { x: 0.5, y: 0.5 },
      trail: [{ x: 0.5, y: 0.5 }],
      goals: [0, 0],
      legs: [0, 0],
      sets: [0, 0],
      visit: [],
      currentPlayer: 0,
      complete: false,
      winner: null,
      lastEvent: null,
      deadTargets: []
    };
  }

  function pushTrail(state) {
    state.trail.push(state.ball);
    if (state.trail.length > TRAIL_MAX) state.trail.shift();
  }
  function restart(state, kicker) {
    state.ball = { x: 0.5, y: 0.5 };
    state.trail = [{ x: 0.5, y: 0.5 }];
    state.visit = [];
    state.currentPlayer = kicker;
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
    var p = state.currentPlayer;
    var notation = DartScoring.notationFor(ring, segment);
    var magnitude = MAGNITUDE[ring] || 0;
    var order = DartScoring.NUMBERS.indexOf(segment);
    var angle = order >= 0 ? order * 18 : 0;
    var rad = (angle * Math.PI) / 180;
    var newX = state.ball.x + Math.sin(rad) * magnitude;
    var newY = state.ball.y - Math.cos(rad) * magnitude;

    /* Goal line: reaching or crossing it inside the frame scores. Touchline:
       the line itself is still in play, only crossing beyond it is out. */
    var outcome, goalOwner = null;
    if (newY <= 0) {
      if (newX >= GOAL_X_MIN && newX <= GOAL_X_MAX) { outcome = "goal"; goalOwner = 0; }
      else outcome = "out";
    } else if (newY >= 1) {
      if (newX >= GOAL_X_MIN && newX <= GOAL_X_MAX) { outcome = "goal"; goalOwner = 1; }
      else outcome = "out";
    } else if (newX < 0 || newX > 1) {
      outcome = "out";
    } else {
      outcome = "move";
    }

    state.lastEvent = null;

    if (outcome === "out") {
      state.ball = { x: clamp01(newX), y: clamp01(newY) };
      pushTrail(state);
      state.visit.push({ ring: ring, segment: segment, notation: notation, outcome: "out" });
      state.lastEvent = { text: "OUT! — their throw-in", tone: "bad" };
      state.visit = [];
      state.currentPlayer = 1 - p;
      return state;
    }

    if (outcome === "goal") {
      var ownGoal = goalOwner !== p;
      state.goals[goalOwner] += 1;
      state.visit.push({ ring: ring, segment: segment, notation: notation, outcome: ownGoal ? "ownGoal" : "goal" });
      state.lastEvent = ownGoal ? { text: "OWN GOAL!", tone: "bad" } : { text: "GOAL!", tone: "good" };

      if (state.goals[goalOwner] >= GOALS_TO_WIN_LEG) {
        state.legs[goalOwner] += 1;
        if (state.legs[goalOwner] >= LEGS_TO_WIN) {
          state.sets[goalOwner] += 1;
          state.complete = true;
          state.winner = goalOwner;
          return state;
        }
        state.goals = [0, 0];
      }
      restart(state, 1 - goalOwner); /* the side that conceded kicks off */
      return state;
    }

    state.ball = { x: newX, y: newY };
    pushTrail(state);
    state.visit.push({ ring: ring, segment: segment, notation: notation, outcome: "move" });

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

  var PITCH_X0 = 20, PITCH_X1 = 180, PITCH_W = 160;
  var PITCH_Y0 = 40, PITCH_Y1 = 260, PITCH_H = 220;

  function pitchSvg(state, players) {
    var bx = PITCH_X0 + state.ball.x * PITCH_W;
    var by = PITCH_Y0 + state.ball.y * PITCH_H;
    var trailPts = state.trail.map(function (pt) {
      return (PITCH_X0 + pt.x * PITCH_W).toFixed(1) + "," + (PITCH_Y0 + pt.y * PITCH_H).toFixed(1);
    }).join(" ");
    var attackingTop = state.currentPlayer === 0 && !state.complete;
    var attackingBottom = state.currentPlayer === 1 && !state.complete;

    return (
      '<svg viewBox="0 0 200 300" class="pitch-diagram" aria-hidden="true">' +
      '<text x="100" y="12" class="goal-label">' + escapeHtml(players[0]) + "&#8217;s goal</text>" +
      '<rect x="76" y="26" width="48" height="14" class="goal-frame' + (attackingTop ? " attacking" : "") + '"/>' +
      '<rect x="50" y="' + PITCH_Y0 + '" width="100" height="36" class="pitch-box"/>' +
      '<rect x="' + PITCH_X0 + '" y="' + PITCH_Y0 + '" width="' + PITCH_W + '" height="' + PITCH_H + '" class="pitch-outline"/>' +
      '<line x1="' + PITCH_X0 + '" y1="150" x2="' + PITCH_X1 + '" y2="150" class="pitch-line"/>' +
      '<circle cx="100" cy="150" r="30" class="pitch-line" fill="none"/>' +
      '<rect x="50" y="' + (PITCH_Y1 - 36) + '" width="100" height="36" class="pitch-box"/>' +
      '<rect x="76" y="260" width="48" height="14" class="goal-frame' + (attackingBottom ? " attacking" : "") + '"/>' +
      '<text x="100" y="294" class="goal-label">' + escapeHtml(players[1]) + "&#8217;s goal</text>" +
      (trailPts ? '<polyline points="' + trailPts + '" class="ball-trail"/>' : "") +
      '<circle cx="' + bx.toFixed(1) + '" cy="' + by.toFixed(1) + '" r="5.5" class="ball"/>' +
      "</svg>"
    );
  }

  function renderGameBoard(container, state, players) {
    container.innerHTML = "";
    var wrap = document.createElement("div");
    wrap.className = "pitch-board";
    wrap.innerHTML =
      pitchSvg(state, players) +
      '<div class="pitch-scoreline">' +
        '<span class="pitch-team">' + escapeHtml(players[0]) + "</span>" +
        '<span class="pitch-score">' + state.goals[0] + " – " + state.goals[1] + "</span>" +
        '<span class="pitch-team">' + escapeHtml(players[1]) + "</span>" +
      "</div>" +
      '<div class="pitch-tallies">' +
        '<span>' + escapeHtml(players[0]) + " &nbsp;legs" + tallyMarks(state.legs[0], "chalk-tick") + " &nbsp;sets" + tallyMarks(state.sets[0], "brass-pip") + "</span>" +
        '<span>' + escapeHtml(players[1]) + " &nbsp;legs" + tallyMarks(state.legs[1], "chalk-tick") + " &nbsp;sets" + tallyMarks(state.sets[1], "brass-pip") + "</span>" +
      "</div>";
    container.appendChild(wrap);
  }

  global.GamePlugins = global.GamePlugins || {};
  global.GamePlugins.kickoff = {
    freshState: freshState,
    applyEvent: applyEvent,
    renderGameBoard: renderGameBoard
  };
})(window);
