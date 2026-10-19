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
                bool mapMoney = key.StartsWith("map.", StringComparison.Ordinal);
                if (key.EndsWith(".footer") || key.Contains(".eyebrow") || key.EndsWith(".source") || key.EndsWith(".banner") ||
                    (!mapMoney && (key.EndsWith(".funds") || key.Contains("treasuries"))))
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
                    bool decoration = name.StartsWith("banner ", StringComparison.Ordinal) || name.Contains("eyebrow") || name.Contains("footer") || name.Contains("vertical");
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
            if (currentScreen == "knowledge")
            {
                var heading = Get<TMP_Text>("knowledge.heading");
                if (heading != null) { heading.enableAutoSizing = true; heading.fontSizeMin = 28; heading.fontSizeMax = 36; }
            }
        }

        void RenderCityMetricsHUD()
        {
            if (cityMetricsHUD == null)
            {
                var panel = CivicUIFactory.Panel(roots["map-stela"].transform, "City indicators", new Color32(30, 56, 60, 244));
                CivicUIFactory.Box(panel.rectTransform, 550, 154, 1000, 64);
                var font = Get<TMP_Text>("map.funds")?.font ?? Get<TMP_Text>("map.identity")?.font;
                cityMetricsHUD = CivicUIFactory.Text(panel.transform, "City indicators values", "", font, 24, new Color32(238, 227, 199, 255));
                CivicUIFactory.Fill(cityMetricsHUD.rectTransform, new Vector2(18, 7), new Vector2(18, 7));
                cityMetricsHUD.enableAutoSizing = true; cityMetricsHUD.fontSizeMin = 18; cityMetricsHUD.fontSizeMax = 24;
                cityMetricsHUD.alignment = TextAlignmentOptions.Center;
                targets["map.city-metrics"] = cityMetricsHUD;
            }
            cityMetricsHUD.text = "Επισιτισμός " + Campaign.foodSecurity + "/100  ·  Ετοιμότητα " + Signed(Campaign.readiness)
                + "\nΘεσμική εμπιστοσύνη " + Signed(Campaign.legitimacy) + "  ·  Πολεμική εντολή " + Signed(Campaign.war);
        }
    }
}
