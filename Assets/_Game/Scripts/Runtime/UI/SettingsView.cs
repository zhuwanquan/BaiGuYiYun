using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BaiguVN
{
    public class SettingsView : MonoBehaviour
    {
        [Header("Sliders")]
        public Slider textSpeedSlider;
        public Slider fontSizeSlider;
        public Slider bgmVolumeSlider;
        public Slider seVolumeSlider;

        [Header("Dialogue")]
        public DialogueView dialogueView;

        [Header("Audio")]
        public AudioService audioService;

        private SettingsService service;

        private void Awake()
        {
            service = new SettingsService();
            service.Load();

            textSpeedSlider.onValueChanged.AddListener(OnTextSpeedChanged);
            fontSizeSlider.onValueChanged.AddListener(OnFontSizeChanged);
            bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
            seVolumeSlider.onValueChanged.AddListener(OnSeVolumeChanged);

            RefreshControls();
            ApplyDialogueSettings();
        }

        private void OnDestroy()
        {
            if (textSpeedSlider != null)
                textSpeedSlider.onValueChanged.RemoveListener(OnTextSpeedChanged);

            if (fontSizeSlider != null)
                fontSizeSlider.onValueChanged.RemoveListener(OnFontSizeChanged);

            if (bgmVolumeSlider != null)
                bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmVolumeChanged);

            if (seVolumeSlider != null)
                seVolumeSlider.onValueChanged.RemoveListener(OnSeVolumeChanged);
        }

        private void RefreshControls()
        {
            textSpeedSlider.SetValueWithoutNotify(
                service.settings.textSpeed
            );

            fontSizeSlider.SetValueWithoutNotify(
                service.settings.fontSize
            );

            bgmVolumeSlider.SetValueWithoutNotify(
                service.settings.bgmVolume
            );

            seVolumeSlider.SetValueWithoutNotify(
                service.settings.seVolume
            );
        }

        private void OnTextSpeedChanged(float value)
        {
            service.settings.textSpeed = value;

            ApplyDialogueSettings();
            service.Save();
        }

        private void OnFontSizeChanged(float value)
        {
            service.settings.fontSize =
                Mathf.RoundToInt(value);

            ApplyDialogueSettings();
            service.Save();
        }

        private void OnBgmVolumeChanged(float value)
        {
            service.settings.bgmVolume = value;

            // AudioService 做好以后在这里应用音量。
            if (audioService != null)
                audioService.SetBgmVolume(value);
            service.Save();
        }

        private void OnSeVolumeChanged(float value)
        {
            service.settings.seVolume = value;

            // AudioService 做好以后在这里应用音量。
            if (audioService != null)
                audioService.SetSeVolume(value);
            service.Save();
        }

        private void ApplyDialogueSettings()
        {
            if (dialogueView != null)
            {
                dialogueView.textSpeed =
                    service.settings.textSpeed;

                if (dialogueView.speakerText != null)
                {
                    dialogueView.speakerText.fontSize =
                        service.settings.fontSize;
                }

                if (dialogueView.bodyText != null)
                {
                    dialogueView.bodyText.fontSize =
                        service.settings.fontSize;
                }
            }

            if (audioService != null)
            {
                audioService.SetBgmVolume(
                    service.settings.bgmVolume
                );

                audioService.SetSeVolume(
                    service.settings.seVolume
                );
            }
        }
    }
}