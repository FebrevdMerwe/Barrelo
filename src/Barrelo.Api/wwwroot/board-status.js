/* The board pill in the header rail — which detector this host is configured for, and whether it is
   actually there right now. Both screens use it and both feed it the same way: one fetch on load for the
   state, then DetectionStatusChanged pushes on the game hub for every transition after, so a board
   manager restarted mid-match shows up as offline and back again without anyone refreshing.

   It only ever renders the two fields the host reports (source + connected). Naming a detector is a
   lookup here rather than a branch anywhere upstream, so adding a detector is one entry in this table. */
function createBoardStatusPill(pillEl, labelEl) {
  "use strict";

  var SOURCE_NAMES = {
    AutoDarts: "AutoDarts board",
    Simulator: "Board simulator",
  };

  // Mock and Manual mean the same thing to someone looking at the screen: there is no board, you are the
  // detector. Neither can drop, so neither ever shows a connection state.
  function isManual(source) {
    return source === "Mock" || source === "Manual";
  }

  function apply(status) {
    if (!pillEl || !labelEl) return;

    if (!status) {
      pillEl.className = "source-pill";
      labelEl.textContent = "Board status unavailable";
      return;
    }

    if (isManual(status.source)) {
      pillEl.className = "source-pill manual";
      labelEl.textContent = "Manual entry — no board connected";
      return;
    }

    var name = SOURCE_NAMES[status.source] || status.source;
    pillEl.className = "source-pill " + (status.isConnected ? "live" : "offline");
    labelEl.textContent = status.isConnected ? name + " connected" : name + " offline — reconnecting";
  }

  return {
    apply: apply,

    /* Reads the current state once. A failure here is not worth breaking the page over — the pill just
       says it doesn't know, and the next push corrects it. */
    load: function () {
      return fetch("/api/detection/status")
        .then(function (res) { return res.ok ? res.json() : null; })
        .then(apply)
        .catch(function (err) {
          console.error("Could not read board status: " + err);
          apply(null);
        });
    },

    /* Every change after load arrives here. Wire this up before starting the connection so a board that
       drops during startup isn't missed. */
    listen: function (connection) {
      connection.on("DetectionStatusChanged", apply);
    },
  };
}
