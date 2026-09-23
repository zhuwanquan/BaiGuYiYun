using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BaiguVN
{
    // Main-story IDs are shared; route-private IDs may never be entered by another route.
    public class StoryRepository
    {
        private VNStory story;
        private readonly Dictionary<string, VNNode> nodes = new Dictionary<string, VNNode>();
        private readonly Dictionary<string, VNRoute> routes = new Dictionary<string, VNRoute>();
        private readonly Dictionary<string, string> owners = new Dictionary<string, string>();
        private readonly HashSet<string> prologue = new HashSet<string>();
        public string ContentVersion => story?.contentVersion;
        public string StartNode => story?.startNode;
        public string FirstChoiceNodeId { get; private set; }
        public IEnumerable<VNNode> Nodes => nodes.Values;
        public IEnumerable<VNRoute> Routes => routes.Values;

        public void Load(TextAsset asset, VNRouteIndex routeIndex = null)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            Load(JsonUtility.FromJson<VNStory>(asset.text), routeIndex);
        }

        public void Load(VNStory source, VNRouteIndex routeIndex = null)
        {
            nodes.Clear(); routes.Clear(); owners.Clear(); prologue.Clear(); FirstChoiceNodeId = null;
            story = source ?? throw new Exception("剧情 JSON 解析失败。");
            Require(story.schemaVersion == 2 || story.schemaVersion == 3, "不支持的剧情版本。");
            Require(!string.IsNullOrWhiteSpace(story.contentVersion), "contentVersion 不能为空。");
            Require(story.nodes != null && story.nodes.Length > 0, "剧情 nodes 不能为空。");
            AddNodes(story.nodes, null);
            if (routeIndex != null)
            {
                Require(routeIndex.schemaVersion == 1, "不支持的路线目录版本。");
                var ids = new HashSet<string>();
                foreach (var route in routeIndex.routes ?? Array.Empty<VNRoute>())
                {
                    Require(route != null && !string.IsNullOrWhiteSpace(route.routeId), "路线 ID 不能为空。");
                    Require(ids.Add(route.routeId), "重复路线 ID：" + route.routeId);
                    Require(route.status == "draft" || route.status == "disabled" || route.status == "published", "无效路线状态：" + route.routeId);
                    if (route.status != "published") continue;
                    Require(route.schemaVersion == 1 && RouteKinds.IsValid(route.kind), "无效路线版本或类型：" + route.routeId);
                    Require(route.contentRevision >= 1 && route.artReviewed, "发布路线必须确认美术并设置修订号：" + route.routeId);
                    Require(!string.IsNullOrWhiteSpace(route.title) && !string.IsNullOrWhiteSpace(route.choiceText)
                        && !string.IsNullOrWhiteSpace(route.choiceGroupId), "路线缺少标题、选项文字或选项组：" + route.routeId);
                    routes.Add(route.routeId, route);
                    AddNodes(route.nodes ?? Array.Empty<VNNode>(), route.routeId);
                }
            }
            Require(HasNode(story.startNode), "startNode 不存在：" + story.startNode);
            foreach (var node in nodes.Values) ValidateNode(node);
            foreach (var route in routes.Values)
            {
                Require(IsNodeAvailable(route.routeId, route.entryNode), "路线入口不存在或属于其他路线：" + route.routeId);
                Require(nodes.Values.Any(n => n.type == "choice" && n.choiceGroupId == route.choiceGroupId), "路线选项组不存在：" + route.routeId);
                ValidateCompletion(route.entryNode, route.routeId);
            }
            var pending = new Stack<string>(); pending.Push(StartNode);
            while (pending.Count > 0)
            {
                string id = pending.Pop();
                if (!prologue.Add(id)) continue;
                var node = Get(id);
                if (node.type == "choice" && !string.IsNullOrEmpty(node.choiceGroupId))
                {
                    Require(FirstChoiceNodeId == null || FirstChoiceNodeId == id, "共同开场只能有一个路线选择入口。");
                    FirstChoiceNodeId = id; continue;
                }
                foreach (var next in Targets(node)) pending.Push(next);
            }
            ValidateCompletion(StartNode, null);
        }

        private void AddNodes(IEnumerable<VNNode> items, string owner)
        {
            foreach (var node in items)
            {
                Require(node != null && !string.IsNullOrWhiteSpace(node.id), "存在空剧情节点或空 ID。");
                Require(!nodes.ContainsKey(node.id), "重复节点 ID：" + node.id);
                nodes.Add(node.id, node); owners.Add(node.id, owner);
            }
        }
        public VNNode Get(string id) => HasNode(id) ? nodes[id] : throw new KeyNotFoundException("找不到剧情节点：" + id);
        public bool HasNode(string id) => !string.IsNullOrEmpty(id) && nodes.ContainsKey(id);
        public VNRoute GetRoute(string id) => !string.IsNullOrEmpty(id) && routes.TryGetValue(id, out var route) ? route : null;
        public bool IsPrologue(string id) => id != null && prologue.Contains(id);
        public bool IsNodeAvailable(string routeId, string id) => HasNode(id) && (owners[id] == null || owners[id] == routeId);
        public List<VNChoice> GetChoices(VNNode node)
        {
            if (string.IsNullOrEmpty(node.choiceGroupId)) return new List<VNChoice>(node.choices ?? Array.Empty<VNChoice>());
            return routes.Values.Where(r => r.choiceGroupId == node.choiceGroupId).OrderBy(r => r.order)
                .ThenBy(r => r.routeId, StringComparer.Ordinal).Select(r => new VNChoice { id = r.routeId, label = r.choiceText, next = r.entryNode }).ToList();
        }
        public IEnumerable<string> Targets(VNNode node)
        {
            if (node.type == "line" || node.type == "pause") yield return node.next;
            if (node.type == "observe")
            {
                yield return node.resume;
                foreach (var obj in node.objects ?? Array.Empty<VNObserveObject>()) yield return obj?.next;
            }
            if (node.type == "choice") foreach (var choice in GetChoices(node)) yield return choice?.next;
        }
        private void ValidateNode(VNNode node)
        {
            Require(node.type == "line" || node.type == "pause" || node.type == "end" || node.type == "observe" || node.type == "choice", "无效节点类型：" + node.id);
            if (node.type == "end" || node.type == "choice") Require(string.IsNullOrEmpty(node.next), "结束/选择节点不能设置 next：" + node.id);
            if (node.type == "choice")
            {
                Require(string.IsNullOrEmpty(node.choiceGroupId) || node.choices == null || node.choices.Length == 0, "不能混用动态组和内置选项：" + node.id);
                var choices = GetChoices(node); var ids = new HashSet<string>();
                Require(choices.Count > 0, "选择节点没有可发布选项：" + node.id);
                foreach (var c in choices) Require(c != null && !string.IsNullOrWhiteSpace(c.id) && !string.IsNullOrWhiteSpace(c.label) && ids.Add(c.id), "选项文字/ID缺失或重复：" + node.id);
            }
            if (node.type == "observe")
            {
                Require(node.objects != null && node.objects.Length > 0, "观察对象不能为空：" + node.id);
                var ids = new HashSet<string>();
                foreach (var obj in node.objects) Require(obj != null && !string.IsNullOrWhiteSpace(obj.id) && !string.IsNullOrWhiteSpace(obj.label) && ids.Add(obj.id), "观察对象无效：" + node.id);
            }
            foreach (var target in Targets(node))
            {
                Require(HasNode(target), "节点目标不存在：" + node.id + " -> " + target);
                if (string.IsNullOrEmpty(node.choiceGroupId))
                    Require(owners[target] == null || owners[target] == owners[node.id], "不允许跨路线进入私有节点：" + node.id);
            }
            foreach (var action in node.actions ?? Array.Empty<VNVisualAction>()) ValidateAction(node.id, action);
        }
        private void ValidateCompletion(string start, string routeId)
        {
            var reachable = new HashSet<string>(); var pending = new Stack<string>(); pending.Push(start);
            while (pending.Count > 0)
            {
                string id = pending.Pop(); if (!reachable.Add(id)) continue;
                var node = Get(id);
                if (routeId != null)
                {
                    Require(IsNodeAvailable(routeId, id), "路线跨入其他路线：" + routeId);
                    Require(string.IsNullOrEmpty(node.choiceGroupId), "路线不能返回路线菜单：" + routeId);
                }
                foreach (var next in Targets(node)) pending.Push(next);
            }
            var completable = new HashSet<string>(reachable.Where(id => Get(id).type == "end"));
            bool changed;
            do
            {
                changed = false;
                foreach (string id in reachable)
                    if (!completable.Contains(id) && Targets(Get(id)).Any(completable.Contains)) { completable.Add(id); changed = true; }
            } while (changed);
            Require(reachable.All(completable.Contains), "剧情存在无法通关的节点：" + string.Join(", ", reachable.Except(completable).Take(6)));
        }
        private static void ValidateAction(string node, VNVisualAction action)
        {
            Require(action != null && Finite(action.duration) && action.duration >= 0 && action.duration <= 10, "演出时长无效：" + node);
            Require(new[] { "transform", "flash", "shake", "prop", "clearProp", "wait" }.Contains(action.type), "演出动作无效：" + node);
            if (action.type == "transform") Require(action.slot >= 0 && action.slot <= 2 && !string.IsNullOrWhiteSpace(action.assetId), "变身需指定立绘和槽位：" + node);
            if (action.type == "prop") Require(!string.IsNullOrWhiteSpace(action.assetId) && !string.IsNullOrWhiteSpace(action.instanceId) && Finite(action.scale) && action.scale > 0 && action.scale <= 10 && Finite(action.x) && Finite(action.y), "道具参数无效：" + node);
            if (action.type == "clearProp") Require(!string.IsNullOrWhiteSpace(action.instanceId), "清除道具需指定实例 ID：" + node);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    }
}
