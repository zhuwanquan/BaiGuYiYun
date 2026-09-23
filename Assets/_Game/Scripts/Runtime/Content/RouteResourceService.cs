using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaiguVN
{
    // Preparing does not change resources used by the current screen. Commit
    // only after story and resource validation both succeed.
    public class RouteResourceService : IDisposable
    {
        private RouteResourceCatalog sharedCatalog;
        private RouteResourceCatalog activeCatalog;
        private RouteResourceCatalog preparedCatalog;
        private Dictionary<string, RouteResourceEntry> shared = NewIndex();
        private Dictionary<string, RouteResourceEntry> active = NewIndex();
        private Dictionary<string, RouteResourceEntry> prepared;
        private bool hasPrepared;

        public void SetShared(RouteResourceCatalog catalog)
        {
            Dictionary<string, RouteResourceEntry> candidate = NewIndex();
            if (catalog != null)
            {
                if (!RouteResourceCatalog.Validate(catalog, out string error))
                {
                    throw new ArgumentException(error, nameof(catalog));
                }
                candidate = BuildIndex(catalog);
            }

            if (!ValidateNoConflict(candidate, active, out string conflict) ||
                (hasPrepared && !ValidateNoConflict(candidate, prepared, out conflict)))
            {
                throw new ArgumentException(conflict, nameof(catalog));
            }

            sharedCatalog = catalog;
            shared = candidate;
        }

        public bool TryPrepare(string resourcesPath, out string error)
        {
            CancelPrepared();
            error = null;
            RouteResourceCatalog candidateCatalog = null;
            Dictionary<string, RouteResourceEntry> candidate = NewIndex();

            if (!string.IsNullOrEmpty(resourcesPath))
            {
                if (string.IsNullOrWhiteSpace(resourcesPath) ||
                    resourcesPath != resourcesPath.Trim() ||
                    resourcesPath.Contains("\\") ||
                    resourcesPath.StartsWith("/", StringComparison.Ordinal) ||
                    resourcesPath.Contains(":") ||
                    resourcesPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    error = "路线资源路径必须是 Resources 内不带扩展名的相对路径。";
                    return false;
                }

                try
                {
                    candidateCatalog = Resources.Load<RouteResourceCatalog>(resourcesPath);
                }
                catch (Exception ex)
                {
                    error = $"无法加载路线资源目录 {resourcesPath}：{ex.Message}";
                    return false;
                }

                if (!RouteResourceCatalog.Validate(candidateCatalog, out error))
                {
                    error = $"路线资源目录 {resourcesPath}：{error}";
                    return false;
                }
                candidate = BuildIndex(candidateCatalog);
            }

            if (!ValidateNoConflict(shared, candidate, out error))
            {
                return false;
            }

            preparedCatalog = candidateCatalog;
            prepared = candidate;
            hasPrepared = true;
            return true;
        }

        public void CommitPrepared()
        {
            if (!hasPrepared)
            {
                throw new InvalidOperationException("尚未成功准备路线资源，不能切换。");
            }
            activeCatalog = preparedCatalog;
            active = prepared;
            preparedCatalog = null;
            prepared = null;
            hasPrepared = false;
        }

        public void CancelPrepared()
        {
            preparedCatalog = null;
            prepared = null;
            hasPrepared = false;
        }

        public Sprite GetSprite(string id, string kind)
        {
            if (!RouteResourceCatalog.IsSpriteKind(kind)) return null;
            RouteResourceEntry entry = Find(id, kind, active);
            return entry != null ? entry.sprite : null;
        }

        public AudioClip GetAudio(string id, string kind)
        {
            if (!RouteResourceCatalog.IsAudioKind(kind)) return null;
            RouteResourceEntry entry = Find(id, kind, active);
            return entry != null ? entry.clip : null;
        }

        public bool Has(string id, string kind)
        {
            return HasReference(Find(id, kind, active));
        }

        // Each pair is (business ID, kind). Pending validation checks shared +
        // pending only: an old route must never satisfy a new route's references.
        public bool ValidateIds(
            IEnumerable<KeyValuePair<string, string>> refs,
            out string error,
            bool includePrepared = false)
        {
            error = null;
            if (refs == null)
            {
                error = "待校验的资源引用列表不能为空。";
                return false;
            }
            if (includePrepared && !hasPrepared)
            {
                error = "尚未成功准备路线资源。";
                return false;
            }

            Dictionary<string, RouteResourceEntry> route = includePrepared ? prepared : active;
            foreach (KeyValuePair<string, string> reference in refs)
            {
                if (!HasReference(Find(reference.Key, reference.Value, route)))
                {
                    error = $"当前路线与共享目录中不存在可用资源：{reference.Key} ({reference.Value})。";
                    return false;
                }
            }
            return true;
        }

        public void ClearRoute()
        {
            CancelPrepared();
            activeCatalog = null;
            active = NewIndex();
        }

        public void Dispose()
        {
            ClearRoute();
            sharedCatalog = null;
            shared = NewIndex();
            // Drop our references only. Presentation/audio can still use these
            // assets, so unloading here would invalidate the live screen.
        }

        private RouteResourceEntry Find(
            string id, string kind, Dictionary<string, RouteResourceEntry> route)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(kind)) return null;
            if (route != null && route.TryGetValue(id, out RouteResourceEntry own))
            {
                return own.kind == kind ? own : null;
            }
            if (shared.TryGetValue(id, out RouteResourceEntry common))
            {
                return common.kind == kind ? common : null;
            }
            return null;
        }

        private static bool HasReference(RouteResourceEntry entry)
        {
            if (entry == null || entry.placeholder) return false;
            return RouteResourceCatalog.IsSpriteKind(entry.kind)
                ? entry.sprite != null
                : RouteResourceCatalog.IsAudioKind(entry.kind) && entry.clip != null;
        }

        private static Dictionary<string, RouteResourceEntry> NewIndex()
        {
            return new Dictionary<string, RouteResourceEntry>(StringComparer.Ordinal);
        }

        private static Dictionary<string, RouteResourceEntry> BuildIndex(RouteResourceCatalog catalog)
        {
            Dictionary<string, RouteResourceEntry> result = NewIndex();
            foreach (RouteResourceEntry entry in catalog.entries)
            {
                // Asset edits must not mutate active lookup keys/types mid-route.
                result.Add(entry.id, new RouteResourceEntry
                {
                    id = entry.id,
                    kind = entry.kind,
                    sprite = entry.sprite,
                    clip = entry.clip,
                    placeholder = entry.placeholder
                });
            }
            return result;
        }

        private static bool ValidateNoConflict(
            Dictionary<string, RouteResourceEntry> common,
            Dictionary<string, RouteResourceEntry> route,
            out string error)
        {
            foreach (string id in route.Keys)
            {
                if (common.ContainsKey(id))
                {
                    error = $"共享资源和路线私有资源的 ID 冲突：{id}。";
                    return false;
                }
            }
            error = null;
            return true;
        }
    }
}
