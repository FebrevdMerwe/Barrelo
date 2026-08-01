/* Interactive controls: drives the active match (dartboard input, Miss/Undo/End Turn) with no matchId
   in the URL — meant for the tablet/phone someone actually holds, while view.html sits passively on a
   TV showing the same state. Bootstraps via GET /api/session/current; if no match is active, shows an
   idle panel pointing back to setup. A match starting while idle (or finishing while active) arrives
   as a GameStateUpdated push and flips the UI over automatically — there's only ever one match, so no
   join/subscribe step is needed.

   Chrome (whose turn it is, darts this visit, leg/set) comes from two places depending on the game. An
   in-process game reports it in the snapshot the host builds. A client-owned game runs its rules in the
   browser, so the host has none of it and the game sends it up as a display hint instead — see
   game-frame.js. Hints win when present; otherwise the snapshot stands. */
(function () {
  "use strict";

  var idlePanel = document.getElementById("idlePanel");
  var sceneEl = document.getElementById("scene");
  var gameLabelEl = document.getElementById("gameLabel");
  var legMetaEl = document.getElementById("legMeta");
  var gameBoardEl = document.getElementById("game-board");
  var ledgerStrip = document.getElementById("ledgerStrip");
  var winBanner = document.getElementById("winBanner");
  var winLeaderboard = document.getElementById("winLeaderboard");
  var leaderboardList = document.getElementById("leaderboardList");

  var inputDrawer = document.getElementById("inputDrawer");
  var drawerPeek = document.getElementById("drawerPeek");
  var btnCollapseDrawer = document.getElementById("btnCollapseDrawer");
  var visitSlotsEl = document.getElementById("visitSlots");
  var peekTurnEl = document.getElementById("peekTurn");
  var peekSubEl = document.getElementById("peekSub");
  var openTitleEl = document.getElementById("openTitle");

  var dartboard = createDartboard(document.getElementById("dartboard"));
  var boardStatus = createBoardStatusPill(
    document.getElementById("boardPill"), document.getElementById("boardLabel"));

  var playerNames = {};
  var gameNames = {};
  var drawerOpen = false;
  var lastCurrentPlayer = null;
  var lastSnapshot = null;
  var displayHint = null;

  var gameFrame = createGameFrame({
    onDisplay: function (hint) {
      displayHint = hint;
      // The hint lands after the snapshot that produced it, so the chrome is drawn a second time here
      // rather than waiting — the board itself is already up to date.
      if (lastSnapshot) renderChrome(lastSnapshot);
    },
    onMatchComplete: function () {
      // The host confirms completion by pushing a final snapshot with isComplete set, so there is
      // nothing to do locally beyond letting that arrive.
    },
    onGameChanged: function (gameId) {
      gameLabelEl.textContent = gameNames[gameId] || gameId;
      displayHint = null;
    },
  });

  function showIdle() {
    idlePanel.hidden = false;
    sceneEl.hidden = true;
    inputDrawer.hidden = true;
  }

  function showActive() {
    idlePanel.hidden = true;
    sceneEl.hidden = false;
    inputDrawer.hidden = false;
  }

  function setDrawerOpen(open) {
    drawerOpen = open;
    inputDrawer.classList.toggle("open", open);
    sceneEl.classList.toggle("drawer-open", open);
  }

  function currentPlayerOf(snapshot) {
    return displayHint && displayHint.currentPlayerId !== undefined
      ? displayHint.currentPlayerId
      : snapshot.currentPlayerId;
  }

  function visitThrows(snapshot) {
    if (displayHint && Array.isArray(displayHint.visitThrows)) return displayHint.visitThrows;
    var payload = snapshot.payload;
    return payload && Array.isArray(payload.currentVisitThrows) ? payload.currentVisitThrows : [];
  }

  function legMetaOf(snapshot) {
    var leg = displayHint && displayHint.legNumber !== undefined ? displayHint.legNumber : snapshot.legNumber;
    var set = displayHint && displayHint.setNumber !== undefined ? displayHint.setNumber : snapshot.setNumber;
    return "Leg " + leg + " · Set " + set;
  }

  function renderVisitSlots(snapshot) {
    visitSlotsEl.innerHTML = "";
    var throwsThisVisit = visitThrows(snapshot);
    for (var i = 0; i < 3; i++) {
      var slot = document.createElement("div");
      slot.className = "dart-slot";
      var t = throwsThisVisit[i];
      if (t) {
        slot.classList.add("filled");
        slot.textContent = t.rawNotation;
      }
      visitSlotsEl.appendChild(slot);
    }
  }

  /* A visit is full at three darts but does not end there — only a takeout (End turn) hands the oche
     over, which is what a detection source reports. So a fourth dart is refused by the game, and the
     board has to say so rather than let someone tap into a rejected request. */
  function visitIsFull(snapshot) {
    return visitThrows(snapshot).length >= 3;
  }

  function updateDrawerCopy(snapshot) {
    var name = playerNames[currentPlayerOf(snapshot)] || "—";
    var full = visitIsFull(snapshot);
    var dartNum = Math.min(visitThrows(snapshot).length + 1, 3);
    peekTurnEl.textContent = snapshot.isComplete ? "Match complete" : name + "'s turn";
    peekSubEl.textContent = snapshot.isComplete
      ? "Tap below to start a new match"
      : full
        ? "Three darts in · end the turn"
        : "Dart " + dartNum + " of 3 · tap to throw";
    openTitleEl.textContent = full ? "Three darts in — end the turn" : "Dart " + dartNum + " of 3";
    drawerPeek.disabled = snapshot.isComplete;
  }

  function renderLedger(snapshot) {
    ledgerStrip.innerHTML = "";
    var throwHistory = snapshot.recentThrows || [];
    if (throwHistory.length === 0) {
      var empty = document.createElement("span");
      empty.className = "empty";
      empty.textContent = "No darts thrown yet — tap the board to open the leg.";
      ledgerStrip.appendChild(empty);
      return;
    }
    throwHistory.forEach(function (t) {
      var chip = document.createElement("span");
      chip.className = "chip" + (t.ring === "Miss" ? " miss" : "");
      chip.textContent = t.rawNotation;
      ledgerStrip.appendChild(chip);
    });
    ledgerStrip.scrollLeft = ledgerStrip.scrollWidth;
  }

  function renderLeaderboard(standings) {
    leaderboardList.innerHTML = "";
    standings.forEach(function (entry, i) {
      var li = document.createElement("li");
      li.className = "leaderboard-row";
      li.innerHTML =
        '<span class="lb-rank">' + (i + 1) + '</span>' +
        '<span class="lb-name">' + entry.playerName + '</span>' +
        '<span class="lb-points">' + entry.points + ' pt' + (entry.points === 1 ? '' : 's') + '</span>';
      leaderboardList.appendChild(li);
    });
    winLeaderboard.hidden = standings.length === 0;
  }

  /* Everything outside the board region. Split out from render() because a display hint arriving from
     the game re-runs this on its own, without re-pushing state into the board. */
  function renderChrome(snapshot) {
    var current = currentPlayerOf(snapshot);
    var turnChanged = current !== lastCurrentPlayer;
    if (turnChanged || snapshot.isComplete) setDrawerOpen(false);
    lastCurrentPlayer = current;

    renderVisitSlots(snapshot);
    updateDrawerCopy(snapshot);
    renderLedger(snapshot);

    dartboard.setDisabled(snapshot.isComplete || visitIsFull(snapshot));
    dartboard.setDeadTargets((displayHint && displayHint.deadTargets) || []);
    document.getElementById("btnMiss").disabled = snapshot.isComplete || visitIsFull(snapshot);

    legMetaEl.textContent = snapshot.isComplete ? "Match complete" : legMetaOf(snapshot);

    if (snapshot.status === "Aborted") {
      document.getElementById("winTitle").textContent = "Game interrupted";
      document.getElementById("winSub").textContent = "This match was abandoned and can't continue.";
      winLeaderboard.hidden = true;
      winBanner.classList.add("show");
    } else if (snapshot.isComplete) {
      var winnerIds = snapshot.winnerPlayerIds || [];
      var winnerNames = winnerIds.map(function (id) { return playerNames[id] || "A player"; }).join(" & ");
      document.getElementById("winTitle").textContent = "Game shot!";
      document.getElementById("winSub").textContent = (winnerNames || "A player") + (winnerIds.length > 1 ? " win the match." : " wins the match.");
      renderLeaderboard(snapshot.sessionLeaderboard || []);
      winBanner.classList.add("show");
    } else {
      winBanner.classList.remove("show");
    }
  }

  async function render(snapshot) {
    showActive();
    lastSnapshot = snapshot;
    renderChrome(snapshot);
    await gameFrame.render(gameBoardEl, snapshot, playerNames);
  }

  async function post(url, body) {
    var res = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body || {}),
    });
    if (!res.ok) {
      console.error("POST " + url + " failed: " + res.status);
      return null;
    }
    return res.json();
  }

  dartboard.onThrow(function (ring, segment) {
    post("/api/detection/manual-throw", { segment: segment, ring: ring }).then(function (s) { if (s) render(s); });
  });
  document.getElementById("btnMiss").addEventListener("click", function () {
    post("/api/detection/manual-throw", { segment: 0, ring: "Miss" }).then(function (s) { if (s) render(s); });
  });
  document.getElementById("btnUndo").addEventListener("click", function () {
    post("/api/detection/undo", {}).then(function (s) { if (s) render(s); });
  });
  document.getElementById("btnEndTurn").addEventListener("click", function () {
    post("/api/detection/manual-end-turn", {}).then(function (s) { if (s) render(s); });
  });
  document.getElementById("btnRack").addEventListener("click", function () {
    window.location.href = "index.html?fromMatch=1";
  });
  drawerPeek.addEventListener("click", function () { setDrawerOpen(true); });
  btnCollapseDrawer.addEventListener("click", function () { setDrawerOpen(false); });

  function connectSignalR() {
    var connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/game")
      .withAutomaticReconnect()
      .build();
    connection.on("GameStateUpdated", function (snapshot) { render(snapshot); });
    boardStatus.listen(connection);
    return connection.start();
  }

  async function init() {
    boardStatus.load();

    var responses = await Promise.all([
      fetch("/api/session/current"),
      fetch("/api/players"),
      fetch("/api/games"),
    ]);
    var sessionRes = responses[0], playersRes = responses[1], gamesRes = responses[2];

    var session = await sessionRes.json();
    var players = await playersRes.json();
    var games = await gamesRes.json();

    players.forEach(function (p) { playerNames[p.id] = p.name; });
    games.forEach(function (g) { gameNames[g.gameId] = g.displayName; });

    if (session.hasActiveMatch) {
      await render(session.snapshot);
    } else {
      showIdle();
    }

    await connectSignalR();

    requestAnimationFrame(function () {
      requestAnimationFrame(function () {
        sceneEl.classList.add("is-ready");
      });
    });
  }

  init();
})();
