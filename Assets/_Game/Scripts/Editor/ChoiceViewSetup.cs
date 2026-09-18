#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BaiguVN.Editor
{
    // 一键搭建 ChoiceView 的 UI，并挂接到场景里的 StoryRunner。
    //
    // 用法：在 Unity 编辑器菜单点击 Baigu -> Setup ChoiceView。
    // 运行一次即可，之后无需手动拖拽。
    public static class ChoiceViewSetup
    {
        private const string FontAssetPath =
            "Assets/_Game/Fonts/NotoSansSC[wght] SDF.asset";

        private const string RootName = "ChoiceView";

        [MenuItem("Baigu/Setup ChoiceView")]
        public static void Build()
        {
            Canvas canvas =
                UnityEngine.Object.FindFirstObjectByType<Canvas>();

            if (canvas == null)
            {
                Debug.LogError("场景里找不到 Canvas。");
                return;
            }

            StoryRunner runner =
                UnityEngine.Object.FindFirstObjectByType<StoryRunner>();

            if (runner == null)
            {
                Debug.LogError("场景里找不到 StoryRunner。");
                return;
            }

            TMP_FontAsset font =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    FontAssetPath
                );

            // 已存在则删除重建，保证幂等。
            Transform existing =
                canvas.transform.Find(RootName);

            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    existing.gameObject
                );
            }

            // ---------- 根：全屏遮罩 ----------
            RectTransform rootRect = MakeRect(
                RootName,
                canvas.transform,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero
            );

            Image rootBg =
                rootRect.gameObject.AddComponent<Image>();

            rootBg.color = new Color(0f, 0f, 0f, 0.5f);
            rootBg.raycastTarget = true;

            ChoiceView view =
                rootRect.gameObject.AddComponent<ChoiceView>();

            // ---------- 主面板 ----------
            RectTransform panel = MakeRect(
                "Panel",
                rootRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-480f, -320f),
                new Vector2(480f, 320f)
            );

            Image panelBg =
                panel.gameObject.AddComponent<Image>();

            panelBg.color =
                new Color(0.08f, 0.08f, 0.12f, 0.97f);

            // ---------- 提示文字 ----------
            TMP_Text speaker = CreateText(
                "SpeakerText",
                panel,
                font,
                30,
                TextAlignmentOptions.TopLeft,
                Color.white
            );

            SetRect(
                (RectTransform)speaker.transform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(28f, -64f),
                new Vector2(-28f, -22f)
            );

            TMP_Text prompt = CreateText(
                "PromptText",
                panel,
                font,
                24,
                TextAlignmentOptions.TopLeft,
                new Color(0.85f, 0.85f, 0.9f)
            );

            SetRect(
                (RectTransform)prompt.transform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(28f, -208f),
                new Vector2(-28f, -72f)
            );

            // ---------- ScrollRect ----------
            RectTransform scrollRect = MakeRect(
                "Scroll",
                panel,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(28f, 24f),
                new Vector2(-28f, -208f)
            );

            ScrollRect sr =
                scrollRect.gameObject.AddComponent<ScrollRect>();

            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType =
                ScrollRect.MovementType.Clamped;

            // Viewport
            RectTransform viewport = MakeRect(
                "Viewport",
                scrollRect,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero
            );

            Image viewportImg =
                viewport.gameObject.AddComponent<Image>();

            viewportImg.color = Color.clear;
            viewport.gameObject.AddComponent<Mask>();

            // Content
            RectTransform content = MakeRect(
                "Content",
                viewport,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f)
            );

            content.pivot = new Vector2(0.5f, 1f);

            VerticalLayoutGroup layout =
                content.gameObject.AddComponent<VerticalLayoutGroup>();

            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, 0, 0);

            ContentSizeFitter fitter =
                content.gameObject.AddComponent<ContentSizeFitter>();

            fitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = viewport;
            sr.content = content;

            // ---------- 按钮模板 ----------
            RectTransform template = MakeRect(
                "ChoiceButtonTemplate",
                panel,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -70f),
                new Vector2(0f, 0f)
            );

            template.pivot = new Vector2(0.5f, 1f);

            Image btnBg =
                template.gameObject.AddComponent<Image>();

            btnBg.color =
                new Color(0.16f, 0.16f, 0.22f, 1f);

            Button btn =
                template.gameObject.AddComponent<Button>();

            btn.targetGraphic = btnBg;

            TMP_Text label = CreateText(
                "Label",
                template,
                font,
                24,
                TextAlignmentOptions.MidlineLeft,
                Color.white
            );

            SetRect(
                (RectTransform)label.transform,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(20f, 0f),
                new Vector2(-20f, 0f)
            );

            // 模板仅用于克隆，平时隐藏。
            template.gameObject.SetActive(false);

            // ---------- 挂接 ----------
            view.speakerText = speaker;
            view.promptText = prompt;
            view.contentRoot = content;
            view.choiceButtonPrefab = template.gameObject;

            runner.choiceView = view;

            // 初始隐藏，进入 choice 节点时才显示。
            rootRect.gameObject.SetActive(false);

            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(runner);
            EditorSceneManager.MarkSceneDirty(
                canvas.gameObject.scene
            );

            Debug.Log(
                "ChoiceView 已搭建并挂接到 StoryRunner。"
            );
        }

        private static RectTransform MakeRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform)
            );

            go.transform.SetParent(parent, false);

            RectTransform rect =
                (RectTransform)go.transform;

            SetRect(
                rect,
                anchorMin,
                anchorMax,
                offsetMin,
                offsetMax
            );

            return rect;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform)
            );

            go.transform.SetParent(parent, false);

            TextMeshProUGUI tmp =
                go.AddComponent<TextMeshProUGUI>();

            if (font != null)
            {
                tmp.font = font;
            }

            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;

            return tmp;
        }
    }
}
#endif
