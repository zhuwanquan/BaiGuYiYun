using System;
using TMPro;
using UnityEngine;

namespace BaiguVN
{
    [Serializable]
    public class MemorialDefinition
    {
        public string id;
        public string title;

        [TextArea(2, 5)]
        public string description;
    }

    public class MemorialView : MonoBehaviour
    {
        [Header("数据")]
        public StoryRunner storyRunner;

        [Header("UI")]
        public TMP_Text contentText;

        [Header("纪念目录")]
        public MemorialDefinition[] memorials;

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (contentText == null)
            {
                return;
            }

            if (storyRunner == null)
            {
                contentText.text =
                    "MemorialView：未指定 StoryRunner。";

                return;
            }

            if (memorials == null ||
                memorials.Length == 0)
            {
                contentText.text =
                    "暂无纪念内容。";

                return;
            }

            string result = "";

            foreach (
                MemorialDefinition memorial
                in memorials)
            {
                if (memorial == null ||
                    string.IsNullOrWhiteSpace(
                        memorial.id))
                {
                    continue;
                }

                bool unlocked =
                    storyRunner
                        .IsMemorialUnlocked(
                            memorial.id
                        );

                if (unlocked)
                {
                    result +=
                        $"【{memorial.title}】\n";

                    result +=
                        $"{memorial.description}\n\n";
                }
                else
                {
                    result +=
                        "【？？？】\n";

                    result +=
                        "尚未解锁\n\n";
                }
            }

            contentText.text =
                result.TrimEnd();
        }
    }
}