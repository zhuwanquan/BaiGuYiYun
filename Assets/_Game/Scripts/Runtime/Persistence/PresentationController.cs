using System;
using UnityEngine;
using UnityEngine.UI;

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
                HideAllCharacters();
                return;
            }

            if (id == "@clear")
            {
                HideCharacter(slot);
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

            // 不清除另外两个槽位
            ShowCharacter(
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

        public void ResetForNewGame()
        {
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
                        slots[i].color =
                            activePortraitColor;
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
                    slots[i].color =
                        activePortraitColor;
                }
                else
                {
                    slots[i].color =
                        inactivePortraitColor;
                }
            }
        }
    }
}