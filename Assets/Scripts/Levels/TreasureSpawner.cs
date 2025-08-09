using System;
using JetBrains.Annotations;
using Network;
using UnityEngine;
using Util;

namespace Levels
{
    public class TreasureSpawner : MonoBehaviour
    {
        public GameObject onePt, threePt;
        private float timer = 10f;
        private NetServer netServer;
        private uint nextBubbleId = 0;
        public void Start()
        {
            if (NetServer.BuiltRunningMode != NetServer.RunningMode.Server)
            {
                this.enabled = false;
                return;
            }
            netServer = UnityEngine.Object.FindAnyObjectByType<NetServer>();
            nextBubbleId = 0;
        }

        public void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0)
                return;
            float randomX = UnityEngine.Random.Range(-4.17695f, 4.17695f);
            float randomZ = UnityEngine.Random.Range(-4.17695f, 4.17695f);
            float pntScore = UnityEngine.Random.Range(0f, 100f);

            timer = UnityEngine.Random.Range(0.2f, 1.8f);

            GameObject newBubble = pntScore >= 70 ? threePt : onePt;

            var bubbObj = Instantiate(newBubble, new Vector3(randomX, 7, randomZ), Quaternion.identity, new InstantiateParameters()
            {
                parent = LevelLoader.parentForItems.transform,
                worldSpace = true
            });
            nextBubbleId++;
            bubbObj.GetComponent<BubbleID>().bubbleId = nextBubbleId;

            netServer.SendMessage(MessagePacker.PackNewBubbleMessage(new MessagePacker.NewBubbleMessage
            {
                bubbleId = nextBubbleId,
                posX = randomX,
                posY = 7,
                posZ = randomZ,
                bubbleScore = (uint)(pntScore >= 70 ? 3 : 1)
            }));

        }
    }
}