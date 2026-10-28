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
    public sealed partial class GameFlowController
    {
        public void NextPrytany()
        {
            if (modalOpen || State == null || State.phase != "prytany-title")
                return;
            State.phase = "lottery";
            SaveCheckpoint();
            Show(State.phase);
        }

        public void DrawNext()
        {
            if (modalOpen || State == null || State.phase != "lottery" || State.currentTribe >= 0 || drawingLottery)
                return;
            List<int> available = GameRules.AvailableTribes(State, Mathf.Min(10, config.tribes.Count));
            if (available.Count == 0)
            {
                SetStatus(JsonContent.Copy("Copy_5a94a27ea56fd278"));
                return;
            }

            StartCoroutine(AnimateLottery(available));
        }

        private IEnumerator AnimateLottery(List<int> available)
        {
            drawingLottery = true;
            SetInteractable("lottery.primary", false);
            SetText("lottery.result", JsonContent.Copy("Copy_77a67d0477966069"));
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

            // Η σειρά πρυτανειών είναι σταθερή (GameContent.prytanyTribes) — η κλήρωση είναι σκηνικό.
            int fixedTribe = Mathf.Clamp(State.prytany - 1, 0, config.tribes.Count - 1);
            State.currentTribe = fixedTribe;
            if (!State.drawnTribes.Contains(fixedTribe)) State.drawnTribes.Add(fixedTribe);
            drawingLottery = false;
            SaveCheckpoint();
            RenderLottery();
        }

        public void BeginPrytany()
        {
            if (modalOpen || drawingLottery || State == null || State.phase != "lottery" || State.currentTribe < 0)
                return;
            State.phase = "map-stela";
            if (!SaveCheckpoint())
                return;
            Show(State.phase);
        }
    }
}
