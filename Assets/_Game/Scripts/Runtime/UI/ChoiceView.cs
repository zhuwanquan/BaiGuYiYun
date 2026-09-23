using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BaiguVN
{
    /// <summary>A runtime-created, modal list of story choices.</summary>
    public sealed class ChoiceView : MonoBehaviour
    {
        public Transform ControlsAbove { get; set; }
        private readonly List<Button> buttons = new List<Button>();
        private CanvasGroup canvasGroup;
        private TMP_FontAsset font;
        private TMP_Text promptText;
        private TMP_Text hintText;
        private RectTransform viewport;
        private RectTransform content;
        private ScrollRect scrollRect;
        private Scrollbar scrollbar;
        private Coroutine focusCoroutine;
        private bool interactionRequested;
        private bool committed;
        private int acceptInputAfterFrame;
        private int lastFocusedIndex;
        private GameObject focusedObject;

        public static ChoiceView Create(Transform parent, TMP_FontAsset font)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            RectTransform root = CreateRect("ChoicePanel", parent);
            Stretch(root);
            Image shade = root.gameObject.AddComponent<Image>();
            shade.color = new Color(0.025f, 0.022f, 0.018f, 0.88f);

            ChoiceView view = root.gameObject.AddComponent<ChoiceView>();
            view.font = font;
            view.canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
            view.BuildPanel();
            view.Hide();
            return view;
        }

        public void Show(string prompt, IList<VNChoice> choices, Action<string> onSelected)
        {
            BeginShow(string.IsNullOrWhiteSpace(prompt)
                ? "请选择悟空接下来的行动" : prompt);

            if (choices != null)
            {
                for (int i = 0; i < choices.Count; i++)
                {
                    VNChoice choice = choices[i];
                    if (choice == null || string.IsNullOrWhiteSpace(choice.id))
                        continue;

                    string choiceId = choice.id;
                    AddOption(string.IsNullOrWhiteSpace(choice.label)
                        ? "未命名选项" : choice.label,
                        () => onSelected?.Invoke(choiceId));
                }
            }

            if (buttons.Count == 0)
            {
                TMP_Text empty = CreateText("Empty", content, 26f);
                empty.text = "暂无可选剧情。";
            }

            FinishShow();
        }

        public void ShowMessage(string message, string actionLabel, Action action,
            string cancelLabel = null, Action cancel = null)
        {
            BeginShow(message ?? "");
            AddOption(string.IsNullOrWhiteSpace(actionLabel) ? "返回" : actionLabel,
                () => { if (action != null) action(); else Hide(); });

            if (!string.IsNullOrWhiteSpace(cancelLabel))
            {
                AddOption(cancelLabel,
                    () => { if (cancel != null) cancel(); else Hide(); });
            }

            FinishShow();
        }

        public void Hide()
        {
            committed = true;
            interactionRequested = false;
            StopFocusCoroutine();
            ClearSelectionIfOwned();
            ClearOptions();
            gameObject.SetActive(false);
        }

        public void SetInteractable(bool enabled)
        {
            interactionRequested = enabled;
            bool interactive = enabled && !committed;
            canvasGroup.interactable = interactive;
            // The modal backdrop continues to protect the scene underneath.
            canvasGroup.blocksRaycasts = true;
            scrollRect.enabled = interactive;
            scrollbar.interactable = interactive;

            for (int i = 0; i < buttons.Count; i++)
                buttons[i].interactable = interactive;

            if (!interactive)
            {
                StopFocusCoroutine();
                scrollRect.StopMovement();
                ClearSelectionIfOwned();
            }
            else if (isActiveAndEnabled && focusCoroutine == null &&
                (EventSystem.current == null ||
                 EventSystem.current.currentSelectedGameObject == null ||
                 !EventSystem.current.currentSelectedGameObject.transform.IsChildOf(content)))
            {
                focusCoroutine = StartCoroutine(FocusAfterLayout());
            }
        }

        private void BuildPanel()
        {
            RectTransform panel = CreateRect("ChoiceFrame", transform);
            panel.anchorMin = new Vector2(0.12f, 0.10f);
            panel.anchorMax = new Vector2(0.88f, 0.91f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.105f, 0.09f, 0.075f, 1f);

            VerticalLayoutGroup panelLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(28, 28, 24, 20);
            panelLayout.spacing = 18f;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            promptText = CreateText("Prompt", panel, 32f);
            promptText.fontStyle = FontStyles.Bold;

            RectTransform scrollArea = CreateRect("ChoiceScroll", panel);
            LayoutElement scrollLayout = scrollArea.gameObject.AddComponent<LayoutElement>();
            scrollLayout.minHeight = 100f;
            scrollLayout.flexibleHeight = 1f;
            Image scrollBackground = scrollArea.gameObject.AddComponent<Image>();
            scrollBackground.color = new Color(0f, 0f, 0f, 0.08f);
            scrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();

            viewport = CreateRect("Viewport", scrollArea);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-20f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup listLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(3, 3, 3, 3);
            listLayout.spacing = 12f;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollbar = CreateScrollbar(scrollArea);
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 45f;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            hintText = CreateText("ScrollHint", panel, 20f);
            hintText.text = "滑动或滚动查看更多选项";
            hintText.color = new Color(0.72f, 0.66f, 0.54f, 1f);
        }

        private Scrollbar CreateScrollbar(RectTransform parent)
        {
            RectTransform track = CreateRect("Scrollbar", parent);
            track.anchorMin = new Vector2(1f, 0f);
            track.anchorMax = Vector2.one;
            track.pivot = new Vector2(1f, 0.5f);
            track.sizeDelta = new Vector2(10f, 0f);
            track.anchoredPosition = Vector2.zero;
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0.2f, 0.17f, 0.13f, 1f);

            RectTransform handle = CreateRect("Handle", track);
            Stretch(handle);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.64f, 0.50f, 0.29f, 1f);

            Scrollbar bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = handleImage;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            return bar;
        }

        private void AddOption(string label, Action action)
        {
            RectTransform row = CreateRect("Choice", content);
            Image image = row.gameObject.AddComponent<Image>();
            image.color = Color.white;
            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.22f, 0.185f, 0.135f, 1f);
            colors.highlightedColor = new Color(0.39f, 0.31f, 0.19f, 1f);
            colors.selectedColor = new Color(0.39f, 0.31f, 0.19f, 1f);
            colors.pressedColor = new Color(0.52f, 0.40f, 0.22f, 1f);
            colors.disabledColor = new Color(0.16f, 0.14f, 0.105f, 1f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            LayoutElement rowElement = row.gameObject.AddComponent<LayoutElement>();
            rowElement.minHeight = 76f;
            HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(22, 22, 16, 16);
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            TMP_Text text = CreateText("Label", row, 28f);
            text.text = label;
            LayoutElement textLayout = text.gameObject.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1f;

            button.onClick.AddListener(() => CommitSelection(action));
            buttons.Add(button);
        }

        private void BeginShow(string prompt)
        {
            StopFocusCoroutine();
            ClearSelectionIfOwned();
            ClearOptions();
            committed = false;
            interactionRequested = true;
            acceptInputAfterFrame = Time.frameCount + 1;
            lastFocusedIndex = 0;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (ControlsAbove != null && ControlsAbove.parent == transform.parent)
                ControlsAbove.SetAsLastSibling();
            promptText.text = prompt;
            scrollRect.StopMovement();
            content.anchoredPosition = Vector2.zero;
        }

        private void FinishShow()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = buttons[Mathf.Max(0, i - 1)],
                    selectOnDown = buttons[Mathf.Min(buttons.Count - 1, i + 1)]
                };
            }

            hintText.gameObject.SetActive(buttons.Count > 5);
            SetInteractable(true);
        }

        private void CommitSelection(Action action)
        {
            if (!isActiveAndEnabled || !interactionRequested || committed ||
                Time.frameCount < acceptInputAfterFrame)
                return;

            committed = true;
            SetInteractable(false);
            action?.Invoke();
        }

        private IEnumerator FocusAfterLayout()
        {
            yield return null;
            focusCoroutine = null;
            if (!CanReceiveInput() || buttons.Count == 0)
                yield break;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            FocusLastOption();
        }

        private void LateUpdate()
        {
            // A background click can clear selection. Restore navigation without
            // polling legacy Input APIs or submitting any option automatically.
            if (!CanReceiveInput() || focusCoroutine != null || buttons.Count == 0 ||
                EventSystem.current == null)
                return;

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null)
            {
                FocusLastOption();
                return;
            }

            if (selected == focusedObject || !selected.transform.IsChildOf(content))
                return;

            int index = buttons.IndexOf(selected.GetComponent<Button>());
            if (index >= 0)
            {
                focusedObject = selected;
                lastFocusedIndex = index;
                EnsureVisible((RectTransform)buttons[index].transform);
            }
        }

        private bool CanReceiveInput()
        {
            return isActiveAndEnabled && interactionRequested && !committed;
        }

        private void FocusLastOption()
        {
            if (EventSystem.current == null || buttons.Count == 0)
                return;

            lastFocusedIndex = Mathf.Clamp(lastFocusedIndex, 0, buttons.Count - 1);
            focusedObject = buttons[lastFocusedIndex].gameObject;
            EventSystem.current.SetSelectedGameObject(focusedObject);
            EnsureVisible((RectTransform)buttons[lastFocusedIndex].transform);
        }

        private void EnsureVisible(RectTransform row)
        {
            Canvas.ForceUpdateCanvases();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, row);
            Rect visible = viewport.rect;
            float adjustment = 0f;
            if (bounds.size.y > visible.height || bounds.max.y > visible.yMax)
                adjustment = visible.yMax - bounds.max.y;
            else if (bounds.min.y < visible.yMin)
                adjustment = visible.yMin - bounds.min.y;

            if (Mathf.Abs(adjustment) > 0.1f)
            {
                scrollRect.StopMovement();
                Vector2 position = content.anchoredPosition;
                position.y = Mathf.Clamp(position.y + adjustment, 0f,
                    Mathf.Max(0f, content.rect.height - visible.height));
                content.anchoredPosition = position;
            }
        }

        private void ClearOptions()
        {
            for (int i = 0; i < buttons.Count; i++)
                if (buttons[i] != null) buttons[i].onClick.RemoveAllListeners();
            buttons.Clear();

            if (content == null)
                return;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                GameObject child = content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private void ClearSelectionIfOwned()
        {
            focusedObject = null;
            if (EventSystem.current == null)
                return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void StopFocusCoroutine()
        {
            if (focusCoroutine == null)
                return;
            StopCoroutine(focusCoroutine);
            focusCoroutine = null;
        }

        private void OnDisable()
        {
            StopFocusCoroutine();
            ClearSelectionIfOwned();
        }

        private TMP_Text CreateText(string name, Transform parent, float size)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = new Color(0.96f, 0.91f, 0.80f, 1f);
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

}
