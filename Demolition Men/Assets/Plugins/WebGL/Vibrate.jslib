mergeInto(LibraryManager.library, {
  // PhoneMotor in a browser: buzz for this many milliseconds, 0 stops it. Returns 0 where
  // the browser has no navigator.vibrate (Safari), else 1; a browser may still ignore the
  // call (a desktop has no motor, a frame nobody has tapped yet may not vibrate).
  DemolitionVibrate: function (milliseconds) {
    if (typeof navigator === 'undefined' || typeof navigator.vibrate !== 'function')
      return 0;
    try { navigator.vibrate(milliseconds); } catch (e) {}
    return 1;
  }
});
