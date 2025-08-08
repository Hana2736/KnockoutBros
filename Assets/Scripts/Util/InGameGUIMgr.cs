using Network;
using TMPro;
using UnityEngine;

public class InGameGUIMgr : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public TMP_Text bannerText;
    void Start()
    {
        if(NetServer.BuiltRunningMode != NetServer.RunningMode.Client)
            Destroy(this);
    }

    public void UpdateGuiWeQualified()
    {
        bannerText.gameObject.SetActive(true);
        bannerText.text = "Qualified!";
    }
    
    public void UpdateGuiWeEliminated()
    {
        bannerText.gameObject.SetActive(true);
        bannerText.text = "Eliminated!";
    }
    
}
