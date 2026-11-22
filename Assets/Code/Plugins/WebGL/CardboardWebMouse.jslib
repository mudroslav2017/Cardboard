mergeInto(LibraryManager.library, {
  $CardboardWebMouse: {
    canvas: null,
    capture: false,
    tracking: false,
    skipNext: true,
    x: 0,
    y: 0,
    clear: function () {
      CardboardWebMouse.x = CardboardWebMouse.y = 0;
      CardboardWebMouse.skipNext = true;
    },
    locked: function () {
      return document.pointerLockElement === CardboardWebMouse.canvas;
    },
    init: function () {
      var state = CardboardWebMouse;
      if (state.canvas) return;
      state.canvas = Module.canvas;
      // Unity owns lock requests and trusted-click deferral. Prefer raw input when available.
      if (state.canvas.requestPointerLock) {
        var request = state.canvas.requestPointerLock.bind(state.canvas);
        state.canvas.requestPointerLock = function (options) {
          var rawOptions = Object.assign({}, options || {}, { unadjustedMovement: true });
          function fallback(error) {
            if (error && (error.name === "NotSupportedError" || error.name === "TypeError"))
              return request(options);
            throw error;
          }
          try {
            var result = request(rawOptions);
            return result && typeof result.catch === "function" ? result.catch(fallback) : result;
          } catch (error) {
            return fallback(error);
          }
        };
      }
      document.addEventListener("pointerlockchange", state.clear);
      window.addEventListener("blur", state.clear);
      document.addEventListener("visibilitychange", function () {
        if (document.hidden) state.clear();
      });
      document.addEventListener("mousemove", function (event) {
        if (!state.capture || !state.tracking || !state.locked() || document.hidden) return;
        // The first event after a transition can contain the browser's recenter delta.
        if (state.skipNext) { state.skipNext = false; return; }
        var bounds = state.canvas.getBoundingClientRect();
        var limit = Math.max(256, Math.min(bounds.width, bounds.height) * .5);
        var x = event.movementX, y = event.movementY;
        // Reject individual warp events, not frame totals, preserving high-polling-rate input.
        if (Number.isFinite(x) && Math.abs(x) <= limit) state.x += x;
        if (Number.isFinite(y) && Math.abs(y) <= limit) state.y -= y;
      });
    }
  },
  CardboardWebMouse_Configure__deps: ["$CardboardWebMouse"],
  CardboardWebMouse_Configure__sig: "vii",
  CardboardWebMouse_Configure: function (capture, tracking) {
    var state = CardboardWebMouse;
    state.init();
    capture = !!capture; tracking = !!tracking;
    if (state.capture !== capture || state.tracking !== tracking) state.clear();
    state.capture = capture; state.tracking = tracking;
    if (!capture || !tracking || !state.locked()) state.clear();
  },
  CardboardWebMouse_Read__deps: ["$CardboardWebMouse"],
  CardboardWebMouse_Read__sig: "ip",
  CardboardWebMouse_Read: function (pointer) {
    var state = CardboardWebMouse;
    var locked = state.capture && state.tracking && state.locked() && !document.hidden;
    HEAPF32[pointer >> 2] = locked ? state.x : 0;
    HEAPF32[(pointer >> 2) + 1] = locked ? state.y : 0;
    state.x = state.y = 0;
    return locked ? 1 : 0;
  }
});