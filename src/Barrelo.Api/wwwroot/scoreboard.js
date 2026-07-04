/* Shell: chrome shared by every game (identity, visit darts, ledger, board input,
   controls, winner banner), now driven by the real GameStateSnapshot pushed over
   SignalR instead of a locally-simulated state machine. The shell reads only the
   universal envelope fields (matchId/gameId/status/currentPlayerId/legNumber/
   setNumber/recentThrows/isComplete/winnerPlayerIds) plus one soft, best-effort
   convention on payload (payload.currentVisitThrows, if a game provides it) for
   the visit-progress strip — everything else in payload is opaque and handed
   straight to the per-game renderGameBoard. */
(function () {
  "use strict";

  var params = new URLSearchParams(window.location.search);
  var matchId = params.get("matchId");
  if (!matchId) {
    window.location.href = "index.html";
    return;
  }

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

  var playerNames = {};
  var gameNames = {};
  var loadedRendererFor = null;
  var boardRenderer = null;
  var drawerOpen = false;
  var lastCurrentPlayer = null;

  function setDrawerOpen(open) {
    drawerOpen = open;
    inputDrawer.classList.toggle("open", open);
    sceneEl.classList.toggle("drawer-open", open);
  }

  function defaultRenderGameBoard(container, snapshot) {
    container.innerHTML = "";
    var pre = document.createElement("pre");
    pre.className = "default-payload-dump";
    pre.textContent = JSON.stringify(snapshot.payload, null, 2);
    container.appendChild(pre);
  }

  /* Convention (PLAN.md): the shell always looks for /plugins/{gameId}/render.js.
     If it defines window.renderGameBoard, use it; a 404 or missing global falls
     back to the generic payload dump above so a game that ships no UI still works. */
  function loadGameRenderer(gameId) {
    return new Promise(function (resolve) {
      if (loadedRendererFor === gameId && typeof window.renderGameBoard === "function") {
        resolve(window.renderGameBoard);
        return;
      }
      window.renderGameBoard = undefined;
      var script = document.createElement("script");
      script.src = "/plugins/" + encodeURIComponent(gameId) + "/render.js";
      script.onload = function () {
        loadedRendererFor = gameId;
        resolve(typeof window.renderGameBoard === "function" ? window.renderGameBoard : defaultRenderGameBoard);
      };
      script.onerror = function () {
        console.warn('No render.js for game "' + gameId + '" — using the default board renderer.');
        loadedRendererFor = gameId;
        resolve(defaultRenderGameBoard);
      };
      document.head.appendChild(script);
    });
  }

  function visitThrows(snapshot) {
    var payload = snapshot.payload;
    return payload && Array.isArray(payload.currentVisitThrows) ? payload.currentVisitThrows : [];
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

  function updateDrawerCopy(snapshot) {
    var name = playerNames[snapshot.currentPlayerId] || "—";
    var dartNum = Math.min(visitThrows(snapshot).length + 1, 3);
    peekTurnEl.textContent = snapshot.isComplete ? "Match complete" : name + "'s turn";
    peekSubEl.textContent = snapshot.isComplete
      ? "Tap below to start a new match"
      : "Dart " + dartNum + " of 3 · tap to throw";
    openTitleEl.textContent = "Dart " + dartNum + " of 3";
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

  async function render(snapshot) {
    var turnChanged = snapshot.currentPlayerId !== lastCurrentPlayer;
    if (turnChanged || snapshot.isComplete) setDrawerOpen(false);
    lastCurrentPlayer = snapshot.currentPlayerId;

    renderVisitSlots(snapshot);
    updateDrawerCopy(snapshot);
    renderLedger(snapshot);

    if (loadedRendererFor !== snapshot.gameId) {
      boardRenderer = await loadGameRenderer(snapshot.gameId);
      gameLabelEl.textContent = gameNames[snapshot.gameId] || snapshot.gameId;
    }
    boardRenderer(gameBoardEl, snapshot, playerNames);

    dartboard.setDisabled(snapshot.isComplete);
    dartboard.setDeadTargets([]);

    legMetaEl.textContent = snapshot.isComplete
      ? "Match complete"
      : "Leg " + snapshot.legNumber + " · Set " + snapshot.setNumber;

    if (snapshot.isComplete) {
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

  async function connectSignalR() {
    var connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/game")
      .withAutomaticReconnect()
      .build();
    connection.on("GameStateUpdated", function (snapshot) {
      if (snapshot.matchId === matchId) render(snapshot);
    });
    await connection.start();
    await connection.invoke("JoinMatch", matchId);
  }

  async function init() {
    var responses = await Promise.all([
      fetch("/api/matches/" + encodeURIComponent(matchId)),
      fetch("/api/players"),
      fetch("/api/games"),
    ]);
    var matchRes = responses[0], playersRes = responses[1], gamesRes = responses[2];

    if (!matchRes.ok) {
      alert("Couldn't load this match — it may not exist.");
      window.location.href = "index.html";
      return;
    }

    var snapshot = await matchRes.json();
    var players = await playersRes.json();
    var games = await gamesRes.json();

    players.forEach(function (p) { playerNames[p.id] = p.name; });
    games.forEach(function (g) { gameNames[g.gameId] = g.displayName; });

    await render(snapshot);
    await connectSignalR();

    requestAnimationFrame(function () {
      requestAnimationFrame(function () {
        sceneEl.classList.add("is-ready");
      });
    });
  }

  init();
})();
