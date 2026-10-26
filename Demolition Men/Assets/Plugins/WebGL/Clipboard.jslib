mergeInto(LibraryManager.library, {
  // Platform.Copy in a browser: GUIUtility.systemCopyBuffer never leaves the page.
  DemolitionCopy: function (text) {
    var s = UTF8ToString(text);
    var fallback = function () {
      var area = document.createElement('textarea');
      area.value = s;
      area.style.position = 'fixed';
      area.style.opacity = '0';
      document.body.appendChild(area);
      area.select();
      try { document.execCommand('copy'); } catch (e) {}
      document.body.removeChild(area);
    };
    if (navigator.clipboard && window.isSecureContext)
      navigator.clipboard.writeText(s).catch(fallback);
    else
      fallback();
  }
});
