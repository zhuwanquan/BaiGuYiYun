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

        public void ApplyBackground(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;

            if (id == "-")
            {
                background.sprite = null;
                background.enabled = false;
                return;
            }

            Sprite sprite = FindSprite(backgrounds, id);

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

        public void ShowCharacter(int slot, Sprite sprite)
        {
            if (slots == null ||
                slot < 0 ||
                slot >= slots.Length)
            {
                return;
            }

            slots[slot].sprite = sprite;
            slots[slot].enabled = sprite != null;
        }

        public void HideCharacter(int slot)
        {
            if (slots == null ||
                slot < 0 ||
                slot >= slots.Length)
            {
                return;
            }

            slots[slot].sprite = null;
            slots[slot].enabled = false;
        }

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

        public void ApplySnapshot(VisualSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            ApplyBackground(snapshot.backgroundId);
        }

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

        private void Awake()
        {
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

            if (cgImage != null)
            {
                cgImage.sprite = null;
                cgImage.enabled = false;
            }
        }
    }
}