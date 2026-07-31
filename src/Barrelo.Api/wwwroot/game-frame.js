/* Owns the boundary between the shell and a game's own board UI, for both control.html and view.html.

   Board UI resolution, tried in order per gameId:
     1. /plugins/{gameId}/ui/index.html — iframe'd, fed the GameStateSnapshot via postMessage. This is what
        lets a game's board be PixiJS/Phaser/a Unity WebGL build/anything else, fully sandboxed from the
        host page.
     2. /plugins/{gameId}/render.js — defines window.renderGameBoard(container, snapshot), called directly.
     3. Neither present — generic payload dump, so a game that ships no UI still works.

   The iframe channel is two-way. A client-owned game runs its own rules in there, so the host doesn't know
   whose turn it is or what the score means; the game sends that back as advisory display hints, and
   announces its own result when its rules say the match is over. Nothing arriving from the frame is
   trusted for anything the host can determine itself. */
window.createGameFrame = function (options) {
  "use strict";

  options = options || {};

  var loadedRendererFor = null;
  var boardRenderer = null;
  var boardRendererIsIframe = false;
  var frameWindow = null;

  function defaultRenderGameBoard(container, snapshot) {
    container.innerHTML = "";
    var pre = document.createElement("pre");
    pre.className = "default-payload-dump";
    pre.textContent = JSON.stringify(snapshot.payload, null, 2);
    container.appendChild(pre);
  }

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
          frameWindow = iframe.contentWindow;
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
    var iframeUrl = "/plugins/" + encodeURIComponent(gameId) + "/ui/index.html";
    return fetch(iframeUrl, { method: "HEAD" })
      .then(function (res) {
        if (res.ok) {
          boardRendererIsIframe = true;
          return createIframeRenderer(iframeUrl);
        }
        console.warn('No ' + iframeUrl + ' for game "' + gameId + '" (HTTP ' + res.status + ') — falling back to render.js.');
        boardRendererIsIframe = false;
        return loadScriptRenderer(gameId);
      })
      .catch(function (err) {
        console.warn('Fetching ' + iframeUrl + ' failed (' + err + ') — falling back to render.js.');
        boardRendererIsIframe = false;
        return loadScriptRenderer(gameId);
      });
  }

  function post(url, body) {
    return fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body || {}),
    }).catch(function (err) {
      console.warn("POST " + url + " failed: " + err);
      return null;
    });
  }

  /* Both checks matter: origin alone would accept a message from any same-origin frame or opener, and
     source alone would accept one from a cross-origin frame we happen to hold a handle to. */
  window.addEventListener("message", function (event) {
    if (event.origin !== window.location.origin) return;
    if (!frameWindow || event.source !== frameWindow) return;

    var data = event.data;
    if (!data || typeof data !== "object") return;

    if (data.type === "barrelo:display") {
      // Advisory chrome only — whose turn it is, which leg, which targets are spent. The host can't
      // derive any of it for a client-owned game, and none of it is trusted for scoring.
      if (typeof data.logHash === "string" && typeof data.stateHash === "string") {
        post("/api/session/state-hash", { logHash: data.logHash, stateHash: data.stateHash });
      }
      if (options.onDisplay) options.onDisplay(data);
      return;
    }

    if (data.type === "barrelo:matchComplete" && options.onMatchComplete) {
      // Only the interactive page reports this — the passive viewer must never end a match it is only
      // watching, and two clients reporting the same result would race for the same session.
      post("/api/session/result", {
        winnerPlayerIds: data.winnerPlayerIds || [],
        finalStandings: data.finalStandings || [],
      }).then(function (res) {
        if (res && !res.ok) console.warn("Match result rejected: HTTP " + res.status);
        if (res && res.ok) options.onMatchComplete(data);
      });
    }
  });

  return {
    isIframe: function () { return boardRendererIsIframe; },

    /* Resolves (and caches) the renderer for this snapshot's game, then draws it. onGameChanged fires
       only when the resolved game actually changes, which is when page chrome outside the board region
       needs updating too. */
    render: function (container, snapshot, playerNames) {
      if (loadedRendererFor === snapshot.gameId && boardRenderer) {
        boardRenderer(container, snapshot, playerNames);
        return Promise.resolve();
      }

      return loadGameRenderer(snapshot.gameId).then(function (renderer) {
        loadedRendererFor = snapshot.gameId;
        boardRenderer = renderer;
        if (options.onGameChanged) options.onGameChanged(snapshot.gameId, boardRendererIsIframe);
        boardRenderer(container, snapshot, playerNames);
      });
    },
  };
};
