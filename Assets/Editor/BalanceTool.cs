using System.IO;
using System.Linq;
using System.Text;
using Agora;
using DemocracyWay.NewUI;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Agora_FINAL_DESIGN ενότητα 8, βήμα 8: 5.000 παρτίδες ανά στρατηγική με τις συναρτήσεις του παιχνιδιού,
/// σύγκριση με τους πίνακες της ενότητας 4 (±8) και αναφορά στο Docs/Balance/balance_report.md.
/// Τρέχει και headless: -executeMethod BalanceTool.Run
/// </summary>
public static class BalanceTool
{
    const int Games = 5000;
    const int Seed = 1000;
    const string ReportPath = "Docs/Balance/balance_report.md";

    static readonly (BalanceSimulator.Strategy strategy, string name)[] Names =
    {
        (BalanceSimulator.Strategy.Silent, "σιωπηλός"),
        (BalanceSimulator.Strategy.DemocratSpeaker, "δημοκράτης ρήτορας"),
        (BalanceSimulator.Strategy.OligarchSpeaker, "ολιγαρχικός ρήτορας"),
        (BalanceSimulator.Strategy.DemocratBriber, "δημοκράτης δωροδόκος"),
        (BalanceSimulator.Strategy.OligarchBriber, "ολιγαρχικός δωροδόκος"),
        (BalanceSimulator.Strategy.Random, "τυχαίος"),
        (BalanceSimulator.Strategy.HalfSpeakerA, "μισός ρήτορας (Α)"),
    };

    static string Name(BalanceSimulator.Strategy s) => Names.First(n => n.strategy == s).name;

    [MenuItem("Agora/Ισορροπία (5.000 παρτίδες)")]
    public static void Run()
    {
        var content = AssetDatabase.LoadAssetAtPath<GameContent>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:GameContent").First()));
        var summaries = BalanceSimulator.All.Select((s, i) =>
        {
            EditorUtility.DisplayProgressBar("Ισορροπία", Name(s), (float)i / BalanceSimulator.All.Length);
            return BalanceSimulator.Run(content, s, Games, Seed + i);
        }).ToList();
        EditorUtility.ClearProgressBar();
        var checks = BalanceTargets.Compare(summaries);
        string report = Report(content.config, summaries, checks);
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report, new UTF8Encoding(false));
        int failed = checks.Count(c => !c.Ok);
        Debug.Log($"[Balance] {checks.Count - failed}/{checks.Count} έλεγχοι μέσα στο ±{BalanceTargets.Tolerance} · αναφορά: {ReportPath}");
    }

    static string Cell(double target, double actual) =>
        $"{actual:0} ({target:0}) {(System.Math.Abs(actual - target) <= BalanceTargets.Tolerance ? "✔" : "✘")}";

    static string Report(GameConfig cfg, System.Collections.Generic.List<BalanceSimulator.Summary> summaries, System.Collections.Generic.List<BalanceTargets.Check> checks)
    {
        var b = new StringBuilder();
        int failed = checks.Count(c => !c.Ok);
        b.AppendLine("# Ισορροπία — σύγκριση με την ενότητα 4");
        b.AppendLine();
        b.AppendLine($"{Games} παρτίδες ανά στρατηγική, ζευγίτης, seed {Seed}. Παράγεται από το μενού «Agora > Ισορροπία» με τις συναρτήσεις του παιχνιδιού.");
        b.AppendLine($"Κάθε κελί: **αποτέλεσμα (στόχος)**, ✔ αν απέχει έως ±{BalanceTargets.Tolerance}. Σύνολο: **{checks.Count - failed}/{checks.Count}** μέσα στην ανοχή.");
        b.AppendLine();
        b.AppendLine($"Παράμετροι του παιχνιδιού: διάθεση ±{cfg.moodRange}, ανώτατη Πειθώ {cfg.speechPeithoMax}, δυσκολίες {cfg.difficultyEasy}/{cfg.difficultyMedium}/{cfg.difficultyHard}. "
            + "Ο πίνακας-στόχος βγήκε από το final_sim.py με διάθεση ±6, ανώτατη Πειθώ 80 και δυσκολίες 30/40/50.");
        b.AppendLine();
        b.AppendLine("## Τέλη πόλης ανά στρατηγική (%)");
        b.AppendLine();
        b.AppendLine("| Στρατηγική | Η πόλη άντεξε | Φρουρά | Όριο περιουσίας | Στάσις | Λιμός | Ψηφοφορίες που κρίνει |");
        b.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var s in summaries)
        {
            var t = BalanceTargets.City[s.strategy];
            b.AppendLine($"| {Name(s.strategy)} | {Cell(t.endures, s.CityPercent(CityEnding.Counterfactual))} | {Cell(t.garrison, s.CityPercent(CityEnding.GarrisonOnly))} | "
                + $"{Cell(t.limit, s.CityPercent(CityEnding.PropertyLimit))} | {Cell(t.stasis, s.CityPercent(CityEnding.Stasis))} | {Cell(t.famine, s.CityPercent(CityEnding.Famine))} | "
                + $"{s.decided:0.0} ({t.decided:0.0}) |");
        }
        b.AppendLine();
        b.AppendLine("## Τέλη παίκτη ανά στρατηγική (%)");
        b.AppendLine();
        var personal = (PersonalEnding[])System.Enum.GetValues(typeof(PersonalEnding));
        b.AppendLine("| Στρατηγική | " + string.Join(" | ", personal) + " | Πειθώ | Φήμη | Ηθική |");
        b.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---|", personal.Length + 3)));
        foreach (var s in summaries)
            b.AppendLine($"| {Name(s.strategy)} | " + string.Join(" | ", personal.Select(e => s.PersonalPercent(e).ToString("0"))) + $" | {s.peitho:0} | {s.fame:0} | {s.ethos:0} |");
        b.AppendLine();
        b.AppendLine("## % Α ανά υπόθεση");
        b.AppendLine();
        b.AppendLine("| Υπ. | Σιωπηλός | Δημοκράτης ρήτορας |");
        b.AppendLine("|---|---|---|");
        var silent = summaries.First(s => s.strategy == BalanceSimulator.Strategy.Silent);
        var democrat = summaries.First(s => s.strategy == BalanceSimulator.Strategy.DemocratSpeaker);
        for (int i = 0; i < 20; i++)
            b.AppendLine($"| {i + 1} | {Cell(BalanceTargets.SilentA[i], silent.CasePercentA(i))} | {Cell(BalanceTargets.DemocratA[i], democrat.CasePercentA(i))} |");
        return b.ToString();
    }
}
