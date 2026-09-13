using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace BaiguVN
{
    public class JourneyView : MonoBehaviour
    {
        [Header("Dependencies")]
        public StoryRunner storyRunner;

        [Header("UI")]
        public TMP_Text journeyText;

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (journeyText == null)
            {
                Debug.LogError(
                    "JourneyView：没有指定 JourneyText。"
                );
                return;
            }

            if (storyRunner == null)
            {
                journeyText.text =
                    "暂无旅程信息。";
                return;
            }

            List<string> chapters =
                storyRunner.GetCompletedChaptersCopy();

            if (chapters == null ||
                chapters.Count == 0)
            {
                journeyText.text =
                    "旅程刚刚开始。\n\n当前尚未完成任何章节。";
                return;
            }

            StringBuilder builder =
                new StringBuilder();

            builder.AppendLine("已完成的旅程：");
            builder.AppendLine();

            for (int i = 0; i < chapters.Count; i++)
            {
                builder.AppendLine(
                    $"{i + 1}. {chapters[i]}"
                );
            }

            journeyText.text =
                builder.ToString();
        }
    }
}