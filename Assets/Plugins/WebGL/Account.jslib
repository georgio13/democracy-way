mergeInto(LibraryManager.library, {
  DW_AccountRegister: function (name) { if (window.DWAccount) window.DWAccount.bridge(UTF8ToString(name)); },
  DW_AccountApplied: function () { if (window.DWAccount) window.DWAccount.syncApplied(); },
  DW_AccountFailed: function () { if (window.DWAccount) window.DWAccount.syncFailed(); },
  DW_AccountUpload: function (slot, json) { if (window.DWAccount) window.DWAccount.upload(slot, UTF8ToString(json)); },
  DW_AccountDelete: function (slot) { if (window.DWAccount) window.DWAccount.remove(slot); },
  DW_AccountOpen: function () { if (window.DWAccount) window.DWAccount.show(); }
});
