/* Barrelo.Games.Kickoff's own board region. Reads snapshot.payload (KickoffStatePayload:
   { groups: [{groupIndex, playerIds, goals, legsWon}] (exactly 2), ball: {x,y}, trail: [{x,y}],
   lastEvent: {text, tone} | null, currentVisitThrows: [...] }) and the shell-provided
   playerId -> name map. One shared pitch, one shared ball — not a panel per side. The shell
   never looks inside payload itself — this file owns everything about how Kickoff's state
   renders. */
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

  /* Same convention as Cricket's namesFor: "› " marks whichever member is the active
     thrower, members joined with " & " so a team reads as one name on the pitch. */
  function namesFor(group, snapshot, playerNames) {
    return group.playerIds.map(function (id) {
      var isThrower = id === snapshot.currentPlayerId && !snapshot.isComplete;
      return (isThrower ? "› " : "") + escapeHtml((playerNames && playerNames[id]) || "Player");
    }).join(" & ");
  }

  /* Landscape, right-to-left: groups[0]'s goal sits on the right (ball.x -> 1), groups[1]'s on the
     left (ball.x -> 0) — the engine scores goals east/west, so this is a direct, unrotated read of
     ball.x/ball.y onto screen-x/screen-y. Kicking east (e.g. segment 6) sends the ball right on
     screen, exactly as it does on the dartboard. */
  var PITCH_X0 = 16, PITCH_X1 = 284, PITCH_W = 268;
  var PITCH_Y0 = 16, PITCH_Y1 = 154, PITCH_H = 138;
  var GOAL_W = 18, GOAL_H = 56;
  var GOAL_Y0 = (PITCH_Y0 + PITCH_Y1) / 2 - GOAL_H / 2;
  var GOAL_Y1 = GOAL_Y0 + GOAL_H;

  function goalRivets(xInner, xOuter) {
    return (
      '<circle cx="' + xInner + '" cy="' + GOAL_Y0 + '" r="1.7" class="goal-rivet"/>' +
      '<circle cx="' + xOuter + '" cy="' + GOAL_Y0 + '" r="1.7" class="goal-rivet"/>' +
      '<circle cx="' + xInner + '" cy="' + GOAL_Y1 + '" r="1.7" class="goal-rivet"/>' +
      '<circle cx="' + xOuter + '" cy="' + GOAL_Y1 + '" r="1.7" class="goal-rivet"/>'
    );
  }

  function pitchSvg(payload, snapshot) {
    var ball = payload.ball;
    var bx = PITCH_X0 + ball.x * PITCH_W;
    var by = PITCH_Y0 + ball.y * PITCH_H;
    var trailPts = (payload.trail || []).map(function (pt) {
      return (PITCH_X0 + pt.x * PITCH_W).toFixed(1) + "," + (PITCH_Y0 + pt.y * PITCH_H).toFixed(1);
    }).join(" ");

    var sideOnBall = payload.groups.find(function (g) {
      return g.playerIds.indexOf(snapshot.currentPlayerId) !== -1;
    });
    var attackingRight = !snapshot.isComplete && sideOnBall && sideOnBall.groupIndex === payload.groups[0].groupIndex;
    var attackingLeft = !snapshot.isComplete && sideOnBall && sideOnBall.groupIndex === payload.groups[1].groupIndex;
    var midY = (PITCH_Y0 + PITCH_Y1) / 2;
    var midX = (PITCH_X0 + PITCH_X1) / 2;

    return (
      '<svg viewBox="0 0 300 170" class="pitch-diagram" aria-hidden="true">' +
      '<rect x="' + PITCH_X0 + '" y="' + PITCH_Y0 + '" width="' + PITCH_W + '" height="' + PITCH_H + '" rx="3" class="pitch-outline"/>' +
      '<line x1="' + midX + '" y1="' + PITCH_Y0 + '" x2="' + midX + '" y2="' + PITCH_Y1 + '" class="pitch-line"/>' +
      '<circle cx="' + midX + '" cy="' + midY + '" r="24" class="pitch-line" fill="none"/>' +
      '<path d="M ' + PITCH_X0 + ' ' + (midY - 26) + ' Q ' + (PITCH_X0 + 26) + ' ' + midY + ' ' + PITCH_X0 + ' ' + (midY + 26) + '" class="pitch-line" fill="none"/>' +
      '<path d="M ' + PITCH_X1 + ' ' + (midY - 26) + ' Q ' + (PITCH_X1 - 26) + ' ' + midY + ' ' + PITCH_X1 + ' ' + (midY + 26) + '" class="pitch-line" fill="none"/>' +
      '<rect x="' + (PITCH_X1 - GOAL_W) + '" y="' + GOAL_Y0 + '" width="' + GOAL_W + '" height="' + GOAL_H + '" rx="1" class="goal-frame' + (attackingRight ? " attacking" : "") + '"/>' +
      goalRivets(PITCH_X1 - GOAL_W, PITCH_X1) +
      '<rect x="' + PITCH_X0 + '" y="' + GOAL_Y0 + '" width="' + GOAL_W + '" height="' + GOAL_H + '" rx="1" class="goal-frame' + (attackingLeft ? " attacking" : "") + '"/>' +
      goalRivets(PITCH_X0, PITCH_X0 + GOAL_W) +
      (trailPts ? '<polyline points="' + trailPts + '" class="ball-trail"/>' : "") +
      '<circle cx="' + bx.toFixed(1) + '" cy="' + by.toFixed(1) + '" r="5.5" class="ball"/>' +
      "</svg>"
    );
  }

  function renderGameBoard(container, snapshot, playerNames) {
    var payload = snapshot.payload;
    if (!payload || !payload.groups || payload.groups.length !== 2) {
      container.innerHTML = "";
      return;
    }

    var groups = payload.groups;
    var teamLabels = groups.map(function (g) { return namesFor(g, snapshot, playerNames); });

    container.innerHTML = "";
    var wrap = document.createElement("div");
    wrap.className = "pitch-board";

    var html =
      pitchSvg(payload, snapshot) +
      (payload.lastEvent
        ? '<div class="pitch-event pitch-event-' + escapeHtml(payload.lastEvent.tone) + '">' + escapeHtml(payload.lastEvent.text) + "</div>"
        : "") +
      '<div class="pitch-scoreline">' +
        '<span class="pitch-team">' + teamLabels[0] + "</span>" +
        '<span class="pitch-score">' + groups[0].goals + " – " + groups[1].goals + "</span>" +
        '<span class="pitch-team">' + teamLabels[1] + "</span>" +
      "</div>" +
      '<div class="pitch-tallies">' +
        '<span>' + teamLabels[0] + " &nbsp;legs" + tallyMarks(groups[0].legsWon, "chalk-tick") + "</span>" +
        '<span>' + teamLabels[1] + " &nbsp;legs" + tallyMarks(groups[1].legsWon, "chalk-tick") + "</span>" +
      "</div>";

    wrap.innerHTML = html;
    container.appendChild(wrap);
  }

  global.renderGameBoard = renderGameBoard;
})(window);
