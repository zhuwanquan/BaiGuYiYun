using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BaiguVN
{
    public class MenuController : MonoBehaviour
    {
        [Header("剧情")]
        public StoryRunner storyRunner;

        [Header("主要页面")]
        public GameObject titlePanel;
        public GameObject storyPanel;

        [Header("标题页")]
        public Button continueButton;

        [Header("覆盖页面")]
        public GameObject historyPanel;
        public GameObject saveLoadPanel;
        public GameObject settingsPanel;
        public GameObject journeyPanel;
        public GameObject memorialPanel;

        private readonly Stack<GameObject> panelStack =
            new Stack<GameObject>();

        private bool titleMode;

        private void Start()
        {
            ShowTitle();
        }

        private void Update()
        {
            if (Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Back();
            }
        }

        // =========================================================
        // 新游戏
        // =========================================================

        public void StartNewGame()
        {
            CloseAllOverlayPanels();

            titleMode = false;

            if (titlePanel != null)
            {
                titlePanel.SetActive(false);
            }

            if (storyPanel != null)
            {
                storyPanel.SetActive(true);
            }

            if (storyRunner != null)
            {
                storyRunner.SetMenuPaused(false);
                storyRunner.StartStory();
            }
        }

        // =========================================================
        // 继续游戏：读取自动档 Slot 0
        // =========================================================

        public void ContinueGame()
        {
            if (storyRunner == null)
            {
                Debug.LogError(
                    "MenuController：没有指定 StoryRunner。"
                );
                return;
            }

            bool loaded =
                storyRunner.TryLoadSlot(0);

            if (!loaded)
            {
                Debug.LogWarning(
                    "没有可以继续的自动存档。"
                );

                RefreshContinueButton();
                return;
            }

            CloseAllOverlayPanels();

            titleMode = false;

            if (titlePanel != null)
            {
                titlePanel.SetActive(false);
            }

            if (storyPanel != null)
            {
                storyPanel.SetActive(true);
            }

            storyRunner.SetMenuPaused(false);
        }

        // =========================================================
        // 标题页
        // =========================================================

        public void ReturnToTitle()
        {
            ShowTitle();
        }

        public void ShowTitle()
        {
            CloseAllOverlayPanels();

            titleMode = true;

            if (storyPanel != null)
            {
                storyPanel.SetActive(false);
            }

            if (titlePanel != null)
            {
                titlePanel.SetActive(true);
            }

            if (storyRunner != null)
            {
                storyRunner.SetMenuPaused(true);
            }

            RefreshContinueButton();
        }

        private void RefreshContinueButton()
        {
            if (continueButton == null)
            {
                return;
            }

            bool hasAutoSave =
                storyRunner != null &&
                storyRunner.HasSaveSlot(0);

            continueButton.interactable =
                hasAutoSave;
        }

        // =========================================================
        // 覆盖页面
        // =========================================================

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
            if (panel == null ||
                panel.activeSelf)
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

            GameObject panel =
                panelStack.Pop();

            if (panel != null)
            {
                panel.SetActive(false);
            }

            if (panelStack.Count == 0 &&
                storyRunner != null)
            {
                storyRunner.SetMenuPaused(
                    titleMode
                );
            }
        }

        public void Back()
        {
            if (panelStack.Count > 0)
            {
                Pop();
                return;
            }

            if (!titleMode)
            {
                ShowTitle();
            }
        }

        private void CloseAllOverlayPanels()
        {
            panelStack.Clear();

            SetPanelActive(historyPanel, false);
            SetPanelActive(saveLoadPanel, false);
            SetPanelActive(settingsPanel, false);
            SetPanelActive(journeyPanel, false);
            SetPanelActive(memorialPanel, false);
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