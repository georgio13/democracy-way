using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DemocracyWay.Menu
{
    using DemocracyWay.Framework;

    /// <summary>
    /// Settings panel UI. Used identically by MainMenu and PauseMenu via prefab reuse.
    /// Reads current values from <see cref="SettingsManager"/> on Open, pushes changes
    /// back through its setters (which auto-save to PlayerPrefs).
    /// </summary>
    [AddComponentMenu("DemocracyWay/Settings Panel")]
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private CanvasGroup rootGroup;

        [Header("Controls")]
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider dialogueVolumeSlider;
        [SerializeField] private MenuButton backButton;

        [Header("Labels (optional)")]
        [SerializeField] private TMP_Text musicVolumeLabel;
        [SerializeField] private TMP_Text sfxVolumeLabel;
        [SerializeField] private TMP_Text dialogueVolumeLabel;

        private Action onCloseCallback;
        private List<SettingsManager.ResolutionOption> cachedResolutions;
        private bool isPopulating;

        void Awake()
        {
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            if (dialogueVolumeSlider != null) dialogueVolumeSlider.onValueChanged.AddListener(OnDialogueVolumeChanged);
            if (backButton != null) backButton.onClick.AddListener(Close);
        }

        public void Open(Action onClose = null)
        {
            onCloseCallback = onClose;
            gameObject.SetActive(true);
            if (rootGroup != null)
            {
                rootGroup.alpha = 1f;
                rootGroup.blocksRaycasts = true;
                rootGroup.interactable = true;
            }
            PopulateFromSettings();
        }

        public void Close()
        {
            if (rootGroup != null)
            {
                rootGroup.alpha = 0f;
                rootGroup.blocksRaycasts = false;
                rootGroup.interactable = false;
            }
            onCloseCallback?.Invoke();
            onCloseCallback = null;
        }

        private void PopulateFromSettings()
        {
            var sm = SettingsManager.Instance;
            if (sm == null) return;
            isPopulating = true;

            // Fullscreen
            if (fullscreenToggle != null) fullscreenToggle.isOn = sm.Fullscreen;

            // Resolutions
            if (resolutionDropdown != null)
            {
                cachedResolutions = sm.GetAvailableResolutions();
                resolutionDropdown.ClearOptions();
                var optionLabels = new List<string>(cachedResolutions.Count);
                foreach (var r in cachedResolutions) optionLabels.Add(r.ToString());
                resolutionDropdown.AddOptions(optionLabels);
                resolutionDropdown.value = sm.FindCurrentResolutionIndex(cachedResolutions);
                resolutionDropdown.RefreshShownValue();
            }

            // Volumes (sliders represent 0..1 linear)
            if (musicVolumeSlider != null) musicVolumeSlider.value = sm.VolumeMusic;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = sm.VolumeSfx;
            if (dialogueVolumeSlider != null) dialogueVolumeSlider.value = sm.VolumeDialogue;

            UpdateVolumeLabels();
            isPopulating = false;
        }

        private void UpdateVolumeLabels()
        {
            if (musicVolumeLabel != null && musicVolumeSlider != null)
                musicVolumeLabel.text = $"{Mathf.RoundToInt(musicVolumeSlider.value * 100)}%";
            if (sfxVolumeLabel != null && sfxVolumeSlider != null)
                sfxVolumeLabel.text = $"{Mathf.RoundToInt(sfxVolumeSlider.value * 100)}%";
            if (dialogueVolumeLabel != null && dialogueVolumeSlider != null)
                dialogueVolumeLabel.text = $"{Mathf.RoundToInt(dialogueVolumeSlider.value * 100)}%";
        }

        // ════════════ Change handlers ════════════

        private void OnFullscreenChanged(bool value)
        {
            if (isPopulating) return;
            SettingsManager.Instance?.SetFullscreen(value);
        }

        private void OnResolutionChanged(int index)
        {
            if (isPopulating || cachedResolutions == null) return;
            if (index < 0 || index >= cachedResolutions.Count) return;
            var r = cachedResolutions[index];
            SettingsManager.Instance?.SetResolution(r.width, r.height);
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (isPopulating) return;
            SettingsManager.Instance?.SetMusicVolume(value);
            UpdateVolumeLabels();
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (isPopulating) return;
            SettingsManager.Instance?.SetSfxVolume(value);
            UpdateVolumeLabels();
        }

        private void OnDialogueVolumeChanged(float value)
        {
            if (isPopulating) return;
            SettingsManager.Instance?.SetDialogueVolume(value);
            UpdateVolumeLabels();
        }
    }
}
