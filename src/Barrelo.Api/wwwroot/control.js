/* Interactive controls: drives the active match (dartboard input, Miss/Undo/End Turn) with no matchId
   in the URL — meant for the tablet/phone someone actually holds, while view.html sits passively on a
   TV showing the same state. Bootstraps via GET /api/session/current; if no match is active, shows an
   idle panel pointing back to setup. A match starting while idle (or finishing while active) arrives
   as a GameStateUpdated push and flips the UI over automatically — there's only ever one match, so no
   join/subscribe step is needed. */
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

  var playerNames = {};
  var gameNames = {};
  var loadedRendererFor = null;
  var boardRenderer = null;
  var drawerOpen = false;
  var lastCurrentPlayer = null;

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

  function defaultRenderGameBoard(container, snapshot) {
    container.innerHTML = "";
    var pre = document.createElement("pre");
    pre.className = "default-payload-dump";
    pre.textContent = JSON.stringify(snapshot.payload, null, 2);
    container.appendChild(pre);
  }

  /* Board UI resolution, tried in order per gameId:
       1. /plugins/{gameId}/ui/index.html — iframe'd, fed GameStateSnapshot via postMessage. This is what
          lets a game's board be PixiJS/Phaser/a Unity WebGL build/anything else, fully sandboxed from the
          host page.
       2. /plugins/{gameId}/render.js — defines window.renderGameBoard(container, snapshot), called directly.
       3. Neither present — generic payload dump, so a game that ships no UI still works. */
  function createIframeRenderer(url) {
    var iframe = null;
    var ready = false;
    var pending = null;

    function send(snapshot, playerNames) {
      if (iframe && iframe.contentWindow) {
        iframe.contentWindow.postMessage(
          { type: "barrelo:gameState", snapshot: snapshot, playerNames: playerNames || {} },
          window.location.origin);
      }
    }

    return function (container, snapshot, playerNames) {
      if (!iframe) {
        container.innerHTML = "";
        iframe = document.createElement("iframe");
        iframe.className = "game-board-iframe";
        iframe.title = "Game board";
        iframe.addEventListener("load", function () {
          ready = true;
          if (pending) { send(pending.snapshot, pending.playerNames); pending = null; }
        });
        iframe.src = url;
        container.appendChild(iframe);
      }
      if (ready) send(snapshot, playerNames); else pending = { snapshot: snapshot, playerNames: playerNames };
    };
  }

  function loadScriptRenderer(gameId) {
    return new Promise(function (resolve) {
      window.renderGameBoard = undefined;
      var script = document.createElement("script");
      script.src = "/plugins/" + encodeURIComponent(gameId) + "/render.js";
      script.onload = function () {
        resolve(typeof window.renderGameBoard === "function" ? window.renderGameBoard : defaultRenderGameBoard);
      };
      script.onerror = function () {
        console.warn('No render.js for game "' + gameId + '" — using the default board renderer.');
        resolve(defaultRenderGameBoard);
      };
      document.head.appendChild(script);
    });
  }

  function loadGameRenderer(gameId) {
    if (loadedRendererFor === gameId && boardRenderer) return Promise.resolve(boardRenderer);

    var iframeUrl = "/plugins/" + encodeURIComponent(gameId) + "/ui/index.html";
    return fetch(iframeUrl, { method: "HEAD" })
      .then(function (res) {
        if (res.ok) return createIframeRenderer(iframeUrl);
        console.warn('No ' + iframeUrl + ' for game "' + gameId + '" (HTTP ' + res.status + ') — falling back to render.js.');
        return loadScriptRenderer(gameId);
      })
      .catch(function (err) {
        console.warn('Fetching ' + iframeUrl + ' failed (' + err + ') — falling back to render.js.');
        return loadScriptRenderer(gameId);
      })
      .then(function (renderer) {
        loadedRendererFor = gameId;
        return renderer;
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
    showActive();

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

    if (snapshot.status === "Aborted") {
      document.getElementById("winTitle").textContent = "Game interrupted";
      document.getElementById("winSub").textContent = "The game's process stopped responding — this match can't continue.";
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
    return connection.start();
  }

  async function init() {
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
