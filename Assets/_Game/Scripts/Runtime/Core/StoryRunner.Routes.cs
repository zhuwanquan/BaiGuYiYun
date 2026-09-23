using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BaiguVN
{
    public partial class StoryRunner
    {
        private ChoiceView choiceView;
        private RouteResourceService routeResources;
        private RouteActionPlayer actionPlayer;
        private bool stableNode = true;
        private bool lastLoadRecovered;
        private bool suppressAutoSave;
        private bool recoveryPromptActive;
        public bool CanSave => state != null && stableNode;
        public bool LastSaveSucceeded { get; private set; }
        public string CurrentRouteId => state?.routeId;
        public bool RunCompleted => state != null && state.runCompleted;
        private bool CanRecordRewards => state != null && (string.IsNullOrEmpty(state.routeId) || RouteKinds.CanRecordRewards(state.routeKind));

        private void LoadRouteContent()
        {
            var indexAsset = Resources.Load<TextAsset>("Routes/index");
            var index = indexAsset != null ? JsonUtility.FromJson<VNRouteIndex>(indexAsset.text) : null;
            var candidate = new StoryRepository();
            candidate.Load(storyJson, index);
            repository = candidate;
        }

        private void InitializeRoutePresentation()
        {
            routeResources = new RouteResourceService();
            var shared = Resources.Load<RouteResourceCatalog>("Routes/shared");
            if (shared != null) routeResources.SetShared(shared);
            presentation?.SetResourceService(routeResources);
            audioService?.SetResourceService(routeResources);
            choiceView = ChoiceView.Create(dialogueView.transform.parent, dialogueView.bodyText.font);
            choiceView.ControlsAbove = dialogueView.transform.parent.Find("Toolbar");
            actionPlayer = gameObject.AddComponent<RouteActionPlayer>();
            actionPlayer.Configure(presentation, routeResources);
            foreach (var node in repository.Nodes.Where(n => repository.IsPrologue(n.id)))
                if (!routeResources.ValidateIds(StoryAssetReferences.ForNode(node), out var error))
                    throw new InvalidOperationException("共同开场资源不完整：" + error);
        }

        private void ShowChoices(VNNode node)
        {
            observationView?.Hide();
            dialogueView.gameObject.SetActive(false);
            choiceView.Show(node.text, repository.GetChoices(node), SelectChoice);
            choiceView.SetInteractable(stableNode && !menuPaused);
        }

        public void SelectChoice(string choiceId)
        {
            if (!stableNode || menuPaused || state == null) return;
            var node = repository.Get(state.currentNodeId);
            if (node.type != "choice") return;
            var selected = repository.GetChoices(node).FirstOrDefault(c => c.id == choiceId);
            if (selected == null) { ShowChoices(node); return; }
            if (!string.IsNullOrEmpty(node.choiceGroupId))
            {
                var route = repository.GetRoute(selected.id);
                if (!TryPrepareRoute(route, out var error))
                {
                    choiceView.ShowMessage("这条剧情暂时无法进入：" + error, "重新选择", () => ShowChoices(node));
                    return;
                }
                state.checkpoint = state.ToRunSnapshot();
                state.routeId = route.routeId; state.routeKind = route.kind; state.routeRevision = route.contentRevision;
                routeResources.CommitPrepared();
            }
            state.history.Add(new HistoryEntry { nodeId = node.id, speaker = "孙悟空", text = "【选择】" + selected.label });
            choiceView.Hide();
            GoTo(selected.next);
        }

        private bool TryPrepareRoute(VNRoute route, out string error)
        {
            error = null;
            if (route == null) { error = "路线已被移除或尚未发布。"; return false; }
            if (!routeResources.TryPrepare(route.resourceCatalogPath, out error)) return false;
            var refs = new List<KeyValuePair<string, string>>();
            foreach (var asset in route.requiredAssets ?? Array.Empty<VNAssetRequirement>())
            {
                if (asset == null) { routeResources.CancelPrepared(); error = "资源声明为空。"; return false; }
                refs.Add(new KeyValuePair<string, string>(asset.id, asset.kind));
            }
            var visited = new HashSet<string>(); var pending = new Stack<string>(); pending.Push(route.entryNode);
            while (pending.Count > 0)
            {
                string id = pending.Pop(); if (!visited.Add(id)) continue;
                var node = repository.Get(id); refs.AddRange(StoryAssetReferences.ForNode(node));
                foreach (string next in repository.Targets(node)) pending.Push(next);
            }
            if (routeResources.ValidateIds(refs, out error, true)) return true;
            routeResources.CancelPrepared(); return false;
        }

        private IEnumerator FinishNodePresentation(VNNode node)
        {
            while (presentation != null && presentation.IsBusy) yield return null;
            if (node.actions != null && node.actions.Length > 0)
            {
                var execution = actionPlayer.Play(node.actions, state.visuals);
                while (true)
                {
                    bool more; object frame = null; Exception failure = null;
                    try { more = execution.MoveNext(); if (more) frame = execution.Current; }
                    catch (Exception ex) { more = false; failure = ex; }
                    if (failure != null)
                    {
                        (execution as IDisposable)?.Dispose(); actionPlayer.Cancel();
                        Debug.LogException(failure);
                        recoveryPromptActive = true;
                        choiceView.ShowMessage("这段演出暂时无法完成。可以回到选择前重新选择。", "返回选择前", () =>
                        {
                            var recovery = state.checkpoint != null ? GameState.FromRunSnapshot(state.checkpoint) : new GameState { currentNodeId = repository.StartNode };
                            RestoreValidated(recovery.ToSnapshot(repository.ContentVersion));
                        });
                        yield break;
                    }
                    if (!more) break;
                    yield return frame;
                }
                (execution as IDisposable)?.Dispose();
            }
            while (menuPaused) yield return null;
            stableNode = true;
            CommitStableNode(node);
            if (node.type == "choice" && !string.IsNullOrEmpty(node.choiceGroupId))
                state.checkpoint = state.ToRunSnapshot();
            choiceView.SetInteractable(!menuPaused);
            AutoSaveCurrent();
        }

        private bool RestoreValidated(VNSnapshot saved)
        {
            lastLoadRecovered = false;
            if (saved == null || repository == null) return false;
            GameState candidate = GameState.FromSnapshot(saved);
            string notice = null;
            if (saved.migratedFromV2)
            {
                candidate.currentNodeId = LegacyNode(candidate.currentNodeId);
                if (!repository.IsPrologue(candidate.currentNodeId))
                {
                    var original = repository.Routes.FirstOrDefault(r => r.kind == RouteKinds.Canonical);
                    candidate.routeId = original?.routeId; candidate.routeKind = original?.kind;
                    candidate.routeRevision = original?.contentRevision ?? 0;
                }
            }
            if (!TryPrepareState(candidate, out var error))
            {
                lastLoadRecovered = true;
                routeResources.CancelPrepared();
                var checkpoint = candidate.checkpoint;
                if (checkpoint != null && checkpoint.currentNodeId == repository.FirstChoiceNodeId && string.IsNullOrEmpty(checkpoint.routeId))
                {
                    candidate = GameState.FromRunSnapshot(checkpoint);
                    candidate.checkpoint = GameState.CloneRunSnapshot(checkpoint);
                    if (!TryPrepareState(candidate, out var checkpointError))
                    {
                        Debug.LogWarning("选择前检查点也已不兼容：" + checkpointError);
                        candidate = null;
                    }
                }
                else candidate = null;
                if (candidate == null)
                {
                    candidate = new GameState { currentNodeId = repository.StartNode };
                    if (!TryPrepareState(candidate, out var startError))
                    { Debug.LogError("无法恢复开场：" + startError); return false; }
                    notice = "存档对应的剧情已调整，且没有可用的选择前检查点。已回到故事开场。原存档仍保留。";
                }
                else notice = "存档对应的路线、节点或美术资源已调整。已恢复到选择前的完整状态，请重新选择。原存档仍保留。";
                Debug.LogWarning(error);
            }
            StopAllCoroutines(); actionPlayer.Cancel(); choiceView.Hide();
            routeResources.CommitPrepared();
            state = candidate; stableNode = true; recoveryPromptActive = false; SetMenuPaused(false);
            presentation?.ResetForNewGame();
            presentation?.ApplySnapshot(state.visuals);
            actionPlayer.RestoreProps(state.visuals);
            if (audioService != null)
            {
                audioService.StopBgm(); audioService.StopAmbience(); audioService.StopSe();
                audioService.ApplyBgm(state.visuals.bgmId); audioService.ApplyAmbience(state.visuals.ambienceId);
            }
            RenderRestoredNode();
            if (notice != null) choiceView.ShowMessage(notice, "继续", () => { choiceView.Hide(); RenderRestoredNode(); });
            // Loading never writes a migrated or recovered save. The next explicit progress does.
            return true;
        }

        private bool TryPrepareState(GameState candidate, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(candidate.routeId))
            {
                if (!repository.IsPrologue(candidate.currentNodeId)) { error = "未选择路线的存档已离开共同开场。"; return false; }
                if (!routeResources.TryPrepare(null, out error)) return false;
            }
            else
            {
                var route = repository.GetRoute(candidate.routeId);
                if (route == null || !repository.IsNodeAvailable(route.routeId, candidate.currentNodeId))
                { error = "存档路线或节点不存在。"; return false; }
                if (!TryPrepareRoute(route, out error)) return false;
                candidate.routeKind = route.kind; candidate.routeRevision = route.contentRevision;
            }
            if (!routeResources.ValidateIds(StoryAssetReferences.ForVisuals(candidate.visuals), out error, true))
            { routeResources.CancelPrepared(); return false; }
            return true;
        }

        private static string LegacyNode(string id)
        {
            switch (id)
            {
                case "ch27_s04_i01": case "ch27_s04_i01_jar": case "ch27_s04_i01_bottle": return "ch27_s04_014";
                case "ch27_s09_i02": case "ch27_s09_i02_spine": case "ch27_s09_i02_tangseng": return "ch27_s09_012";
                default: return id;
            }
        }
    }
}
