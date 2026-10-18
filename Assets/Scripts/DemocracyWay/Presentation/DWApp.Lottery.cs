using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DemocracyWay.NewUI
{
    public sealed partial class DWApp
    {
        public void NextPrytany()
        {
            if (modalOpen || State == null || State.phase != "prytany-title")
                return;
            State.phase = "lottery";
            SaveCheckpoint();
            Show("lottery");
        }

        public void DrawNext()
        {
            if (modalOpen || State == null || State.phase != "lottery" || State.currentTribe >= 0 || drawingLottery)
                return;
            List<int> available = DWGameRules.AvailableTribes(State, Mathf.Min(10, config.tribes.Count));
            if (available.Count == 0)
            {
                SetStatus("Όλες οι φυλές έχουν ήδη κληρωθεί.");
                return;
            }

            StartCoroutine(AnimateLottery(available));
        }

        private IEnumerator AnimateLottery(List<int> available)
        {
            drawingLottery = true;
            SetInteractable("lottery.primary", false);
            SetText("lottery.result", "Οι κλήροι αλλάζουν θέση…");
            for (int step = 0; step < 18; step++)
            {
                int index = available[step % available.Count];
                for (int i = 0; i < config.tribes.Count; i++)
                    SetActive("lottery.tribe." + i + ".selected", i == index);
                float elapsed = 0f;
                while (elapsed < .055f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            DWGameRules.DrawTribe(State, config.tribes.Count, UnityEngine.Random.Range(0, available.Count));
            drawingLottery = false;
            SaveCheckpoint();
            RenderLottery();
        }

        public void BeginPrytany()
        {
            if (modalOpen || drawingLottery || State == null || State.phase != "lottery" || State.currentTribe < 0)
                return;
            State.phase = Campaign.initialComplete ? "map-stela" : "initial-knowledge";
            if (!SaveCheckpoint())
                return;
            Show(State.phase);
        }
    }
}
