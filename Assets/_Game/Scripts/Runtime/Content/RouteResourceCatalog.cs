using System;
using System.Collections.Generic;
using UnityEngine;

namespace BaiguVN
{
    [Serializable]
    public class RouteResourceEntry
    {
        public string id;
        public string kind;
        public Sprite sprite;
        public AudioClip clip;
        public bool placeholder;
    }

    [CreateAssetMenu(fileName = "RouteResources", menuName = "BaiguVN/Route Resource Catalog")]
    public class RouteResourceCatalog : ScriptableObject
    {
        public RouteResourceEntry[] entries = new RouteResourceEntry[0];

        public static bool IsSpriteKind(string kind)
        {
            return kind == "background" || kind == "portrait" ||
                kind == "cg" || kind == "prop" || kind == "effect";
        }

        public static bool IsAudioKind(string kind)
        {
            return kind == "bgm" || kind == "se" || kind == "ambience";
        }

        // Preview tools may explicitly allow marked placeholders. Published
        // catalogs always use the default strict validation.
        public static bool Validate(
            RouteResourceCatalog catalog, out string error, bool allowPlaceholders = false)
        {
            if (catalog == null)
            {
                error = "资源目录不存在。";
                return false;
            }

            return ValidateEntries(catalog.entries, out error, allowPlaceholders);
        }

        public static bool ValidateEntries(
            IEnumerable<RouteResourceEntry> entries,
            out string error,
            bool allowPlaceholders = false)
        {
            error = null;
            if (entries == null)
            {
                error = "资源目录的 entries 不能为空；无资源时请使用空数组。";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            int position = 0;
            foreach (RouteResourceEntry entry in entries)
            {
                position++;
                if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                {
                    error = $"资源目录第 {position} 项缺少业务 ID。";
                    return false;
                }

                if (entry.id != entry.id.Trim())
                {
                    error = $"资源 ID 首尾不能包含空白：'{entry.id}'。";
                    return false;
                }

                if (!ids.Add(entry.id))
                {
                    error = $"资源目录存在重复 ID：{entry.id}。";
                    return false;
                }

                bool spriteKind = IsSpriteKind(entry.kind);
                bool audioKind = IsAudioKind(entry.kind);
                if (!spriteKind && !audioKind)
                {
                    error = $"资源 {entry.id} 的类型无效：{entry.kind}。";
                    return false;
                }

                if ((spriteKind && entry.clip != null) || (audioKind && entry.sprite != null))
                {
                    error = $"资源 {entry.id} 包含与 {entry.kind} 类型不符的引用。";
                    return false;
                }

                if (entry.placeholder && !allowPlaceholders)
                {
                    error = $"资源 {entry.id} 仍是占位资源，不能发布。";
                    return false;
                }

                bool missing = spriteKind ? entry.sprite == null : entry.clip == null;
                if (missing && !(allowPlaceholders && entry.placeholder))
                {
                    error = $"资源 {entry.id} 缺少 {entry.kind} 资源引用。";
                    return false;
                }
            }

            return true;
        }
    }
}
