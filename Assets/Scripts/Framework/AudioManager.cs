using System.Collections;
using UnityEngine;

namespace DemocracyWay.Framework
{
    /// <summary>
    /// Persistent singleton (created in Bootstrap scene) that owns three AudioSources:
    /// Music (looped ambient), SFX (one-shots), Dialogue (voice-over). Volumes are driven
    /// by <see cref="SettingsManager"/> via PlayerPrefs. Button hover/click clips are
    /// wired from serialized fields so menus can fire them without knowing asset paths.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Audio Manager")]
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Sources (auto-created if null)")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource dialogueSource;
        [SerializeField] private AudioSource ambientSourceA;
        [SerializeField] private AudioSource ambientSourceB;

        [Header("Shared UI SFX")]
        [Tooltip("Clip played on MenuButton hover. Wire to A_ButtonHover.")]
        [SerializeField] private AudioClip buttonHoverClip;

        [Tooltip("Clip played on MenuButton click. Wire to A_ButtonClick.")]
        [SerializeField] private AudioClip buttonClickClip;

        [Header("Defaults")]
        [Tooltip("Multiplier applied to music volume while the pause menu is open.")]
        [Range(0f, 1f)]
        [SerializeField] private float pauseDuckFactor = 0.4f;

        [Tooltip("Music is 'underscore', not soundtrack — scaled down so ambient + dialogue stay dominant.")]
        [Range(0f, 1f)]
        [SerializeField] private float musicUnderscoreMultiplier = 0.35f;

        [Tooltip("Ceiling for ambient loops — ambient is the bed, kept below dialogue clarity.")]
        [Range(0f, 1f)]
        [SerializeField] private float ambientMaxMultiplier = 0.75f;

        // Base volumes (from SettingsManager, linear 0..1)
        private float baseMusicVolume = 0.7f;
        private float baseSfxVolume = 0.9f;
        private float baseDialogueVolume = 1.0f;
        private float baseAmbientVolume = 0.7f;
        private bool isDucking;

        // Ambient crossfade state
        private bool ambientUseA = true;   // which source is currently "active"
        private Coroutine ambientFadeCo;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // DontDestroyOnLoad only works on root GameObjects. Unparent first so the
            // entire subtree (AudioManager + its three child AudioSources) migrates
            // cleanly to the DDoL scene as one unit. Create sources BEFORE DDoL so
            // they belong to the same hierarchy snapshot that gets moved.
            if (transform.parent != null) transform.SetParent(null, false);
            EnsureSources();
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            // SettingsManager pushes volumes via ApplyVolumes() after it loads prefs.
            // This line handles the case where AudioManager is ready before SettingsManager.
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.ApplyAllSettings();
        }

        /// <summary>
        /// Creates any missing AudioSource child. Uses Unity's fake-null check
        /// (<c>== null</c>) so both truly-unassigned and destroyed references are
        /// re-created transparently. Safe to call every frame.
        /// </summary>
        private void EnsureSources()
        {
            if (musicSource == null)
            {
                var go = new GameObject("MusicSource");
                go.transform.SetParent(transform, false);
                musicSource = go.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
                musicSource.spatialBlend = 0f;
                musicSource.ignoreListenerPause = true;
                musicSource.volume = baseMusicVolume * (isDucking ? pauseDuckFactor : 1f);
            }
            if (sfxSource == null)
            {
                var go = new GameObject("SfxSource");
                go.transform.SetParent(transform, false);
                sfxSource = go.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f;
                sfxSource.ignoreListenerPause = true; // menu clicks still play while paused
                sfxSource.volume = baseSfxVolume;
            }
            if (dialogueSource == null)
            {
                var go = new GameObject("DialogueSource");
                go.transform.SetParent(transform, false);
                dialogueSource = go.AddComponent<AudioSource>();
                dialogueSource.loop = false;
                dialogueSource.playOnAwake = false;
                dialogueSource.spatialBlend = 0f;
                dialogueSource.ignoreListenerPause = false; // voice pauses with game
                dialogueSource.volume = baseDialogueVolume;
            }
            ambientSourceA = EnsureAmbientSource(ambientSourceA, "AmbientSourceA");
            ambientSourceB = EnsureAmbientSource(ambientSourceB, "AmbientSourceB");
        }

        private AudioSource EnsureAmbientSource(AudioSource existing, string name)
        {
            if (existing != null) return existing;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.ignoreListenerPause = true;
            src.volume = 0f;
            return src;
        }

        // ════════════ Volume API (called by SettingsManager) ════════════

        public void SetMusicVolume(float linear01)
        {
            baseMusicVolume = Mathf.Clamp01(linear01);
            EnsureSources();
            musicSource.volume = baseMusicVolume * musicUnderscoreMultiplier *
                                 (isDucking ? pauseDuckFactor : 1f);
        }

        /// <summary>
        /// Ambient loop volume. Kept in the SFX bucket conceptually (doesn't duck
        /// on pause like music); re-routes to the currently-active ambient source.
        /// </summary>
        public void SetAmbientVolume(float linear01)
        {
            baseAmbientVolume = Mathf.Clamp01(linear01);
            EnsureSources();
            var active = ActiveAmbient();
            if (active != null && active.isPlaying)
                active.volume = baseAmbientVolume * ambientMaxMultiplier;
        }

        public void SetSfxVolume(float linear01)
        {
            baseSfxVolume = Mathf.Clamp01(linear01);
            EnsureSources();
            sfxSource.volume = baseSfxVolume;
        }

        public void SetDialogueVolume(float linear01)
        {
            baseDialogueVolume = Mathf.Clamp01(linear01);
            EnsureSources();
            dialogueSource.volume = baseDialogueVolume;
        }

        public float MusicVolume => baseMusicVolume;
        public float SfxVolume => baseSfxVolume;
        public float DialogueVolume => baseDialogueVolume;

        // ════════════ Music ════════════

        public void PlayMusic(AudioClip clip, bool restartIfSame = false)
        {
            if (clip == null) return;
            EnsureSources();
            if (!restartIfSame && musicSource.clip == clip && musicSource.isPlaying) return;
            musicSource.clip = clip;
            musicSource.volume = baseMusicVolume * musicUnderscoreMultiplier *
                                 (isDucking ? pauseDuckFactor : 1f);
            musicSource.Play();
        }

        public void StopMusic()
        {
            EnsureSources();
            musicSource.Stop();
        }

        // ════════════ Pause ducking ════════════

        public void SetPauseDucking(bool ducking)
        {
            isDucking = ducking;
            EnsureSources();
            musicSource.volume = baseMusicVolume * musicUnderscoreMultiplier *
                                 (ducking ? pauseDuckFactor : 1f);
        }

        // ════════════ Ambient (crossfaded loops) ════════════

        private AudioSource ActiveAmbient() => ambientUseA ? ambientSourceA : ambientSourceB;
        private AudioSource IdleAmbient()   => ambientUseA ? ambientSourceB : ambientSourceA;

        /// <summary>
        /// Crossfades the ambient bed to <paramref name="clip"/> over
        /// <paramref name="duration"/> seconds (default 2s per design spec).
        /// No-ops if the clip is already the active one so repeated #scene:X
        /// Ink tags don't re-trigger fades.
        /// </summary>
        public void CrossfadeAmbient(AudioClip clip, float duration = 2f)
        {
            if (clip == null) return;
            EnsureSources();

            var active = ActiveAmbient();
            if (active.clip == clip && active.isPlaying) return;

            if (ambientFadeCo != null) StopCoroutine(ambientFadeCo);
            ambientFadeCo = StartCoroutine(AmbientFadeRoutine(clip, duration));
        }

        public void StopAmbient(float fadeOut = 1f)
        {
            EnsureSources();
            if (ambientFadeCo != null) StopCoroutine(ambientFadeCo);
            ambientFadeCo = StartCoroutine(AmbientFadeRoutine(null, fadeOut));
        }

        private IEnumerator AmbientFadeRoutine(AudioClip newClip, float duration)
        {
            var fadeOut = ActiveAmbient();
            var fadeIn  = IdleAmbient();

            // Prepare fade-in source with the new clip at 0 volume.
            if (newClip != null)
            {
                fadeIn.clip = newClip;
                fadeIn.volume = 0f;
                fadeIn.Play();
            }

            float startOut = fadeOut.volume;
            float targetIn = newClip != null ? baseAmbientVolume * ambientMaxMultiplier : 0f;
            float t = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                // Ease in/out (smoothstep).
                float s = k * k * (3f - 2f * k);
                fadeOut.volume = Mathf.Lerp(startOut, 0f, s);
                if (newClip != null) fadeIn.volume = Mathf.Lerp(0f, targetIn, s);
                yield return null;
            }

            fadeOut.Stop();
            fadeOut.clip = null;
            if (newClip != null) ambientUseA = !ambientUseA;
            ambientFadeCo = null;
        }

        // ════════════ SFX ════════════

        public void PlaySfx(AudioClip clip)
        {
            if (clip == null) return;
            EnsureSources();
            sfxSource.PlayOneShot(clip, baseSfxVolume);
        }

        public void PlayButtonHover()
        {
            if (buttonHoverClip == null) return;
            EnsureSources();
            sfxSource.PlayOneShot(buttonHoverClip, baseSfxVolume);
        }

        public void PlayButtonClick()
        {
            if (buttonClickClip == null) return;
            EnsureSources();
            sfxSource.PlayOneShot(buttonClickClip, baseSfxVolume);
        }

        // ════════════ Dialogue ════════════

        public void PlayDialogue(AudioClip clip)
        {
            if (clip == null) return;
            EnsureSources();
            dialogueSource.Stop();
            dialogueSource.clip = clip;
            dialogueSource.Play();
        }

        public void StopDialogue()
        {
            if (dialogueSource != null && dialogueSource.isPlaying) dialogueSource.Stop();
        }

        public bool IsDialoguePlaying => dialogueSource != null && dialogueSource.isPlaying;
    }
}
