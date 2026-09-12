using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BaiguVN
{
    public class MenuController : MonoBehaviour
    {
        [Header("剧情")]
        public StoryRunner storyRunner;

        [Header("主要页面")]
        public GameObject titlePanel;
        public GameObject storyPanel;

        [Header("覆盖页面")]
        public GameObject historyPanel;
        public GameObject saveLoadPanel;
        public GameObject settingsPanel;
        public GameObject journeyPanel;
        public GameObject memorialPanel;

        private readonly Stack<GameObject> panelStack =
            new Stack<GameObject>();

        private void Start()
        {
            // 启动时确保覆盖窗口全部关闭
            SetPanelActive(historyPanel, false);
            SetPanelActive(saveLoadPanel, false);
            SetPanelActive(settingsPanel, false);
            SetPanelActive(journeyPanel, false);
            SetPanelActive(memorialPanel, false);
        }

        private void Update()
        {
            // PC 的 Esc / Android 返回键后面都统一走 Back()
            if (Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Back();
            }
        }

        public void OpenHistory()
        {
            Push(historyPanel);
        }

        public void OpenSaveLoad()
        {
            Push(saveLoadPanel);
        }

        public void OpenSettings()
        {
            Push(settingsPanel);
        }

        public void OpenJourney()
        {
            Push(journeyPanel);
        }

        public void OpenMemorial()
        {
            Push(memorialPanel);
        }

        public void Push(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            // 已经打开就不要重复压栈
            if (panel.activeSelf)
            {
                return;
            }

            panel.SetActive(true);
            panel.transform.SetAsLastSibling();

            panelStack.Push(panel);

            if (storyRunner != null)
            {
                storyRunner.SetMenuPaused(true);
            }
        }

        public void Pop()
        {
            if (panelStack.Count == 0)
            {
                return;
            }

            GameObject panel = panelStack.Pop();

            if (panel != null)
            {
                panel.SetActive(false);
            }

            if (panelStack.Count == 0 &&
                storyRunner != null)
            {
                storyRunner.SetMenuPaused(false);
            }
        }

        public void Back()
        {
            // 优先关闭最上层覆盖窗口
            if (panelStack.Count > 0)
            {
                Pop();
                return;
            }

            // 目前标题页还没正式制作，
            // 所以没有覆盖窗口时暂时什么都不做。
        }

        private void SetPanelActive(
            GameObject panel,
            bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }
    }
}