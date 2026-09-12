using UnityEngine;

namespace BaiguVN
{
    public class AudioService : MonoBehaviour
    {
        [Header("Audio Sources")]
        public AudioSource bgmSource;
        public AudioSource seSource;

        public void SetBgmVolume(float value)
        {
            if (bgmSource != null)
                bgmSource.volume = Mathf.Clamp01(value);
        }

        public void SetSeVolume(float value)
        {
            if (seSource != null)
                seSource.volume = Mathf.Clamp01(value);
        }

        public void PlayBgm(AudioClip clip)
        {
            if (bgmSource == null || clip == null)
                return;

            if (bgmSource.clip == clip && bgmSource.isPlaying)
                return;

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        public void StopBgm()
        {
            if (bgmSource != null)
                bgmSource.Stop();
        }

        public void PlaySe(AudioClip clip)
        {
            if (seSource == null || clip == null)
                return;

            seSource.PlayOneShot(clip);
        }

        public void OnAppPause(bool paused)
        {
            if (bgmSource == null)
                return;

            if (paused)
                bgmSource.Pause();
            else
                bgmSource.UnPause();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            OnAppPause(pauseStatus);
        }
    }
}