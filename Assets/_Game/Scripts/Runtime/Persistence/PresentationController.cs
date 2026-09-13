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

            // "-" = 清除全部人物
            if (id == "-")
            {
                HideAllCharacters();
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

            // 合法槽位：
            // 0 = 左
            // 1 = 中
            // 2 = 右
            if (slot < 0 || slot > 2)
            {
                Debug.LogWarning(
                    $"立绘槽位无效：{slot}，自动改用中间槽。"
                );

                slot = 1;
            }

            // 当前版本一次只显示一个人物
            HideAllCharacters();

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

            if (!string.IsNullOrEmpty(
                snapshot.backgroundId))
            {
                ApplyBackground(
                    snapshot.backgroundId
                );
            }

            if (!string.IsNullOrEmpty(
                snapshot.portraitId))
            {
                ApplyPortrait(
                    snapshot.portraitId,
                    snapshot.portraitSlot
                );
            }
            else
            {
                HideAllCharacters();
            }
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
    }
}