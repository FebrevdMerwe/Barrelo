/* Barrelo.Games.Cricket's own board region. Reads snapshot.payload (CricketStatePayload:
   { groups: [{groupIndex, playerIds, marks, points, closedCount}], currentVisitThrows: [...] })
   and the shell-provided playerId -> name map. marks is a fixed 7-element array index-aligned
   with TARGET_LABELS below (20,19,18,17,16,15,BULL). A group shares one cumulative marks/points
   state (team play); an ungrouped/solo player is just a group of one. The shell never looks
   inside payload itself — this file owns everything about how Cricket's state renders. */
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

  var TARGET_LABELS = ["20", "19", "18", "17", "16", "15", "BULL"];

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
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

  function namesFor(group, snapshot, playerNames) {
    return group.playerIds.map(function (id) {
      var isThrower = id === snapshot.currentPlayerId && !snapshot.isComplete;
      return (isThrower ? "› " : "") + escapeHtml((playerNames && playerNames[id]) || "Player");
    }).join(" & ");
  }

  function renderGameBoard(container, snapshot, playerNames) {
    var groups = (snapshot.payload && snapshot.payload.groups) || [];
    container.innerHTML = "";

    var board = document.createElement("div");
    board.className = "cricket-board";

    var slates = document.createElement("div");
    slates.className = "cricket-slates";

    groups.forEach(function (g) {
      var isActive = !snapshot.isComplete && g.playerIds.indexOf(snapshot.currentPlayerId) !== -1;

      var slate = document.createElement("div");
      slate.className = "cricket-slate" + (isActive ? " active" : "");

      var name = document.createElement("span");
      name.className = "slate-name";
      name.innerHTML = namesFor(g, snapshot, playerNames);
      slate.appendChild(name);

      TARGET_LABELS.forEach(function (label, i) {
        var allClosed = groups.length > 0 && groups.every(function (og) { return og.marks[i] >= 3; });
        var row = document.createElement("div");
        row.className = "slate-row" + (allClosed ? " dead" : "");
        row.innerHTML = '<span class="slate-target">' + label + "</span>" + markGlyph(g.marks[i]);
        slate.appendChild(row);
      });

      var score = document.createElement("div");
      score.className = "slate-score";
      score.innerHTML = g.points + "<small>Score</small>";
      slate.appendChild(score);

      slates.appendChild(slate);
    });

    board.appendChild(slates);
    container.appendChild(board);
  }

  global.renderGameBoard = renderGameBoard;
})(window);
