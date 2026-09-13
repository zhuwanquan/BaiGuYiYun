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
            // 游戏启动时隐藏空的角色槽
            if (slots != null)
            {
                foreach (Image slot in slots)
                {
                    if (slot == null)
                        continue;

                    slot.sprite = null;
                    slot.enabled = false;
                }
            }

            // 没有 CG 时隐藏 CGImage
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
                return;

            if (background == null)
                return;

            // "-" 表示清除
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
        //
        // 目前先统一显示到中间槽 slot 1。
        // 后面再扩展左 / 中 / 右多人同屏。
        // =========================================================

        public void ApplyPortrait(
            string id,
            int slot)
        {
            if (string.IsNullOrEmpty(id))
                return;

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

            if (slot < 0 || slot > 2)
            {
                Debug.LogWarning(
                    $"立绘槽位无效：{slot}，自动使用中间槽。"
                );

                slot = 1;
            }

            // 当前阶段仍然一次只显示一个主立绘
            HideAllCharacters();

            ShowCharacter(slot, sprite);
        }

        public void HideCharacter(int slot)
        {
            if (slots == null ||
                slot < 0 ||
                slot >= slots.Length ||
                slots[slot] == null)
            {
                return;
            }

            slots[slot].sprite = null;
            slots[slot].enabled = false;
        }

        public void HideAllCharacters()
        {
            if (slots == null)
                return;

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
                return;

            cgImage.sprite = sprite;
            cgImage.enabled = sprite != null;
        }

        public void HideCG()
        {
            if (cgImage == null)
                return;

            cgImage.sprite = null;
            cgImage.enabled = false;
        }

        // =========================================================
        // 读档恢复画面
        // =========================================================

        public void ApplySnapshot(
            VisualSnapshot snapshot)
        {
            if (snapshot == null)
                return;

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
        // Sprite 查找
        // =========================================================

        private Sprite FindSprite(
            VNSpriteEntry[] entries,
            string id)
        {
            if (entries == null)
                return null;

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