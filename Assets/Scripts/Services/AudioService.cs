using UnityEngine;

namespace DemocracyWay.Services
{
    /// <summary>
    /// Three audio channels as child AudioSources, created on demand:
    ///   Music — looped ambient (keeps playing while paused, ducked instead)
    ///   Sfx   — one-shots, UI hover/click (keeps playing while paused)
    ///   Voice — dialogue speech (pauses with the game)
    /// Volumes are pushed here by <see cref="SettingsService"/>.
    /// The AudioListener lives on this object, because this object survives
    /// scene loads while every scene camera dies with its scene.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Audio Service")]
    [DisallowMultipleComponent]
    public class AudioService : MonoBehaviour
    {
        [Tooltip("Πολλαπλασιαστής έντασης μουσικής όσο το παιχνίδι είναι σε παύση.")]
        [Range(0f, 1f)]
        [SerializeField] private float pauseDuckFactor = 0.4f;

        private AudioSource musicSource;
        private AudioSource sfxSource;
        private AudioSource voiceSource;

        private float baseMusicVolume = 0.7f;
        private float baseSfxVolume = 0.9f;
        private float baseVoiceVolume = 1.0f;
        private bool isDucking;

        void Awake()
        {
            musicSource = CreateSource("MusicSource", loop: true, ignoreListenerPause: true);
            sfxSource = CreateSource("SfxSource", loop: false, ignoreListenerPause: true);
            voiceSource = CreateSource("VoiceSource", loop: false, ignoreListenerPause: false);
        }

        private AudioSource CreateSource(string name, bool loop, bool ignoreListenerPause)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D
            source.ignoreListenerPause = ignoreListenerPause;
            return source;
        }

        // ════════ Music ════════

        public void PlayMusic(AudioClip clip, bool restartIfSame = false)
        {
            if (clip == null) return;
            if (!restartIfSame && musicSource.clip == clip && musicSource.isPlaying) return;
            musicSource.clip = clip;
            musicSource.Play();
        }

        public void StopMusic() => musicSource.Stop();

        // ════════ SFX ════════

        public void PlaySfx(AudioClip clip)
        {
            // PlayOneShot scales by sfxSource.volume, which already carries baseSfxVolume.
            if (clip != null) sfxSource.PlayOneShot(clip);
        }

        public void PlayUiHover() => PlaySfx(ServicesRoot.Config != null ? ServicesRoot.Config.uiHoverSfx : null);
        public void PlayUiClick() => PlaySfx(ServicesRoot.Config != null ? ServicesRoot.Config.uiClickSfx : null);

        // ════════ Voice ════════

        public void PlayVoice(AudioClip clip)
        {
            if (clip == null) return;
            voiceSource.Stop();
            voiceSource.clip = clip;
            voiceSource.Play();
        }

        public void StopVoice() => voiceSource.Stop();

        // ════════ Volumes ════════

        /// <summary>Called by SettingsService whenever the sliders change.</summary>
        public void ApplyVolumes(float music, float sfx, float voice)
        {
            baseMusicVolume = Mathf.Clamp01(music);
            baseSfxVolume = Mathf.Clamp01(sfx);
            baseVoiceVolume = Mathf.Clamp01(voice);
            RefreshSourceVolumes();
        }

        public void SetPauseDucking(bool ducking)
        {
            isDucking = ducking;
            RefreshSourceVolumes();
        }

        private void RefreshSourceVolumes()
        {
            musicSource.volume = baseMusicVolume * (isDucking ? pauseDuckFactor : 1f);
            sfxSource.volume = baseSfxVolume;
            voiceSource.volume = baseVoiceVolume;
        }
    }
}
