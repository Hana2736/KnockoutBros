using System;
using System.Collections;
using Network;
using UnityEngine;
using Util;

public class NetReconnector : MonoBehaviour
{
    public static Guid secretKey;
    public static bool isReconnecting;
    private static bool coroRunning;
    private static NetReconnector c;

    private void Start()
    {
        isReconnecting = false;
        coroRunning = false;
        c = this;
    }

    private void HandleDisconnectInternal(string reason)
    {
        NetClient.keyRecovered = false;
        if (!coroRunning)
            StartCoroutine(connectCoroutine(reason));
    }

    public static void HandleDisconnect(string reason)
    {
        c.HandleDisconnectInternal(reason);
    }

    private IEnumerator connectCoroutine(string reason)
    {
        coroRunning = true;
        if (secretKey == Guid.Empty)
        {
            DialogMgr.ShowDialog("Connection failed!", "Refresh the page and try again.");
            yield break;
        }

        reason = reason switch
        {
            "Abnormal" => "Connection ended abruptly.\nCheck the Internet connection.",
            "ServerError" => "Something went wrong on the server.\nSorry about that!",
            _ => "Check your Internet connection."
        };
        DialogMgr.ShowDialog("Disconnected from the server!", reason + "\n\nReconnecting...");
        yield return new WaitForSeconds(2);
       // NetClient.ResetConnection();
        yield return new WaitForSeconds(2);
        coroRunning = false;
        if (isReconnecting)
            yield break;
        ;
        isReconnecting = true;
        //NetClient.outMessageQueue.AddFirst(MessagePacker.PackSecretKeyMsg(secretKey));
    }


    public static void HandleReconnect()
    {
        isReconnecting = false;
        DialogMgr.HideDialog();
    }
}