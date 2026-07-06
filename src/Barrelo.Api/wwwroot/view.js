/* Passive viewer: renders whatever match is currently active, with no matchId in the URL — meant
   to sit open on a TV indefinitely while a separate device (control.html) drives the game. Bootstraps
   via GET /api/session/current, then just re-renders on every GameStateUpdated push. There's only ever
   one match active at a time, so no join/subscribe step is needed — every connection gets every push. */
(function () {
  "use strict";

  var gameLabelEl = document.getElementById("gameLabel");
  var legMetaEl = document.getElementById("legMeta");
  var gameBoardEl = document.getElementById("game-board");
  var ledgerStrip = document.getElementById("ledgerStrip");
  var winBanner = document.getElementById("winBanner");
  var winLeaderboard = document.getElementById("winLeaderboard");
  var leaderboardList = document.getElementById("leaderboardList");

  var playerNames = {};
  var gameNames = {};
  var loadedRendererFor = null;
  var boardRenderer = null;

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
    renderLedger(snapshot);

    if (loadedRendererFor !== snapshot.gameId) {
      boardRenderer = await loadGameRenderer(snapshot.gameId);
      gameLabelEl.textContent = gameNames[snapshot.gameId] || snapshot.gameId;
    }
    boardRenderer(gameBoardEl, snapshot, playerNames);

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

    if (session.hasActiveMatch) await render(session.snapshot);
    // else: leave the idle placeholder in place, or — if we've already rendered a completed match
    // in a prior session on this page — keep showing that final result rather than blanking it.

    await connectSignalR();

    requestAnimationFrame(function () {
      requestAnimationFrame(function () {
        document.getElementById("scene").classList.add("is-ready");
      });
    });
  }

  init();
})();
