using System.Collections.Generic;
using System.Text;

namespace DemocracyWay.NewUI
{
    /// <summary>
    /// Σημαδεύει μέσα σε απλό κείμενο τις λέξεις που υπάρχουν στο ευρετήριο (τίτλοι και aliases):
    /// υπογράμμιση + TMP link με το id του λήμματος, ώστε το κλικ να ανοίγει το LexiconPanel.
    /// Καθαρή λογική κειμένου — καθόλου UI. Το λεξικό όρων χτίζεται μία φορά ανά LexiconContent.
    /// </summary>
    public static class LexiconMarkup
    {
        static LexiconContent source;
        static Dictionary<string, string> terms;   // κανονικοποιημένη λέξη → id λήμματος

        public static string Decorate(string text, LexiconContent content)
        {
            if (string.IsNullOrEmpty(text) || content == null || content.entries == null)
                return text ?? "";
            Build(content);
            var b = new StringBuilder(text.Length + 64);
            int i = 0;
            while (i < text.Length)
            {
                if (!char.IsLetter(text[i]))
                {
                    // Το κείμενο αποδίδεται πλέον με richText: ό,τι μοιάζει με tag ουδετεροποιείται.
                    b.Append(text[i] == '<' ? '‹' : text[i] == '>' ? '›' : text[i]);
                    i++;
                    continue;
                }

                int start = i;
                while (i < text.Length && char.IsLetter(text[i]))
                    i++;
                string word = text.Substring(start, i - start);
                if (terms.TryGetValue(LexiconPanel.Normalize(word), out string id))
                    b.Append("<u><link=\"").Append(id).Append("\">").Append(word).Append("</link></u>");
                else
                    b.Append(word);
            }

            return b.ToString();
        }

        static void Build(LexiconContent content)
        {
            if (source == content && terms != null)
                return;
            source = content;
            terms = new Dictionary<string, string>();
            foreach (var entry in content.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id))
                    continue;
                Add(entry.title, entry.id);
                if (entry.aliases != null)
                    foreach (string alias in entry.aliases)
                        Add(alias, entry.id);
            }
        }

        static void Add(string term, string id)
        {
            string key = LexiconPanel.Normalize(term);
            if (key.Length >= 3 && !key.Contains(" ") && !terms.ContainsKey(key))
                terms[key] = id;
        }
    }
}
