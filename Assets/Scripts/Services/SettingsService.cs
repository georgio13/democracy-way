using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.Services
{
    /// <summary>
    /// Player preferences: fullscreen, resolution and the three volume
    /// channels. Loads from PlayerPrefs on Awake, applies everything on Start
    /// (so AudioService's Awake has run), saves + applies on every setter —
    /// the settings panel just calls the setters.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Settings Service")]
    [DisallowMultipleComponent]
    public class SettingsService : MonoBehaviour
    {
        // PlayerPrefs keys
        private const string KeyFullscreen = "demokratia.settings.fullscreen";
        private const string KeyResWidth = "demokratia.settings.resolutionWidth";
        private const string KeyResHeight = "demokratia.settings.resolutionHeight";
        private const string KeyVolMusic = "demokratia.settings.volumeMusic";
        private const string KeyVolSfx = "demokratia.settings.volumeSfx";
        private const string KeyVolVoice = "demokratia.settings.volumeVoice";

        public bool Fullscreen { get; private set; }
        public int ResolutionWidth { get; private set; }
        public int ResolutionHeight { get; private set; }
        public float VolumeMusic { get; private set; }
        public float VolumeSfx { get; private set; }
        public float VolumeVoice { get; private set; }

        public struct ResolutionOption
        {
            public int width;
            public int height;
            public override string ToString() => $"{width} × {height}";
        }

        void Awake()
        {
            var current = Screen.currentResolution;
            Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
            ResolutionWidth = PlayerPrefs.GetInt(KeyResWidth, current.width);
            ResolutionHeight = PlayerPrefs.GetInt(KeyResHeight, current.height);
            VolumeMusic = PlayerPrefs.GetFloat(KeyVolMusic, 0.7f);
            VolumeSfx = PlayerPrefs.GetFloat(KeyVolSfx, 0.9f);
            VolumeVoice = PlayerPrefs.GetFloat(KeyVolVoice, 1.0f);
        }

        void Start()
        {
            ApplyDisplay();
            ApplyVolumes();
        }

        /// <summary>Distinct resolutions the current display supports, largest last.</summary>
        public List<ResolutionOption> GetResolutionOptions()
        {
            var options = new List<ResolutionOption>();
            foreach (var res in Screen.resolutions)
            {
                var option = new ResolutionOption { width = res.width, height = res.height };
                if (!options.Contains(option))   // Screen.resolutions repeats per refresh rate
                    options.Add(option);
            }
            return options;
        }

        // ════════ Setters (the settings panel calls these) ════════

        public void SetFullscreen(bool value)
        {
            Fullscreen = value;
            ApplyDisplay();
            SaveToPrefs();
        }

        public void SetResolution(int width, int height)
        {
            ResolutionWidth = width;
            ResolutionHeight = height;
            ApplyDisplay();
            SaveToPrefs();
        }

        public void SetVolumeMusic(float value) { VolumeMusic = Mathf.Clamp01(value); ApplyVolumes(); SaveToPrefs(); }
        public void SetVolumeSfx(float value) { VolumeSfx = Mathf.Clamp01(value); ApplyVolumes(); SaveToPrefs(); }
        public void SetVolumeVoice(float value) { VolumeVoice = Mathf.Clamp01(value); ApplyVolumes(); SaveToPrefs(); }

        // ════════ Apply / persist ════════

        private void ApplyDisplay()
        {
            Screen.SetResolution(ResolutionWidth, ResolutionHeight,
                Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        private void ApplyVolumes()
        {
            var audio = ServicesRoot.Audio;
            if (audio != null)
                audio.ApplyVolumes(VolumeMusic, VolumeSfx, VolumeVoice);
        }

        private void SaveToPrefs()
        {
            PlayerPrefs.SetInt(KeyFullscreen, Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(KeyResWidth, ResolutionWidth);
            PlayerPrefs.SetInt(KeyResHeight, ResolutionHeight);
            PlayerPrefs.SetFloat(KeyVolMusic, VolumeMusic);
            PlayerPrefs.SetFloat(KeyVolSfx, VolumeSfx);
            PlayerPrefs.SetFloat(KeyVolVoice, VolumeVoice);
            PlayerPrefs.Save();
        }
    }
}
