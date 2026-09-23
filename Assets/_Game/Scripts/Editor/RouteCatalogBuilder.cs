using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BaiguVN.Editor
{
    [InitializeOnLoad]
    public static class RouteCatalogBuilder
    {
        public const string SourceRoot = "Assets/_Game/Data/Routes";
        public const string StoryPath = "Assets/_Game/Data/Story/baigu_story_v2.json";
        public const string OutputRoot = "Assets/Resources/Routes";
        public const string IndexPath = OutputRoot + "/index.json";
        public const string SharedPath = OutputRoot + "/shared.asset";
        private const string ManifestPath = OutputRoot + "/generated-files.json";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private static readonly HashSet<string> WatchedAssets = new HashSet<string>(StringComparer.Ordinal);
        private static bool rebuilding;
        private static bool queued;

        [Serializable]
        private sealed class GeneratedFiles
        {
            public int schemaVersion = 1;
            public string[] catalogPaths = Array.Empty<string>();
        }
        private sealed class BuildPlan
        {
            public VNRouteIndex index;
            public readonly Dictionary<string, RouteResourceCatalog> catalogs = new Dictionary<string, RouteResourceCatalog>(StringComparer.Ordinal);
            public readonly HashSet<string> dependencies = new HashSet<string>(StringComparer.Ordinal);
        }

        static RouteCatalogBuilder()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            QueueRebuild();
        }
        [MenuItem("BaiguVN/Routes/Rebuild Published Catalog")]
        private static void RebuildMenu()
        {
            if (Rebuild(out string error)) Debug.Log("路线菜单与资源目录已同步；草稿没有加入正式选项。");
            else Debug.LogError(error);
        }
        public static void RebuildOrThrow()
        {
            if (!Rebuild(out string error)) throw new BuildFailedException(error);
        }
        public static bool Rebuild(out string error)
        {
            if (rebuilding) { error = "路线目录正在生成，请等待本次更新完成。"; return false; }
            rebuilding = true;
            try
            {
                BuildPlan plan = ValidateSources();
                WriteOutputs(plan);
                WatchedAssets.Clear(); WatchedAssets.UnionWith(plan.dependencies);
                error = null; return true;
            }
            catch (Exception ex) { error = "路线目录未发布：" + ex.Message; return false; }
            finally { rebuilding = false; }
        }

        private static BuildPlan ValidateSources()
        {
            Require(Directory.Exists(SourceRoot), "找不到路线源目录：" + SourceRoot);
            Require(File.Exists(StoryPath), "找不到主剧本：" + StoryPath);
            VNStory story = ReadJson<VNStory>(StoryPath);
            var published = new List<VNRoute>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var plan = new BuildPlan();
            RouteResourceCatalog shared = AssetDatabase.LoadAssetAtPath<RouteResourceCatalog>(SharedPath);
            Require(RouteResourceCatalog.Validate(shared, out string sharedError, false), SharedPath + "：" + sharedError);
            var common = MakeResourceIndex(shared);
            AddDependencies(plan, SharedPath);

            foreach (string folder in Directory.GetDirectories(SourceRoot).OrderBy(p => p, StringComparer.Ordinal))
            {
                string routePath = Normalize(folder) + "/route.json";
                if (!File.Exists(routePath)) continue;
                VNRoute route = ReadJson<VNRoute>(routePath);
                Require(route != null, "无法解析路线：" + routePath);
                Require(IsRouteId(route.routeId), "路线 ID 须以小写字母开头，只含小写字母、数字、下划线或连字符：" + routePath);
                Require(Path.GetFileName(folder) == route.routeId, "目录名必须与 routeId 一致：" + routePath);
                Require(ids.Add(route.routeId), "路线 ID 重复：" + route.routeId);
                Require(route.status == "draft" || route.status == "disabled" || route.status == "published", "路线状态必须是 draft、disabled 或 published：" + route.routeId);
                if (route.status != "published") continue;

                Require(route.artReviewed, "美术尚未确认完成：" + route.routeId);
                string catalogPath = Normalize(folder) + "/resource-catalog.asset";
                RouteResourceCatalog local = AssetDatabase.LoadAssetAtPath<RouteResourceCatalog>(catalogPath);
                Require(RouteResourceCatalog.Validate(local, out string localError, false), route.routeId + "：" + localError);
                var available = new Dictionary<string, RouteResourceEntry>(common, StringComparer.Ordinal);
                foreach (RouteResourceEntry entry in local.entries)
                {
                    Require(!available.ContainsKey(entry.id), "共享资源与路线资源 ID 冲突：" + route.routeId + "/" + entry.id);
                    available.Add(entry.id, entry);
                }
                var requirements = new HashSet<string>(StringComparer.Ordinal);
                foreach (VNAssetRequirement requirement in route.requiredAssets ?? Array.Empty<VNAssetRequirement>())
                {
                    Require(requirement != null && requirements.Add(requirement.id ?? ""), "资源要求为空或重复：" + route.routeId);
                    ValidateReference(available, requirement.id, requirement.kind, route.routeId + " 的 requiredAssets");
                }
                foreach (VNNode node in route.nodes ?? Array.Empty<VNNode>())
                {
                    Require(node != null, "路线中存在空剧情节点：" + route.routeId);
                    ValidateNodeReferences(node, available, route.routeId);
                }
                route.resourceCatalogPath = "Routes/" + route.routeId;
                published.Add(route);
                plan.catalogs.Add(OutputRoot + "/" + route.routeId + ".asset", local);
                AddDependencies(plan, catalogPath);
            }

            Require(published.Count(r => r.kind == RouteKinds.Canonical) <= 1, "只能发布一条原著路线。");
            Require(published.Count(r => r.kind == RouteKinds.Perfect) <= 1, "只能发布一条完美路线。");
            plan.index = new VNRouteIndex { routes = published.OrderBy(r => r.order).ThenBy(r => r.routeId, StringComparer.Ordinal).ToArray() };
            var repository = new StoryRepository();
            repository.Load(story, plan.index);
            foreach (VNNode node in story.nodes) ValidateNodeReferences(node, common, "主剧本");
            // Repository checks completion; also reject private orphan nodes.
            foreach (VNRoute route in published)
            {
                var reachable = new HashSet<string>(StringComparer.Ordinal);
                var pending = new Stack<string>(); pending.Push(route.entryNode);
                while (pending.Count > 0)
                {
                    string id = pending.Pop(); if (!reachable.Add(id)) continue;
                    foreach (string next in repository.Targets(repository.Get(id))) pending.Push(next);
                }
                foreach (VNNode node in route.nodes ?? Array.Empty<VNNode>())
                    Require(reachable.Contains(node.id), "正式路线有无法从入口到达的节点：" + route.routeId + "/" + node.id);
            }
            return plan;
        }
        private static Dictionary<string, RouteResourceEntry> MakeResourceIndex(RouteResourceCatalog catalog)
        {
            var index = new Dictionary<string, RouteResourceEntry>(StringComparer.Ordinal);
            foreach (RouteResourceEntry entry in catalog.entries) index.Add(entry.id, entry);
            return index;
        }
        private static void ValidateNodeReferences(VNNode node, Dictionary<string, RouteResourceEntry> available, string context)
        {
            foreach (KeyValuePair<string, string> reference in StoryAssetReferences.ForNode(node))
                ValidateReference(available, reference.Key, reference.Value, context + "/" + node.id);
        }
        private static void ValidateReference(Dictionary<string, RouteResourceEntry> available, string id, string kind, string context)
        {
            Require(!string.IsNullOrWhiteSpace(id) && available.TryGetValue(id, out RouteResourceEntry entry) && entry.kind == kind,
                context + " 缺少匹配的资源：" + id + " (" + kind + ")");
        }
        private static void AddDependencies(BuildPlan plan, string catalogPath)
        {
            plan.dependencies.Add(catalogPath);
            foreach (string dependency in AssetDatabase.GetDependencies(catalogPath, true)) plan.dependencies.Add(dependency);
        }

        private static void WriteOutputs(BuildPlan plan)
        {
            EnsureAssetFolder(OutputRoot);
            GeneratedFiles previous = File.Exists(ManifestPath) ? ReadJson<GeneratedFiles>(ManifestPath) : new GeneratedFiles();
            Require(previous != null && previous.schemaVersion == 1 && previous.catalogPaths != null, "生成文件记录无效：" + ManifestPath);
            var tracked = new HashSet<string>(previous.catalogPaths, StringComparer.Ordinal);
            foreach (string path in tracked) Require(IsGeneratedCatalogPath(path), "拒绝清理非生成目录文件：" + path);
            foreach (string path in plan.catalogs.Keys)
                Require(!File.Exists(path) || tracked.Contains(path), "生成目标已存在且不在生成记录中，拒绝覆盖：" + path);

            byte[] oldIndex = File.Exists(IndexPath) ? File.ReadAllBytes(IndexPath) : null;
            byte[] oldManifest = File.Exists(ManifestPath) ? File.ReadAllBytes(ManifestPath) : null;
            var backups = new Dictionary<string, RouteResourceCatalog>(StringComparer.Ordinal);
            var created = new List<string>();
            try
            {
                foreach (KeyValuePair<string, RouteResourceCatalog> pair in plan.catalogs)
                {
                    RouteResourceCatalog target = AssetDatabase.LoadAssetAtPath<RouteResourceCatalog>(pair.Key);
                    if (File.Exists(pair.Key))
                    {
                        Require(target != null, "生成目标不是路线资源目录：" + pair.Key);
                        backups.Add(pair.Key, UnityEngine.Object.Instantiate(target));
                        EditorUtility.CopySerialized(pair.Value, target);
                        target.name = Path.GetFileNameWithoutExtension(pair.Key);
                        EditorUtility.SetDirty(target);
                    }
                    else
                    {
                        target = UnityEngine.Object.Instantiate(pair.Value);
                        target.name = Path.GetFileNameWithoutExtension(pair.Key);
                        AssetDatabase.CreateAsset(target, pair.Key);
                        created.Add(pair.Key);
                    }
                }
                AssetDatabase.SaveAssets();
                // Commit the complete index after every resource catalog succeeds.
                WriteTextIfChanged(IndexPath, JsonUtility.ToJson(plan.index, true));
                tracked.UnionWith(plan.catalogs.Keys);
                WriteManifest(tracked);
            }
            catch
            {
                foreach (KeyValuePair<string, RouteResourceCatalog> pair in backups)
                {
                    RouteResourceCatalog target = AssetDatabase.LoadAssetAtPath<RouteResourceCatalog>(pair.Key);
                    if (target != null) { EditorUtility.CopySerialized(pair.Value, target); EditorUtility.SetDirty(target); }
                }
                foreach (string path in created) DeleteGeneratedCatalog(path);
                RestoreGeneratedJson(IndexPath, oldIndex); RestoreGeneratedJson(ManifestPath, oldManifest);
                AssetDatabase.SaveAssets();
                throw;
            }
            finally { foreach (RouteResourceCatalog backup in backups.Values) UnityEngine.Object.DestroyImmediate(backup); }

            // No source art or shared catalog may be removed by this cleanup.
            foreach (string path in tracked.ToArray())
            {
                if (plan.catalogs.ContainsKey(path)) continue;
                if (DeleteGeneratedCatalog(path)) tracked.Remove(path);
                else Debug.LogWarning("路线已从选项移除，但旧生成目录暂未清理：" + path);
            }
            WriteManifest(tracked);
            AssetDatabase.ImportAsset(IndexPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ManifestPath, ImportAssetOptions.ForceUpdate);
        }
        private static bool DeleteGeneratedCatalog(string path)
        {
            Require(IsGeneratedCatalogPath(path), "拒绝删除非生成资源文件：" + path);
            if (!File.Exists(path)) return true;
            if (AssetDatabase.LoadAssetAtPath<RouteResourceCatalog>(path) == null) return false;
            return AssetDatabase.DeleteAsset(path);
        }
        private static void RestoreGeneratedJson(string path, byte[] previous)
        {
            Require(path == IndexPath || path == ManifestPath, "拒绝恢复未知生成文件。");
            if (previous != null) File.WriteAllBytes(path, previous);
            else if (File.Exists(path))
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
                else File.Delete(path);
            }
        }
        private static void WriteManifest(IEnumerable<string> paths)
        {
            WriteTextIfChanged(ManifestPath, JsonUtility.ToJson(new GeneratedFiles
                { catalogPaths = paths.OrderBy(p => p, StringComparer.Ordinal).ToArray() }, true));
        }
        private static void WriteTextIfChanged(string path, string text)
        {
            string complete = text + "\n";
            if (!File.Exists(path) || File.ReadAllText(path, Utf8) != complete) File.WriteAllText(path, complete, Utf8);
        }
        private static bool IsGeneratedCatalogPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path == Normalize(path)
                && Path.GetDirectoryName(path)?.Replace('\\', '/') == OutputRoot
                && path.EndsWith(".asset", StringComparison.Ordinal)
                && IsRouteId(Path.GetFileNameWithoutExtension(path));
        }
        private static bool IsRouteId(string id)
        {
            return id != null && Regex.IsMatch(id, "^[a-z][a-z0-9_-]*$")
                && id != "shared" && id != "index" && id != "generated-files";
        }
        private static T ReadJson<T>(string path) where T : class
        {
            try { return JsonUtility.FromJson<T>(File.ReadAllText(path, Utf8)); }
            catch (Exception ex) { throw new InvalidOperationException(path + " 解析失败：" + ex.Message, ex); }
        }
        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Normalize(Path.GetDirectoryName(path));
            Require(!string.IsNullOrEmpty(parent) && path.StartsWith("Assets/", StringComparison.Ordinal), "无效输出目录。");
            EnsureAssetFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        internal static void NotifyAssetsChanged(IEnumerable<string> paths)
        {
            if (rebuilding) return;
            if (paths.Any(p => p == StoryPath || p == SharedPath || p == SourceRoot
                || p.StartsWith(SourceRoot + "/", StringComparison.Ordinal) || WatchedAssets.Contains(p))) QueueRebuild();
        }
        private static void QueueRebuild()
        {
            if (queued) return;
            queued = true; EditorApplication.delayCall += RebuildWhenReady;
        }
        private static void RebuildWhenReady()
        {
            queued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { QueueRebuild(); return; }
            if (!Rebuild(out string error)) Debug.LogError(error);
        }
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && !Rebuild(out string error))
            {
                EditorApplication.isPlaying = false;
                Debug.LogError(error + " 已阻止使用过期目录进入运行模式。");
            }
            else if (state == PlayModeStateChange.EnteredEditMode) QueueRebuild();
        }
        private static string Normalize(string path) => path?.Replace('\\', '/');
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }

    public sealed class RouteCatalogAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            => RouteCatalogBuilder.NotifyAssetsChanged(imported.Concat(deleted).Concat(moved).Concat(movedFrom));
    }
    public sealed class RouteCatalogBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => RouteCatalogBuilder.RebuildOrThrow();
    }
}
