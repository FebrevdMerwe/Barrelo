/* Darts.Games.X01's own board region. Reads snapshot.payload (X01StatePayload:
   { players: [{playerId, remainingScore, legsWon, setsWon}], currentVisitThrows: [...] })
   and the shell-provided playerId -> name map. The shell never looks inside payload
   itself — this file owns everything about how X01's state renders. */
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

  function renderGameBoard(container, snapshot, playerNames) {
    var players = (snapshot.payload && snapshot.payload.players) || [];
    container.innerHTML = "";
    var wrap = document.createElement("div");
    wrap.className = "x01-board";
    players.forEach(function (p) {
      var isActive = p.playerId === snapshot.currentPlayerId && !snapshot.isComplete;
      var pulse = lastRemaining[p.playerId] !== undefined && lastRemaining[p.playerId] !== p.remainingScore;
      lastRemaining[p.playerId] = p.remainingScore;

      var name = (playerNames && playerNames[p.playerId]) || "Player";
      var panel = document.createElement("div");
      panel.className = "x01-panel" + (isActive ? " active" : "");
      panel.innerHTML =
        '<div class="x01-name">' + escapeHtml(name) + "</div>" +
        '<div class="x01-remaining' + (pulse ? " pulse" : "") + '">' + p.remainingScore + "</div>" +
        '<div class="tallies">' +
          '<span class="tally-group">legs' + tallyMarks(p.legsWon, "chalk-tick") + "</span>" +
          '<span class="tally-group">sets' + tallyMarks(p.setsWon, "brass-pip") + "</span>" +
        "</div>";
      wrap.appendChild(panel);
    });
    container.appendChild(wrap);
  }

  global.renderGameBoard = renderGameBoard;
})(window);
