using System.Collections.Generic;
using UnityEngine;

namespace Agora
{
    /// <summary>
    /// Ό,τι καταγράφεται για την έρευνα. Μία κλήση ανά γεγονός, flat key/value — εύκολο για Firestore.
    /// Το δικό σου Firestore component υλοποιεί αυτό το interface και συνδέεται στον GameSession από τον Inspector.
    /// Γεγονότα: character_created, prytany_start, case_start, game_end (η πλήρης καταγραφή: βήμα 9).
    /// </summary>
    public interface ITelemetry
    {
        void Log(string eventName, Dictionary<string, object> data);
    }

    /// <summary>Προεπιλογή όταν δεν έχει συνδεθεί τίποτα: Debug.Log. Απλή κλάση, όχι component.</summary>
    public class ConsoleTelemetry : ITelemetry
    {
        public void Log(string eventName, Dictionary<string, object> data)
        {
            var sb = new System.Text.StringBuilder(eventName).Append(" {");
            foreach (var kv in data) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(", ");
            Debug.Log(sb.Append('}').ToString());
        }
    }
}
