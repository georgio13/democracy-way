using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace DemocracyWay.NewUI
{
    public sealed partial class GameFlowController
    {
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

        static readonly Color MetricCell = new Color32(22, 43, 47, 255);
        static readonly Color MetricPressing = new Color32(122, 42, 36, 255);

        /// <summary>Το HUD του χάρτη (αντικείμενο σκηνής): οι 4 μετρικές του παίκτη και οι 5 της πόλης, όλες ορατές.
        /// Δείκτης της πόλης κάτω από το όριο της πίεσης βάφεται κόκκινος και γράφει «Πιέζει τις ψηφοφορίες».</summary>
        void RenderMetricsHUD()
        {
            EnsureAgoraReady();
            var cfg = AgoraCfg;
            var p = State.citizen;
            var c = State.polis;
            int income = p.income, pay = c.payAbolished ? 0 : cfg.assemblyPayPerPrytany;
            Metric("peitho", HudText.LabelPeitho, p.peitho.ToString(), false,
                string.Format(JsonContent.Copy(HudText.HintPeitho), cfg.speechPeithoDivisor, cfg.peithoPerSpeech, cfg.speechPeithoMax));
            Metric("fame", HudText.LabelFame, p.fame.ToString(), false,
                string.Format(JsonContent.Copy(HudText.HintFame), cfg.fameSpeech, cfg.fameWin, cfg.fameConviction, Signed(cfg.fameSecondSilence), Signed(cfg.fameAcquittal), cfg.bribeExposureFameLoss));
            Metric("ethos", HudText.LabelEthos, p.ethos.ToString(), false,
                string.Format(JsonContent.Copy(HudText.HintEthos), cfg.ethosConviction, cfg.bribeEthosLoss, Signed(cfg.ethosVoteAgainstSpeech)));
            Metric("drachmas", HudText.LabelDrachmas, p.wallet + " δρ.", false,
                string.Format(JsonContent.Copy(HudText.HintDrachmas), income, pay, cfg.logographerCost, cfg.bribeCost, cfg.accuserFine));
            Metric("treasury", HudText.LabelTreasury, c.treasury.ToString(), c.treasury < cfg.pressureBelow,
                string.Format(JsonContent.Copy(HudText.HintTreasury), Signed(cfg.peaceTreasury), Signed(cfg.warTreasury)));
            Metric("food", HudText.LabelFood, c.food.ToString(), c.food < cfg.pressureBelow,
                string.Format(JsonContent.Copy(HudText.HintFood), Signed(cfg.peaceFood), Signed(cfg.warFood), cfg.famineFood));
            Metric("readiness", HudText.LabelReadiness, c.readiness.ToString(), c.readiness < cfg.pressureBelow,
                string.Format(JsonContent.Copy(HudText.HintReadiness), Signed(cfg.peaceReadiness), Signed(cfg.warReadiness), cfg.famineReadiness));
            Metric("trust", HudText.LabelTrust, c.trust.ToString(), c.trust < cfg.pressureBelow,
                string.Format(JsonContent.Copy(HudText.HintTrust), cfg.bribeExposureTrustLoss, cfg.stasisTrust, cfg.demosPowerMajority));
            // Η Ισχύς του Δήμου δεν είναι δείκτης πίεσης: δεν βάφεται ποτέ κόκκινη.
            Metric("demos", HudText.LabelDemos, c.demosPower.ToString(), false,
                string.Format(JsonContent.Copy(HudText.HintDemos), cfg.coupDemosPower, cfg.demosPowerMajority, cfg.enduresMinAll));
        }

        void Metric(string id, string labelId, string value, bool pressing, string hint)
        {
            string key = "map.metric." + id;
            SetText(key + ".label", JsonContent.Copy(labelId));
            SetText(key + ".value", value);
            var cell = Get<PanelGraphic>(key);
            if (cell != null) cell.color = pressing ? MetricPressing : MetricCell;
            var note = Get<TMP_Text>(key + ".note");
            if (note != null)
            {
                note.gameObject.SetActive(pressing);
                note.text = JsonContent.Copy(HudText.PressNote);
            }
            var tooltip = cell != null ? cell.GetComponent<MetricHint>() : null;
            if (tooltip != null)
                tooltip.text = pressing
                    ? hint + "\n\n" + string.Format(JsonContent.Copy(HudText.HintPressure), AgoraCfg.pressureBelow, AgoraCfg.pressurePoints)
                    : hint;
        }
    }
}

