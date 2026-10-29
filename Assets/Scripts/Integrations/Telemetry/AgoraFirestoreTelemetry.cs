using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Πύλη γεγονότων έρευνας (case_result, game_end κ.λπ.) προς το Firestore. Ίδια πολιτική με το
    /// ResearchTelemetry: φεύγει μόνο για συνδεδεμένο λογαριασμό (ResearchConsent, πλέον πάντα ενεργό), και η ταυτότητα λογαριασμού
    /// (email/uid) δεν περνά ποτέ αυτό το σύνορο — μόνο το ανώνυμο ResearchParticipantId.
    /// Εκτός WebGL τα γεγονότα γράφονται στην Console.
    /// Η σελίδα φιλοξενίας ορίζει window.DWResearch.agoraEvent(json) — βλ. AgoraTelemetry.jslib.
    /// </summary>
    public static class AgoraFirestoreTelemetry
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void DW_AgoraEvent(string json);
#endif

        public static void Send(string eventName, Dictionary<string, object> data)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            string json = Serialise(eventName, data);
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!AccountBridge.ResearchConsent) return;
            try { DW_AgoraEvent(json); }
            catch (Exception e) { Debug.LogWarning("Agora research event not sent: " + e.GetType().Name); }
#else
            Debug.Log("[AgoraTelemetry] " + json);
#endif
        }

        static string Serialise(string eventName, Dictionary<string, object> data)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"event\":").Append(Quote(eventName));
            sb.Append(",\"participantId\":").Append(Quote(AccountBridge.ResearchParticipantId ?? ""));
            sb.Append(",\"utc\":").Append(Quote(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
            sb.Append(",\"data\":{");
            bool first = true;
            if (data != null)
                foreach (var pair in data)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(Quote(pair.Key)).Append(':').Append(Value(pair.Value));
                }
            return sb.Append("}}").ToString();
        }

        static string Value(object value)
        {
            if (value == null) return "null";
            if (value is bool flag) return flag ? "true" : "false";
            if (value is float single)
                return float.IsNaN(single) || float.IsInfinity(single) ? Quote(single.ToString(CultureInfo.InvariantCulture)) : single.ToString("R", CultureInfo.InvariantCulture);
            if (value is double number)
                return double.IsNaN(number) || double.IsInfinity(number) ? Quote(number.ToString(CultureInfo.InvariantCulture)) : number.ToString("R", CultureInfo.InvariantCulture);
            if (value is int || value is long || value is short || value is byte || value is uint || value is ulong || value is ushort || value is sbyte)
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            if (value is Enum) return Quote(value.ToString());
            return Quote(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
        }

        static string Quote(string text)
        {
            var sb = new StringBuilder((text ?? "").Length + 2).Append('"');
            foreach (char c in text ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }
    }
}
