using UnityEngine;

namespace DemocracyWay.NewUI
{
    // Owns audio preferences and playback; views only display the resulting values.
    internal sealed class DWAudioSettings
    {
        // Keep existing keys so the refactor does not reset a player's preferences.
        private const string MusicKey = "DW.NewUI.Music";
        private const string SfxKey = "DW.NewUI.Sfx";
        private readonly AudioSource musicSource;
        private readonly AudioSource sfxSource;
        private readonly AudioClip clickSound;
        private readonly AudioClip hoverSound;
        public float MusicVolume { get; private set; }
        public float SfxVolume { get; private set; }

        public DWAudioSettings(AudioSource musicSource, AudioSource sfxSource, AudioClip music, AudioClip clickSound, AudioClip hoverSound)
        {
            this.musicSource = musicSource;
            this.sfxSource = sfxSource;
            this.clickSound = clickSound;
            this.hoverSound = hoverSound;
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.65f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, 0.7f));
            if (musicSource != null)
            {
                if (music != null)
                    musicSource.clip = music;
                musicSource.volume = MusicVolume;
                musicSource.loop = true;
                musicSource.spatialBlend = 0;
                if (musicSource.clip != null && !musicSource.isPlaying)
                    musicSource.Play();
            }

            if (sfxSource != null)
            {
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
                sfxSource.spatialBlend = 0;
                sfxSource.ignoreListenerPause = true;
                sfxSource.volume = SfxVolume;
            }
        }

        public void SetMusic(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            if (musicSource != null)
                musicSource.volume = MusicVolume;
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        }

        public void SetSfx(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            if (sfxSource != null)
                sfxSource.volume = SfxVolume;
            PlayerPrefs.SetFloat(SfxKey, SfxVolume);
        }

        public void ChangeMusic(AudioClip clip)
        {
            if (musicSource == null || clip == null || musicSource.clip == clip) return;
            musicSource.clip = clip;
            musicSource.Play();
        }

        public void PlayClick()
        {
            Play(clickSound);
        }

        public void PlayHover()
        {
            Play(hoverSound);
        }

        private void Play(AudioClip clip)
        {
            if (sfxSource != null && clip != null)
                sfxSource.PlayOneShot(clip);
        }
    }
}
