mergeInto(LibraryManager.library, {
  DW_AgoraEvent: function (json) {
    if (window.DWResearch && window.DWResearch.agoraEvent) window.DWResearch.agoraEvent(UTF8ToString(json));
  }
});
