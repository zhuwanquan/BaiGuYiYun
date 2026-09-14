using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace BaiguVN
{
    [Serializable]
    public class VNSpriteEntry
    {
        public string id;
        public Sprite sprite;
    }

    public class PresentationController : MonoBehaviour
    {
        [Header("UI")]
        public Image background;
        public Image cgImage;
        public Image fadeOverlay;

        [Header("Character Slots")]
        public Image[] slots;

        [Header("Background Resources")]
        public VNSpriteEntry[] backgrounds;

        [Header("Portrait Resources")]
        public VNSpriteEntry[] portraits;

        [Header("Portrait Focus")]
        public Color activePortraitColor =
            Color.white;

        [Header("Character Fade")]
        [SerializeField, Min(0f)]
        private float characterFadeSeconds = 0.25f;

        public bool IsBusy { get; private set; }

        private int activeCharacterFadeCount;

        public Color inactivePortraitColor =
            new Color(
                0.55f,
                0.55f,
                0.55f,
                1f
            );

        private void Awake()
        {
            // 启动时隐藏所有空角色槽
            HideAllCharacters();

            // 启动时隐藏 CG
            if (cgImage != null)
            {
                cgImage.sprite = null;
                cgImage.enabled = false;
            }
        }

        // =========================================================
        // 背景
        // =========================================================

        public void ApplyBackground(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            if (background == null)
            {
                return;
            }

            // "-" = 清除背景
            if (id == "-")
            {
                background.sprite = null;
                background.enabled = false;
                return;
            }

            Sprite sprite =
                FindSprite(backgrounds, id);

            if (sprite == null)
            {
                Debug.LogWarning(
                    $"找不到背景资源：{id}"
                );
                return;
            }

            background.sprite = sprite;
            background.enabled = true;
        }

        // =========================================================
        // 立绘
        // =========================================================

        public void ApplyPortrait(
            string id,
            int slot)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            if (id == "-")
            {
                HideAllCharactersForStory();
                return;
            }

            if (id == "@clear")
            {
                HideCharacterForStory(slot);
                return;
            }

            Sprite sprite =
                FindSprite(portraits, id);

            if (sprite == null)
            {
                Debug.LogWarning(
                    $"找不到立绘资源：{id}"
                );
                return;
            }

            if (slot < 0 || slot > 2)
            {
                slot = 1;
            }

            // 正常剧情路径：
            // 空槽第一次登场时淡入；
            // 已有角色时直接切换 Sprite。
            ShowCharacterForStory(
                slot,
                sprite
            );
        }

        // =========================================================
        // 显示指定槽位人物
        // =========================================================

        public void ShowCharacter(
            int slot,
            Sprite sprite)
        {
            if (slots == null)
            {
                return;
            }

            if (slot < 0 ||
                slot >= slots.Length)
            {
                return;
            }

            if (slots[slot] == null)
            {
                return;
            }

            slots[slot].sprite = sprite;
            slots[slot].enabled =
                sprite != null;
        }

        private void ShowCharacterForStory(
            int slot,
            Sprite sprite)
        {
            if (slots == null)
            {
                return;
            }

            if (slot < 0 ||
                slot >= slots.Length)
            {
                return;
            }

            Image image = slots[slot];

            if (image == null)
            {
                return;
            }

            if (sprite == null)
            {
                return;
            }

            // 槽位之前是否为空。
            // 只有真正的“第一次登场”才播放淡入。
            bool wasEmpty =
                !image.enabled ||
                image.sprite == null;

            // 先设置正确立绘
            image.sprite = sprite;
            image.enabled = true;

            // 已经有人物时，仅代表表情/立绘替换。
            // 直接换 Sprite，不播放淡入。
            if (!wasEmpty)
            {
                return;
            }

            // 第一次登场：从透明开始
            SetImageAlpha(image, 0f);

            StartCoroutine(
                FadeCharacterIn(image));
        }

        // =========================================================
        // 隐藏指定槽位人物
        // =========================================================

        public void HideCharacter(int slot)
        {
            if (slots == null)
            {
                return;
            }

            if (slot < 0 ||
                slot >= slots.Length)
            {
                return;
            }

            if (slots[slot] == null)
            {
                return;
            }

            slots[slot].sprite = null;
            slots[slot].enabled = false;

            // 清除旧的高亮状态
            slots[slot].color =
                activePortraitColor;
        }

        private void HideCharacterForStory(int slot)
        {
            if (slots == null)
            {
                return;
            }

            if (slot < 0 ||
                slot >= slots.Length)
            {
                return;
            }

            Image image = slots[slot];

            if (image == null)
            {
                return;
            }

            // 本来就是空槽，不需要演出
            if (!image.enabled ||
                image.sprite == null)
            {
                HideCharacter(slot);
                return;
            }

            StartCoroutine(
                FadeCharacterOut(
                    slot,
                    image));
        }

        // =========================================================
        // 隐藏所有人物
        // =========================================================

        public void HideAllCharacters()
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                HideCharacter(i);
            }
        }

        private void HideAllCharactersForStory()
        {
            if (slots == null)
            {
                return;
            }

            bool foundCharacter = false;

            for (int i = 0; i < slots.Length; i++)
            {
                Image image = slots[i];

                if (image == null)
                {
                    continue;
                }

                if (!image.enabled ||
                    image.sprite == null)
                {
                    continue;
                }

                foundCharacter = true;

                StartCoroutine(
                    FadeCharacterOut(
                        i,
                        image));
            }

            // 如果本来就没人，确保空槽保持干净状态
            if (!foundCharacter)
            {
                HideAllCharacters();
            }
        }

        public void ResetForNewGame()
        {
            // 强制终止当前所有视觉演出。
            // 新游戏恢复最终干净状态，不播放退场动画。
            StopAllCoroutines();

            activeCharacterFadeCount = 0;
            IsBusy = false;

            // 清除背景
            if (background != null)
            {
                background.sprite = null;
                background.enabled = false;
            }

            // 清除人物立绘
            HideAllCharacters();

            // 清除 CG
            HideCG();

            // 确保黑色遮罩不是残留状态
            if (fadeOverlay != null)
            {
                Color color = fadeOverlay.color;
                color.a = 0f;
                fadeOverlay.color = color;
                fadeOverlay.raycastTarget = false;
            }
        }

        // =========================================================
        // CG
        // =========================================================

        public void ShowCG(Sprite sprite)
        {
            if (cgImage == null)
            {
                return;
            }

            cgImage.sprite = sprite;
            cgImage.enabled =
                sprite != null;
        }

        public void HideCG()
        {
            if (cgImage == null)
            {
                return;
            }

            cgImage.sprite = null;
            cgImage.enabled = false;
        }

        // =========================================================
        // 读档恢复视觉状态
        // =========================================================

        public void ApplySnapshot(
            VisualSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            // 读档恢复的是最终视觉状态。
            // 先终止任何仍在运行的人物演出，避免旧 Coroutine
            // 在快照恢复以后继续修改角色 Alpha。
            StopAllCoroutines();

            activeCharacterFadeCount = 0;
            IsBusy = false;
            
            // 背景
            if (!string.IsNullOrEmpty(
                snapshot.backgroundId))
            {
                ApplyBackground(
                    snapshot.backgroundId
                );
            }

            // 读档时先清掉当前所有人物，
            // 防止上一局残留
            HideAllCharacters();

            // 左
            RestorePortraitSlot(
                0,
                snapshot.leftPortraitId
            );

            // 中
            RestorePortraitSlot(
                1,
                snapshot.centerPortraitId
            );

            // 右
            RestorePortraitSlot(
                2,
                snapshot.rightPortraitId
            );

            // 恢复当前说话角色的亮暗状态
            ApplyFocus(
                snapshot.focusSlot
            );
        }

        // =========================================================
        // 根据字符串 ID 找 Sprite
        // =========================================================

        private Sprite FindSprite(
            VNSpriteEntry[] entries,
            string id)
        {
            if (entries == null)
            {
                return null;
            }

            foreach (VNSpriteEntry entry in entries)
            {
                if (entry != null &&
                    entry.id == id)
                {
                    return entry.sprite;
                }
            }

            return null;
        }

        private void RestorePortraitSlot(
            int slot,
            string portraitId)
        {
            if (string.IsNullOrEmpty(
                portraitId))
            {
                return;
            }

            Sprite sprite =
                FindSprite(
                    portraits,
                    portraitId
                );

            if (sprite == null)
            {
                Debug.LogWarning(
                    $"读档时找不到立绘资源：{portraitId}"
                );
                return;
            }

            ShowCharacter(
                slot,
                sprite
            );
        }

        public void ApplyFocus(int focusSlot)
        {
            if (slots == null)
            {
                return;
            }

            // -1 = 所有人恢复正常亮度
            if (focusSlot < 0 ||
                focusSlot >= slots.Length)
            {
                for (int i = 0;
                    i < slots.Length;
                    i++)
                {
                    if (slots[i] != null)
                    {
                        SetImageRgbKeepAlpha(
                            slots[i],
                            activePortraitColor);
                    }
                }

                return;
            }

            for (int i = 0;
                i < slots.Length;
                i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                if (i == focusSlot)
                {
                    SetImageRgbKeepAlpha(
                        slots[i],
                        activePortraitColor);
                }
                else
                {
                    SetImageRgbKeepAlpha(
                        slots[i],
                        inactivePortraitColor);
                }
            }
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
                return;

            Color color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }

        private static void SetImageRgbKeepAlpha(
            Image image,
            Color targetColor)
        {
            if (image == null)
                return;

            Color color = targetColor;
            color.a = image.color.a;
            image.color = color;
        }

        private IEnumerator FadeImageAlpha(
            Image image,
            float targetAlpha)
        {
            if (image == null)
            {
                yield break;
            }

            float startAlpha = image.color.a;
            float duration = Mathf.Max(0f, characterFadeSeconds);

            // 时长为 0 时直接得到最终状态
            if (duration <= 0f)
            {
                SetImageAlpha(image, targetAlpha);
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(
                    elapsed / duration);

                float alpha = Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    t);

                SetImageAlpha(image, alpha);

                yield return null;
            }

            // 防止浮点误差，最后强制落到准确目标值
            SetImageAlpha(image, targetAlpha);
        }

        private void BeginCharacterFade()
        {
            activeCharacterFadeCount++;
            IsBusy = true;
        }

        private void EndCharacterFade()
        {
            activeCharacterFadeCount =
                Mathf.Max(0, activeCharacterFadeCount - 1);

            IsBusy = activeCharacterFadeCount > 0;
        }

        private IEnumerator FadeCharacterIn(
            Image image)
        {
            if (image == null)
            {
                yield break;
            }

            BeginCharacterFade();

            yield return FadeImageAlpha(
                image,
                1f);

            // 最终状态强制完整显示
            SetImageAlpha(image, 1f);

            EndCharacterFade();
        }

        private IEnumerator FadeCharacterOut(
            int slot,
            Image image)
        {
            if (image == null)
            {
                yield break;
            }

            BeginCharacterFade();

            yield return FadeImageAlpha(
                image,
                0f);

            // 淡出结束后才真正清掉角色
            if (slots != null &&
                slot >= 0 &&
                slot < slots.Length &&
                slots[slot] == image)
            {
                image.sprite = null;
                image.enabled = false;

                // 为下一次角色登场准备干净状态
                SetImageAlpha(image, 1f);

                SetImageRgbKeepAlpha(
                    image,
                    activePortraitColor);
            }

            EndCharacterFade();
}
    }
}