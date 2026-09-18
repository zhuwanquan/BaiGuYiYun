using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BaiguVN
{
    // 剧情分支选择视图。
    //
    // 与 ObservationView 的区别：
    // - observe 是「调查物证 + 继续」，对象会被标记已看。
    // - choice 是「选一条路」，点一次立刻进入分支，不保留已看状态。
    //
    // 按钮完全由节点数据动态生成：
    // 选项数量无上限，增加/删除娱乐线剧本时，无需改代码或场景。
    public class ChoiceView : MonoBehaviour
    {
        [Header("提示")]
        public TMP_Text speakerText;
        public TMP_Text promptText;

        [Header("选项容器")]
        public RectTransform contentRoot;

        [Header("按钮模板")]
        public GameObject choiceButtonPrefab;

        public event Action<string> ChoiceSelected;

        public void Show(VNNode choiceNode)
        {
            if (choiceNode == null)
            {
                Debug.LogError("ChoiceView.Show 收到空节点。");
                return;
            }

            gameObject.SetActive(true);

            if (speakerText != null)
            {
                speakerText.text = choiceNode.speaker ?? "";
            }

            if (promptText != null)
            {
                promptText.text = choiceNode.text ?? "";
            }

            ClearChoices();

            if (choiceNode.choices == null)
            {
                return;
            }

            foreach (VNChoice choice in choiceNode.choices)
            {
                if (choice == null)
                {
                    continue;
                }

                if (choiceButtonPrefab == null ||
                    contentRoot == null)
                {
                    Debug.LogError(
                        "ChoiceView 没有指定按钮模板或容器。"
                    );
                    return;
                }

                GameObject item = Instantiate(
                    choiceButtonPrefab,
                    contentRoot
                );

                // 模板通常处于非激活状态，实例化后需激活。
                item.SetActive(true);

                TMP_Text label =
                    item.GetComponentInChildren<TMP_Text>(true);

                if (label != null)
                {
                    label.text = BuildLabel(choice);
                }

                Button button =
                    item.GetComponent<Button>();

                if (button == null)
                {
                    button =
                        item.GetComponentInChildren<Button>(true);
                }

                if (button != null)
                {
                    string choiceId = choice.id;

                    button.onClick.AddListener(
                        () => ChoiceSelected?.Invoke(choiceId)
                    );
                }
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private string BuildLabel(VNChoice choice)
        {
            string text = choice.label ?? "";

            if (!string.IsNullOrWhiteSpace(choice.tag))
            {
                return $"[{choice.tag}] {text}";
            }

            return text;
        }

        private void ClearChoices()
        {
            if (contentRoot == null)
            {
                return;
            }

            for (int i = contentRoot.childCount - 1;
                i >= 0;
                i--)
            {
                Transform child = contentRoot.GetChild(i);
                Destroy(child.gameObject);
            }
        }
    }
}
