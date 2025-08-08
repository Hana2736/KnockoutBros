using System;
using Network;
using UnityEngine;
using Util;
using Random = UnityEngine.Random;

public class CeilingSpikeGenerator : MonoBehaviour
{
    public GameObject CeilingSpikePrefab;
    public GameManager gameMgr;
    private float timeUntilNext = 15f;

    public void Start()
    {
        gameMgr = GameObject.Find("EventSystem").GetComponent<GameManager>();
    }

    private void Update()
    {
        if (NetServer.BuiltRunningMode != NetServer.RunningMode.Server)
            return;
        timeUntilNext -= Time.deltaTime;
        if (timeUntilNext > 0)
            return;
        timeUntilNext = Random.Range(0.25f, 1.333f);
        var randomX = Random.Range(-8f, 8f);
        var randomZ = Random.Range(-8f, 8f);

        Instantiate(CeilingSpikePrefab, new Vector3(randomX, -1.72f, randomZ), Quaternion.identity,
            new InstantiateParameters
            {
                parent = LevelLoader.parentForItems.transform,
                worldSpace = true
            });
        gameMgr.serverHandler.netServer.SendMessage(MessagePacker.PackRainSpikeMessage(
            new MessagePacker.RainSpikeMessage
            {
                locationX = randomX,
                locationZ = randomZ
            }));
    }
}