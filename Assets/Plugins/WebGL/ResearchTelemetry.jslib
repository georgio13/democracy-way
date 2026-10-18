mergeInto(LibraryManager.library, {
  DW_RecordCheckpoint: function (json) {
    if (window.DWResearch) window.DWResearch.capture(UTF8ToString(json));
  }
});
