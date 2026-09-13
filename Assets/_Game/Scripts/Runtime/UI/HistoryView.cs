using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace BaiguVN
{
    public class HistoryView : MonoBehaviour
    {
        [Header("Dependencies")]
        public StoryRunner storyRunner;

        [Header("UI")]
        public TMP_Text historyText;

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (historyText == null)
            {
                Debug.LogError(
                    "HistoryView：没有指定 HistoryText。"
                );
                return;
            }

            if (storyRunner == null)
            {
                historyText.text = "暂无历史记录。";
                return;
            }

            List<HistoryEntry> history =
                storyRunner.GetHistoryCopy();

            if (history == null || history.Count == 0)
            {
                historyText.text = "暂无历史记录。";
                return;
            }

            StringBuilder builder = new StringBuilder();

            foreach (HistoryEntry entry in history)
            {
                if (!string.IsNullOrWhiteSpace(entry.speaker))
                {
                    builder.AppendLine(
                        $"<b>{entry.speaker}</b>"
                    );
                }

                builder.AppendLine(entry.text ?? "");
                builder.AppendLine();
            }

            historyText.text = builder.ToString();
        }
    }
}