using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BaiguVN
{
    public class DialogueView : MonoBehaviour
    {
        [Header("UI")]
        public TMP_Text speakerText;
        public TMP_Text bodyText;
        public Button nextButton;

        [Header("Typing")]
        [Tooltip("每秒显示多少个字符。0 = 立即显示全部文字")]
        public float textSpeed = 30f;

        private Coroutine typingCoroutine;
        private bool isTyping;
        private bool appPaused;

        public bool IsTyping
        {
            get { return isTyping; }
        }

        public void Show(VNNode node)
        {
            if (node == null)
            {
                Debug.LogError("DialogueView.Show 收到空节点。");
                return;
            }

            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }

            speakerText.text = node.speaker ?? "";
            bodyText.text = node.text ?? "";

            bodyText.maxVisibleCharacters = 0;
            bodyText.ForceMeshUpdate();

            if (textSpeed <= 0f)
            {
                bodyText.maxVisibleCharacters =
                    bodyText.textInfo.characterCount;

                isTyping = false;
                return;
            }

            typingCoroutine = StartCoroutine(TypeLine());
        }

        public void CompleteTyping()
        {
            if (!isTyping)
            {
                return;
            }

            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }

            bodyText.ForceMeshUpdate();

            bodyText.maxVisibleCharacters =
                bodyText.textInfo.characterCount;

            isTyping = false;
        }

        private IEnumerator TypeLine()
        {
            isTyping = true;

            bodyText.ForceMeshUpdate();

            int characterCount =
                bodyText.textInfo.characterCount;

            bodyText.maxVisibleCharacters = 0;

            for (int i = 1; i <= characterCount; i++)
            {
                while (appPaused)
                {
                    yield return null;
                }

                bodyText.maxVisibleCharacters = i;

                if (textSpeed > 0f)
                {
                    yield return new WaitForSecondsRealtime(
                        1f / textSpeed
                    );
                }
            }

            bodyText.maxVisibleCharacters = characterCount;

            isTyping = false;
            typingCoroutine = null;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            appPaused = pauseStatus;
        }
    }
}