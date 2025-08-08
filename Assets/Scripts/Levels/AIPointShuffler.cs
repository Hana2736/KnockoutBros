using System.Collections.Generic;
using System.Linq; // Keep for RemoveAll
using Network;
using Tags;
using UnityEngine;

public class AIPointShuffler : MonoBehaviour
{
    // Drag your waypoint GameObjects here in the Unity Inspector.
    // This is much safer than finding them by name.
    public List<BotWaypoint> waypoints;

    private float cooldown = 0.25f;

    void Start()
    {
        if (NetServer.BuiltRunningMode != NetServer.RunningMode.Server)
        {
            this.enabled = false; // Disable script if not the server
            return;
        }

        // Check if waypoints were actually assigned in the Inspector
        if (waypoints == null || waypoints.Count < 2)
        {
            Debug.LogError("AIPointShuffler: Please assign at least 2 waypoints in the Inspector!", this);
            this.enabled = false; // Disable script if not set up correctly
        }
    }

    void Update()
    {
        // No need for the server check here if Start() already disabled the component
        
        cooldown -= Time.deltaTime;
        if (cooldown > 0)
            return;
        
        cooldown = 0.25f;

        // Clean up any waypoints that might have been destroyed during the game.
        // This removes any "null" entries from the list.
        waypoints.RemoveAll(item => item == null);

        // Don't try to assign targets if there aren't enough waypoints to choose from
        if (waypoints.Count < 2)
            return;

        // Loop through each waypoint to assign it a new target
        foreach (var waypoint in waypoints)
        {
            // This is a safety check in case the waypoint was part of the list
            // that just got cleaned. This can happen in rare race conditions.
            if (waypoint == null) continue;

            BotWaypoint targetWaypoint;
            do
            {
                // Pick a random waypoint from the cleaned list
                int randomIndex = Random.Range(0, waypoints.Count);
                targetWaypoint = waypoints[randomIndex];
            }
            // Keep picking until we find a waypoint that is NOT the current one
            while (targetWaypoint == waypoint);

            // Finally, assign the new target
            waypoint.targetPosition = targetWaypoint.gameObject;
        }
    }
}