using UnityEngine;

namespace BaiguVN
{
    public class AudioChannelDebug : MonoBehaviour
    {
        [Header("Target")]
        public AudioService audioService;

        [Header("Test Ambience ID")]
        public string ambienceTestId =
            "amb_mountain_wind";

        private bool oldBgmMute;
        private bool oldSeMute;
        private bool oldAmbienceMute;

        private void Start()
        {
            if (audioService == null)
            {
                Debug.LogError(
                    "AudioChannelDebug：没有绑定 AudioService。"
                );

                return;
            }

            SaveMuteState();
            CheckReferences();
            PrintStatus();
        }

        private void SaveMuteState()
        {
            if (audioService.bgmSource != null)
            {
                oldBgmMute =
                    audioService.bgmSource.mute;
            }

            if (audioService.seSource != null)
            {
                oldSeMute =
                    audioService.seSource.mute;
            }

            if (audioService.ambienceSource != null)
            {
                oldAmbienceMute =
                    audioService.ambienceSource.mute;
            }
        }

        private void OnDisable()
        {
            RestoreOriginalMuteState();
        }

        private void CheckReferences()
        {
            AudioSource bgm =
                audioService.bgmSource;

            AudioSource se =
                audioService.seSource;

            AudioSource ambience =
                audioService.ambienceSource;

            if (bgm == null)
            {
                Debug.LogError(
                    "【AudioTest】BGM Source = NULL"
                );
            }

            if (se == null)
            {
                Debug.LogError(
                    "【AudioTest】SE Source = NULL"
                );
            }

            if (ambience == null)
            {
                Debug.LogError(
                    "【AudioTest】Ambience Source = NULL"
                );
            }

            if (bgm != null &&
                ambience != null &&
                bgm == ambience)
            {
                Debug.LogError(
                    "【AudioTest】错误：BGM 和 Ambience " +
                    "引用的是同一个 AudioSource！"
                );
            }

            if (bgm != null &&
                se != null &&
                bgm == se)
            {
                Debug.LogError(
                    "【AudioTest】错误：BGM 和 SE " +
                    "引用的是同一个 AudioSource！"
                );
            }

            if (se != null &&
                ambience != null &&
                se == ambience)
            {
                Debug.LogError(
                    "【AudioTest】错误：SE 和 Ambience " +
                    "引用的是同一个 AudioSource！"
                );
            }

            if (bgm != null &&
                se != null &&
                ambience != null &&
                bgm != se &&
                bgm != ambience &&
                se != ambience)
            {
                Debug.Log(
                    "【AudioTest】通过：三个 AudioSource " +
                    "引用彼此独立。"
                );
            }
        }

        public void PrintStatus()
        {
            if (audioService == null)
                return;

            Debug.Log(
                BuildStatus(
                    "BGM",
                    audioService.bgmSource
                )
            );

            Debug.Log(
                BuildStatus(
                    "SE",
                    audioService.seSource
                )
            );

            Debug.Log(
                BuildStatus(
                    "AMBIENCE",
                    audioService.ambienceSource
                )
            );
        }

        private string BuildStatus(
            string label,
            AudioSource source)
        {
            if (source == null)
            {
                return
                    $"【AudioTest】{label}: NULL";
            }

            string clipName =
                source.clip != null
                ? source.clip.name
                : "NULL";

            string mixer =
                source.outputAudioMixerGroup != null
                ? source.outputAudioMixerGroup.name
                : "None";

            float playbackTime = 0f;

            if (source.clip != null)
            {
                playbackTime = source.time;
            }

            return
                $"【AudioTest】{label} | " +
                $"Playing={source.isPlaying} | " +
                $"Mute={source.mute} | " +
                $"Volume={source.volume:F2} | " +
                $"Clip={clipName} | " +
                $"GameObject={source.gameObject.name} | " +
                $"EntityID={source.GetEntityId()} | " +
                $"Time={playbackTime:F2} | " +
                $"Enabled={source.enabled} | " +
                $"Active={source.gameObject.activeInHierarchy} | " +
                $"Mixer={mixer}";
        }
        public void OnlyBgm()
        {
            SetMute(
                false,
                true,
                true
            );

            Debug.Log(
                "【AudioTest】现在只监听 BGM。"
            );
        }

        public void OnlySe()
        {
            SetMute(
                true,
                false,
                true
            );

            Debug.Log(
                "【AudioTest】现在只监听 SE。"
            );
        }

        public void OnlyAmbience()
        {
            SetMute(
                true,
                true,
                false
            );

            Debug.Log(
                "【AudioTest】现在只监听 Ambience。"
            );
        }

        public void HearAll()
        {
            SetMute(
                false,
                false,
                false
            );

            Debug.Log(
                "【AudioTest】三个通道全部恢复。"
            );
        }

        public void ForceAmbience()
        {
            if (audioService == null)
                return;

            Debug.Log(
                $"【AudioTest】强制 ApplyAmbience：" +
                $"{ambienceTestId}"
            );

            audioService.ApplyAmbience(
                ambienceTestId
            );

            PrintStatus();
        }

        private void SetMute(
            bool bgm,
            bool se,
            bool ambience)
        {
            if (audioService.bgmSource != null)
            {
                audioService.bgmSource.mute =
                    bgm;
            }

            if (audioService.seSource != null)
            {
                audioService.seSource.mute =
                    se;
            }

            if (audioService.ambienceSource != null)
            {
                audioService.ambienceSource.mute =
                    ambience;
            }
        }

        private void RestoreOriginalMuteState()
        {
            if (audioService == null)
                return;

            if (audioService.bgmSource != null)
            {
                audioService.bgmSource.mute =
                    oldBgmMute;
            }

            if (audioService.seSource != null)
            {
                audioService.seSource.mute =
                    oldSeMute;
            }

            if (audioService.ambienceSource != null)
            {
                audioService.ambienceSource.mute =
                    oldAmbienceMute;
            }
        }

        private void OnGUI()
        {
            if (audioService == null)
                return;

            GUILayout.BeginArea(
                new Rect(
                    20,
                    20,
                    300,
                    310
                ),
                GUI.skin.box
            );

            GUILayout.Label(
                "AUDIO DEBUG"
            );

            if (GUILayout.Button(
                "Only BGM"))
            {
                OnlyBgm();
            }

            if (GUILayout.Button(
                "Only Ambience"))
            {
                OnlyAmbience();
            }

            if (GUILayout.Button(
                "Only SE"))
            {
                OnlySe();
            }

            if (GUILayout.Button(
                "Hear All"))
            {
                HearAll();
            }

            GUILayout.Space(10);

            if (GUILayout.Button(
                "Force Ambience"))
            {
                ForceAmbience();
            }

            if (GUILayout.Button(
                "Print Status"))
            {
                PrintStatus();
            }

            GUILayout.EndArea();
        }
    }
}