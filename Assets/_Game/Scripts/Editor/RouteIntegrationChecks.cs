#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BaiguVN.Editor
{
    // Batch invocation deliberately runs in a separate, marked validation project.
    // Do not pass -quit: frame-dependent checks exit the editor after completion.
    [InitializeOnLoad]
    public static class RouteIntegrationChecks
    {
        private const string Prefix = "BaiguVN.RouteIntegration.";
        private const string ValidationProduct = "BaiGuYiYun_Validation";
        private static Report report;
        private static ChoiceView view;
        private static GameObject canvasObject;
        private static GameObject eventObject;
        private static GameObject cameraObject;
        private static StoryRunner liveRunner;
        private static VNProfile liveProfile;
        private static int callbacks;
        private static string selected;
        private static int step;
        private static int afterFrame;
        private static double deadline;
        private static bool uiStarted;

        [Serializable]
        private class Report
        {
            public string unityVersion;
            public List<string> passed = new List<string>();
            public List<string> failed = new List<string>();
            public List<string> notes = new List<string>();
        }

        static RouteIntegrationChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += OnUpdate;
        }

        [MenuItem("BaiguVN/验证/路线集成检查")]
        public static void RunFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出运行模式再执行检查。");
            Begin(false);
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode || !IsIsolatedProject())
                throw new InvalidOperationException("批量检查仅允许在独立的 BaiGuYiYun_Validation 工程运行。");
            Begin(true);
        }

        private static bool IsIsolatedProject()
        {
            return PlayerSettings.productName == ValidationProduct &&
                File.Exists(Path.Combine(ProjectRoot, ".codex-validation-project"));
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        private static void Begin(bool batch)
        {
            report = new Report { unityVersion = Application.unityVersion };
            SessionState.SetBool(Prefix + "Batch", batch);
            RunCoreChecks();
            if (!IsIsolatedProject())
            {
                report.notes.Add("原工程仅运行无文件写入的检查。存档文件与逐帧UI检查请在独立验证工程运行。");
                Finish();
                return;
            }

            RunCase("原生存档读取迁移不改写文件", CheckLegacyFileRead);
            RunCase("正式源目录生成且草稿不混入发布索引", () =>
            {
                RouteCatalogBuilder.RebuildOrThrow();
                VNRouteIndex generated = JsonUtility.FromJson<VNRouteIndex>(File.ReadAllText(RouteCatalogBuilder.IndexPath));
                var expected = Directory.GetFiles(RouteCatalogBuilder.SourceRoot, "route.json", SearchOption.AllDirectories)
                    .Select(path => JsonUtility.FromJson<VNRoute>(File.ReadAllText(path))).Where(route => route.status == "published")
                    .Select(route => route.routeId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                Require(generated.routes.Select(route => route.routeId).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(expected), "生成索引与发布源不一致。");
                report.notes.Add("正式生成路线：" + string.Join(", ", expected));
            });
            SaveReport();
            SessionState.SetBool(Prefix + "Pending", true);
            SessionState.SetBool(Prefix + "Finalize", false);
            SessionState.SetBool(Prefix + "UiStarted", false);
            SessionState.SetBool(Prefix + "PlayReady", false);
            // Only the isolated copy gets a temporary scene or enters play mode.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void RunCoreChecks()
        {
            RunCase("Unity JsonUtility schema3继承字段、道具与检查点往返", () =>
            {
                GameState original = MakeState();
                VNSnapshot snapshot = original.ToSnapshot("integration-content");
                string json = JsonUtility.ToJson(snapshot);
                Require(json.Contains("currentNodeId") && json.Contains("routeCheckpoint"), "继承字段或检查点未序列化。");
                Require(!json.Contains("migratedFromV2") && !json.Contains("sourceSchemaVersion"), "迁移标记不应落盘。");
                Require(SaveService.TryDeserialize(json, out VNSnapshot loaded, out string error), error);
                Require(loaded.schemaVersion == 3 && loaded.sourceSchemaVersion == 3 && !loaded.migratedFromV2, "版本元数据错误。");
                Require(loaded.currentNodeId == "fun.current" && loaded.routeId == "fun_01" && loaded.routeRevision == 2, "路线数据丢失。");
                Require(loaded.visuals.props.Count == 1 && loaded.visuals.props[0].assetId == "staff.sprite", "道具状态丢失。");
                Require(loaded.routeCheckpoint.currentNodeId == "choice.first" && loaded.routeCheckpoint.GetType() == typeof(VNRunSnapshot), "检查点递归或节点丢失。");
            });
            RunCase("存档、还原与检查点深拷贝隔离", () =>
            {
                GameState state = MakeState();
                VNSnapshot snapshot = state.ToSnapshot("integration-content");
                state.visuals.props[0].x = 100; state.checkpoint.visuals.props[0].x = 200;
                state.history[0].text = "changed";
                Require(snapshot.visuals.props[0].x == 5 && snapshot.routeCheckpoint.visuals.props[0].x == 5 && snapshot.history[0].text == "original", "保存对象仍引用游戏状态。");
                GameState restored = GameState.FromSnapshot(snapshot);
                restored.visuals.props[0].x = 300; restored.checkpoint.visuals.props[0].x = 400;
                Require(snapshot.visuals.props[0].x == 5 && snapshot.routeCheckpoint.visuals.props[0].x == 5, "恢复对象仍引用存档。");
            });
            RunCase("schema3无检查点开场存档原生往返", () =>
            {
                VNSnapshot source = new GameState { currentNodeId = "opening" }.ToSnapshot("integration");
                Require(!source.hasRouteCheckpoint && source.routeCheckpoint == null, "无检查点状态标记错误。");
                Require(SaveService.TryDeserialize(JsonUtility.ToJson(source), out VNSnapshot loaded, out string error), error);
                Require(!loaded.hasRouteCheckpoint && loaded.routeCheckpoint == null && GameState.FromSnapshot(loaded).checkpoint == null, "Unity空内联对象被误认为检查点。");
                source.hasRouteCheckpoint = true;
                Require(!SaveService.TryDeserialize(JsonUtility.ToJson(source), out _, out _), "已声明但损坏的检查点未拒绝。");
            });
            RunCase("原生v2旧内容版本迁移", () =>
            {
                Require(SaveService.TryDeserialize(LegacyJson, out VNSnapshot loaded, out string error), error);
                Require(loaded.schemaVersion == 3 && loaded.migratedFromV2 && loaded.sourceSchemaVersion == 2, "v2未在内存迁移。");
                Require(loaded.contentVersion == "legacy-content" && loaded.routeId == null && loaded.routeCheckpoint == null, "旧档错误推断路线。");
                Require(loaded.runCompleted && loaded.mainCompleted && loaded.visuals.props != null, "旧状态未保留/归一化。");
            });
            RunCase("损坏存档与错误版本拒绝", () =>
            {
                foreach (string invalid in new[] { "", "{}", "{broken", LegacyJson.Replace("BAIGU_V2_SAVE", "wrong"), LegacyJson.Replace("\"schemaVersion\":2", "\"schemaVersion\":99"), LegacyJson.Replace("old.node", "") })
                    Require(!SaveService.TryDeserialize(invalid, out _, out _), "错误存档被接受：" + invalid);
            });
            RunCase("坏道具缩放、通配实例与重复实例在恢复前拒绝", () =>
            {
                foreach (float scale in new[] { 0f, -1f, 11f })
                {
                    VNSnapshot bad = MakeState().ToSnapshot("integration"); bad.visuals.props[0].scale = scale;
                    Require(!SaveService.TryDeserialize(JsonUtility.ToJson(bad), out _, out _), "错误道具缩放被接受。");
                }
                VNSnapshot wildcard = MakeState().ToSnapshot("integration"); wildcard.visuals.props[0].instanceId = "*";
                Require(!SaveService.TryDeserialize(JsonUtility.ToJson(wildcard), out _, out _), "通配道具实例被接受。");
                VNSnapshot duplicate = MakeState().ToSnapshot("integration"); duplicate.visuals.props.Add(duplicate.visuals.props[0]);
                Require(!SaveService.TryDeserialize(JsonUtility.ToJson(duplicate), out _, out _), "重复道具实例被接受。");
            });
            RunCase("12条娱乐线与两条正经线动态生成14个选项", () =>
            {
                StoryRepository repo = Load(MakeIndex(12));
                Require(repo.GetChoices(repo.Get("choice.first")).Count == 14, "选项数量不随路线生成。");
                Require(repo.FirstChoiceNodeId == "choice.first", "首次分流点错误。");
                Require(repo.GetChoices(repo.Get("choice.first"))[0].id == "canonical", "路线排序不稳定。");
            });
            RunCase("增加、删除及停用路线同步改变选项", () =>
            {
                VNRouteIndex index = MakeIndex(13);
                StoryRepository repo = Load(index);
                Require(repo.GetChoices(repo.Get("choice.first")).Count == 15, "增加路线没有增加选项。");
                index.routes = index.routes.Where(r => r.routeId != "fun_12").ToArray();
                index.routes.First(r => r.routeId == "fun_11").status = "disabled";
                index.routes.First(r => r.routeId == "fun_10").status = "draft";
                repo.Load(MakeStory(), index);
                Require(repo.GetChoices(repo.Get("choice.first")).Count == 12, "移除/停用/草稿没有同步减少选项。");
                Require(!repo.HasNode("fun_12.end") && !repo.HasNode("fun_11.end") && repo.GetRoute("fun_10") == null, "未发布路线仍可访问。");
            });
            RunCase("同组选项只允许一条发布原著线", () =>
            {
                VNRouteIndex index = MakeIndex(1);
                index.routes = index.routes.Concat(new[] { MakeRoute("canonical_2", RouteKinds.Canonical, 3) }).ToArray();
                MustReject(() => Load(index));
            });
            RunCase("同组选项只允许一条发布完美线", () =>
            {
                VNRouteIndex index = MakeIndex(1);
                index.routes = index.routes.Concat(new[] { MakeRoute("perfect_2", RouteKinds.Perfect, 3) }).ToArray();
                MustReject(() => Load(index));
            });
            RunCase("正经线草稿不占用发布资格", () =>
            {
                VNRouteIndex index = MakeIndex(1);
                VNRoute draft = MakeRoute("perfect_draft", RouteKinds.Perfect, 9); draft.status = "draft";
                index.routes = index.routes.Concat(new[] { draft }).ToArray();
                Require(Load(index).GetRoute(draft.routeId) == null, "草稿进入了运行目录。");
            });
            RunCase("缺失节点目标拒绝", () =>
            {
                VNRouteIndex index = MakeIndex(1);
                VNRoute route = index.routes.Last();
                route.nodes[0].type = "line"; route.nodes[0].next = "missing.node";
                MustReject(() => Load(index));
            });
            RunCase("无出口死循环拒绝", () =>
            {
                VNRouteIndex index = MakeIndex(1);
                VNRoute route = index.routes.Last();
                route.nodes[0].type = "line"; route.nodes[0].next = route.nodes[0].id;
                MustReject(() => Load(index));
            });
            RunCase("跨入其他路线私有节点拒绝", () =>
            {
                VNRouteIndex index = MakeIndex(2);
                VNRoute route = index.routes.Last();
                route.nodes[0].type = "line"; route.nodes[0].next = "fun_00.end";
                MustReject(() => Load(index));
            });
            RunCase("娱乐不记录正式奖励，原著/完美允许", () =>
            {
                Require(!RouteKinds.CanRecordRewards(RouteKinds.Entertainment), "娱乐被允许记录奖励。");
                Require(RouteKinds.CanRecordRewards(RouteKinds.Canonical) && RouteKinds.CanRecordRewards(RouteKinds.Perfect), "正经线奖励资格缺失。");
                Require(!RouteKinds.CanRecordRewards(null) && !RouteKinds.CanRecordRewards("unknown"), "未知类型被授予奖励。");
            });
            RunCase("娱乐经过B03共享收尾不误记主线", () =>
            {
                GameObject owner = new GameObject("IntegrationRewardCheck");
                owner.SetActive(false);
                try
                {
                    StoryRunner runner = owner.AddComponent<StoryRunner>();
                    GameState state = MakeState(); state.completedChapters.Clear(); state.mainCompleted = false;
                    SetField(runner, "state", state);
                    VNProfile profile = new VNProfile(); SetField(runner, "profile", profile);
                    int chapterEvents = 0; runner.OnChapterReached += _ => chapterEvents++;
                    typeof(StoryRunner).GetMethod("RegisterCompletedChapter", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(runner, new object[] { new VNNode { id = "shared.end", type = "end", completeChapter = "B03" } });
                    Require(state.runCompleted && !state.mainCompleted && state.completedChapters.Count == 0 && chapterEvents == 0, "娱乐通关误记本轮主线。");
                    Require(profile.completedChapters.Count == 0 && profile.memorials.Count == 0 && profile.collections.Count == 0, "娱乐收尾写入永久奖励。");
                }
                finally { UnityEngine.Object.DestroyImmediate(owner); }
            });
        }

        private static void CheckLegacyFileRead()
        {
            Require(Application.persistentDataPath.Contains("Validation"), "存档测试未隔离到Validation目录。");
            Directory.CreateDirectory(Application.persistentDataPath);
            const int slot = 918027;
            string path = Path.Combine(Application.persistentDataPath, "save_" + slot + ".json");
            string backup = path + ".bak";
            byte[] old = File.Exists(path) ? File.ReadAllBytes(path) : null;
            byte[] oldBackup = File.Exists(backup) ? File.ReadAllBytes(backup) : null;
            try
            {
                File.WriteAllText(path, LegacyJson);
                VNSnapshot migrated = new SaveService().ReadSlot(slot, "new-content");
                Require(migrated != null && migrated.migratedFromV2, "旧版本内容差异导致读档失败。");
                Require(File.ReadAllText(path) == LegacyJson, "读取迁移改写了源存档。");
                File.WriteAllText(backup, LegacyJson);
                File.WriteAllText(path, "{broken");
                Require(new SaveService().ReadSlot(slot, "new-content")?.migratedFromV2 == true, "损坏主档没有使用有效备份。");
                Require(File.ReadAllText(path) == "{broken" && File.ReadAllText(backup) == LegacyJson, "备份恢复改写了文件。");
            }
            finally
            {
                if (old == null) File.Delete(path); else File.WriteAllBytes(path, old);
                if (oldBackup == null) File.Delete(backup); else File.WriteAllBytes(backup, oldBackup);
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Prefix + "Pending", false))
            {
                report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Report", "{}"));
                SessionState.SetBool(Prefix + "PlayReady", true);
                SessionState.SetFloat(Prefix + "ReadyAt", (float)EditorApplication.timeSinceStartup + 2f);
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Prefix + "Finalize", false))
            {
                SessionState.SetBool(Prefix + "Finalize", false);
                report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Report", "{}"));
                Finish();
            }
        }

        private static void BeginUiChecks()
        {
            uiStarted = true;
            SessionState.SetBool(Prefix + "UiStarted", true);
            canvasObject = new GameObject("IntegrationCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvasObject.transform).sizeDelta = new Vector2(1280, 720);
            cameraObject = new GameObject("IntegrationCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 360;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            eventObject = new GameObject("IntegrationEventSystem", typeof(EventSystem));
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/Fonts/NotoSansSC[wght] SDF.asset");
            view = ChoiceView.Create(canvasObject.transform, font);
            GameObject toolbar = new GameObject("Toolbar", typeof(RectTransform), typeof(Image));
            RectTransform toolbarRect = (RectTransform)toolbar.transform; toolbarRect.SetParent(canvasObject.transform, false);
            toolbarRect.anchorMin = new Vector2(0, 0.93f); toolbarRect.anchorMax = Vector2.one;
            toolbarRect.offsetMin = Vector2.zero; toolbarRect.offsetMax = Vector2.zero;
            toolbar.GetComponent<Image>().color = new Color(0.25f, 0.2f, 0.1f, 1);
            view.ControlsAbove = toolbar.transform;
            callbacks = 0; selected = null;
            view.Show("请选择悟空接下来的行动（布局测试）", Choices(14), OnSelected);
            view.GetComponentsInChildren<Button>()[0].onClick.Invoke();
            Require(callbacks == 0, "选择面板接收了打开当帧的穿透点击。");
            step = 0; afterFrame = Time.frameCount + 3;
            deadline = EditorApplication.timeSinceStartup + 45;
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(Prefix + "Pending", false) || !EditorApplication.isPlaying) return;
            if (!SessionState.GetBool(Prefix + "PlayReady", false) || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.timeSinceStartup < SessionState.GetFloat(Prefix + "ReadyAt", 0)) return;
            if (!uiStarted)
            {
                report = JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "Report", "{}"));
                if (SessionState.GetBool(Prefix + "UiStarted", false))
                    FailUi(new InvalidOperationException("验证期间发生脚本重载，逐帧测试被中断；请在源码稳定后重试。"));
                else
                {
                    try { BeginUiChecks(); }
                    catch (Exception ex) { FailUi(ex); }
                }
                return;
            }
            if (EditorApplication.timeSinceStartup > deadline) { FailUi(new Exception("UI逐帧检查超时。")); return; }
            if (Time.frameCount < afterFrame) return;
            try
            {
                if (step == 0)
                {
                    Canvas.ForceUpdateCanvases();
                    ScrollRect scroll = view.GetComponentInChildren<ScrollRect>();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                    Button[] buttons = view.GetComponentsInChildren<Button>();
                    Require(buttons.Length == 14, "14条路线没有对应14个按钮。");
                    Require(scroll.vertical && !scroll.horizontal && scroll.content.rect.height > scroll.viewport.rect.height, "长列表未形成纵向滚动区。");
                    Require(view.ControlsAbove.GetSiblingIndex() > view.transform.GetSiblingIndex(), "工具栏被选项面板遮住。");
                    report.passed.Add("ChoiceView真实布局展示14个按钮并纵向滚动");
                    CaptureUi("choice-14-top.png");
                    EventSystem.current.SetSelectedGameObject(buttons.Last().gameObject);
                    step = 1; afterFrame = Time.frameCount + 2;
                }
                else if (step == 1)
                {
                    ScrollRect scroll = view.GetComponentInChildren<ScrollRect>();
                    Require(scroll.content.anchoredPosition.y > 0, "导航到底部选项未自动滚动。");
                    CaptureUi("choice-14-bottom.png");
                    Button[] buttons = view.GetComponentsInChildren<Button>();
                    buttons.Last().onClick.Invoke(); buttons[0].onClick.Invoke();
                    Require(callbacks == 1 && selected == "option_13", "同一组选项发生重复提交或选错ID。");
                    report.passed.Add("ChoiceView导航滚动与只提交一次");
                    view.Show("Changed routes", Choices(2), OnSelected);
                    step = 2; afterFrame = Time.frameCount + 3;
                }
                else if (step == 2)
                {
                    Button[] buttons = view.GetComponentsInChildren<Button>();
                    Require(buttons.Length == 2, "路线减少后旧按钮仍然存在。");
                    view.SetInteractable(false); buttons[0].onClick.Invoke();
                    Require(callbacks == 1, "暂停交互时仍然提交选择。");
                    view.SetInteractable(true); buttons[0].onClick.Invoke(); buttons[1].onClick.Invoke();
                    Require(callbacks == 2 && selected == "option_00", "新一组选项没有正确重置单次提交。");
                    report.passed.Add("ChoiceView数量减少、暂停与重新选择");
                    UnityEngine.Object.Destroy(canvasObject); UnityEngine.Object.Destroy(eventObject); UnityEngine.Object.Destroy(cameraObject);
                    view = null;
                    EditorSceneManager.LoadSceneInPlayMode("Assets/_Game/Scenes/VNMain.unity", new LoadSceneParameters(LoadSceneMode.Additive));
                    step = 3; afterFrame = Time.frameCount + 4;
                }
                else if (step == 3)
                {
                    liveRunner = UnityEngine.Object.FindAnyObjectByType<StoryRunner>();
                    Require(liveRunner != null && liveRunner.enabled, "真实VNMain场景的StoryRunner初始化失败。");
                    MenuController menu = UnityEngine.Object.FindAnyObjectByType<MenuController>();
                    Require(menu != null, "VNMain缺少MenuController。");
                    menu.titlePanel.SetActive(false); menu.storyPanel.SetActive(true);
                    SetField(liveRunner, "suppressAutoSave", true);
                    liveProfile = new VNProfile(); SetField(liveRunner, "profile", liveProfile);
                    VNStory story = MakeStory();
                    story.nodes = story.nodes.Concat(new[] { new VNNode { id = "shared.end", type = "end", text = "Entertainment complete", completeChapter = "B03", memorialId = "must_not_unlock", collectionId = "must_not_collect", resultId = "canonical" } }).ToArray();
                    VNRouteIndex index = MakeIndex(1); VNRoute fun = index.routes.Last(); fun.entryNode = "shared.end"; fun.nodes = Array.Empty<VNNode>();
                    StoryRepository repository = new StoryRepository(); repository.Load(story, index); SetField(liveRunner, "repository", repository);
                    GameState checkpoint = new GameState { currentNodeId = "choice.first" };
                    checkpoint.history.Add(new HistoryEntry { nodeId = "opening", text = "before branch" });
                    liveRunner.Restore(checkpoint.ToSnapshot(repository.ContentVersion));
                    liveRunner.SelectChoice("fun_00");
                    step = 4; afterFrame = Time.frameCount + 3;
                }
                else if (step == 4)
                {
                    GameState finished = GetState(liveRunner);
                    Require(liveRunner.RunCompleted && finished.routeKind == RouteKinds.Entertainment && !finished.mainCompleted, "实际娱乐路线未通关或误记主线。");
                    Require(finished.completedChapters.Count == 0 && liveProfile.completedChapters.Count == 0 && liveProfile.memorials.Count == 0 && liveProfile.collections.Count == 0, "共享收尾写入正式奖励。");
                    report.passed.Add("真实StoryRunner娱乐进入共享B03终点通关且不记录奖励");
                    VNSnapshot deleted = finished.ToSnapshot("integration");
                    deleted.currentNodeId = "removed.node"; deleted.routeId = "removed_fun";
                    deleted.visuals.backgroundId = "removed.background";
                    deleted.visuals.props.Add(new VNPropState { instanceId = "removed", assetId = "removed.prop", scale = 1 });
                    liveRunner.Restore(deleted);
                    GameState recovered = GetState(liveRunner);
                    Require(recovered.currentNodeId == "choice.first" && string.IsNullOrEmpty(recovered.routeId) && !recovered.runCompleted, "缺失路线未恢复选择前运行状态。");
                    Require(recovered.history.Count == 1 && recovered.history[0].text == "before branch" && recovered.visuals.props.Count == 0 && string.IsNullOrEmpty(recovered.visuals.backgroundId), "缺失路线恢复残留分支画面或日志。");
                    report.passed.Add("真实StoryRunner删除路线存档恢复完整选择前检查点");
                    step = 5; afterFrame = Time.frameCount + 3;
                }
                else if (step == 5)
                {
                    ChoiceView recoveryView = (ChoiceView)typeof(StoryRunner).GetField("choiceView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(liveRunner);
                    Require(recoveryView.gameObject.activeInHierarchy, "删除路线提示没有显示。");
                    Button[] recoveryButtons = recoveryView.GetComponentsInChildren<Button>();
                    Require(recoveryButtons.Length == 1 && recoveryButtons[0].interactable, "删除路线恢复提示不可继续。");
                    recoveryButtons[0].onClick.Invoke();
                    step = 6; afterFrame = Time.frameCount + 2;
                }
                else if (step == 6)
                {
                    ChoiceView restoredChoices = (ChoiceView)typeof(StoryRunner).GetField("choiceView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(liveRunner);
                    Require(restoredChoices.GetComponentsInChildren<Button>().Length == 3, "关闭恢复提示后未回到动态选择菜单。");
                    report.passed.Add("恢复提示可继续并重新显示路线菜单");
                    CompleteUi();
                }
            }
            catch (Exception ex) { FailUi(ex); }
        }

        private static void OnSelected(string id) { callbacks++; selected = id; }
        private static GameState GetState(StoryRunner runner) => (GameState)typeof(StoryRunner).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runner);
        private static void CaptureUi(string filename)
        {
            RenderTexture target = null; Texture2D image = null; RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                target = new RenderTexture(1280, 720, 24); camera.targetTexture = target; camera.Render();
                RenderTexture.active = target; image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(ProjectRoot, filename), image.EncodeToPNG());
                camera.targetTexture = null;
            }
            catch (Exception ex) { report.notes.Add("截图不可用：" + ex.Message); }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) UnityEngine.Object.Destroy(target);
                if (image != null) UnityEngine.Object.Destroy(image);
            }
        }
        private static void FailUi(Exception exception)
        {
            report.failed.Add("ChoiceView逐帧检查: " + exception);
            CompleteUi();
        }
        private static void CompleteUi()
        {
            SessionState.SetBool(Prefix + "Pending", false);
            SessionState.SetBool(Prefix + "Finalize", true);
            SaveReport();
            EditorApplication.isPlaying = false;
        }
        private static void Finish()
        {
            SaveReport();
            string summary = $"ROUTE_INTEGRATION_RESULT passed={report.passed.Count} failed={report.failed.Count}";
            if (report.failed.Count == 0) Debug.Log(summary); else Debug.LogError(summary + "\n" + string.Join("\n", report.failed));
            if (SessionState.GetBool(Prefix + "Batch", false)) EditorApplication.Exit(report.failed.Count == 0 ? 0 : 1);
        }
        private static void SaveReport()
        {
            string json = JsonUtility.ToJson(report, true);
            SessionState.SetString(Prefix + "Report", json);
            if (IsIsolatedProject()) File.WriteAllText(Path.Combine(ProjectRoot, "route-integration-results.json"), json);
        }
        private static void RunCase(string title, Action action)
        {
            try { action(); report.passed.Add(title); }
            catch (Exception ex) { report.failed.Add(title + ": " + ex); }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void MustReject(Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { return; }
            throw new InvalidOperationException("无效路线被接受。");
        }
        private static void SetField(object instance, string field, object value)
        {
            typeof(StoryRunner).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
        }
        private const string LegacyJson = "{\"magic\":\"BAIGU_V2_SAVE\",\"schemaVersion\":2,\"contentVersion\":\"legacy-content\",\"currentNodeId\":\"old.node\",\"pageIndex\":0,\"mainCompleted\":true}";
        private static GameState MakeState()
        {
            GameState state = new GameState { currentNodeId = "fun.current", routeId = "fun_01", routeKind = RouteKinds.Entertainment, routeRevision = 2, runCompleted = true, resultId = "fun.complete" };
            state.history.Add(new HistoryEntry { nodeId = "history", text = "original", speaker = "Wukong" });
            state.visuals.props.Add(new VNPropState { instanceId = "staff", assetId = "staff.sprite", x = 5, y = 8, scale = 1 });
            state.checkpoint = state.ToRunSnapshot(); state.checkpoint.currentNodeId = "choice.first";
            state.checkpoint.routeId = null; state.checkpoint.routeKind = null; state.checkpoint.runCompleted = false;
            return state;
        }
        private static VNStory MakeStory() => new VNStory { schemaVersion = 3, contentVersion = "integration", startNode = "opening", nodes = new[] {
            new VNNode { id = "opening", type = "line", text = "Opening", next = "choice.first" },
            new VNNode { id = "choice.first", type = "choice", choiceGroupId = "first_encounter", text = "Choose" }
        } };
        private static VNRoute MakeRoute(string id, string kind, int order) => new VNRoute {
            routeId = id, kind = kind, title = id, choiceText = id, order = order,
            status = "published", artReviewed = true, entryNode = id + ".end",
            nodes = new[] { new VNNode { id = id + ".end", type = "end", text = "Complete" } }
        };
        private static VNRouteIndex MakeIndex(int entertainmentCount)
        {
            List<VNRoute> routes = new List<VNRoute> { MakeRoute("canonical", RouteKinds.Canonical, 0), MakeRoute("perfect", RouteKinds.Perfect, 1) };
            for (int i = 0; i < entertainmentCount; i++) routes.Add(MakeRoute("fun_" + i.ToString("00"), RouteKinds.Entertainment, 10 + i));
            return new VNRouteIndex { routes = routes.ToArray() };
        }
        private static StoryRepository Load(VNRouteIndex index)
        {
            StoryRepository repository = new StoryRepository(); repository.Load(MakeStory(), index); return repository;
        }
        private static List<VNChoice> Choices(int count)
        {
            return Enumerable.Range(0, count).Select(i => new VNChoice { id = "option_" + i.ToString("00"), label = i == 0 ? "举棒降妖，继续原著路线" : i == 1 ? "暂不动手，用机智化解危机" : "娱乐身份选项 " + (i - 1).ToString("00") + "：变化身份与白骨精周旋", next = "end" }).ToList();
        }
    }
}
#endif
