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

        [Header("Audio")]
        public AudioService audioService;

        private StoryRepository repository;
        private GameState state;
        private SaveService saveService;

        private ProfileService profileService;
        private VNProfile profile;

        private const string MainCompletionChapterId =
            "B03";

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

                profileService =
                    new ProfileService();

                profile =
                    profileService.LoadOrCreate();
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
            // 先清除上一局残留的视觉内容
            if (presentation != null)
            {
                presentation.ResetForNewGame();
            }

            // 新游戏重新开始 BGM，
            // 防止继承上一局正在播放的音乐状态
            if (audioService != null)
            {
                audioService.StopBgm();
                audioService.StopAmbience();
            }

            // 创建全新的剧情状态
            state = new GameState();

            if (observationView != null)
            {
                observationView.Hide();
            }

            if (dialogueView != null)
            {
                dialogueView.gameObject.SetActive(true);
            }

            // 从故事起点重新开始
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

            // 人物演出期间不接受推进输入。
            // Busy 期间的点击直接丢弃，不排队。
            if (presentation != null &&
                presentation.IsBusy)
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

            // 演出期间不接受观察对象输入。
            if (presentation != null &&
                presentation.IsBusy)
            {
                return;
            }

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

            // 演出期间不接受观察继续输入。
            if (presentation != null &&
                presentation.IsBusy)
            {
                return;
            }

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

        private void EnsureProfileLoaded()
        {
            if (profileService == null)
            {
                profileService =
                    new ProfileService();
            }

            if (profile == null)
            {
                profile =
                    profileService.LoadOrCreate();
            }
        }

        private bool TrySaveProfile()
        {
            EnsureProfileLoaded();

            try
            {
                profileService.SaveProfile(
                    profile
                );

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"Profile 保存失败：\n{ex}"
                );

                return false;
            }
        }

        private void MarkNodeRead(
            VNNode node)
        {
            if (node == null ||
                string.IsNullOrWhiteSpace(node.id))
            {
                return;
            }

            EnsureProfileLoaded();

            if (profile.readNodeIds.Contains(
                node.id))
            {
                return;
            }

            profile.readNodeIds.Add(
                node.id
            );

            if (!TrySaveProfile())
            {
                // 保存失败则撤销内存修改，
                // 下次完成节点时还可以再次尝试。
                profile.readNodeIds.Remove(
                    node.id
                );

                return;
            }

            Debug.Log(
                $"Profile 已读节点：{node.id}"
            );
        }

        private void MarkProfileChapterCompleted(
            string chapterId)
        {
            if (string.IsNullOrWhiteSpace(
                chapterId))
            {
                return;
            }

            EnsureProfileLoaded();

            if (profile.completedChapters.Contains(
                chapterId))
            {
                return;
            }

            profile.completedChapters.Add(
                chapterId
            );

            if (!TrySaveProfile())
            {
                profile.completedChapters.Remove(
                    chapterId
                );

                return;
            }

            Debug.Log(
                $"Profile 永久章节完成：{chapterId}"
            );
        }

        private void UnlockMemorial(
            string memorialId)
        {
            if (string.IsNullOrWhiteSpace(
                memorialId))
            {
                return;
            }

            EnsureProfileLoaded();

            if (profile.memorials.Contains(
                memorialId))
            {
                return;
            }

            profile.memorials.Add(
                memorialId
            );

            if (!TrySaveProfile())
            {
                // 保存失败则回滚内存状态，
                // 以后再次到达节点仍可重试。
                profile.memorials.Remove(
                    memorialId
                );

                return;
            }

            Debug.Log(
                $"Memorial 永久解锁：{memorialId}"
            );
        }

        private void UnlockCollection(
            string collectionId)
        {
            if (string.IsNullOrWhiteSpace(
                collectionId))
            {
                return;
            }

            EnsureProfileLoaded();

            if (profile.collections.Contains(
                collectionId))
            {
                return;
            }

            profile.collections.Add(
                collectionId
            );

            if (!TrySaveProfile())
            {
                profile.collections.Remove(
                    collectionId
                );

                return;
            }

            Debug.Log(
                $"Collection 永久解锁：{collectionId}"
            );
        }

        // =========================================================
        // 完成当前节点
        // =========================================================

        private void CompleteCurrentNode(
            VNNode node)
        {
            if (node == null)
            {
                return;
            }

            // 普通正文继续进入本轮 History。
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

                OnNodeCompleted?.Invoke(
                    node
                );
            }

            // line / pause 真正被玩家读完并推进后，
            // 才计入跨轮已读记录。
            if (node.type == "line" ||
                node.type == "pause")
            {
                MarkNodeRead(
                    node
                );
            }
        }

        private void RegisterCompletedChapter(
            VNNode node)
        {
            if (node == null ||
                string.IsNullOrWhiteSpace(
                    node.completeChapter))
            {
                return;
            }

            if (state == null)
            {
                return;
            }

            if (state.completedChapters == null)
            {
                state.completedChapters =
                    new List<string>();
            }

            bool newlyCompletedThisRun =
                false;

            // 当前这一轮的章节完成记录。
            if (!state.completedChapters.Contains(
                node.completeChapter))
            {
                state.completedChapters.Add(
                    node.completeChapter
                );

                newlyCompletedThisRun =
                    true;

                Debug.Log(
                    $"章节完成：{node.completeChapter}"
                );
            }

            // 当前这一轮首次到达完成点时广播。
            if (newlyCompletedThisRun)
            {
                OnChapterReached?.Invoke(
                    node.completeChapter
                );
            }

            // 跨轮永久记录。
            MarkProfileChapterCompleted(
                node.completeChapter
            );

            // 正式主线完成只认 B03 完成点，
            // 不再把所有 end 都当作通关。
            if (node.completeChapter ==
                MainCompletionChapterId)
            {
                state.mainCompleted =
                    true;
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

            // 背景
            if (presentation != null &&
                !string.IsNullOrEmpty(node.backgroundId))
            {
                presentation.ApplyBackgroundForStory(
                    node.backgroundId
                );

                state.visuals.backgroundId =
                    node.backgroundId;
            }

            // CG
            if (presentation != null &&
                !string.IsNullOrEmpty(node.cgId))
            {
                presentation.ApplyCGForStory(
                    node.cgId
                );

                state.visuals.cgId =
                    node.cgId == "-"
                    ? ""
                    : node.cgId;
            }

            // 立绘
            if (presentation != null &&
                !string.IsNullOrEmpty(
                    node.portraitId))
            {
                presentation.ApplyPortrait(
                    node.portraitId,
                    node.portraitSlot
                );

                ApplyPortraitToState(
                    node.portraitId,
                    node.portraitSlot
                );
            }

            if (presentation != null)
            {
                presentation.ApplyFocus(
                    node.focusSlot
                );

                state.visuals.focusSlot =
                    node.focusSlot;
            }

            // BGM
            if (audioService != null &&
                !string.IsNullOrEmpty(node.bgmId))
            {
                audioService.ApplyBgm(
                    node.bgmId
                );

                state.visuals.bgmId =
                    node.bgmId;
            }

            // 环境音
            if (audioService != null &&
                !string.IsNullOrEmpty(
                    node.ambienceId))
            {
                audioService.ApplyAmbience(
                    node.ambienceId
                );

                state.visuals.ambienceId =
                    node.ambienceId;
            }

            // 一次性剧情音效
            // SE：只播放一次，不写进存档
            if (audioService != null &&
                !string.IsNullOrEmpty(node.seId))
            {
                audioService.PlaySe(
                    node.seId
                );
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

                    dialogueView.Show(node);
                    break;

                default:
                    Debug.LogError(
                        $"未知节点类型：{node.type}"
                    );
                    break;
            }

            RegisterCompletedChapter(node);
            
            // 跨轮纪念解锁。
            // 只有节点明确填写 memorialId 才触发。
            if (!string.IsNullOrWhiteSpace(
                node.memorialId))
            {
                UnlockMemorial(
                    node.memorialId
                );
            }

            if (!string.IsNullOrWhiteSpace(
                node.collectionId))
            {
                UnlockCollection(
                    node.collectionId
                );
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

        public List<HistoryEntry> GetHistoryCopy()
        {
            if (state == null || state.history == null)
            {
                return new List<HistoryEntry>();
            }

            List<HistoryEntry> copy =
                new List<HistoryEntry>();

            foreach (HistoryEntry entry in state.history)
            {
                copy.Add(
                    new HistoryEntry
                    {
                        nodeId = entry.nodeId,
                        speaker = entry.speaker,
                        text = entry.text
                    }
                );
            }

            return copy;
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

                    cgId =
                        saved.visuals.cgId,

                    leftPortraitId =
                        saved.visuals.leftPortraitId,

                    centerPortraitId =
                        saved.visuals.centerPortraitId,

                    rightPortraitId =
                        saved.visuals.rightPortraitId,
                    
                    focusSlot =
                        saved.visuals.focusSlot,

                    bgmId =
                        saved.visuals.bgmId,

                    ambienceId =
                        saved.visuals.ambienceId
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

            if (audioService != null &&
                !string.IsNullOrEmpty(
                    state.visuals.bgmId))
            {
                audioService.ApplyBgm(
                    state.visuals.bgmId
                );
            }

            if (audioService != null &&
                !string.IsNullOrEmpty(
                    state.visuals.ambienceId))
            {
                audioService.ApplyAmbience(
                    state.visuals.ambienceId
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

        public List<string> GetCompletedChaptersCopy()
        {
            if (state == null ||
                state.completedChapters == null)
            {
                return new List<string>();
            }

            return new List<string>(
                state.completedChapters
            );
        }

        public bool IsMemorialUnlocked(
            string memorialId)
        {
            if (string.IsNullOrWhiteSpace(
                memorialId))
            {
                return false;
            }

            EnsureProfileLoaded();

            return profile.memorials.Contains(
                memorialId
            );
        }

        public List<string> GetMemorialsCopy()
        {
            EnsureProfileLoaded();

            return new List<string>(
                profile.memorials
            );
        }

        private void ApplyPortraitToState(
            string portraitId,
            int slot)
        {
            if (state == null ||
                state.visuals == null)
            {
                return;
            }

            // "-" = 清空全部槽位
            if (portraitId == "-")
            {
                state.visuals.leftPortraitId = "";
                state.visuals.centerPortraitId = "";
                state.visuals.rightPortraitId = "";

                return;
            }

            // 防止错误槽位
            if (slot < 0 || slot > 2)
            {
                slot = 1;
            }

            // "@clear" = 清空指定槽位
            string value =
                portraitId == "@clear"
                ? ""
                : portraitId;

            switch (slot)
            {
                case 0:
                    state.visuals.leftPortraitId =
                        value;
                    break;

                case 1:
                    state.visuals.centerPortraitId =
                        value;
                    break;

                case 2:
                    state.visuals.rightPortraitId =
                        value;
                    break;
            }
        }
    }
}