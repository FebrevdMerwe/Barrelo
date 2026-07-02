/* Shell: chrome shared by every game (identity, visit darts, ledger, board input,
   controls, winner banner). Never reads plugin-specific fields off `state` beyond
   the small universal contract: currentPlayer, visit, complete, winner, lastEvent,
   deadTargets. Everything else is opaque and handed straight to renderGameBoard. */
(function () {
  "use strict";

  var params = new URLSearchParams(window.location.search);
  var gameId = params.get("game") || "x01";
  var plugin = window.GamePlugins && window.GamePlugins[gameId];
  if (!plugin) {
    console.warn('Unknown game "' + gameId + '", falling back to x01.');
    gameId = "x01";
    plugin = window.GamePlugins.x01;
  }

  var descriptor = (window.GAME_CATALOG || []).find(function (g) { return g.id === gameId; }) || { name: gameId };
  var players = [params.get("p1") || "Febre", params.get("p2") || "Sam"];

  document.title = "Oché — " + descriptor.name;
  document.getElementById("gameLabel").textContent = descriptor.name;
  players.forEach(function (name, idx) {
    document.querySelectorAll('[data-player="' + idx + '"] .player-name')[0].textContent = name;
  });

  var history = [];

  function simulate() {
    var s = plugin.freshState();
    for (var i = 0; i < history.length; i++) plugin.applyEvent(s, history[i]);
    return s;
  }

  var cards = Array.prototype.slice.call(document.querySelectorAll(".scorecard"));
  var flourishEl = document.getElementById("flourish");
  var ledgerStrip = document.getElementById("ledgerStrip");
  var winBanner = document.getElementById("winBanner");
  var gameBoardEl = document.getElementById("gameBoard");
  var legMetaEl = document.getElementById("legMeta");

  var dartboard = createDartboard(document.getElementById("dartboard"));

  function renderCardChrome(card, idx, state) {
    var isActive = state.currentPlayer === idx && !state.complete;
    card.classList.toggle("active", isActive);

    var visitEl = card.querySelector('[data-el="visit"]');
    visitEl.innerHTML = "";
    var visitThrows = state.currentPlayer === idx ? state.visit : [];
    for (var i = 0; i < 3; i++) {
      var slot = document.createElement("div");
      slot.className = "dart-slot";
      var t = visitThrows[i];
      if (t) {
        slot.classList.add("filled");
        slot.textContent = t.notation;
      }
      visitEl.appendChild(slot);
    }
  }

  function renderLedger() {
    ledgerStrip.innerHTML = "";
    if (history.length === 0) {
      var empty = document.createElement("span");
      empty.className = "empty";
      empty.textContent = "No darts thrown yet — tap the board to open the leg.";
      ledgerStrip.appendChild(empty);
      return;
    }
    history.forEach(function (ev) {
      var chip = document.createElement("span");
      if (ev.type === "endturn") {
        chip.className = "chip divider";
        chip.textContent = "‖";
      } else {
        var notation = DartScoring.notationFor(ev.ring, ev.segment);
        chip.className = "chip" + (ev.ring === "Miss" ? " miss" : "");
        chip.textContent = notation;
      }
      ledgerStrip.appendChild(chip);
    });
    ledgerStrip.scrollLeft = ledgerStrip.scrollWidth;
  }

  var flourishTimer = null;
  function showFlourish(event) {
    if (!event || !event.text) return;
    clearTimeout(flourishTimer);
    flourishEl.textContent = event.text;
    flourishEl.className = "flourish show " + (event.tone === "bad" ? "bad" : "good");
    flourishTimer = setTimeout(function () { flourishEl.classList.remove("show"); }, 1500);
  }

  function render(state) {
    cards.forEach(function (card) {
      renderCardChrome(card, parseInt(card.getAttribute("data-player"), 10), state);
    });
    renderLedger();
    plugin.renderGameBoard(gameBoardEl, state, players);
    dartboard.setDisabled(state.complete);
    dartboard.setDeadTargets(state.deadTargets);

    var legsPlayed = (state.legs ? state.legs[0] + state.legs[1] : 0);
    legMetaEl.textContent = state.complete
      ? "Match complete"
      : "Leg " + (legsPlayed + 1) + " · Set " + ((state.sets ? state.sets[0] + state.sets[1] : 0) + 1);

    if (state.complete) {
      document.getElementById("winTitle").textContent = "Game shot!";
      document.getElementById("winSub").textContent =
        players[state.winner] + " wins the match " + state.legs[state.winner] + "–" + state.legs[1 - state.winner] + ".";
      winBanner.classList.add("show");
    } else {
      winBanner.classList.remove("show");
    }

    if (state.lastEvent) showFlourish(state.lastEvent);
  }

  function pushAndRender(ev) {
    history.push(ev);
    render(simulate());
  }

  dartboard.onThrow(function (ring, segment) {
    pushAndRender({ type: "throw", ring: ring, segment: segment });
  });
  document.getElementById("btnMiss").addEventListener("click", function () {
    pushAndRender({ type: "throw", ring: "Miss", segment: 0 });
  });
  document.getElementById("btnUndo").addEventListener("click", function () {
    history.pop();
    render(simulate());
  });
  document.getElementById("btnEndTurn").addEventListener("click", function () {
    pushAndRender({ type: "endturn" });
  });
  document.getElementById("btnRack").addEventListener("click", function () {
    history = [];
    render(simulate());
  });

  render(simulate());
  requestAnimationFrame(function () {
    requestAnimationFrame(function () {
      document.getElementById("scene").classList.add("is-ready");
    });
  });
})();
