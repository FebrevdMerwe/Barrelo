/* Numeric throw-entry panel: an alternative to clicking the SVG dartboard.
   Lists segments 1-20 with Single/Double/Triple buttons per row, plus a Bull/25 row.
   Fires the same (ring, segment) shape as dartboard.js's onThrow, just without a position. */
(function (global) {
  "use strict";

  function el(name, attrs, text) {
    var e = document.createElement(name);
    for (var k in attrs) e.setAttribute(k, attrs[k]);
    if (text !== undefined) e.textContent = text;
    return e;
  }

  /**
   * Builds the numeric entry panel inside `containerEl` and returns a controller
   * shaped like createDartboard's, so index.html can wire both inputs the same way.
   */
  function createNumberPanel(containerEl) {
    var activateCb = null;

    function fire(ring, segment) {
      if (activateCb) activateCb(ring, segment);
    }

    function addButton(row, ring, segment, label, cls) {
      var btn = el("button", { type: "button", class: cls || "" }, label);
      btn.addEventListener("click", function () { fire(ring, segment); });
      row.appendChild(btn);
      return btn;
    }

    var list = el("div", { class: "number-list" });
    for (var seg = 1; seg <= 20; seg++) {
      var row = el("div", { class: "number-row" });
      addButton(row, "Outer", seg, String(seg), "num-single");
      addButton(row, "Double", seg, "D", "num-double");
      addButton(row, "Triple", seg, "T", "num-triple");
      list.appendChild(row);
    }
    containerEl.appendChild(list);

    var bullRow = el("div", { class: "number-row bull-row" });
    addButton(bullRow, "OuterBull", 25, "25 (Outer Bull)", "num-bull");
    addButton(bullRow, "InnerBull", 50, "50 (Inner Bull)", "num-bull");
    containerEl.appendChild(bullRow);

    return {
      onThrow: function (cb) { activateCb = cb; }
    };
  }

  global.createNumberPanel = createNumberPanel;
})(window);
