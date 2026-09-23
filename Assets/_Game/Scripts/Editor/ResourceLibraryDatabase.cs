#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BaiguVN.Editor
{
    [Serializable]
    public sealed class ResourceLibraryRecord
    {
        [SerializeField] private string id;
        [SerializeField] private string scope;
        [SerializeField] private string kind;
        [SerializeField] private string subject;
        [SerializeField] private string variant;
        [SerializeField] private string path;
        [SerializeField] private string guid;
        [SerializeField] private bool isValid;

        public string Id => id;
        public string Scope => scope;
        public string Kind => kind;
        public string Subject => subject;
        public string Variant => variant;
        public string Path => path;
        public string Guid => guid;
        public bool IsValid => isValid;

        internal ResourceLibraryRecord(string assetPath, string assetGuid, ResourceName name,
            string inferredScope, string inferredKind)
        {
            path = assetPath;
            guid = assetGuid;
            id = name != null ? name.Id : System.IO.Path.GetFileNameWithoutExtension(assetPath);
            scope = name != null ? name.Scope : inferredScope;
            kind = name != null ? name.Kind : inferredKind;
            subject = name?.Subject;
            variant = name?.Variant;
            isValid = true;
        }

        internal void Invalidate() => isValid = false;
    }

    [Serializable]
    public sealed class ResourceLibraryDiagnostic
    {
        [SerializeField] private string scope;
        [SerializeField] private string path;
        [SerializeField] private string code;
        [SerializeField] private string message;

        public string Scope => scope;
        public string Path => path;
        public string Code => code;
        public string Message => message;

        internal ResourceLibraryDiagnostic(string owner, string assetPath, string errorCode, string error)
        {
            scope = owner;
            path = assetPath;
            code = errorCode;
            message = error;
        }

        public override string ToString() => $"{path}: {message}";
    }

    public sealed class LibrarySnapshot
    {
        private readonly ResourceLibraryRecord[] records;
        private readonly ResourceLibraryDiagnostic[] diagnostics;
        private readonly string[] scopes;

        public IReadOnlyList<ResourceLibraryRecord> Records { get; }
        public IReadOnlyList<ResourceLibraryDiagnostic> Diagnostics { get; }
        public IReadOnlyList<string> Scopes { get; }

        internal LibrarySnapshot(IEnumerable<ResourceLibraryRecord> items,
            IEnumerable<ResourceLibraryDiagnostic> errors)
        {
            records = items.OrderBy(record => record.Path, StringComparer.Ordinal).ToArray();
            diagnostics = errors.OrderBy(error => error.Path, StringComparer.Ordinal)
                .ThenBy(error => error.Code, StringComparer.Ordinal).ToArray();
            scopes = records.Select(record => record.Scope).Concat(diagnostics.Select(error => error.Scope))
                .Where(scope => !string.IsNullOrEmpty(scope)).Distinct(StringComparer.Ordinal)
                .OrderBy(scope => scope, StringComparer.Ordinal).ToArray();
            Records = Array.AsReadOnly(records);
            Diagnostics = Array.AsReadOnly(diagnostics);
            Scopes = Array.AsReadOnly(scopes);
        }

        public static LibrarySnapshot Scan() => ResourceLibraryDatabase.Scan();

        public IReadOnlyList<ResourceLibraryRecord> Query(string scope = null, string kind = null, string text = null)
        {
            return records.Where(record =>
                (string.IsNullOrEmpty(scope) || record.Scope == scope) &&
                (string.IsNullOrEmpty(kind) || record.Kind == kind) &&
                (string.IsNullOrWhiteSpace(text) || Contains(record.Id, text) || Contains(record.Subject, text) ||
                    Contains(record.Variant, text) || Contains(record.Path, text) || Contains(record.Guid, text)))
                .ToArray();
        }

        public IReadOnlyList<ResourceLibraryDiagnostic> GetScopeDiagnostics(string scope)
        {
            return diagnostics.Where(error => error.Scope == scope).ToArray();
        }

        public RouteResourceEntry[] ResolveScope(string scope)
        {
            if (!TryResolveScope(scope, out RouteResourceEntry[] entries, out string[] errors))
                throw new InvalidOperationException($"资源库范围 '{scope}' 无法使用：\n" + string.Join("\n", errors));
            return entries;
        }

        public RouteResourceEntry[] GetScopeEntries(string scope, out string[] errors)
        {
            return TryResolveScope(scope, out RouteResourceEntry[] entries, out errors) ? entries : null;
        }

        // An invalid unpublished scope never prevents a different scope from
        // resolving. No Unity image/audio object is loaded before this method.
        public bool TryResolveScope(string scope, out RouteResourceEntry[] entries, out string[] errors)
        {
            entries = null;
            List<string> failures = new List<string>();
            if (string.IsNullOrWhiteSpace(scope)) failures.Add("必须指定资源范围（shared 或路线 ID）。");
            failures.AddRange(diagnostics.Where(error => error.Scope == scope).Select(error => error.ToString()));
            if (failures.Count > 0) { errors = failures.ToArray(); return false; }

            List<RouteResourceEntry> resolved = new List<RouteResourceEntry>();
            foreach (ResourceLibraryRecord record in records.Where(record => record.Scope == scope))
            {
                try
                {
                    if (AssetDatabase.GUIDToAssetPath(record.Guid) != record.Path ||
                        !ResourceNaming.TryParseAssetPath(record.Path, out ResourceName currentName, out string namingError) ||
                        currentName.Id != record.Id)
                    {
                        failures.Add(record.Path + ": 资源已移动或命名已改变，请重新扫描。");
                        continue;
                    }
                    string importerError = ResourceLibraryDatabase.GetImporterError(record.Path, record.Kind);
                    if (importerError != null)
                    {
                        failures.Add(record.Path + ": " + importerError);
                        continue;
                    }

                    RouteResourceEntry entry = new RouteResourceEntry { id = record.Id, kind = record.Kind };
                    if (RouteResourceCatalog.IsSpriteKind(record.Kind))
                        entry.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(record.Path);
                    else
                        entry.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(record.Path);
                    if (entry.sprite == null && entry.clip == null)
                        failures.Add(record.Path + ": 导入未产生可用的 " + record.Kind + " 资源，请检查源文件与导入错误。");
                    else
                        resolved.Add(entry);
                }
                catch (Exception ex) { failures.Add(record.Path + ": " + ex.Message); }
            }

            if (failures.Count == 0 && !RouteResourceCatalog.ValidateEntries(resolved, out string catalogError))
                failures.Add(catalogError);
            errors = failures.ToArray();
            if (errors.Length != 0) return false;
            entries = resolved.ToArray();
            return true;
        }

        private static bool Contains(string value, string text)
        {
            return value != null && value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public static class ResourceLibraryDatabase
    {
        public const string MetadataIndexPath = "Library/BaiguVN/resource-library-index.json";
        public static event Action Changed;
        private static bool notificationQueued;

        [Serializable]
        private sealed class MetadataIndex
        {
            public int schemaVersion = 1;
            public string rootPath;
            public string generatedAtUtc;
            public ResourceLibraryRecord[] records;
            public ResourceLibraryDiagnostic[] diagnostics;
        }

        public static LibrarySnapshot Scan()
        {
            List<ResourceLibraryRecord> records = new List<ResourceLibraryRecord>();
            List<ResourceLibraryDiagnostic> diagnostics = new List<ResourceLibraryDiagnostic>();
            Dictionary<string, List<ResourceLibraryRecord>> names = new Dictionary<string, List<ResourceLibraryRecord>>(StringComparer.Ordinal);
            if (!AssetDatabase.IsValidFolder(ResourceNaming.RootPath)) return new LibrarySnapshot(records, diagnostics);

            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { ResourceNaming.RootPath }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (!ResourceNaming.IsManagedPath(path) || AssetDatabase.IsValidFolder(path) || IsDocumentation(path)) continue;
                InferLocation(path, out string inferredScope, out string inferredKind);
                bool validName = ResourceNaming.TryParseAssetPath(path, out ResourceName name, out string error);
                ResourceLibraryRecord record = new ResourceLibraryRecord(path, guid, validName ? name : null, inferredScope, inferredKind);
                records.Add(record);
                if (!validName)
                {
                    AddDiagnostic(record, "invalid_name", error, diagnostics);
                    continue;
                }

                if (!names.TryGetValue(record.Id, out List<ResourceLibraryRecord> matching))
                    names.Add(record.Id, matching = new List<ResourceLibraryRecord>());
                matching.Add(record);
                string importerError = GetImporterError(path, record.Kind);
                if (importerError != null) AddDiagnostic(record, "invalid_importer", importerError, diagnostics);
            }

            foreach (KeyValuePair<string, List<ResourceLibraryRecord>> pair in names.Where(pair => pair.Value.Count > 1))
                foreach (ResourceLibraryRecord record in pair.Value)
                    AddDiagnostic(record, "duplicate_id", "业务 ID 重复：" + pair.Key + "。请为不同素材使用不同 subject/variant。", diagnostics);

            return new LibrarySnapshot(records, diagnostics);
        }

        public static string ExportMetadataIndex(LibrarySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string output = Path.GetFullPath(Path.Combine(projectRoot, MetadataIndexPath));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            MetadataIndex index = new MetadataIndex
            {
                rootPath = ResourceNaming.RootPath,
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                records = snapshot.Records.ToArray(),
                diagnostics = snapshot.Diagnostics.ToArray()
            };
            File.WriteAllText(output, JsonUtility.ToJson(index, true), new UTF8Encoding(false));
            return output;
        }

        internal static string GetImporterError(string path, string kind)
        {
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (RouteResourceCatalog.IsSpriteKind(kind))
            {
                TextureImporter texture = importer as TextureImporter;
                if (texture == null) return "需要 Unity TextureImporter；请使用规范支持的 PNG/JPG 图片。";
                if (texture.textureType != TextureImporterType.Sprite || texture.spriteImportMode != SpriteImportMode.Single)
                    return "图片须导入为 Sprite / Single。重新导入规范命名的文件可自动设置。";
                if (texture.mipmapEnabled) return "视觉小说图片须关闭 Mip Maps；请重新导入。";
                if (texture.alphaSource != TextureImporterAlphaSource.FromInput || !texture.alphaIsTransparency)
                    return "图片须保留源透明通道并启用 Alpha Is Transparency；请重新导入。";
            }
            else if (RouteResourceCatalog.IsAudioKind(kind))
            {
                if (!(importer is AudioImporter)) return "需要 Unity AudioImporter；请检查 WAV/OGG 文件。";
            }
            else return "不支持的资源类型：" + kind;
            return null;
        }

        private static bool IsDocumentation(string path)
        {
            string filename = Path.GetFileName(path);
            string extension = Path.GetExtension(path);
            return extension.Equals(".meta", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
                filename.Equals("readme.txt", StringComparison.OrdinalIgnoreCase) ||
                filename.Equals(".gitkeep", StringComparison.OrdinalIgnoreCase) ||
                filename.Equals(".keep", StringComparison.OrdinalIgnoreCase) ||
                filename.Equals(".gitignore", StringComparison.OrdinalIgnoreCase) ||
                filename.Equals("LICENSE", StringComparison.OrdinalIgnoreCase);
        }

        private static void InferLocation(string path, out string scope, out string kind)
        {
            string remainder = path.Substring(ResourceNaming.RootPath.TrimEnd('/').Length).TrimStart('/');
            string[] segments = remainder.Split('/');
            scope = segments.Length > 1 ? segments[0] : string.Empty;
            kind = segments.Length > 2 ? segments[1] : string.Empty;
        }

        private static void AddDiagnostic(ResourceLibraryRecord record, string code, string message,
            List<ResourceLibraryDiagnostic> diagnostics)
        {
            record.Invalidate();
            diagnostics.Add(new ResourceLibraryDiagnostic(record.Scope, record.Path, code, message));
        }

        internal static void NotifyChanged()
        {
            if (notificationQueued) return;
            notificationQueued = true;
            EditorApplication.delayCall += () => { notificationQueued = false; Changed?.Invoke(); };
        }
    }

    // Unity invokes this only for image imports. Unmanaged paths and invalidly
    // named files retain their existing settings and instead receive diagnostics.
    internal sealed class ResourceLibraryImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!ResourceNaming.IsManagedPath(assetPath) ||
                !ResourceNaming.TryParseAssetPath(assetPath, out ResourceName name, out _) ||
                !RouteResourceCatalog.IsSpriteKind(name.Kind)) return;
            TextureImporter texture = assetImporter as TextureImporter;
            if (texture == null) return;
            texture.textureType = TextureImporterType.Sprite;
            texture.spriteImportMode = SpriteImportMode.Single;
            texture.mipmapEnabled = false;
            texture.alphaSource = TextureImporterAlphaSource.FromInput;
            texture.alphaIsTransparency = true;
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets.Concat(deletedAssets).Concat(movedAssets).Concat(movedFromAssetPaths)
                .Any(ResourceNaming.IsManagedPath)) ResourceLibraryDatabase.NotifyChanged();
        }
    }

    public sealed class ResourceLibraryQueryWindow : EditorWindow
    {
        private LibrarySnapshot snapshot;
        private string scope = string.Empty;
        private string kind = string.Empty;
        private string search = string.Empty;
        private Vector2 scroll;
        private int page;
        private const int PageSize = 100;
        private static readonly string[] Kinds = { "", "background", "portrait", "cg", "prop", "effect", "bgm", "se", "ambience" };

        [MenuItem("BaiguVN/Resources/Query Resource Library")]
        public static void Open() => GetWindow<ResourceLibraryQueryWindow>("资源查询");

        private void OnEnable()
        {
            ResourceLibraryDatabase.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => ResourceLibraryDatabase.Changed -= Refresh;

        private void Refresh()
        {
            snapshot = ResourceLibraryDatabase.Scan();
            page = 0;
            Repaint();
        }

        private void OnGUI()
        {
            if (snapshot == null) Refresh();
            EditorGUILayout.LabelField(ResourceNaming.RootPath, EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("刷新")) Refresh();
            if (GUILayout.Button("导出索引 JSON"))
            {
                string path = ResourceLibraryDatabase.ExportMetadataIndex(snapshot);
                Debug.Log("资源 metadata 索引已导出：" + path);
                ShowNotification(new GUIContent("已导出到 Library/BaiguVN"));
            }
            EditorGUILayout.EndHorizontal();

            string[] scopes = new[] { "" }.Concat(snapshot.Scopes).ToArray();
            string[] scopeLabels = scopes.Select(value => string.IsNullOrEmpty(value) ? "全部范围" : value).ToArray();
            string[] kindLabels = Kinds.Select(value => string.IsNullOrEmpty(value) ? "全部类型" : value).ToArray();
            EditorGUI.BeginChangeCheck();
            scope = scopes[EditorGUILayout.Popup("范围", Math.Max(0, Array.IndexOf(scopes, scope)), scopeLabels)];
            kind = Kinds[EditorGUILayout.Popup("类型", Math.Max(0, Array.IndexOf(Kinds, kind)), kindLabels)];
            search = EditorGUILayout.TextField("查找 ID / 名称 / 路径", search);
            if (EditorGUI.EndChangeCheck()) page = 0;

            IReadOnlyList<ResourceLibraryRecord> matches = snapshot.Query(scope, kind, search);
            int pages = Math.Max(1, (matches.Count + PageSize - 1) / PageSize);
            page = Mathf.Clamp(page, 0, pages - 1);
            EditorGUILayout.LabelField($"{matches.Count} 条资源；扫描仅读取索引，选择发布范围时才加载素材。");
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(page == 0)) if (GUILayout.Button("上一页")) page--;
            GUILayout.Label($"{page + 1} / {pages}", GUILayout.Width(70));
            using (new EditorGUI.DisabledScope(page >= pages - 1)) if (GUILayout.Button("下一页")) page++;
            EditorGUILayout.EndHorizontal();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (ResourceLibraryRecord record in matches.Skip(page * PageSize).Take(PageSize))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.SelectableLabel((record.IsValid ? "✓ " : "! ") + record.Id, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("定位", GUILayout.Width(50)))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(record.Path));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(record.Scope + " / " + record.Kind, EditorStyles.miniLabel);
                EditorGUILayout.SelectableLabel(record.Path, EditorStyles.miniLabel, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.EndVertical();
            }
            foreach (ResourceLibraryDiagnostic diagnostic in snapshot.Diagnostics.Where(error => string.IsNullOrEmpty(scope) || error.Scope == scope))
                EditorGUILayout.HelpBox(diagnostic.ToString(), MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
