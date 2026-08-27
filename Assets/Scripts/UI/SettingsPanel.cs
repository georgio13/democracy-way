using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DemocracyWay.Services;

namespace DemocracyWay.UI
{
    /// <summary>
    /// The settings panel used verbatim in BOTH the main menu and the pause
    /// menu — it binds only to ServicesRoot.Settings, so it needs no knowledge
    /// of who opened it. Every change applies immediately through the service
    /// setters (no Apply button): players expect volume sliders to be audible
    /// as they drag. Open() re-reads everything from the service, because
    /// another screen (or another monitor being plugged in) may have changed
    /// values since the panel was last shown. The GameObject is saved inactive.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Settings Panel")]
    [DisallowMultipleComponent]
    public class SettingsPanel : MonoBehaviour
    {
        [Tooltip("Toggle «Πλήρης Οθόνη».")]
        [SerializeField] private Toggle fullscreenToggle;

        [Tooltip("Dropdown «Ανάλυση» — γεμίζει από Settings.GetResolutionOptions() σε κάθε Open().")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;

        [Tooltip("Slider «Ένταση Μουσικής», εύρος 0–1.")]
        [SerializeField] private Slider musicSlider;

        [Tooltip("Slider «Ένταση Ηχητικών Εφέ», εύρος 0–1.")]
        [SerializeField] private Slider sfxSlider;

        [Tooltip("Slider «Ένταση Ομιλίας», εύρος 0–1.")]
        [SerializeField] private Slider voiceSlider;

        [Tooltip("Κουμπί «Πίσω» — κλείνει το panel και ειδοποιεί όποιον το άνοιξε.")]
        [SerializeField] private UiButton backButton;

        /// <summary>Set per Open() — how the caller restores its own column.</summary>
        private Action onBack;

        /// <summary>Dropdown index → resolution, rebuilt on every Open().</summary>
        private List<SettingsService.ResolutionOption> resolutionOptions = new List<SettingsService.ResolutionOption>();

        void Awake()
        {
            // Wired once; RefreshFromService uses the *WithoutNotify setters so
            // these callbacks only fire on genuine player input.
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(HandleFullscreenChanged);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(HandleResolutionChanged);
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(HandleMusicChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            if (voiceSlider != null) voiceSlider.onValueChanged.AddListener(HandleVoiceChanged);
            if (backButton != null) backButton.onClick.AddListener(HandleBack);
        }

        /// <summary>
        /// Shows the panel with fresh values from the service.
        /// <paramref name="onBack"/> runs after Πίσω hides the panel.
        /// </summary>
        public void Open(Action onBack = null)
        {
            this.onBack = onBack;
            gameObject.SetActive(true);   // before refresh, so Awake has wired the listeners
            RefreshFromService();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void RefreshFromService()
        {
            var settings = ServicesRoot.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[SettingsPanel] ServicesRoot.Settings είναι null — άνοιξε το παιχνίδι από το Boot.", this);
                return;
            }

            if (fullscreenToggle != null)
                fullscreenToggle.SetIsOnWithoutNotify(settings.Fullscreen);

            if (resolutionDropdown != null)
            {
                resolutionOptions = settings.GetResolutionOptions();
                resolutionDropdown.ClearOptions();
                var labels = new List<string>(resolutionOptions.Count);
                foreach (var option in resolutionOptions)
                    labels.Add(option.ToString());
                resolutionDropdown.AddOptions(labels);

                int currentIndex = resolutionOptions.FindIndex(o =>
                    o.width == settings.ResolutionWidth && o.height == settings.ResolutionHeight);
                // The stored resolution can be absent from the current display
                // (monitor swap) — fall back to the largest supported one.
                if (currentIndex < 0) currentIndex = resolutionOptions.Count - 1;
                if (currentIndex >= 0) resolutionDropdown.SetValueWithoutNotify(currentIndex);
                resolutionDropdown.RefreshShownValue();
            }

            if (musicSlider != null) musicSlider.SetValueWithoutNotify(settings.VolumeMusic);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(settings.VolumeSfx);
            if (voiceSlider != null) voiceSlider.SetValueWithoutNotify(settings.VolumeVoice);
        }

        // ════════ Player input → service setters (apply + persist instantly) ════════

        private void HandleFullscreenChanged(bool value) => ServicesRoot.Settings?.SetFullscreen(value);

        private void HandleResolutionChanged(int index)
        {
            if (index < 0 || index >= resolutionOptions.Count) return;
            var option = resolutionOptions[index];
            ServicesRoot.Settings?.SetResolution(option.width, option.height);
        }

        private void HandleMusicChanged(float value) => ServicesRoot.Settings?.SetVolumeMusic(value);
        private void HandleSfxChanged(float value) => ServicesRoot.Settings?.SetVolumeSfx(value);
        private void HandleVoiceChanged(float value) => ServicesRoot.Settings?.SetVolumeVoice(value);

        private void HandleBack()
        {
            Hide();
            var callback = onBack;
            onBack = null;
            callback?.Invoke();
        }
    }
}
