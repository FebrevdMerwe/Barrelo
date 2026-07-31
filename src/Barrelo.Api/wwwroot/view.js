/* Passive viewer: renders whatever match is currently active, with no matchId in the URL — meant
   to sit open on a TV indefinitely while a separate device (control.html) drives the game. Bootstraps
   via GET /api/session/current, then just re-renders on every GameStateUpdated push. There's only ever
   one match active at a time, so no join/subscribe step is needed — every connection gets every push.

   Deliberately reports no match result: this page only watches, and a match must be ended by the device
   someone is actually holding. It does still report its replay hash, because divergence is only visible
   by comparing two screens — see IReplayDivergenceMonitor. */
(function () {
  "use strict";

  var sceneEl = document.getElementById("scene");
  var gameLabelEl = document.getElementById("gameLabel");
  var legMetaEl = document.getElementById("legMeta");
  var gameBoardEl = document.getElementById("game-board");
  var ledgerStrip = document.getElementById("ledgerStrip");
  var winBanner = document.getElementById("winBanner");
  var winLeaderboard = document.getElementById("winLeaderboard");
  var leaderboardList = document.getElementById("leaderboardList");

  var playerNames = {};
  var gameNames = {};
  var lastSnapshot = null;
  var displayHint = null;

  var gameFrame = createGameFrame({
    onDisplay: function (hint) {
      displayHint = hint;
      if (lastSnapshot) renderChrome(lastSnapshot);
    },
    onGameChanged: function (gameId, isIframe) {
      gameLabelEl.textContent = gameNames[gameId] || gameId;
      sceneEl.classList.toggle("board-fullscreen", isIframe);
      displayHint = null;
    },
  });

  function legMetaOf(snapshot) {
    var leg = displayHint && displayHint.legNumber !== undefined ? displayHint.legNumber : snapshot.legNumber;
    var set = displayHint && displayHint.setNumber !== undefined ? displayHint.setNumber : snapshot.setNumber;
    return "Leg " + leg + " · Set " + set;
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
    renderLedger(snapshot);

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
    lastSnapshot = snapshot;
    renderChrome(snapshot);
    await gameFrame.render(gameBoardEl, snapshot, playerNames);
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
