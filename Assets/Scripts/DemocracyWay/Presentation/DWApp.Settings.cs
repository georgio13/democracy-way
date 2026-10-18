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
        private bool FullscreenDisplayState()
        {
#if UNITY_EDITOR
            return editorFullscreenPreview;
#else
            if (fullscreenRequestPending)
            {
                if (Screen.fullScreen == fullscreenRequested || Time.realtimeSinceStartup - fullscreenRequestStarted > 2f)
                    fullscreenRequestPending = false;
                else return fullscreenRequested;
            }
            return Screen.fullScreen;
#endif
        }

        private void SetupSettings()
        {
            Slider music = Get<Slider>("settings.music");
            if (music != null)
            {
                music.minValue = 0;
                music.maxValue = 1;
                music.SetValueWithoutNotify(audioSettings.MusicVolume);
                music.onValueChanged.AddListener(SetMusic);
            }

            Slider sfx = Get<Slider>("settings.sfx");
            if (sfx != null)
            {
                sfx.minValue = 0;
                sfx.maxValue = 1;
                sfx.SetValueWithoutNotify(audioSettings.SfxVolume);
                sfx.onValueChanged.AddListener(SetSfx);
            }

            Toggle fullscreen = Get<Toggle>("settings.fullscreen");
            if (fullscreen != null)
            {
                fullscreen.SetIsOnWithoutNotify(FullscreenDisplayState());
                if (!IsWeb)
                    fullscreen.onValueChanged.AddListener(SetFullscreen);
                DWFullscreenPointerDown pointer = fullscreen.GetComponent<DWFullscreenPointerDown>();
                if (pointer == null)
                    pointer = fullscreen.gameObject.AddComponent<DWFullscreenPointerDown>();
                pointer.app = this;
            }

            SetActive("settings.resolutionRow", !IsWeb);
            RefreshVolumeLabels();
        }

        private void RenderSettings()
        {
            SetText("settings.hint", "Η εικόνα προσαρμόζεται αυτόματα στο παράθυρο του browser. Δεν χρειάζεται ξεχωριστή ρύθμιση ανάλυσης.");
            RefreshVolumeLabels();
        }

        private void RefreshVolumeLabels()
        {
            SetText("settings.music.value", Mathf.RoundToInt(audioSettings.MusicVolume * 100f) + "%");
            SetText("settings.sfx.value", Mathf.RoundToInt(audioSettings.SfxVolume * 100f) + "%");
        }

        public void SetMusic(float value)
        {
            audioSettings.SetMusic(value);
            RefreshVolumeLabels();
        }

        public void SetSfx(float value)
        {
            audioSettings.SetSfx(value);
            RefreshVolumeLabels();
        }

        public void ToggleFullscreen()
        {
            SetFullscreen(!FullscreenDisplayState());
        }

        public void SetFullscreen(bool enabled)
        {
#if UNITY_EDITOR
            editorFullscreenPreview = enabled;
#else
            fullscreenRequested = enabled; fullscreenRequestPending = true;
            fullscreenRequestStarted = Time.realtimeSinceStartup;
            Screen.fullScreen = enabled;
#endif
        }

        private void SetupAudio()
        {
            AudioClip music = SceneManager.GetActiveScene().name == "Introduction" ? config.introductionMusic : SceneManager.GetActiveScene().name == "Gameplay" ? config.MusicForPrytany(State?.prytany ?? 1) : config.mainMenuMusic;
            audioSettings = new DWAudioSettings(Get<AudioSource>("app.music"), Get<AudioSource>("app.sfx"), music, Get<AudioClip>("app.click") ?? config.clickSound, Get<AudioClip>("app.hover") ?? config.hoverSound);
        }

        public void PlayClick()
        {
            audioSettings?.PlayClick();
        }

        public void PlayHover()
        {
            audioSettings?.PlayHover();
        }
    }
}
