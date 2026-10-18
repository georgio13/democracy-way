mergeInto(LibraryManager.library, {
  DW_SyncFS__deps: ['$FS', '$IDBFS'],
  DW_SyncFS: function () {
    // Unity 6000.4's queuePersist coalesces writes with autoSyncPersistentDataPath.
    if (typeof Module !== 'undefined' && Module.__unityIdbfsMount && typeof IDBFS.queuePersist === 'function') {
      IDBFS.queuePersist(Module.__unityIdbfsMount.mount);
      return;
    }
    if (Module.DW_syncRunning) { Module.DW_syncAgain = true; return; }
    Module.DW_syncRunning = true;
    var persist = function () {
      Module.DW_syncAgain = false;
      FS.syncfs(false, function (error) {
        if (error) console.error('[Democracy Way] Browser storage synchronization failed:', error);
        if (Module.DW_syncAgain) persist();
        else Module.DW_syncRunning = false;
      });
    };
    persist();
  }
});
