using UnityEngine;
using UnityEngine.UI;

namespace BaiguVN
{
    public class UIButtonSoundBinder : MonoBehaviour
    {
        [Header("Audio")]
        public AudioService audioService;

        [Header("SE ID")]
        public string clickSeId = "se_click";

        private Button[] buttons;

        private void Start()
        {
            if (audioService == null)
            {
                Debug.LogWarning(
                    "UIButtonSoundBinder：没有指定 AudioService。"
                );
                return;
            }

            // true = 连当前隐藏的 Panel 里的按钮也一起找到
            buttons =
                GetComponentsInChildren<Button>(true);

            foreach (Button button in buttons)
            {
                if (button == null)
                    continue;

                button.onClick.AddListener(
                    PlayClickSound
                );
            }
        }

        private void OnDestroy()
        {
            if (buttons == null)
                return;

            foreach (Button button in buttons)
            {
                if (button == null)
                    continue;

                button.onClick.RemoveListener(
                    PlayClickSound
                );
            }
        }

        private void PlayClickSound()
        {
            if (audioService == null)
                return;

            audioService.PlaySe(
                clickSeId
            );
        }
    }
}