using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DemocracyWay.NewUI
{
    /// <summary>Κοινές συναρτήσεις κατακερματισμού — μία υλοποίηση για saves, cloud sync και research pipeline.</summary>
    public static class Hashing
    {
        /// <summary>FNV-1a σε hex 8 ψηφίων. Πρέπει να παράγει ΙΔΙΑ τιμή σε SaveStore και AccountBridge.</summary>
        public static string Fnv1a(string text)
        {
            unchecked
            {
                uint value = 2166136261;
                foreach (byte b in Encoding.UTF8.GetBytes(text ?? "")) { value ^= b; value *= 16777619; }
                return value.ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>SHA-256 σε πεζό hex.</summary>
        public static string Sha256Hex(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""))).Replace("-", "").ToLowerInvariant();
        }
    }
}
