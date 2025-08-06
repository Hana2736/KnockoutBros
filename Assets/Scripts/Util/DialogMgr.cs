using TMPro;
using UnityEngine;

namespace Util
{
    public class DialogMgr : MonoBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private static GameObject dialogWindow;
        private static TMP_Text dialogText;
        private static TMP_Text dialogName;


        private void Start()
        {
            dialogWindow = GameObject.Find("DialogMsg");
            dialogName = GameObject.Find("DialogMsgTitle").GetComponent<TMP_Text>();
            dialogText = GameObject.Find("DialogMsgText").GetComponent<TMP_Text>();
            dialogWindow.SetActive(false);
        }

        public static void ShowDialog(string winname, string text)
        {
            dialogWindow.SetActive(true);
            dialogName.text = winname;
            dialogText.text = text;
        }

        public static void HideDialog()
        {
            dialogWindow.SetActive(false);
        }
    }
}