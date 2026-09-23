using UnityEngine;
using TMPro;

namespace BaiguVN
{
    public class SaveLoadView : MonoBehaviour
    {
        [Header("Dependencies")]
        public StoryRunner storyRunner;
        public MenuController menuController;
        private TMP_Text statusText;

        public void SaveSlot1()
        {
            Save(1);
        }

        public void SaveSlot2()
        {
            Save(2);
        }

        public void SaveSlot3()
        {
            Save(3);
        }

        public void LoadSlot1()
        {
            Load(1);
        }

        public void LoadSlot2()
        {
            Load(2);
        }

        public void LoadSlot3()
        {
            Load(3);
        }

        public void Close()
        {
            if (menuController != null)
            {
                menuController.Back();
            }
        }

        private void Save(int slot)
        {
            if (storyRunner == null)
            {
                Debug.LogError(
                    "SaveLoadView：没有指定 StoryRunner。"
                );
                return;
            }

            if (!storyRunner.CanSave)
            {
                ShowStatus("演出进行中，请返回剧情等待演出结束后再存档。");
                return;
            }
            storyRunner.SaveCurrent(slot);
            ShowStatus(storyRunner.LastSaveSucceeded ? "已保存到槽位 " + slot + "。" : "保存未成功，请重试。");
        }

        private void Load(int slot)
        {
            if (storyRunner == null)
            {
                Debug.LogError(
                    "SaveLoadView：没有指定 StoryRunner。"
                );
                return;
            }

            bool loaded =
                storyRunner.TryLoadSlot(slot);

            if (loaded &&
                menuController != null)
            {
                menuController.Back();
            }
            if (!loaded) ShowStatus("这个槽位没有可读取的存档，或存档已损坏。");
        }

        private void OnEnable() { if (statusText != null) statusText.text = ""; }
        private void ShowStatus(string message)
        {
            if (statusText == null)
            {
                var obj = new GameObject("SaveStatus", typeof(RectTransform));
                obj.transform.SetParent(transform, false);
                var rect = (RectTransform)obj.transform;
                rect.anchorMin = new Vector2(0.15f, 0.06f); rect.anchorMax = new Vector2(0.85f, 0.14f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                statusText = obj.AddComponent<TextMeshProUGUI>();
                statusText.font = storyRunner.dialogueView.bodyText.font;
                statusText.fontSize = 26; statusText.alignment = TextAlignmentOptions.Center;
                statusText.raycastTarget = false;
            }
            statusText.text = message;
        }
    }
}
