using System.Collections.Generic;
using UnityEngine;

namespace DemocracyWay.Framework
{
    /// <summary>
    /// Persistent singleton. Owns player preferences for fullscreen, resolution, and
    /// the three volume channels. Applies them on startup and every time the settings
    /// panel writes to them. No dependency on AudioMixer — volumes are pushed directly
    /// to the AudioManager's three AudioSources.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Settings Manager")]
    [DisallowMultipleComponent]
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        // PlayerPrefs keys
        public const string KEY_FULLSCREEN       = "demokratia.settings.fullscreen";
        public const string KEY_RES_WIDTH        = "demokratia.settings.resolutionWidth";
        public const string KEY_RES_HEIGHT       = "demokratia.settings.resolutionHeight";
        public const string KEY_VOL_MUSIC        = "demokratia.settings.volumeMusic";
        public const string KEY_VOL_SFX          = "demokratia.settings.volumeSfx";
        public const string KEY_VOL_DIALOGUE     = "demokratia.settings.volumeDialogue";

        // Defaults
        private const float DEFAULT_VOL_MUSIC    = 0.7f;
        private const float DEFAULT_VOL_SFX      = 0.9f;
        private const float DEFAULT_VOL_DIALOGUE = 1.0f;

        // Cached state
        public bool Fullscreen { get; private set; }
        public int ResolutionWidth { get; private set; }
        public int ResolutionHeight { get; private set; }
        public float VolumeMusic { get; private set; }
        public float VolumeSfx { get; private set; }
        public float VolumeDialogue { get; private set; }

        public struct ResolutionOption
        {
            public int width;
            public int height;
            public override string ToString() => $"{width} × {height}";
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent != null) transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);

            LoadFromPrefs();
        }

        void Start()
        {
            ApplyAllSettings();
        }

        // ════════════ Load / Save ════════════

        private void LoadFromPrefs()
        {
            Fullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, 1) == 1;

            var current = Screen.currentResolution;
            ResolutionWidth = PlayerPrefs.GetInt(KEY_RES_WIDTH, current.width);
            ResolutionHeight = PlayerPrefs.GetInt(KEY_RES_HEIGHT, current.height);

            VolumeMusic = PlayerPrefs.GetFloat(KEY_VOL_MUSIC, DEFAULT_VOL_MUSIC);
            VolumeSfx = PlayerPrefs.GetFloat(KEY_VOL_SFX, DEFAULT_VOL_SFX);
            VolumeDialogue = PlayerPrefs.GetFloat(KEY_VOL_DIALOGUE, DEFAULT_VOL_DIALOGUE);
        }

        private void SaveToPrefs()
        {
            PlayerPrefs.SetInt(KEY_FULLSCREEN, Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(KEY_RES_WIDTH, ResolutionWidth);
            PlayerPrefs.SetInt(KEY_RES_HEIGHT, ResolutionHeight);
            PlayerPrefs.SetFloat(KEY_VOL_MUSIC, VolumeMusic);
            PlayerPrefs.SetFloat(KEY_VOL_SFX, VolumeSfx);
            PlayerPrefs.SetFloat(KEY_VOL_DIALOGUE, VolumeDialogue);
            PlayerPrefs.Save();
        }

        // ════════════ Apply ════════════

        public void ApplyAllSettings()
        {
            ApplyDisplay();
            ApplyVolumes();
        }

        public void ApplyDisplay()
        {
            Screen.SetResolution(
                ResolutionWidth,
                ResolutionHeight,
                Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        public void ApplyVolumes()
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            audio.SetMusicVolume(VolumeMusic);
            audio.SetSfxVolume(VolumeSfx);
            audio.SetDialogueVolume(VolumeDialogue);
        }

        // ════════════ Setters (called by SettingsPanel) ════════════

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

        public void SetMusicVolume(float v)
        {
            VolumeMusic = Mathf.Clamp01(v);
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicVolume(VolumeMusic);
            SaveToPrefs();
        }

        public void SetSfxVolume(float v)
        {
            VolumeSfx = Mathf.Clamp01(v);
            if (AudioManager.Instance != null) AudioManager.Instance.SetSfxVolume(VolumeSfx);
            SaveToPrefs();
        }

        public void SetDialogueVolume(float v)
        {
            VolumeDialogue = Mathf.Clamp01(v);
            if (AudioManager.Instance != null) AudioManager.Instance.SetDialogueVolume(VolumeDialogue);
            SaveToPrefs();
        }

        // ════════════ Available resolutions ════════════

        /// <summary>
        /// Returns the deduped list of display resolutions available on this machine,
        /// sorted by (width, height) ascending. Refresh rate variants are collapsed.
        /// </summary>
        public List<ResolutionOption> GetAvailableResolutions()
        {
            var seen = new HashSet<(int w, int h)>();
            var list = new List<ResolutionOption>();
            foreach (var r in Screen.resolutions)
            {
                var key = (r.width, r.height);
                if (seen.Add(key))
                    list.Add(new ResolutionOption { width = r.width, height = r.height });
            }
            list.Sort((a, b) =>
            {
                int c = a.width.CompareTo(b.width);
                return c != 0 ? c : a.height.CompareTo(b.height);
            });
            return list;
        }

        public int FindCurrentResolutionIndex(List<ResolutionOption> options)
        {
            for (int i = 0; i < options.Count; i++)
                if (options[i].width == ResolutionWidth && options[i].height == ResolutionHeight)
                    return i;
            return Mathf.Max(0, options.Count - 1);
        }
    }
}
