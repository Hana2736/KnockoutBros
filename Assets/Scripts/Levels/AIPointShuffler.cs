using System;
using System.Collections.Generic;
using System.Linq;
using Network;
using NUnit.Framework;
using Tags;
using Unity.VisualScripting;
using UnityEngine;

public class AIPointShuffler : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private float cooldown = 0.25f;
    private List<BotWaypoint> waypoints;
    void Start()
    {
        if(NetServer.BuiltRunningMode != NetServer.RunningMode.Server)
            return;
        waypoints = new();   
        for (int i = 1; i <= 6; i++)
        {
            waypoints.Add(GameObject.Find("AiPoint"+i).GetComponent<BotWaypoint>());
        }
    }

    // Update is called once per frame
    void Update()
    {
        try
        {
            if (NetServer.BuiltRunningMode != NetServer.RunningMode.Server)
                return;
            cooldown -= Time.deltaTime;
            if (cooldown > 0)
                return;
            cooldown = 0.25f;
            foreach (var waypoint in waypoints)
            {
                waypoint.targetPosition = waypoints.Where(w => w != waypoint).ToList()[UnityEngine.Random.Range(0, waypoints.Count - 1)].gameObject;
            }
        }
        catch (Exception e)
        {
            this.enabled = false;
    }
        
    }
}
