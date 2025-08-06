using System;
using Network;
using UnityEngine;

public class DummyScript : MonoBehaviour
{
    private double timer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
    }

    // Update is called once per frame
    private void Update()
    {
        //timer += Time.deltaTime;
        transform.Rotate(Vector3.up * (10 * Time.deltaTime));
        if (timer >= .2)
        {
            var rot = transform.rotation.eulerAngles.y;
            timer = 0;
            byte messageID = 0x01;
            byte delimiter = 0xFF; // Delimiter byte
            var message = new byte[6];
            message[0] = messageID;
            var rotationBytes = BitConverter.GetBytes(rot);
            Buffer.BlockCopy(rotationBytes, 0, message, 1, rotationBytes.Length);
            message[5] = delimiter;
            NetClient.sock.Send(message);
        }
    }
}