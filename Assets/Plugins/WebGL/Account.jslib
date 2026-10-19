mergeInto(LibraryManager.library, {
  DW_AccountPixelRatio: function () { var canvas = Module.canvas; return canvas && canvas.clientWidth > 0 ? canvas.width / canvas.clientWidth : 1; },
  DW_AccountRegister: function (name) { if (window.DWAccount) window.DWAccount.bridge(UTF8ToString(name)); },
  DW_AccountApplied: function () { if (window.DWAccount) window.DWAccount.syncApplied(); },
  DW_AccountFailed: function () { if (window.DWAccount) window.DWAccount.syncFailed(); },
  DW_AccountUpload: function (slot, json) { if (window.DWAccount) window.DWAccount.upload(slot, UTF8ToString(json)); },
  DW_AccountDelete: function (slot) { if (window.DWAccount) window.DWAccount.remove(slot); },
  DW_AccountOpen: function () { if (window.DWAccount) window.DWAccount.show(); },
  DW_AccountLogin: function (email, password) { if (window.DWAccount) window.DWAccount.login(UTF8ToString(email), UTF8ToString(password)); },
  DW_AccountCreate: function (email, password) { if (window.DWAccount) window.DWAccount.register(UTF8ToString(email), UTF8ToString(password)); },
  DW_AccountReset: function (email) { if (window.DWAccount) window.DWAccount.resetPassword(UTF8ToString(email)); },
  DW_AccountSignOut: function () { if (window.DWAccount) window.DWAccount.signOut(); },
  DW_AccountResearchConsent: function (consent) { if (window.DWAccount) window.DWAccount.setResearchConsent(consent !== 0); }
});
