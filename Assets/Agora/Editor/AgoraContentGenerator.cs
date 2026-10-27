#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Agora.EditorTools
{
    /// <summary>
    /// Μενού Agora → Generate Content: δημιουργεί ΜΙΑ ΦΟΡΑ τα assets (αξιώματα, πρόσωπα, λογογράφοι,
    /// 20 υποθέσεις με όλους τους αριθμούς, GameConfig, GameContent) στο Assets/Agora/Content.
    /// Τα ΚΕΙΜΕΝΑ (είδηση, ΕΚ1, λόγοι, ΕΚ3) τα επικολλάς από την καρτέλα «Υποθέσεις» στον Inspector.
    /// Τρέχει στον Editor, όχι στο παιχνίδι — τίποτα δεν δημιουργείται σε runtime.
    /// </summary>
    public static class AgoraContentGenerator
    {
        private const string Root = "Assets/Agora/Content";
        private static readonly Dictionary<string, OfficeData> Offices = new Dictionary<string, OfficeData>();
        private static readonly Dictionary<string, OfficialData> Officials = new Dictionary<string, OfficialData>();

        [MenuItem("Agora/Generate Content (once)")]
        public static void Generate()
        {
            foreach (var d in new[] { "", "/Offices", "/Officials", "/Logographers", "/Cases" })
                if (!AssetDatabase.IsValidFolder(Root + d)) Directory.CreateDirectory(Root + d);
            AssetDatabase.Refresh();

            var cfg = Create<GameConfig>($"{Root}/GameConfig.asset");
            MakeOffices();
            MakeOfficials();
            var logos = MakeLogographers();
            var cases = MakeCases();

            var content = Create<GameContent>($"{Root}/GameContent.asset");
            content.config = cfg;
            content.cases = cases;
            content.offices = Offices.Values.ToList();
            content.officials = Officials.Values.ToList();
            content.logographers = logos;
            content.prytanyEpistatai = new List<string> { "Νικόστρατος", "Φιλοκλής", "Δημοχάρης", "Αριστόνικος", "Καλλίας", "Θεόδοτος", "Μενεκράτης", "Ξενοκλής", "Πολύζηλος", "Ηγήμων" };
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();
            Debug.Log("Agora: περιεχόμενο δημιουργήθηκε στο " + Root);
        }

        // ---------------- Αξιώματα (πραγματικό πλήθος, ΑΠ 42–62) ----------------

        private static void MakeOffices()
        {
            void O(string key, string name, string withCount, int count, SelectionMode mode, int pay, string ap, int w)
            {
                var o = Create<OfficeData>($"{Root}/Offices/Office_{key}.asset");
                o.displayName = name; o.displayNameWithCount = withCount; o.count = count; o.selection = mode;
                o.payObols = pay; o.athPolRef = ap; o.defaultWeight = w; Offices[key] = o; EditorUtility.SetDirty(o);
            }
            O("Boule", "Βουλευταί", "Η Βουλή των Πεντακοσίων", 500, SelectionMode.Lot, 5, "43.2", 15);
            O("Prytaneis", "Πρυτάνεις", "Οι πενήντα πρυτάνεις", 50, SelectionMode.Lot, 6, "43.2–3", 10);
            O("Epistates", "Επιστάτης των πρυτάνεων", "Ο επιστάτης των πρυτάνεων", 1, SelectionMode.Lot, 0, "44.1", 5);
            O("Proedroi", "Πρόεδροι", "Οι εννέα πρόεδροι", 9, SelectionMode.Lot, 0, "44.2–3", 5);
            O("Archon", "Άρχων", "Ο επώνυμος άρχων", 1, SelectionMode.Lot, 4, "55–56", 10);
            O("Basileus", "Βασιλεύς", "Ο άρχων βασιλεύς", 1, SelectionMode.Lot, 4, "57", 10);
            O("Polemarchos", "Πολέμαρχος", "Ο πολέμαρχος", 1, SelectionMode.Lot, 4, "58", 5);
            O("Thesmothetai", "Θεσμοθέται", "Οι έξι θεσμοθέται", 6, SelectionMode.Lot, 4, "59", 10);
            O("Areopagus", "Αρεοπαγίται", "Η βουλή του Αρείου Πάγου", 150, SelectionMode.Lifelong, 0, "25, 57.3", 10);
            O("Strategoi", "Στρατηγοί", "Οι δέκα στρατηγοί", 10, SelectionMode.Election, 0, "61.1", 15);
            O("Taxiarchoi", "Ταξίαρχοι", "Οι δέκα ταξίαρχοι", 10, SelectionMode.Election, 0, "61.3", 5);
            O("Hipparchoi", "Ίππαρχοι", "Οι δύο ίππαρχοι", 2, SelectionMode.Election, 0, "61.4", 5);
            O("TamiasStrat", "Ταμίας των στρατιωτικών", "Ο ταμίας των στρατιωτικών", 1, SelectionMode.Election, 0, "43.1", 10);
            O("Dioikesis", "Ο επί τη διοικήσει", "Ο επί τη διοικήσει", 1, SelectionMode.Election, 0, "— (επιγραφικά)", 10);
            O("Theorikon", "Οι επί το θεωρικόν", "Οι επί το θεωρικόν", 10, SelectionMode.Election, 0, "43.1, 47.2", 10);
            O("TamiaiAthenas", "Ταμίαι της Αθηνάς", "Οι δέκα ταμίαι της Αθηνάς", 10, SelectionMode.Lot, 0, "47.1", 10);
            O("Poletai", "Πωληταί", "Οι δέκα πωληταί", 10, SelectionMode.Lot, 0, "47.2–3", 5);
            O("Apodektai", "Αποδέκται", "Οι δέκα αποδέκται", 10, SelectionMode.Lot, 0, "48.1–2", 5);
            O("LogistaiBoule", "Λογισταί της Βουλής", "Οι δέκα λογισταί της Βουλής", 10, SelectionMode.FromCouncil, 0, "48.3", 5);
            O("Logistai", "Λογισταί", "Οι δέκα λογισταί", 10, SelectionMode.Lot, 0, "54.2", 5);
            O("Euthynoi", "Εύθυνοι", "Οι δέκα εύθυνοι", 10, SelectionMode.Lot, 0, "48.4", 5);
            O("Trieropoioi", "Τριηροποιοί", "Οι δέκα τριηροποιοί", 10, SelectionMode.FromCouncil, 0, "46.1", 5);
            O("Sitophylakes", "Σιτοφύλακες", "Οι τριάντα πέντε σιτοφύλακες", 35, SelectionMode.Lot, 0, "51.3", 5);
            O("Emporiou", "Επιμεληταί του εμπορίου", "Οι δέκα επιμεληταί του εμπορίου", 10, SelectionMode.Lot, 0, "51.4", 5);
            O("Agoranomoi", "Αγορανόμοι", "Οι δέκα αγορανόμοι", 10, SelectionMode.Lot, 0, "51.1", 5);
            O("Metronomoi", "Μετρονόμοι", "Οι δέκα μετρονόμοι", 10, SelectionMode.Lot, 0, "51.2", 5);
            O("Astynomoi", "Αστυνόμοι", "Οι δέκα αστυνόμοι", 10, SelectionMode.Lot, 0, "50.2", 5);
            O("Hendeka", "Οι Ένδεκα", "Οι Ένδεκα", 11, SelectionMode.Lot, 0, "52.1", 5);
            O("Eisagogeis", "Εισαγωγείς", "Οι πέντε εισαγωγείς", 5, SelectionMode.Lot, 0, "52.2", 5);
            O("Tettarakonta", "Οι Τετταράκοντα", "Οι Τετταράκοντα", 40, SelectionMode.Lot, 0, "53.1", 5);
            O("Hodopoioi", "Οδοποιοί", "Οι πέντε οδοποιοί", 5, SelectionMode.Lot, 0, "54.1", 5);
            O("Hieropoioi", "Ιεροποιοί", "Οι δέκα ιεροποιοί", 10, SelectionMode.Lot, 0, "54.6–7", 5);
            O("Athlothetai", "Αθλοθέται", "Οι δέκα αθλοθέται", 10, SelectionMode.Lot, 0, "60.1", 5);
            O("Dikastai", "Δικασταί", "Οι έξι χιλιάδες δικασταί", 6000, SelectionMode.Lot, 3, "24.3, 63–69", 0);
            O("Rhetor", "Ρήτωρ", "Ιδιώτης πολίτης που μιλά στην Εκκλησία", 0, SelectionMode.Election, 0, "—", 10);
        }

        // ---------------- Πρόσωπα ----------------

        private static void MakeOfficials()
        {
            void P(string key, string name, string demotic, string office, bool hist, bool bribable, int tendency)
            {
                var p = Create<OfficialData>($"{Root}/Officials/Official_{key}.asset");
                p.displayName = name; p.demotic = demotic; p.office = Offices[office]; p.isHistorical = hist;
                p.bribable = bribable; p.tendency = tendency; Officials[key] = p; EditorUtility.SetDirty(p);
            }
            P("Phokion", "Φωκίων", "Ποταμεύς", "Strategoi", true, false, 45);
            P("Demosthenes", "Δημοσθένης", "Παιανιεύς", "Rhetor", true, false, 85);
            P("Hypereides", "Υπερείδης", "Κολλυτεύς", "Rhetor", true, false, 90);
            P("Demades", "Δημάδης", "Παιανιεύς", "Rhetor", true, true, 20);
            P("Leosthenes", "Λεωσθένης", "Κεφαλήθεν", "Strategoi", true, false, 50);
            P("Lykourgos", "Λυκούργος", "Βουτάδης", "Dioikesis", true, false, 75);
            P("Deinarchos", "Δείναρχος", "Κορίνθιος", "Rhetor", true, true, 30);
            P("Pytheas", "Πυθέας", "Αλωπεκήθεν", "Rhetor", true, true, 25);
            P("Agnonides", "Αγνωνίδης", "Περγασήθεν", "Rhetor", true, false, 80);
            P("Stratokles", "Στρατοκλής", "Διομειεύς", "Rhetor", true, true, 35);
            P("Antiphilos", "Αντίφιλος", "", "Strategoi", true, false, 60);
            P("Aristonikos", "Αριστόνικος", "Μαραθώνιος", "Epistates", false, false, 60);
            P("Kephisodoros", "Κηφισόδωρος", "Αφιδναίος", "Thesmothetai", false, false, 65);
            P("Euthydemos", "Ευθύδημος", "Σφήττιος", "TamiasStrat", false, true, 30);
            P("Kephalos", "Κέφαλος", "Κολλυτεύς", "LogistaiBoule", false, false, 55);
            P("Mnesitheos", "Μνησίθεος", "Πειραιεύς", "Trieropoioi", false, false, 60);
            P("Thrasyllos", "Θράσυλλος", "Ελευσίνιος", "Euthynoi", false, false, 65);
            P("Timokrates", "Τιμοκράτης", "Αχαρνεύς", "Sitophylakes", false, false, 70);
            P("Archias", "Αρχίας", "Παλληνεύς", "Poletai", false, true, 35);
            P("Polyeuktos", "Πολύευκτος", "Σφήττιος", "Areopagus", false, false, 25);
            P("Eurymedon", "Ευρυμέδων", "Μυρρινούσιος", "Basileus", false, false, 40);
        }

        // ---------------- Λογογράφοι ----------------

        private static List<LogographerData> MakeLogographers()
        {
            var list = new List<LogographerData>();
            void L(string key, string name, int q, int cost)
            {
                var l = Create<LogographerData>($"{Root}/Logographers/Logographer_{key}.asset");
                l.displayName = name; l.quality = q; l.costDrachmas = cost; list.Add(l);
            }
            L("Self", "Χωρίς λογογράφο", 0, 0);
            L("Tier1", "Ένας μαθητής του Ισοκράτη", 3, 20);
            L("Tier2", "Δείναρχος", 6, 60);
            L("Tier3", "Υπερείδης (κατ' εξαίρεση)", 9, 150);
            return list;
        }

        // ---------------- Υποθέσεις: όλοι οι αριθμοί της καρτέλας «Υποθέσεις» ----------------

        private static List<CaseData> MakeCases()
        {
            var list = new List<CaseData>();
            Metric M(string s) => s switch { "T" => Metric.Treasury, "Ep" => Metric.Food, "Et" => Metric.Readiness, "Em" => Metric.Trust, _ => Metric.DemosPower };
            List<MetricDelta> D(params (string m, int v)[] xs) => xs.Select(x => new MetricDelta { metric = M(x.m), amount = x.v }).ToList();
            List<MetricSensitivity> S(params (string m, int v)[] xs) => xs.Select(x => new MetricSensitivity { metric = M(x.m), perTen = x.v }).ToList();
            OfficialStance St(string who, Side side, int w, int T = 0) => new OfficialStance { official = Officials[who], side = side, weight = w, threshold = T };

            CaseData C(int id, int pr, string title, string ap, bool council, int baseS, bool emerg,
                       List<MetricSensitivity> sens, List<MetricDelta> effA, List<MetricDelta> effB,
                       string speakerA, string speakerB, params OfficialStance[] stances)
            {
                var c = Create<CaseData>($"{Root}/Cases/Case_{id:00}.asset");
                c.id = id; c.prytany = pr; c.title = title; c.athPolRef = ap; c.inCouncil = council;
                c.baseSupport = baseS; c.emergencyB = emerg; c.sensitivities = sens; c.effectsA = effA; c.effectsB = effB;
                c.officials = stances.ToList();
                c.speechA = new Speech { speaker = Officials[speakerA] };
                c.speechB = new Speech { speaker = Officials[speakerB] };
                list.Add(c); return c;
            }

            void Complaint(CaseData c, ComplaintProcedure proc, string target, bool guilty, int yes, int no)
                => c.complaint = new ComplaintOption { available = true, procedure = proc, target = Officials[target], guilty = guilty, votesFor = yes, votesAgainst = no };

            // Πρυτανεία 1
            C(1, 1, "Η εγγραφή στον δήμο", "42.1–2", false, 70, false, S(("Em", 3)), D(("Em", 2), ("ID", 2)), D(("Em", -2), ("ID", -2)),
              "Thrasyllos", "Archias", St("Thrasyllos", Side.A, 5), St("Archias", Side.B, 5), St("Kephalos", Side.None, 5, 50));
            C(2, 1, "Η τιμή του σιταριού", "51.3", false, 55, true, S(("Ep", 5)), D(("Ep", 12), ("T", -10), ("ID", 2)), D(("Ep", 8), ("Em", -3), ("ID", -2)),
              "Timokrates", "Demades", St("Timokrates", Side.A, 5), St("Demades", Side.B, 15), St("Aristonikos", Side.None, 10, 60));
            // Πρυτανεία 2
            var c3 = C(3, 2, "Η επιχειροτονία του Φωκίωνα", "43.4, 61.2", false, 45, true, S(("Et", 4)), D(("Em", 3), ("Et", -3), ("ID", 3)), D(("Et", 5), ("Em", -3), ("ID", -2)),
              "Hypereides", "Phokion", St("Hypereides", Side.A, 15), St("Phokion", Side.B, 15), St("Aristonikos", Side.None, 10, 60));
            Complaint(c3, ComplaintProcedure.Eisangelia, "Mnesitheos", true, 318, 183);
            C(4, 2, "Οι τιμές στον Αλέξανδρο", "45.4", true, 55, false, S(("Em", 3)), D(("Em", 2), ("ID", 3)), D(("T", 3), ("Em", -2), ("ID", -3)),
              "Kephisodoros", "Demades", St("Kephisodoros", Side.A, 10), St("Demades", Side.B, 15), St("Kephalos", Side.None, 5, 50));
            // Πρυτανεία 3
            C(5, 3, "Η ικετηρία", "43.6", false, 68, false, S(("Em", 2)), D(("Em", 2), ("ID", 2)), D(("Em", -1), ("ID", -2)),
              "Agnonides", "Eurymedon", St("Agnonides", Side.A, 10), St("Eurymedon", Side.B, 10), St("Pytheas", Side.None, 10, 50));
            C(6, 3, "Η δοκιμασία του Λυσικλή", "55.2–4", true, 55, false, S(("Em", 3), ("Et", -2)), D(("Em", 2), ("ID", 3)), D(("Et", 4), ("T", 3), ("Em", -2), ("ID", -3)),
              "Mnesitheos", "Pytheas", St("Mnesitheos", Side.A, 5), St("Pytheas", Side.B, 10), St("Lykourgos", Side.None, 10, 80));
            // Πρυτανεία 4
            C(7, 4, "Είκοσι νέες τριήρεις", "46.1", true, 48, true, S(("Et", -5)), D(("Et", 10), ("T", -8), ("ID", 2)), D(("Et", 12), ("T", -3), ("Em", -2), ("ID", -2)),
              "Lykourgos", "Euthydemos", St("Lykourgos", Side.A, 10), St("Euthydemos", Side.B, 10), St("Mnesitheos", Side.None, 5, 45));
            C(8, 4, "Τα μεταλλεία του Λαυρίου", "47.2–3", true, 55, false, S(("T", -4)), D(("T", 8), ("Em", 2), ("ID", 2)), D(("T", 12), ("Em", -3), ("ID", -2)),
              "Kephalos", "Archias", St("Kephalos", Side.A, 5), St("Archias", Side.B, 5), St("Demades", Side.None, 15, 60));
            // Πρυτανεία 5 — πόλεμος
            var c9 = C(9, 5, "Ποιος θα ηγηθεί;", "61.1, 44.4", false, 66, false, S(("Em", 2)), D(("Em", 4), ("T", 3), ("ID", 2)), D(("T", 5), ("Em", -4), ("ID", -2)),
              "Phokion", "Leosthenes", St("Phokion", Side.A, 15), St("Leosthenes", Side.B, 15), St("Hypereides", Side.None, 10, 55));
            Complaint(c9, ComplaintProcedure.Endeixis, "Leosthenes", false, 87, 414);
            var c10 = C(10, 5, "Εισφορά ή δάνειο;", "43.4, 47.2", false, 54, true, S(("Et", -4)), D(("Em", 3), ("ID", 3)), D(("Et", 4), ("Em", -3), ("ID", -3)),
              "Hypereides", "Demades", St("Hypereides", Side.A, 10), St("Demades", Side.B, 15), St("Euthydemos", Side.None, 10, 60));
            Complaint(c10, ComplaintProcedure.Phasis, "Demades", true, 301, 200);
            // Πρυτανεία 6
            var c11 = C(11, 6, "Εισαγγελία κατά του στρατηγού", "43.4, 59.2", false, 55, true, S(("Em", 2)), D(("Em", 4), ("ID", 3)), D(("T", 3), ("Em", -3), ("ID", -3)),
              "Phokion", "Agnonides", St("Phokion", Side.A, 15), St("Agnonides", Side.B, 10), St("Pytheas", Side.None, 5, 50));
            Complaint(c11, ComplaintProcedure.GrapheSykophantias, "Agnonides", true, 276, 225);
            var c12 = C(12, 6, "Ο μισθός της Εκκλησίας σε πόλεμο", "41.3, 62.2", false, 62, true, S(("T", 4), ("Em", 2)), D(("T", -8), ("Em", 2), ("ID", 4)), D(("T", 12), ("Em", -3), ("ID", -4)),
              "Hypereides", "Euthydemos", St("Hypereides", Side.A, 10), St("Euthydemos", Side.B, 10), St("Demades", Side.None, 15, 65));
            c12.abolishesPayIfB = true;
            // Πρυτανεία 7
            var c13 = C(13, 7, "Γραφή παρανόμων κατά του Στρατοκλή", "45.4, 59.2", false, 55, true, S(("Et", -3), ("Em", 2)), D(("Em", 4), ("T", 4), ("ID", 3)), D(("Et", 5), ("Em", -4), ("ID", -2)),
              "Lykourgos", "Stratokles", St("Lykourgos", Side.A, 10), St("Stratokles", Side.B, 10), St("Kephisodoros", Side.None, 5, 50));
            Complaint(c13, ComplaintProcedure.Endeixis, "Stratokles", false, 94, 407);
            C(14, 7, "Ο επιστάτης που αρνήθηκε", "44.1–2", false, 45, false, S(("Em", 2)), D(("Em", 2), ("Et", -3), ("ID", 3)), D(("Et", 3), ("Em", -2), ("ID", -2)),
              "Aristonikos", "Deinarchos", St("Aristonikos", Side.A, 5), St("Deinarchos", Side.B, 10), St("Demosthenes", Side.None, 15, 55));
            // Πρυτανεία 8
            var c15 = C(15, 8, "Οι όροι του Αντιπάτρου", "43.6, 44.4", false, 50, true, S(("Et", 3), ("Ep", -3)), D(("Em", 2), ("ID", 2)), D(("Et", 5), ("Em", -3), ("ID", -2)),
              "Hypereides", "Phokion", St("Hypereides", Side.A, 10), St("Phokion", Side.B, 15), St("Demades", Side.None, 15, 70));
            c15.decidesWarEnd = true;
            C(16, 8, "Το σιτάρι του Πειραιά", "51.3–4, 43.4", false, 55, false, S(("Ep", -3)), D(("Ep", 5), ("Em", 2), ("T", -4), ("ID", 2)), D(("T", 5), ("Ep", -3), ("Em", -2), ("ID", -2)),
              "Timokrates", "Archias", St("Timokrates", Side.A, 5), St("Archias", Side.B, 5), St("Lykourgos", Side.None, 10, 50));
            // Πρυτανεία 9
            var c17 = C(17, 9, "Οι εύθυνες του ταμία", "48.4–5, 54.2", false, 66, true, S(("Em", 2)), D(("Em", 3), ("T", -3), ("ID", 3)), D(("T", 5), ("Em", -3), ("ID", -2)),
              "Thrasyllos", "Demades", St("Thrasyllos", Side.A, 5), St("Lykourgos", Side.A, 10), St("Demades", Side.B, 15), St("Kephalos", Side.None, 5, 40));
            c17.auditFineIfA = true;
            var c18 = C(18, 9, "Ο Άρειος Πάγος ζητά πίσω τη φυλακή των νόμων", "25, 57.3–4", false, 66, true, S(("Em", 2)), D(("Em", 2), ("ID", 2)), D(("T", 2), ("Em", -2), ("ID", -2)),
              "Demosthenes", "Polyeuktos", St("Demosthenes", Side.A, 15), St("Polyeuktos", Side.B, 10), St("Eurymedon", Side.B, 5), St("Phokion", Side.None, 15, 75));
            c18.areopagusIfB = true; c18.lowTrustBaseOverride = 52;
            // Πρυτανεία 10
            var c19 = C(19, 10, "Δέκα συγγραφείς αυτοκράτορες", "29.2–3, 43.2", false, 48, true, S(("Et", -5), ("Em", 2)), D(("Et", 6), ("Em", 2), ("ID", 3)), D(("T", 4), ("Em", -3), ("ID", -4)),
              "Lykourgos", "Pytheas", St("Lykourgos", Side.A, 10), St("Agnonides", Side.A, 10), St("Pytheas", Side.B, 5), St("Stratokles", Side.B, 10), St("Demades", Side.None, 15, 70));
            c19.affectedByAreopagus = true;
            var c20 = C(20, 10, "Δύο χιλιάδες δραχμές", "7.3–4, 42.1, 43.1", false, 48, true, S(("Em", 2), ("Et", 2), ("Ep", 2)), D(("Em", 4), ("ID", 4)), D(("Et", 4), ("T", 4), ("Em", -4), ("ID", -5)),
              "Hypereides", "Demades", St("Hypereides", Side.A, 10), St("Demosthenes", Side.A, 15), St("Lykourgos", Side.A, 10),
              St("Demades", Side.B, 15), St("Polyeuktos", Side.B, 10), St("Euthydemos", Side.B, 10), St("Phokion", Side.None, 15, 85));
            c20.affectedByAreopagus = true; c20.lowAnyMetricBaseOverride = 40;

            // τα effects εδώ είναι ήδη κλιμακωμένα → GameConfig scales = 1 (προεπιλογή)
            foreach (var c in list) EditorUtility.SetDirty(c);
            return list;
        }

        private static T Create<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            return so;
        }
    }
}
#endif
