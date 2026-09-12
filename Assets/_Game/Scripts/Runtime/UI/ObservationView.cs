using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BaiguVN
{
    public class ObservationView : MonoBehaviour
    {
        [Header("观察对象按钮")]
        public Button[] objectButtons;

        [Header("继续按钮")]
        public Button continueButton;

        public event Action<string> ObjectSelected;
        public event Action ContinueSelected;

        public void Show(
            VNNode observeNode,
            HashSet<string> observed)
        {
            if (observeNode == null)
            {
                Debug.LogError("ObservationView.Show 收到空节点。");
                return;
            }

            gameObject.SetActive(true);

            for (int i = 0; i < objectButtons.Length; i++)
            {
                Button button = objectButtons[i];

                button.onClick.RemoveAllListeners();

                if (observeNode.objects != null &&
                    i < observeNode.objects.Length)
                {
                    VNObserveObject obj = observeNode.objects[i];

                    button.gameObject.SetActive(true);

                    bool wasObserved =
                        observed != null &&
                        observed.Contains(obj.id);

                    TMP_Text label =
                        button.GetComponentInChildren<TMP_Text>(true);

                    if (label != null)
                    {
                        label.text = wasObserved
                            ? obj.label + "（已看）"
                            : obj.label;
                    }

                    // 已看过的观察项暂时禁止重复点击
                    button.interactable = !wasObserved;

                    string objectId = obj.id;

                    button.onClick.AddListener(
                        () => ObjectSelected?.Invoke(objectId)
                    );
                }
                else
                {
                    button.gameObject.SetActive(false);
                }
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();

                continueButton.onClick.AddListener(
                    () => ContinueSelected?.Invoke()
                );
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}