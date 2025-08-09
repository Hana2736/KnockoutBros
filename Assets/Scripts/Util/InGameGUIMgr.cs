using Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InGameGUIMgr : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject qualElimBanner;
    public Texture qualBanner, elimBanner;
    void Start()
    {
        if(NetServer.BuiltRunningMode != NetServer.RunningMode.Client)
            Destroy(this);
    }

    public void UpdateGuiWeQualified()
    {
        UpdateGuiTemplate(qualBanner);
    }
    
    public void UpdateGuiWeEliminated()
    {
        UpdateGuiTemplate(elimBanner);
    }

    public void UpdateGuiTemplate(Texture toSwapTo)
    {
        qualElimBanner.SetActive(true);
        qualElimBanner.GetComponent<RawImage>().texture = toSwapTo;
    }
    
    public void HideBanner()
    {
        qualElimBanner.SetActive(false);
    }
}