using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaiguVN
{
    public class StoryRunner : MonoBehaviour
    {
        [Header("剧情数据")]
        public TextAsset storyJson;

        [Header("UI")]
        public DialogueView dialogueView;
        public ObservationView observationView;

        [Header("Presentation")]
        public PresentationController presentation;

        private StoryRepository repository;
        private GameState state;
        private SaveService saveService;

        private bool menuPaused;

        public event Action<VNNode> OnNodeCompleted;
        public event Action<string> OnChapterReached;

        private void Start()
        {
            if (storyJson == null)
            {
                Debug.LogError("StoryRunner：没有指定 Story JSON。");
                enabled = false;
                return;
            }

            if (dialogueView == null)
            {
                Debug.LogError("StoryRunner：没有指定 DialogueView。");
                enabled = false;
                return;
            }

            if (observationView == null)
            {
                Debug.LogError("StoryRunner：没有指定 ObservationView。");
                enabled = false;
                return;
            }

            if (dialogueView.nextButton == null)
            {
                Debug.LogError("StoryRunner：DialogueView 没有指定 NextButton。");
                enabled = false;
                return;
            }

            try
            {
                repository = new StoryRepository();
                repository.Load(storyJson);

                saveService = new SaveService();
            }
            catch (Exception ex)
            {
                Debug.LogError("剧情加载失败：\n" + ex);
                enabled = false;
                return;
            }

            dialogueView.nextButton.onClick.AddListener(Advance);

            observationView.ObjectSelected += OpenObserve;
            observationView.ContinueSelected += ResumeObserve;

            // 注意：
            // 这里不自动 StartStory()
            // 因为现在游戏启动后由 TitlePanel 控制，
            // 玩家点击“新游戏”后才调用 StartStory()。
        }

        private void OnDestroy()
        {
            if (dialogueView != null &&
                dialogueView.nextButton != null)
            {
                dialogueView.nextButton.onClick.RemoveListener(Advance);
            }

            if (observationView != null)
            {
                observationView.ObjectSelected -= OpenObserve;
                observationView.ContinueSelected -= ResumeObserve;
            }
        }

        // =========================================================
        // 新游戏
        // =========================================================

        public void StartStory()
        {
            state = new GameState();

            observationView.Hide();

            dialogueView.gameObject.SetActive(true);
            dialogueView.nextButton.interactable = true;

            GoTo(repository.StartNode);
        }

        // =========================================================
        // 正文推进
        // =========================================================

        public void Advance()
        {
            if (menuPaused)
            {
                return;
            }

            if (repository == null || state == null)
            {
                return;
            }

            // 正在逐字显示：
            // 第一次点击只补全文字，不进入下一节点。
            if (dialogueView.IsTyping)
            {
                dialogueView.CompleteTyping();
                return;
            }

            VNNode current =
                repository.Get(state.currentNodeId);

            // 已经是结尾
            if (current.type == "end")
            {
                dialogueView.nextButton.interactable = false;
                Debug.Log("测试剧情已经结束。");
                return;
            }

            // observe 节点不用 NextButton 推进
            if (current.type == "observe")
            {
                return;
            }

            CompleteCurrentNode(current);

            if (string.IsNullOrWhiteSpace(current.next))
            {
                Debug.LogError(
                    $"节点 {current.id} 没有 next。"
                );
                return;
            }

            GoTo(current.next);
        }

        // =========================================================
        // 观察互动
        // =========================================================

        public void OpenObserve(string objectId)
        {
            if (repository == null || state == null)
            {
                return;
            }

            VNNode current =
                repository.Get(state.currentNodeId);

            if (current.type != "observe")
            {
                Debug.LogError(
                    $"当前节点 {current.id} 不是 observe 节点。"
                );
                return;
            }

            if (current.objects == null)
            {
                return;
            }

            foreach (VNObserveObject obj in current.objects)
            {
                if (obj.id == objectId)
                {
                    state.observed.Add(obj.id);

                    GoTo(obj.next);
                    return;
                }
            }

            Debug.LogError(
                $"observe 节点 {current.id} 找不到对象：{objectId}"
            );
        }

        public void ResumeObserve()
        {
            if (repository == null || state == null)
            {
                return;
            }

            VNNode current =
                repository.Get(state.currentNodeId);

            if (current.type != "observe")
            {
                Debug.LogError(
                    $"当前节点 {current.id} 不是 observe 节点。"
                );
                return;
            }

            GoTo(current.resume);
        }

        // =========================================================
        // 完成当前节点
        // =========================================================

        private void CompleteCurrentNode(VNNode node)
        {
            if (node.type == "line")
            {
                state.history.Add(
                    new HistoryEntry
                    {
                        nodeId = node.id,
                        speaker = node.speaker,
                        text = node.text
                    }
                );

                OnNodeCompleted?.Invoke(node);
            }
        }

        // =========================================================
        // 进入剧情节点
        // =========================================================

        private void GoTo(string id)
        {
            VNNode node = repository.Get(id);

            state.currentNodeId = node.id;
            state.pageIndex = 0;

            // -----------------------------------------------------
            // 画面表现
            // 空字符串 = 继承上一节点背景
            // "-" = 清除背景
            // -----------------------------------------------------

            if (presentation != null &&
                !string.IsNullOrEmpty(node.backgroundId))
            {
                presentation.ApplyBackground(
                    node.backgroundId
                );

                state.visuals.backgroundId =
                    node.backgroundId;
            }

            // -----------------------------------------------------
            // 节点类型
            // -----------------------------------------------------

            switch (node.type)
            {
                case "line":
                    observationView.Hide();

                    dialogueView.gameObject.SetActive(true);
                    dialogueView.nextButton.interactable = true;

                    dialogueView.Show(node);
                    break;

                case "observe":
                    dialogueView.gameObject.SetActive(false);

                    observationView.Show(
                        node,
                        state.observed
                    );
                    break;

                case "pause":
                    observationView.Hide();

                    dialogueView.gameObject.SetActive(true);
                    dialogueView.nextButton.interactable = true;

                    dialogueView.Show(node);
                    break;

                case "end":
                    observationView.Hide();

                    dialogueView.gameObject.SetActive(true);

                    state.mainCompleted = true;

                    dialogueView.Show(node);
                    break;

                default:
                    Debug.LogError(
                        $"未知节点类型：{node.type}"
                    );
                    break;
            }

            // 进入一个稳定节点以后自动保存到 Slot 0
            AutoSaveCurrent();
        }

        // =========================================================
        // 菜单暂停
        // =========================================================

        public void SetMenuPaused(bool paused)
        {
            menuPaused = paused;
        }

        // =========================================================
        // 保存
        // =========================================================

        public void SaveCurrent(int slot)
        {
            if (repository == null || state == null)
            {
                Debug.LogWarning("当前没有可以保存的剧情状态。");
                return;
            }

            if (saveService == null)
            {
                saveService = new SaveService();
            }

            try
            {
                VNSnapshot snapshot =
                    state.ToSnapshot(repository.ContentVersion);

                saveService.SaveSlot(slot, snapshot);

                Debug.Log($"已保存到槽位 {slot}。");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"保存槽位 {slot} 失败：\n{ex}"
                );
            }
        }

        public bool HasSaveSlot(int slot)
        {
            if (saveService == null)
            {
                saveService = new SaveService();
            }

            return saveService.HasSlot(slot);
        }

        private void AutoSaveCurrent()
        {
            if (repository == null || state == null)
            {
                return;
            }

            SaveCurrent(0);
        }

        // =========================================================
        // 读取
        // =========================================================

        public void LoadSlot(int slot)
        {
            TryLoadSlot(slot);
        }

        public bool TryLoadSlot(int slot)
        {
            if (repository == null)
            {
                Debug.LogWarning(
                    "剧情尚未加载，无法读档。"
                );
                return false;
            }

            if (saveService == null)
            {
                saveService = new SaveService();
            }

            VNSnapshot snapshot =
                saveService.ReadSlot(
                    slot,
                    repository.ContentVersion
                );

            if (snapshot == null)
            {
                Debug.LogWarning(
                    $"槽位 {slot} 没有可读取的兼容存档。"
                );

                return false;
            }

            Restore(snapshot);

            return true;
        }

        // =========================================================
        // 从存档恢复 GameState
        // =========================================================

        public void Restore(VNSnapshot saved)
        {
            if (saved == null)
            {
                Debug.LogError("Restore 收到空存档。");
                return;
            }

            GameState restored = new GameState();

            restored.currentNodeId = saved.currentNodeId;
            restored.pageIndex = saved.pageIndex;

            // -----------------------------------------------------
            // observed
            // 落盘时是 List<string>
            // 内存恢复成 HashSet<string>
            // -----------------------------------------------------

            restored.observed =
                saved.observed != null
                ? new HashSet<string>(saved.observed)
                : new HashSet<string>();

            // -----------------------------------------------------
            // history 深拷贝
            // -----------------------------------------------------

            restored.history =
                new List<HistoryEntry>();

            if (saved.history != null)
            {
                foreach (HistoryEntry entry in saved.history)
                {
                    restored.history.Add(
                        new HistoryEntry
                        {
                            nodeId = entry.nodeId,
                            speaker = entry.speaker,
                            text = entry.text
                        }
                    );
                }
            }

            // -----------------------------------------------------
            // 视觉状态
            // -----------------------------------------------------

            restored.visuals =
                saved.visuals != null
                ? new VisualSnapshot
                {
                    backgroundId =
                        saved.visuals.backgroundId,

                    portraitId =
                        saved.visuals.portraitId,

                    bgmId =
                        saved.visuals.bgmId
                }
                : new VisualSnapshot();

            // -----------------------------------------------------
            // 已完成章节
            // -----------------------------------------------------

            restored.completedChapters =
                saved.completedChapters != null
                ? new List<string>(
                    saved.completedChapters
                )
                : new List<string>();

            restored.mainCompleted =
                saved.mainCompleted;

            // -----------------------------------------------------
            // 整体替换当前状态
            // -----------------------------------------------------

            state = restored;

            // -----------------------------------------------------
            // 读档只恢复最终画面
            // 不重新跑 GoTo() 的一次性演出
            // -----------------------------------------------------

            if (presentation != null)
            {
                presentation.ApplySnapshot(
                    state.visuals
                );
            }

            RenderRestoredNode();

            menuPaused = false;

            Debug.Log(
                $"读档成功，恢复到节点：{state.currentNodeId}"
            );
        }

        // =========================================================
        // 读档后的 UI 重绘
        //
        // 这是你刚才缺少、导致 CS0103 的方法。
        // =========================================================

        private void RenderRestoredNode()
        {
            if (repository == null ||
                state == null ||
                string.IsNullOrEmpty(state.currentNodeId))
            {
                return;
            }

            VNNode node =
                repository.Get(state.currentNodeId);

            switch (node.type)
            {
                case "line":
                case "pause":
                    observationView.Hide();

                    dialogueView.gameObject.SetActive(true);
                    dialogueView.nextButton.interactable = true;

                    dialogueView.Show(node);
                    dialogueView.CompleteTyping();
                    break;

                case "observe":
                    dialogueView.gameObject.SetActive(false);

                    observationView.Show(
                        node,
                        state.observed
                    );
                    break;

                case "end":
                    observationView.Hide();

                    dialogueView.gameObject.SetActive(true);

                    dialogueView.Show(node);
                    dialogueView.CompleteTyping();

                    dialogueView.nextButton.interactable = false;
                    break;

                default:
                    Debug.LogError(
                        $"无法恢复未知节点类型：{node.type}"
                    );
                    break;
            }
        }
    }
}