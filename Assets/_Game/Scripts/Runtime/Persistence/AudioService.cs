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
        private RouteResourceService routeResources;
        public void SetResourceService(RouteResourceService service) { routeResources = service; }
        [Header("Audio Sources")]
        public AudioSource bgmSource;
        public AudioSource seSource;
        public AudioSource ambienceSource;

        [Header("BGM Resources")]
        public VNAudioEntry[] bgmClips;

        [Header("SE Resources")]
        public VNAudioEntry[] seClips;

        [Header("Ambience Resources")]
        public VNAudioEntry[] ambienceClips;

        // =========================================================
        // Volume
        // =========================================================

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
            float volume =
                Mathf.Clamp01(value);

            if (seSource != null)
            {
                seSource.volume = volume;
            }

            // 环境音暂时跟随 SE 音量
            if (ambienceSource != null)
            {
                ambienceSource.volume = volume;
            }
        }

        // =========================================================
        // BGM
        // =========================================================

        public void ApplyBgm(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            if (id == "-")
            {
                StopBgm();
                return;
            }

            AudioClip clip =
                FindClip(bgmClips, id, "bgm");

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

        // =========================================================
        // SE
        // =========================================================

        public void PlaySe(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            AudioClip clip =
                FindClip(seClips, id, "se");

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

        // =========================================================
        // Ambience
        // =========================================================

        public void ApplyAmbience(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            if (id == "-")
            {
                StopAmbience();
                return;
            }

            AudioClip clip =
                FindClip(ambienceClips, id, "ambience");

            if (clip == null)
            {
                Debug.LogWarning(
                    $"找不到环境音资源：{id}"
                );
                return;
            }

            PlayAmbience(clip);
        }

        public void PlayAmbience(
            AudioClip clip)
        {
            if (ambienceSource == null ||
                clip == null)
            {
                return;
            }

            // 同一个环境音已经在播放时，
            // 不重新从头开始
            if (ambienceSource.clip == clip &&
                ambienceSource.isPlaying)
            {
                return;
            }

            ambienceSource.clip = clip;
            ambienceSource.loop = true;
            ambienceSource.Play();
        }

        public void StopAmbience()
        {
            if (ambienceSource == null)
            {
                return;
            }

            ambienceSource.Stop();
            ambienceSource.clip = null;
        }

        // =========================================================
        // Resource lookup
        // =========================================================

        private AudioClip FindClip(
            VNAudioEntry[] entries, string id, string kind)
        {
            if (routeResources != null)
                return routeResources.GetAudio(id, kind);
            if (entries == null)
            {
                return null;
            }

            foreach (VNAudioEntry entry
                     in entries)
            {
                if (entry != null &&
                    entry.id == id)
                {
                    return entry.clip;
                }
            }

            return null;
        }

        // =========================================================
        // Application Pause
        // =========================================================

        public void OnAppPause(bool paused)
        {
            if (paused)
            {
                if (bgmSource != null)
                {
                    bgmSource.Pause();
                }

                if (ambienceSource != null)
                {
                    ambienceSource.Pause();
                }
            }
            else
            {
                if (bgmSource != null)
                {
                    bgmSource.UnPause();
                }

                if (ambienceSource != null)
                {
                    ambienceSource.UnPause();
                }
            }
        }

        private void OnApplicationPause(
            bool pauseStatus)
        {
            OnAppPause(pauseStatus);
        }

        private void Awake()
        {
            if (bgmSource == null)
            {
                Debug.LogError(
                    "AudioService：Bgm Source 没有绑定。"
                );
            }

            if (seSource == null)
            {
                Debug.LogError(
                    "AudioService：Se Source 没有绑定。"
                );
            }

            if (ambienceSource == null)
            {
                Debug.LogError(
                    "AudioService：Ambience Source 没有绑定。"
                );
            }

            if (bgmSource == ambienceSource)
            {
                Debug.LogError(
                    "AudioService：BGM Source 和 Ambience Source " +
                    "错误地引用了同一个 AudioSource！"
                );
            }

            if (bgmSource == seSource)
            {
                Debug.LogError(
                    "AudioService：BGM Source 和 SE Source " +
                    "错误地引用了同一个 AudioSource！"
                );
            }

            if (seSource == ambienceSource)
            {
                Debug.LogError(
                    "AudioService：SE Source 和 Ambience Source " +
                    "错误地引用了同一个 AudioSource！"
                );
            }
        }
    }
}
