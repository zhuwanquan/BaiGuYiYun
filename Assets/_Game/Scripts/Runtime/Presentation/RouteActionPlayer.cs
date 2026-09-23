using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BaiguVN
{
    // Snapshot state owns persistent pictures; transient effects never enter saves.
    public sealed class RouteActionPlayer : MonoBehaviour
    {
        public bool IsBusy { get; private set; }
        private PresentationController presentation;
        private RouteResourceService resources;
        private RectTransform layer;
        private RectTransform propLayer;
        private readonly Dictionary<string, Image> props = new Dictionary<string, Image>(StringComparer.Ordinal);
        private readonly List<Image> transientImages = new List<Image>();
        private readonly Dictionary<RectTransform, Vector2> shakeOrigins = new Dictionary<RectTransform, Vector2>();
        private int generation;
        private bool paused;

        public void Configure(PresentationController controller, RouteResourceService service)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (service == null) throw new ArgumentNullException(nameof(service));
            ResetForNewGame();
            if (layer != null) Destroy(layer.gameObject);
            layer = null;
            propLayer = null;
            presentation = controller;
            resources = service;
        }

        public void SetPaused(bool value) => paused = value;

        public IEnumerator Play(VNVisualAction[] actions, VisualSnapshot state)
        {
            if (presentation == null || resources == null)
                throw new InvalidOperationException("RouteActionPlayer must be configured before playback.");
            if (state == null) throw new ArgumentNullException(nameof(state));
            Cancel();
            int token = generation;
            IsBusy = true;
            return RunPlayback(actions ?? Array.Empty<VNVisualAction>(), state, token);
        }

        private IEnumerator RunPlayback(VNVisualAction[] actions, VisualSnapshot state, int token)
        {
            // Flatten nested iterators so action exceptions always run our finally.
            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(Sequence(actions, state, token));
            try
            {
                while (token == generation && stack.Count > 0)
                {
                    IEnumerator current = stack.Peek();
                    if (!current.MoveNext())
                    {
                        stack.Pop();
                        (current as IDisposable)?.Dispose();
                    }
                    else if (current.Current is IEnumerator nested) stack.Push(nested);
                    else yield return current.Current;
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                if (token == generation)
                {
                    ClearTransientEffects();
                    IsBusy = false;
                }
            }
        }

        private IEnumerator Sequence(VNVisualAction[] actions, VisualSnapshot state, int token)
        {
            foreach (VNVisualAction action in actions)
            {
                if (action == null) throw new InvalidOperationException("A visual action is null.");
                ValidDuration(action.duration);
                ValidatePosition(action.x, action.y);
                yield return WaitForPresentation(token);
                if (token != generation) yield break;
                switch (action.type)
                {
                    case "transform": yield return TransformPortrait(action, state, token); break;
                    case "flash": yield return Flash(action, token); break;
                    case "shake": yield return Shake(action, token); break;
                    case "prop": ShowProp(action, state); break;
                    case "clearProp": ClearProp(action.instanceId, state); break;
                    case "wait": yield return Delay(action.duration, token); break;
                    default: throw new InvalidOperationException("Unknown visual action: " + action.type);
                }
            }
            yield return WaitForPresentation(token);
        }

        private IEnumerator WaitForPresentation(int token)
        {
            while (token == generation && (paused || presentation.IsBusy)) yield return null;
        }

        private IEnumerator TransformPortrait(VNVisualAction action, VisualSnapshot state, int token)
        {
            Image slot = GetSlot(action.slot);
            ValidScale(action.scale);
            RequireSprite(action.assetId, "portrait");
            Image effect = null;
            if (!string.IsNullOrEmpty(action.effectId))
            {
                effect = NewTransient("Transformation", RequireSprite(action.effectId, "effect"));
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(layer, slot.rectTransform);
                effect.rectTransform.anchorMin = effect.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                effect.rectTransform.anchoredPosition = (Vector2)bounds.center + new Vector2(action.x, action.y);
                effect.rectTransform.sizeDelta = bounds.size;
                effect.rectTransform.localScale = Vector3.one * ValidScale(action.scale);
                effect.preserveAspect = true;
                yield return Fade(effect, 0f, 1f, action.duration * 0.5f, token);
            }
            if (token != generation) yield break;
            presentation.ApplyPortrait(action.assetId, action.slot);
            switch (action.slot)
            {
                case 0: state.leftPortraitId = action.assetId; break;
                case 1: state.centerPortraitId = action.assetId; break;
                case 2: state.rightPortraitId = action.assetId; break;
            }
            yield return WaitForPresentation(token);
            if (effect != null)
            {
                yield return Fade(effect, 1f, 0f, action.duration * 0.5f, token);
                RemoveTransient(effect);
            }
        }

        private IEnumerator Flash(VNVisualAction action, int token)
        {
            Sprite sprite = string.IsNullOrEmpty(action.effectId) ? null : RequireSprite(action.effectId, "effect");
            Image effect = NewTransient("Flash", sprite);
            yield return Fade(effect, 0f, 1f, action.duration * 0.5f, token);
            yield return Fade(effect, 1f, 0f, action.duration * 0.5f, token);
            RemoveTransient(effect);
        }

        private IEnumerator Shake(VNVisualAction action, int token)
        {
            RestoreShake();
            if (action.slot == -1)
            {
                AddShakeTarget(presentation.background);
                AddShakeTarget(presentation.cgImage);
                if (presentation.slots != null)
                    foreach (Image image in presentation.slots) AddShakeTarget(image);
                EnsureLayer();
                shakeOrigins[propLayer] = propLayer.anchoredPosition;
            }
            else AddShakeTarget(GetSlot(action.slot));

            float duration = ValidDuration(action.duration);
            float strengthX = action.x == 0f && action.y == 0f ? 14f : action.x;
            float strengthY = action.x == 0f && action.y == 0f ? 7f : action.y;
            float elapsed = 0f;
            while (token == generation && elapsed < duration)
            {
                if (!paused)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float decay = 1f - Mathf.Clamp01(elapsed / duration);
                    Vector2 offset = new Vector2(Mathf.Sin(elapsed * 83f) * strengthX,
                        Mathf.Sin(elapsed * 109f) * strengthY) * decay;
                    foreach (KeyValuePair<RectTransform, Vector2> origin in shakeOrigins)
                        if (origin.Key != null) origin.Key.anchoredPosition = origin.Value + offset;
                }
                yield return null;
            }
            if (token == generation) RestoreShake();
        }

        private void AddShakeTarget(Image image)
        {
            if (image != null && !shakeOrigins.ContainsKey(image.rectTransform))
                shakeOrigins.Add(image.rectTransform, image.rectTransform.anchoredPosition);
        }

        private void ShowProp(VNVisualAction action, VisualSnapshot state)
        {
            VNPropState prop = new VNPropState
            {
                instanceId = ValidInstanceId(action.instanceId), assetId = action.assetId,
                x = action.x, y = action.y, scale = ValidScale(action.scale)
            };
            DrawProp(prop);
            if (state.props == null) state.props = new List<VNPropState>();
            state.props.RemoveAll(item => item == null || item.instanceId == prop.instanceId);
            state.props.Add(prop);
        }

        private void DrawProp(VNPropState prop)
        {
            string id = ValidInstanceId(prop.instanceId);
            ValidScale(prop.scale);
            ValidatePosition(prop.x, prop.y);
            Sprite sprite = RequireSprite(prop.assetId, "prop");
            EnsureLayer();
            if (!props.TryGetValue(id, out Image image) || image == null)
            {
                image = NewImage("Prop_" + id, propLayer);
                props[id] = image;
            }
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = sprite.rect.size;
            rect.anchoredPosition = new Vector2(prop.x, prop.y);
            rect.localScale = Vector3.one * ValidScale(prop.scale);
            image.gameObject.SetActive(true);
            rect.SetAsLastSibling();
        }

        private void ClearProp(string instanceId, VisualSnapshot state)
        {
            if (instanceId == "*")
            {
                ClearPropImages();
                state.props?.Clear();
                return;
            }
            string id = ValidInstanceId(instanceId);
            if (props.TryGetValue(id, out Image image))
            {
                DestroyImage(image);
                props.Remove(id);
            }
            state.props?.RemoveAll(item => item == null || item.instanceId == id);
        }

        public void RestoreProps(VisualSnapshot state)
        {
            Cancel();
            ClearPropImages();
            if (state?.props == null) return;
            foreach (VNPropState prop in state.props)
            {
                if (prop == null) throw new InvalidOperationException("Saved prop is null.");
                DrawProp(prop);
            }
        }

        public void Cancel()
        {
            generation++;
            ClearTransientEffects();
            IsBusy = false;
        }

        public void ResetForNewGame()
        {
            Cancel();
            paused = false;
            ClearPropImages();
        }

        private void OnDisable() => Cancel();

        private void OnDestroy()
        {
            ResetForNewGame();
            if (layer != null) Destroy(layer.gameObject);
        }

        private IEnumerator Delay(float seconds, int token)
        {
            float elapsed = 0f;
            float duration = ValidDuration(seconds);
            while (token == generation && elapsed < duration)
            {
                if (!paused) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator Fade(Image image, float from, float to, float seconds, int token)
        {
            float elapsed = 0f;
            float duration = ValidDuration(seconds);
            while (token == generation && image != null && elapsed < duration)
            {
                if (!paused)
                {
                    elapsed += Time.unscaledDeltaTime;
                    image.color = new Color(1f, 1f, 1f, Mathf.Lerp(from, to, elapsed / duration));
                }
                yield return null;
            }
            if (token == generation && image != null) image.color = new Color(1f, 1f, 1f, to);
        }

        private Sprite RequireSprite(string id, string kind)
        {
            Sprite sprite = resources.GetSprite(id, kind);
            if (sprite == null) throw new InvalidOperationException("Missing " + kind + " resource: " + id);
            return sprite;
        }

        private Image GetSlot(int slot)
        {
            if (presentation.slots == null || slot < 0 || slot > 2 ||
                slot >= presentation.slots.Length || presentation.slots[slot] == null)
                throw new InvalidOperationException("Portrait slot is unavailable: " + slot);
            return presentation.slots[slot];
        }

        private void EnsureLayer()
        {
            if (layer != null) return;
            Transform parent = presentation.background != null ? presentation.background.transform.parent : null;
            if (parent == null)
            {
                Canvas canvas = presentation.GetComponentInParent<Canvas>();
                if (canvas == null) throw new InvalidOperationException("Presentation has no UI canvas.");
                parent = canvas.transform;
            }
            layer = NewRect("RouteVisualEffects", parent);
            propLayer = NewRect("Props", layer);
            // Effects stay below the transition curtain and dialogue/menu controls.
            Transform curtain = presentation.fadeOverlay != null ? presentation.fadeOverlay.transform : null;
            if (curtain != null && curtain.parent == parent) layer.SetSiblingIndex(curtain.GetSiblingIndex());
            else
            {
                int index = 0;
                if (presentation.cgImage != null && presentation.cgImage.transform.parent == parent)
                    index = presentation.cgImage.transform.GetSiblingIndex() + 1;
                else if (presentation.background != null && presentation.background.transform.parent == parent)
                    index = presentation.background.transform.GetSiblingIndex() + 1;
                layer.SetSiblingIndex(index);
            }
        }

        private static RectTransform NewRect(string objectName, Transform parent)
        {
            GameObject obj = new GameObject(objectName, typeof(RectTransform));
            obj.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static Image NewImage(string objectName, Transform parent)
        {
            RectTransform rect = NewRect(objectName, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private Image NewTransient(string objectName, Sprite sprite)
        {
            EnsureLayer();
            Image image = NewImage(objectName, layer);
            image.sprite = sprite;
            image.color = new Color(1f, 1f, 1f, 0f);
            transientImages.Add(image);
            return image;
        }

        private void RemoveTransient(Image image)
        {
            transientImages.Remove(image);
            DestroyImage(image);
        }

        private void ClearTransientEffects()
        {
            RestoreShake();
            foreach (Image image in transientImages) DestroyImage(image);
            transientImages.Clear();
        }

        private void RestoreShake()
        {
            foreach (KeyValuePair<RectTransform, Vector2> origin in shakeOrigins)
                if (origin.Key != null) origin.Key.anchoredPosition = origin.Value;
            shakeOrigins.Clear();
        }

        private void ClearPropImages()
        {
            foreach (Image image in props.Values) DestroyImage(image);
            props.Clear();
        }

        private static void DestroyImage(Image image)
        {
            if (image == null) return;
            image.gameObject.SetActive(false);
            Destroy(image.gameObject);
        }

        private static float ValidDuration(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                throw new InvalidOperationException("Visual duration must be finite and nonnegative.");
            return value;
        }

        private static void ValidatePosition(float x, float y)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
                throw new InvalidOperationException("Visual offsets must be finite.");
        }

        private static float ValidScale(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                throw new InvalidOperationException("Visual scale must be finite and positive.");
            return value;
        }

        private static string ValidInstanceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "*")
                throw new InvalidOperationException("A prop instance ID must be nonempty and cannot be '*'.");
            return value;
        }
    }
}
