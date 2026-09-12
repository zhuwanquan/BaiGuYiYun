using System;
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

        private StoryRepository repository;
        private GameState state;

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

            StartStory();
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

        public void StartStory()
        {
            state = new GameState();

            observationView.Hide();

            dialogueView.gameObject.SetActive(true);
            dialogueView.nextButton.interactable = true;

            GoTo(repository.StartNode);
        }

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

            if (dialogueView.IsTyping)
            {
                dialogueView.CompleteTyping();
                return;
            }

            VNNode current =
                repository.Get(state.currentNodeId);

            if (current.type == "end")
            {
                dialogueView.nextButton.interactable = false;
                Debug.Log("测试剧情已经结束。");
                return;
            }

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

        private void GoTo(string id)
        {
            VNNode node = repository.Get(id);

            state.currentNodeId = node.id;
            state.pageIndex = 0;

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
        }

        public void SetMenuPaused(bool paused)
        {
            menuPaused = paused;
        }
    }
}