using System;
using UnityEngine;

namespace BaiguVN
{
    [Serializable]
    public class VNAudioEntry
    {
        public string id;
        public AudioClip clip;
    }

    public class AudioService : MonoBehaviour
    {
        [Header("Audio Sources")]
        public AudioSource bgmSource;
        public AudioSource seSource;

        [Header("BGM Resources")]
        public VNAudioEntry[] bgmClips;

        [Header("SE Resources")]
        public VNAudioEntry[] seClips;

        public void SetBgmVolume(float value)
        {
            if (bgmSource != null)
            {
                bgmSource.volume =
                    Mathf.Clamp01(value);
            }
        }

        public void SetSeVolume(float value)
        {
            if (seSource != null)
            {
                seSource.volume =
                    Mathf.Clamp01(value);
            }
        }

        public void ApplyBgm(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            // "-" 表示停止当前 BGM
            if (id == "-")
            {
                StopBgm();
                return;
            }

            AudioClip clip =
                FindClip(bgmClips, id);

            if (clip == null)
            {
                Debug.LogWarning(
                    $"找不到 BGM 资源：{id}"
                );
                return;
            }

            PlayBgm(clip);
        }

        public void PlayBgm(AudioClip clip)
        {
            if (bgmSource == null ||
                clip == null)
            {
                return;
            }

            // 同一首正在播放时不要重新从头播放
            if (bgmSource.clip == clip &&
                bgmSource.isPlaying)
            {
                return;
            }

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        public void StopBgm()
        {
            if (bgmSource == null)
            {
                return;
            }

            bgmSource.Stop();
            bgmSource.clip = null;
        }

        public void PlaySe(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            AudioClip clip =
                FindClip(seClips, id);

            if (clip == null)
            {
                Debug.LogWarning(
                    $"找不到 SE 资源：{id}"
                );
                return;
            }

            PlaySe(clip);
        }

        public void PlaySe(AudioClip clip)
        {
            if (seSource == null ||
                clip == null)
            {
                return;
            }

            seSource.PlayOneShot(clip);
        }

        private AudioClip FindClip(
            VNAudioEntry[] entries,
            string id)
        {
            if (entries == null)
            {
                return null;
            }

            foreach (VNAudioEntry entry in entries)
            {
                if (entry != null &&
                    entry.id == id)
                {
                    return entry.clip;
                }
            }

            return null;
        }

        public void OnAppPause(bool paused)
        {
            if (bgmSource == null)
            {
                return;
            }

            if (paused)
            {
                bgmSource.Pause();
            }
            else
            {
                bgmSource.UnPause();
            }
        }

        private void OnApplicationPause(
            bool pauseStatus)
        {
            OnAppPause(pauseStatus);
        }
    }
}