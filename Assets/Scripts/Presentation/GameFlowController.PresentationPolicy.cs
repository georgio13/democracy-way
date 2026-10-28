using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    public sealed partial class GameFlowController
    {
        TMP_Text cityMetricsHUD;

        void CleanPresentation()
        {
            foreach (var binding in targets.ToArray())
            {
                string key = binding.Key.ToLowerInvariant();
                // Το πορτοφόλι μένει ορατό στον χάρτη και στις οθόνες όπου ξοδεύονται χρήματα.
                bool keepFunds = key.StartsWith("map.", StringComparison.Ordinal)
                    || key.StartsWith("speech.", StringComparison.Ordinal);
                if (key.EndsWith(".banner")) { var banner = Get<GameObject>(binding.Key); if (banner != null) banner.SetActive(true); }
                if (key.EndsWith(".footer") || key.Contains(".eyebrow") || key.EndsWith(".source") ||
                    (!keepFunds && (key.EndsWith(".funds") || key.Contains("treasuries"))))
                {
                    var go = Get<GameObject>(binding.Key);
                    if (go != null) go.SetActive(false);
                }
            }
            roots.TryGetValue("map-stela", out var mapRoot);
            foreach (var root in roots.Values.Where(x => x != null).Distinct())
            {
                var timeline = root.transform.Find("Life timeline");
                foreach (var item in root.GetComponentsInChildren<Transform>(true))
                {
                    if (timeline != null && (item == timeline || item.IsChildOf(timeline))) continue;
                    string name = item.name.ToLowerInvariant();
                    if (name.StartsWith("banner ", StringComparison.Ordinal))
                    {
                        item.gameObject.SetActive(true);
                        continue;
                    }
                    bool decoration = name.Contains("eyebrow") || name.Contains("footer") || name.Contains("vertical");
                    if (decoration && item.GetComponentInChildren<UnityEngine.UI.Selectable>(true) == null)
                        item.gameObject.SetActive(false);
                }
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (timeline != null && text.transform.IsChildOf(timeline)) continue;
                    string name = text.name.ToLowerInvariant();
                    bool vertical = Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(text.transform.eulerAngles.z, 0)) - 90) < 1;
                    bool clutter = name.Contains("footer") || name.Contains("eyebrow") || name.Contains("vertical") || name == "note" || name == "kicker" || name == "calendar note" || name == "institution" ||
                        (root != mapRoot && name.Contains("treasur"));
                    if (vertical || clutter) text.gameObject.SetActive(false);
                }
            }
        }

        void RenderCityMetricsHUD()
        {
            if (cityMetricsHUD == null)
            {
                var panel = CivicUIFactory.Panel(roots["map-stela"].transform, "City indicators", new Color32(30, 56, 60, 244));
                CivicUIFactory.Box(panel.rectTransform, 690, 154, 720, 64);
                var font = Get<TMP_Text>("map.funds")?.font ?? Get<TMP_Text>("map.identity")?.font;
                cityMetricsHUD = CivicUIFactory.Text(panel.transform, "City indicators values", "", font, 24, new Color32(238, 227, 199, 255));
                CivicUIFactory.Fill(cityMetricsHUD.rectTransform, new Vector2(18, 7), new Vector2(18, 7));
                cityMetricsHUD.enableAutoSizing = true; cityMetricsHUD.fontSizeMin = 18; cityMetricsHUD.fontSizeMax = 24;
                cityMetricsHUD.alignment = TextAlignmentOptions.Center;
                targets["map.city-metrics"] = cityMetricsHUD;
            }
            // Οι 4 ορατοί δείκτες της πόλης — το ΙΔ δεν εμφανίζεται ποτέ (spec ΣΤ.6).
            cityMetricsHUD.text = "Ταμείο " + State.polis.treasury + "/100  ·  Επισιτισμός " + State.polis.food + "/100"
                + "\nΕτοιμότητα " + State.polis.readiness + "/100  ·  Εμπιστοσύνη " + State.polis.trust + "/100"
                + (State.polis.war ? "  ·  Πόλεμος" : "");
        }
    }
}

