using TMPro;
using UnityEngine;

namespace Network
{
    public class PingSender : MonoBehaviour
    {
        public TMP_Text pingText;

        private float dTimeAtPingTime;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private uint pingID;
        private float pingTime;
        private float timer;

        private void Start()
        {
            pingTime = 0;
            pingID = 0;
        }

        // Update is called once per frame
        private void Update()
        {
            if (NetServer.BuiltRunningMode != NetServer.RunningMode.Client)
                return;
            timer += Time.deltaTime;
            if (timer >= 3)
            {
                timer = 0;
                pingID++;
                pingTime = Time.time;
                dTimeAtPingTime = Time.deltaTime;
                NetClient.SendMsg(MessagePacker.PackPingMsg(pingID));
            }
        }

        public void UpdatePing(uint id)
        {
            if (id == pingID)
            {
                pingTime = (Time.time - pingTime - dTimeAtPingTime * 2) / 2;
                pingText.text = "Ping: " + Mathf.Max(0, pingTime * 1000) + "ms";
            }
            else
            {
                pingText.text = "Ping: More than 3000ms!!!!!";
            }
        }
    }
}