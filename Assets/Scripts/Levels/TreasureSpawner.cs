using System;
using UnityEngine;
using Util;

namespace Levels
{
    public class TreasureSpawner : MonoBehaviour
    {
        public GameObject onePt, threePt;
        private float timer = 3f;
        public void Start()
        {
            
        }

        public void Update()
        {
            timer -= Time.deltaTime;
            if(timer >0)
                return;
            float randomX = UnityEngine.Random.Range(-4.17695f, 4.17695f);
            float randomZ = UnityEngine.Random.Range(-4.17695f, 4.17695f);
            float pntScore = UnityEngine.Random.Range(0f,100f);

            timer = UnityEngine.Random.Range(0.5f, 4f);
            
            GameObject newBubble = pntScore >= 70 ? threePt : onePt;
            Instantiate(newBubble, new Vector3(randomX, 7, randomZ), Quaternion.identity, new InstantiateParameters()
            {
                parent = LevelLoader.parentForItems.transform,
                worldSpace = true
            });
        }
    }
}