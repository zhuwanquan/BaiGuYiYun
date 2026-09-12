using UnityEngine;

namespace BaiguVN
{
    public class SaveLoadView : MonoBehaviour
    {
        [Header("Dependencies")]
        public StoryRunner storyRunner;
        public MenuController menuController;

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

            storyRunner.SaveCurrent(slot);
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
        }
    }
}