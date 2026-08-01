/* Barrelo.Games.AroundTheClock's own board region. Reads snapshot.payload
   (AroundTheClockStatePayload: { groups: [{groupIndex, playerIds, progress, targetNumber,
   targetIsBull, isFinished}], round, totalSteps, currentVisitThrows: [...], currentVisitSteps: [...] })
   and the shell-provided playerId -> name map.

   Three regions, ported from the Around The Clock design: the active team's spotlight on the left, a
   dartboard highlighting the live target in the middle, and the standings ladder on the right. The
   shell already owns the page header, the win banner and the dart-entry controls, so none of those are
   redrawn here.

   Team colours are the design's saturated palette and stay saturated deliberately — a row's colour is
   how you tie a standings entry to the spotlight, so it's load-bearing rather than decorative.
   Everything else (panels, rules, muted labels) uses the shell's chalk/brass custom properties so the
   board sits inside the surrounding rail instead of on top of it. */
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

  /* Clockwise from the top, matching a real board (and BoardGeometry's own segment order server-side). */
  var ORDER = [20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5];

  var PALETTE = ["#39FF14", "#FF2D95", "#00E5FF", "#FFE600", "#FF7A18", "#B14BFF"];

  /* Board geometry in the SVG's own 400x400 space, centred on 200,200. */
  var BULL_OUTER = 27, BULL_INNER = 13;
  var TREBLE_IN = 86, TREBLE_OUT = 99;
  var DOUBLE_IN = 150, DOUBLE_OUT = 163;
  var NUMBER_RING = 182;

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }

  function rgba(hex, a) {
    var h = hex.replace("#", "");
    return "rgba(" + parseInt(h.substring(0, 2), 16) + "," + parseInt(h.substring(2, 4), 16) +
      "," + parseInt(h.substring(4, 6), 16) + "," + a + ")";
  }

  function colorFor(groupIndex) {
    return PALETTE[groupIndex % PALETTE.length];
  }

  function el(tag, className) {
    var node = document.createElement(tag);
    if (className) node.className = className;
    return node;
  }

  /* ---------- dartboard ---------- */

  function pol(r, a) {
    var rad = a * Math.PI / 180;
    return [200 + r * Math.cos(rad), 200 + r * Math.sin(rad)];
  }

  /* An annulus slice between two radii and two angles — the shape of every wedge, band and highlight. */
  function sector(r1, r2, a1, a2) {
    var p1 = pol(r2, a1), p2 = pol(r2, a2), p3 = pol(r1, a2), p4 = pol(r1, a1);
    var large = (a2 - a1) > 180 ? 1 : 0;
    return "M" + p1[0].toFixed(2) + " " + p1[1].toFixed(2) +
      " A" + r2 + " " + r2 + " 0 " + large + " 1 " + p2[0].toFixed(2) + " " + p2[1].toFixed(2) +
      " L" + p3[0].toFixed(2) + " " + p3[1].toFixed(2) +
      " A" + r1 + " " + r1 + " 0 " + large + " 0 " + p4[0].toFixed(2) + " " + p4[1].toFixed(2) + " Z";
  }

  /* A jump marker sitting in the middle of a band: the whole point of highlighting the treble and
     double separately is that they're worth 3 and 2 rungs, which nothing else on screen says. */
  function bandMarker(radius, midAngle, label) {
    var p = pol(radius, midAngle);
    return '<text class="atc-band-marker" x="' + p[0].toFixed(1) + '" y="' + p[1].toFixed(1) +
      '" text-anchor="middle" dominant-baseline="central">' + label + "</text>";
  }

  function buildBoard(targetNumber, targetIsBull, color) {
    var wedges = "";
    for (var i = 0; i < 20; i++) {
      var a1 = -99 + i * 18, a2 = -81 + i * 18;
      // Same four board tokens the shell's own input dartboard uses, so the two boards a player sees
      // in one session (this one, and the one in the entry drawer) are the same board.
      var single = i % 2 ? "var(--slate-2)" : "var(--chalk)";
      var band = i % 2 ? "var(--board-red)" : "var(--board-green)";
      wedges += '<path d="' + sector(BULL_OUTER, DOUBLE_OUT, a1, a2) + '" fill="' + single + '"/>';
      wedges += '<path d="' + sector(TREBLE_IN, TREBLE_OUT, a1, a2) + '" fill="' + band + '"/>';
      wedges += '<path d="' + sector(DOUBLE_IN, DOUBLE_OUT, a1, a2) + '" fill="' + band + '"/>';
    }

    var labels = "";
    for (var n = 0; n < 20; n++) {
      var p = pol(NUMBER_RING, -90 + n * 18);
      labels += '<text class="atc-board-num" x="' + p[0].toFixed(1) + '" y="' + p[1].toFixed(1) +
        '" text-anchor="middle" dominant-baseline="central">' + ORDER[n] + "</text>";
    }

    var highlight = "";
    if (targetIsBull) {
      highlight =
        '<circle cx="200" cy="200" r="' + BULL_INNER + '" fill="' + rgba(color, 0.5) +
          '" stroke="' + color + '" stroke-width="3" filter="url(#atc-glow)"/>' +
        '<circle cx="200" cy="200" r="34" fill="none" stroke="' + color +
          '" stroke-width="3" stroke-dasharray="6 7" filter="url(#atc-glow)"/>' +
        '<text class="atc-band-marker" x="200" y="200" text-anchor="middle" dominant-baseline="central">50</text>';
    } else if (targetNumber) {
      var idx = ORDER.indexOf(targetNumber);
      if (idx >= 0) {
        var s1 = -99 + idx * 18, s2 = -81 + idx * 18, mid = (s1 + s2) / 2;
        highlight =
          '<path d="' + sector(BULL_OUTER, DOUBLE_OUT, s1, s2) + '" fill="' + color +
            '" fill-opacity="0.24" stroke="' + color + '" stroke-width="3.5" filter="url(#atc-glow)"/>' +
          '<path d="' + sector(TREBLE_IN, TREBLE_OUT, s1, s2) + '" fill="' + color + '"/>' +
          '<path d="' + sector(DOUBLE_IN, DOUBLE_OUT, s1, s2) + '" fill="' + color + '"/>' +
          bandMarker((TREBLE_IN + TREBLE_OUT) / 2, mid, "×3") +
          bandMarker((DOUBLE_IN + DOUBLE_OUT) / 2, mid, "×2");

        var tip = pol(DOUBLE_OUT + 9, mid);
        var w1 = pol(DOUBLE_OUT + 25, mid - 3.2);
        var w2 = pol(DOUBLE_OUT + 25, mid + 3.2);
        highlight += '<path d="M' + tip[0].toFixed(1) + " " + tip[1].toFixed(1) +
          " L" + w1[0].toFixed(1) + " " + w1[1].toFixed(1) +
          " L" + w2[0].toFixed(1) + " " + w2[1].toFixed(1) +
          ' Z" fill="' + color + '" filter="url(#atc-glow)"/>';
      }
    }

    return '<svg class="atc-board-svg" viewBox="0 0 400 400" role="img" aria-hidden="true">' +
      '<defs><filter id="atc-glow" x="-50%" y="-50%" width="200%" height="200%">' +
      '<feGaussianBlur stdDeviation="4" result="b"/>' +
      '<feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter></defs>' +
      '<circle cx="200" cy="200" r="192" fill="var(--slate)"/>' +
      '<circle cx="200" cy="200" r="167" fill="var(--baize-deep)" ' +
        'stroke="var(--brass-dim)" stroke-width="1.4"/>' +
      wedges +
      '<circle cx="200" cy="200" r="' + BULL_OUTER + '" fill="var(--board-green)"/>' +
      '<circle cx="200" cy="200" r="' + BULL_INNER + '" fill="var(--board-red)"/>' +
      highlight +
      labels +
      "</svg>";
  }

  /* ---------- text helpers ---------- */

  function targetLabelOf(group) {
    if (group.isFinished) return "✔";
    return group.targetIsBull ? "BULL" : String(group.targetNumber);
  }

  function targetSubOf(group) {
    if (group.isFinished) return "Finished";
    return group.targetIsBull
      ? "inner bull · 50 only"
      : "single ·1 · double ·2 · treble ·3";
  }

  function targetTagOf(group) {
    if (group.isFinished) return "DONE";
    return group.targetIsBull ? "INNER BULL 50" : "NUMBER " + group.targetNumber;
  }

  function nameOf(playerNames, id) {
    return escapeHtml((playerNames && playerNames[id]) || "Player");
  }

  function namesFor(group, snapshot, playerNames) {
    return group.playerIds.map(function (id) {
      var isThrower = id === snapshot.currentPlayerId && !snapshot.isComplete;
      return (isThrower ? "› " : "") + nameOf(playerNames, id);
    }).join(" & ");
  }

  /* The spotlight follows the thrower while the match is live, and stays on the winning team once it
     isn't — otherwise the whole left column would blank out on the very dart everyone is looking at. */
  function spotlightGroup(groups, snapshot) {
    var focusId = snapshot.isComplete
      ? (snapshot.winnerPlayerIds || [])[0]
      : snapshot.currentPlayerId;

    for (var i = 0; i < groups.length; i++) {
      if (groups[i].playerIds.indexOf(focusId) !== -1) return groups[i];
    }
    return groups[0];
  }

  /* ---------- regions ---------- */

  function renderSpotlight(group, snapshot, playerNames, payload, color) {
    var section = el("section", "atc-spotlight");
    section.style.setProperty("--atc-team", color);
    section.style.setProperty("--atc-team-soft", rgba(color, 0.42));

    var thrower = snapshot.isComplete ? null : snapshot.currentPlayerId;
    var mates = group.playerIds.filter(function (id) { return id !== thrower; });

    var head = el("div", "atc-eyebrow");
    head.innerHTML = '<span class="atc-dot"></span>' +
      (snapshot.isComplete ? "Winner" : "Now throwing");
    section.appendChild(head);

    var name = el("div", "atc-thrower");
    name.innerHTML = thrower ? nameOf(playerNames, thrower) : namesFor(group, snapshot, playerNames);
    section.appendChild(name);

    if (thrower && mates.length) {
      var mateLine = el("div", "atc-mates");
      mateLine.innerHTML = "with " + mates.map(function (id) { return nameOf(playerNames, id); }).join(" & ");
      section.appendChild(mateLine);
    }

    var targetBlock = el("div", "atc-target-block");
    targetBlock.innerHTML =
      '<div class="atc-label">Hit the</div>' +
      '<div class="atc-target-huge">' + escapeHtml(targetLabelOf(group)) + "</div>" +
      '<div class="atc-target-sub">' + escapeHtml(targetSubOf(group)) + "</div>";
    section.appendChild(targetBlock);

    var total = payload.totalSteps || 21;
    var pct = Math.round(Math.min(group.progress, total) / total * 100);
    var progress = el("div", "atc-progress");
    progress.innerHTML =
      '<div class="atc-progress-head"><span>Progress</span><span>' +
        Math.min(group.progress, total) + " / " + total + "</span></div>" +
      '<div class="atc-progress-track"><div class="atc-progress-fill" style="width:' + pct + '%"></div></div>';
    section.appendChild(progress);

    section.appendChild(renderVisit(payload));

    return section;
  }

  function renderVisit(payload) {
    var wrap = el("div", "atc-visit");
    wrap.innerHTML = '<div class="atc-label">This turn</div>';

    var cards = el("div", "atc-darts");
    var throwsThisVisit = payload.currentVisitThrows || [];
    var stepsThisVisit = payload.currentVisitSteps || [];

    for (var i = 0; i < 3; i++) {
      var t = throwsThisVisit[i];
      var card = el("div", "atc-dart");
      if (!t) {
        card.classList.add("empty");
        card.innerHTML = '<span class="atc-dart-label">—</span>' +
          '<span class="atc-dart-sub">Dart ' + (i + 1) + "</span>";
      } else {
        var gained = stepsThisVisit[i] || 0;
        card.classList.add(gained > 0 ? "hit" : "blank");
        card.innerHTML =
          '<span class="atc-dart-label">' + escapeHtml(t.rawNotation) + "</span>" +
          '<span class="atc-dart-sub">' + (gained > 0 ? "+" + gained : "no move") + "</span>";
      }
      cards.appendChild(card);
    }

    wrap.appendChild(cards);
    return wrap;
  }

  function renderTargetBoard(group, color) {
    var section = el("section", "atc-boardcol");
    section.style.setProperty("--atc-team", color);
    section.innerHTML =
      '<div class="atc-label">Live target</div>' +
      '<div class="atc-target-tag">' + escapeHtml(targetTagOf(group)) + "</div>" +
      '<div class="atc-board-frame">' + buildBoard(group.targetNumber, group.targetIsBull, color) + "</div>";
    return section;
  }

  function renderStandings(groups, snapshot, playerNames, payload) {
    var section = el("section", "atc-standings");

    var head = el("div", "atc-standings-head");
    head.innerHTML =
      '<span class="atc-label">Standings</span>' +
      '<span class="atc-round">Round <b>' + (payload.round || 1) + "</b></span>";
    section.appendChild(head);

    var total = payload.totalSteps || 21;
    var rows = el("div", "atc-rows");

    groups.forEach(function (g) {
      var color = colorFor(g.groupIndex);
      var isActive = !snapshot.isComplete && g.playerIds.indexOf(snapshot.currentPlayerId) !== -1;

      var row = el("div", "atc-row" + (isActive ? " active" : "") + (g.isFinished ? " done" : ""));
      row.style.setProperty("--atc-team", color);
      row.style.setProperty("--atc-team-soft", rgba(color, 0.22));

      var top = el("div", "atc-row-top");
      top.innerHTML =
        '<span class="atc-dot"></span>' +
        '<span class="atc-row-name">' + namesFor(g, snapshot, playerNames) + "</span>" +
        (isActive ? '<span class="atc-throwing">throwing</span>' : "") +
        '<span class="atc-row-spacer"></span>' +
        '<span class="atc-row-on">on</span>' +
        '<span class="atc-row-target">' + escapeHtml(targetLabelOf(g)) + "</span>";
      row.appendChild(top);

      var ladder = el("div", "atc-ladder");
      for (var k = 0; k < total; k++) {
        var cell = el("span", "atc-cell" +
          (k < g.progress ? " filled" : "") +
          (k === total - 1 ? " bull" : ""));
        ladder.appendChild(cell);
      }
      var remaining = el("span", "atc-remaining");
      remaining.textContent = g.isFinished ? "WON" : String(total - g.progress);
      ladder.appendChild(remaining);
      row.appendChild(ladder);

      rows.appendChild(row);
    });

    section.appendChild(rows);
    return section;
  }

  /* ---------- entry point ---------- */

  function renderGameBoard(container, snapshot, playerNames) {
    var payload = snapshot.payload || {};
    var groups = payload.groups || [];

    container.innerHTML = "";
    if (groups.length === 0) return;

    var focus = spotlightGroup(groups, snapshot);
    var focusColor = colorFor(focus.groupIndex);

    var board = el("div", "atc-board");
    board.appendChild(renderSpotlight(focus, snapshot, playerNames, payload, focusColor));
    board.appendChild(renderTargetBoard(focus, focusColor));
    board.appendChild(renderStandings(groups, snapshot, playerNames, payload));
    container.appendChild(board);
  }

  global.renderGameBoard = renderGameBoard;
})(window);
