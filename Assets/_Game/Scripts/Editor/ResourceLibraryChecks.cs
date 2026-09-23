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
    // These checks import and remove tiny functional fixtures. They are never
    // allowed to run in the user's working game project, including from a menu.
    public static class ResourceLibraryChecks
    {
        private const string RouteA = "library_check_a";
        private const string RouteB = "library_check_b";
        private const string DraftRoute = "library_check_draft";
        private const string LegacyRoute = "library_check_legacy";
        private const string SharedId = "shared__background__library_check_scene__day";
        private const string PortraitA = RouteA + "__portrait__wukong_clerk__neutral";
        private const string PortraitB = RouteB + "__portrait__wukong_actor__neutral";
        private const string LegacyId = "library_check_legacy_portrait";
        private const string RuntimeSharedPath = "Assets/Resources/Routes/shared-library.asset";
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private static readonly List<string> ownedFiles = new List<string>();
        private static readonly List<string> ownedFolders = new List<string>();
        private static Report report;

        [Serializable]
        private class Report
        {
            public string unityVersion;
            public List<string> passed = new List<string>();
            public List<string> failed = new List<string>();
            public List<string> notes = new List<string>();
        }

        [Serializable]
        private class MetadataProbe
        {
            public MetadataRecord[] records;
        }

        [Serializable]
        private class MetadataRecord
        {
            public string id;
            public string path;
            public string guid;
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        private static string RouteFolder(string route) => RouteCatalogBuilder.SourceRoot + "/" + route;
        private static string RouteJson(string route) => RouteFolder(route) + "/route.json";
        private static string LibraryFolder(string scope) => ResourceNaming.RootPath + "/" + scope;
        private static string ImagePath(string scope, string kind, string id, string extension = ".png")
            => LibraryFolder(scope) + "/" + kind + "/" + id + extension;

        [MenuItem("BaiguVN/验证/资源库集成检查（独立工程）")]
        public static void RunFromMenu() => Begin(false);

        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("RunBatch requires batch mode.");
            Begin(true);
        }

        private static void Begin(bool batch)
        {
            if (PlayerSettings.productName != "BaiGuYiYun_Validation" ||
                !File.Exists(Path.Combine(ProjectRoot, ".codex-validation-project")))
                throw new InvalidOperationException("Resource library checks require the marked isolated validation project.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before running resource library checks.");

            report = new Report { unityVersion = Application.unityVersion };
            ownedFiles.Clear(); ownedFolders.Clear();
            byte[] originalShared = null;
            bool fixturesAllowed = false;
            try
            {
                // Refuse to overwrite remnants or user-owned content with these names.
                foreach (string id in new[] { RouteA, RouteB, DraftRoute, LegacyRoute })
                {
                    Require(!Directory.Exists(RouteFolder(id)), "Fixture route already exists: " + id);
                    Require(!Directory.Exists(LibraryFolder(id)), "Fixture resource scope already exists: " + id);
                }
                Require(!File.Exists(ImagePath("shared", "background", SharedId)), "Shared fixture already exists.");
                Rebuild();
                Require(Ids().SequenceEqual(new[] { "canonical" }), "Run in a validation copy containing only canonical as a published route.");
                originalShared = File.ReadAllBytes(RouteCatalogBuilder.SharedPath);
                fixturesAllowed = true;
                RunCases();
            }
            catch (Exception ex)
            {
                report.failed.Add("Fixture setup or test execution: " + ex);
            }
            finally
            {
                if (fixturesAllowed)
                {
                    RunCase("清理只删除自建fixture，发布目录恢复仅canonical", () =>
                    {
                        Cleanup();
                        Rebuild();
                        ResourceLibraryDatabase.ExportMetadataIndex(ResourceLibraryDatabase.Scan());
                        Require(Ids().SequenceEqual(new[] { "canonical" }), "Fixture routes remain in the published index.");
                        foreach (string id in new[] { RouteA, RouteB, DraftRoute, LegacyRoute })
                            Require(!File.Exists(RouteCatalogBuilder.OutputRoot + "/" + id + ".asset"), "Generated fixture catalog remains: " + id);
                        Require(originalShared.SequenceEqual(File.ReadAllBytes(RouteCatalogBuilder.SharedPath)), "Author-maintained shared.asset was modified.");
                    });
                }
                report.notes.Add("4x4 PNG/JPG files and a short PCM WAV were functional fixtures only; no production art, audio or story was created.");
                report.notes.Add("These checks exercise native Unity import, scanning, generated catalogs and Resources.Load; they do not verify visual composition.");
                File.WriteAllText(Path.Combine(ProjectRoot, "ResourceLibraryCheckResults.json"), JsonUtility.ToJson(report, true), Utf8);
                Debug.Log("Resource library checks: " + report.passed.Count + " passed, " + report.failed.Count + " failed.");
                foreach (string failure in report.failed) Debug.LogError(failure);
                if (batch) EditorApplication.Exit(report.failed.Count == 0 ? 0 : 1);
            }
        }

        private static void RunCases()
        {
            string sharedImage = ImagePath("shared", "background", SharedId);
            string imageA = ImagePath(RouteA, "portrait", PortraitA);
            string imageB = ImagePath(RouteB, "portrait", PortraitB);
            WriteRoute(RouteA, "draft", PortraitA);
            WriteRoute(RouteB, "draft", PortraitB);
            WriteImage(sharedImage, Color.gray);
            WriteImage(imageA, Color.red);
            WriteImage(imageB, Color.blue);

            RunCase("合法文件自动Sprite导入并按scope与ID入库", () =>
            {
                Rebuild();
                var importer = AssetImporter.GetAtPath(imageA) as TextureImporter;
                Require(importer != null && importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single && !importer.mipmapEnabled && importer.alphaIsTransparency,
                    "Named portrait was not imported with Sprite settings.");
                Require(AssetDatabase.LoadAssetAtPath<Sprite>(imageA) != null, "Named portrait has no Sprite subasset.");
                var snapshot = ResourceLibraryDatabase.Scan();
                Require(snapshot.Query(RouteA, "portrait").Any(r => r.Id == PortraitA && r.IsValid && r.Path == imageA), "Portrait is missing from its own scope.");
                Require(snapshot.Query(RouteA, "portrait").All(r => r.Scope == RouteA), "Scoped query includes another scope.");
                Require(snapshot.Query("shared", "background").Any(r => r.Id == SharedId && r.IsValid), "Shared image was not indexed.");
                Require(snapshot.TryResolveScope(RouteA, out var entries, out var errors), string.Join("; ", errors ?? Array.Empty<string>()));
                Require(entries.Any(e => e.id == PortraitA && e.sprite != null), "Resolved own catalog lacks the native Sprite.");
                Require(entries.All(e => e.id != PortraitB && e.id != SharedId), "Resolving one scope borrowed another scope's resources.");
            });

            RunCase("无手工resource-catalog的发布路线自动增加菜单", () =>
            {
                Require(!File.Exists(RouteFolder(RouteA) + "/resource-catalog.asset"), "Fixture unexpectedly has a manual catalog.");
                WriteRoute(RouteA, "published", PortraitA);
                Rebuild();
                Require(Ids().SequenceEqual(new[] { "canonical", RouteA }), "New route was not added to the published menu.");
                var repository = LoadPublishedRepository();
                Require(repository.GetChoices(repository.Get(repository.FirstChoiceNodeId)).Count == 2, "Runtime choices do not reflect generated routes.");
                var catalog = AssetDatabase.LoadAssetAtPath<RouteResourceCatalog>(RouteCatalogBuilder.OutputRoot + "/" + RouteA + ".asset");
                Require(catalog != null && catalog.entries.Any(e => e.id == PortraitA && e.sprite != null), "Generated route catalog lacks its art.");
            });

            RunCase("无透明通道JPG与WAV原生查询解析、索引只导出元数据", () =>
            {
                string jpgId = RouteA + "__background__library_check_scene__day";
                string wavId = RouteA + "__se__library_check__click";
                string jpgPath = ImagePath(RouteA, "background", jpgId, ".jpg");
                string wavPath = ImagePath(RouteA, "se", wavId, ".wav");
                WriteImage(jpgPath, Color.gray);
                WriteWav(wavPath);
                Rebuild();
                var snapshot = ResourceLibraryDatabase.Scan();
                Require(snapshot.Query(RouteA, "background").Any(r => r.Id == jpgId && r.IsValid), "A valid JPEG was rejected by image or alpha validation.");
                Require(snapshot.Query(RouteA, "se").Any(r => r.Id == wavId && r.IsValid), "A valid WAV was not queryable as an audio resource.");
                Require(snapshot.TryResolveScope(RouteA, out var entries, out var errors), string.Join("; ", errors ?? Array.Empty<string>()));
                Require(entries.Any(e => e.id == jpgId && e.sprite != null), "JPEG did not resolve to a native Sprite.");
                Require(entries.Any(e => e.id == wavId && e.clip != null && e.clip.samples > 0), "WAV did not resolve to a playable native AudioClip.");
                string exportPath = ResourceLibraryDatabase.ExportMetadataIndex(snapshot);
                string json = File.ReadAllText(exportPath);
                var metadata = JsonUtility.FromJson<MetadataProbe>(json);
                var wavRecord = snapshot.Query(RouteA, "se").First(r => r.Id == wavId);
                Require(metadata?.records != null && metadata.records.Any(r => r.id == wavId && r.path == wavPath && r.guid == wavRecord.Guid && !string.IsNullOrEmpty(r.guid)), "Exported metadata did not preserve readable id/path/guid.");
                Require(!json.Contains("\"sprite\"") && !json.Contains("\"clip\"") && !json.Contains("\"instanceID\""), "Exported metadata contains Unity object references.");
            });

            RunCase("自动共享资源可用且运行时不借另一娱乐线的图片", () =>
            {
                using (var service = new RouteResourceService())
                {
                    var shared = AssetDatabase.LoadAssetAtPath<RouteResourceCatalog>(RuntimeSharedPath);
                    Require(shared != null, "Merged shared-library catalog was not generated.");
                    service.SetShared(shared);
                    Require(service.TryPrepare("Routes/" + RouteA, out var error), error);
                    service.CommitPrepared();
                    Require(service.GetSprite(SharedId, "background") != null, "Named shared background cannot be used.");
                    Require(service.GetSprite(PortraitA, "portrait") != null, "Active route's portrait cannot be used.");
                    Require(service.GetSprite(PortraitB, "portrait") == null, "A different route's portrait leaked into active lookups.");
                }
            });

            RunCase("新增停用删除同步增减选项与生成资源副本", () =>
            {
                WriteRoute(RouteB, "published", PortraitB); Rebuild();
                Require(Ids().Length == 3, "Second route did not add an option.");
                WriteRoute(RouteB, "disabled", PortraitB); Rebuild();
                Require(Ids().Length == 2 && !Ids().Contains(RouteB), "Disabled route remains selectable.");
                WriteRoute(RouteB, "published", PortraitB); Rebuild();
                Require(Ids().Length == 3, "Republished route did not return.");
                DeleteOwned(RouteJson(RouteB)); Rebuild();
                Require(Ids().Length == 2 && !Ids().Contains(RouteB), "Deleted route remains selectable.");
                Require(!File.Exists(RouteCatalogBuilder.OutputRoot + "/" + RouteB + ".asset"), "Deleted route's generated catalog remains.");
                Require(File.Exists(imageB), "Deleting a route removed author source art.");
            });

            RunCase("发布scope含错名图片时失败且保留上一版索引", () =>
            {
                string bad = LibraryFolder(RouteA) + "/portrait/not-a-valid-resource-name.png";
                try
                {
                    WriteImage(bad, Color.yellow);
                    AssertFailedWithoutChangingIndex();
                }
                finally { DeleteOwned(bad); Rebuild(); }
            });

            RunCase("同一ID的PNG与JPG重复资源被拒绝", () =>
            {
                string duplicateId = RouteA + "__background__duplicate__day";
                string png = ImagePath(RouteA, "background", duplicateId);
                string jpg = ImagePath(RouteA, "background", duplicateId, ".jpg");
                try
                {
                    WriteImage(png, Color.green); WriteImage(jpg, Color.green);
                    AssertFailedWithoutChangingIndex();
                    var snapshot = ResourceLibraryDatabase.Scan();
                    Require(!snapshot.TryResolveScope(RouteA, out _, out _), "Ambiguous scope was resolved despite duplicate IDs.");
                }
                finally { DeleteOwned(png); DeleteOwned(jpg); Rebuild(); }
            });

            RunCase("删除新立绘后不会借旧生成catalog残留通过发布", () =>
            {
                Require(File.Exists(RouteCatalogBuilder.OutputRoot + "/" + RouteA + ".asset"), "No prior generated catalog exists for the stale-reference check.");
                try
                {
                    DeleteOwned(imageA);
                    AssertFailedWithoutChangingIndex();
                    Require(!ResourceLibraryDatabase.Scan().Query(RouteA, "portrait").Any(r => r.Id == PortraitA), "Deleted image is still indexed.");
                }
                finally { WriteImage(imageA, Color.red); Rebuild(); }
            });

            RunCase("损坏草稿scope不阻止其他完整路线发布", () =>
            {
                string bad = LibraryFolder(DraftRoute) + "/portrait/unfinished.png";
                WriteRoute(DraftRoute, "draft", DraftRoute + "__portrait__pending__neutral");
                try
                {
                    WriteImage(bad, Color.magenta);
                    Rebuild();
                    Require(!Ids().Contains(DraftRoute) && Ids().Contains("canonical") && Ids().Contains(RouteA), "Broken draft blocks valid routes or becomes published.");
                    Require(ResourceLibraryDatabase.Scan().Diagnostics.Any(d => d.Scope == DraftRoute), "Bad draft naming did not produce a scoped diagnostic.");
                }
                finally { DeleteOwned(bad); DeleteOwned(RouteJson(DraftRoute)); Rebuild(); }
            });

            RunCase("旧手工Sprite映射继续兼容且不重命名旧图片", () =>
            {
                string legacyImage = RouteFolder(LegacyRoute) + "/Art/legacy-picture.png";
                WriteImage(legacyImage, Color.cyan);
                var importer = (TextureImporter)AssetImporter.GetAtPath(legacyImage);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                var catalog = ScriptableObject.CreateInstance<RouteResourceCatalog>();
                catalog.entries = new[] { new RouteResourceEntry { id = LegacyId, kind = "portrait", sprite = AssetDatabase.LoadAssetAtPath<Sprite>(legacyImage) } };
                string catalogPath = RouteFolder(LegacyRoute) + "/resource-catalog.asset";
                ClaimFile(catalogPath); AssetDatabase.CreateAsset(catalog, catalogPath);
                WriteRoute(LegacyRoute, "published", LegacyId); Rebuild();
                Require(Ids().Contains(LegacyRoute), "Legacy manual mapping was rejected.");
                var generated = AssetDatabase.LoadAssetAtPath<RouteResourceCatalog>(RouteCatalogBuilder.OutputRoot + "/" + LegacyRoute + ".asset");
                Require(generated != null && generated.entries.Any(e => e.id == LegacyId && e.sprite != null), "Legacy mapping was not merged.");
                Require(File.Exists(legacyImage), "Legacy art was moved or renamed.");
            });
        }

        private static void WriteRoute(string id, string status, string portrait)
        {
            VNRoute route = new VNRoute
            {
                routeId = id, kind = RouteKinds.Entertainment, status = status,
                choiceGroupId = "first_encounter", title = "资源功能测试", choiceText = "测试：" + id,
                entryNode = id + ".enter", artReviewed = status == "published", order = 1000,
                requiredAssets = new[] { new VNAssetRequirement { id = portrait, kind = "portrait" }, new VNAssetRequirement { id = SharedId, kind = "background" } },
                nodes = new[]
                {
                    new VNNode { id = id + ".enter", type = "line", text = "资源接入功能测试。", backgroundId = SharedId,
                        actions = new[] { new VNVisualAction { type = "transform", assetId = portrait, slot = 1, duration = 0f } }, next = id + ".end" },
                    new VNNode { id = id + ".end", type = "end", text = "测试结束。", resultId = id + ".complete" }
                }
            };
            string path = RouteJson(id); ClaimFile(path);
            File.WriteAllText(path, JsonUtility.ToJson(route, true), Utf8);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static void WriteImage(string path, Color color)
        {
            ClaimFile(path);
            Texture2D image = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            try
            {
                image.SetPixels(Enumerable.Repeat(color, 16).ToArray()); image.Apply();
                File.WriteAllBytes(path, path.EndsWith(".jpg", StringComparison.Ordinal) ? image.EncodeToJPG() : image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static void WriteWav(string path)
        {
            ClaimFile(path);
            const int sampleRate = 8000;
            const int sampleCount = 400;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + sampleCount * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
                writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(sampleCount * 2);
                for (int i = 0; i < sampleCount; i++) writer.Write((short)(Math.Sin(i * 2.0 * Math.PI * 440.0 / sampleRate) * 1000));
                writer.Flush(); File.WriteAllBytes(path, stream.ToArray());
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static void ClaimFile(string path)
        {
            if (!ownedFiles.Contains(path))
            {
                Require(!File.Exists(path), "Refusing to overwrite non-fixture file: " + path);
                ownedFiles.Add(path);
            }
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            ownedFolders.Add(path);
        }

        private static void DeleteOwned(string path)
        {
            Require(ownedFiles.Contains(path), "Refusing to delete a non-fixture asset: " + path);
            if (File.Exists(path)) Require(AssetDatabase.DeleteAsset(path), "Unable to delete fixture: " + path);
        }

        private static void Cleanup()
        {
            for (int i = ownedFiles.Count - 1; i >= 0; i--) DeleteOwned(ownedFiles[i]);
            for (int i = ownedFolders.Count - 1; i >= 0; i--)
            {
                string folder = ownedFolders[i];
                if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                    Require(AssetDatabase.DeleteAsset(folder), "Unable to remove empty fixture folder: " + folder);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static string[] Ids()
            => JsonUtility.FromJson<VNRouteIndex>(File.ReadAllText(RouteCatalogBuilder.IndexPath)).routes
                .Select(r => r.routeId).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        private static StoryRepository LoadPublishedRepository()
        {
            var repository = new StoryRepository();
            repository.Load(JsonUtility.FromJson<VNStory>(File.ReadAllText(RouteCatalogBuilder.StoryPath)),
                JsonUtility.FromJson<VNRouteIndex>(File.ReadAllText(RouteCatalogBuilder.IndexPath)));
            return repository;
        }

        private static void Rebuild()
        {
            Require(RouteCatalogBuilder.Rebuild(out string error), error);
            AssetDatabase.SaveAssets();
        }

        private static void AssertFailedWithoutChangingIndex()
        {
            byte[] before = File.ReadAllBytes(RouteCatalogBuilder.IndexPath);
            Require(!RouteCatalogBuilder.Rebuild(out string error), "Invalid published assets unexpectedly passed rebuilding.");
            Require(!string.IsNullOrWhiteSpace(error), "Failed publishing supplied no error.");
            Require(before.SequenceEqual(File.ReadAllBytes(RouteCatalogBuilder.IndexPath)), "Failed publishing changed the previous index.");
        }

        private static void RunCase(string name, Action action)
        {
            try { action(); report.passed.Add(name); Debug.Log("PASS " + name); }
            catch (Exception ex) { report.failed.Add(name + ": " + ex); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
