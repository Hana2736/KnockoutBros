using UnityEngine;
using System.Collections;

public class WaveMove : MonoBehaviour
{
    private float speed = 0f;
    private Vector3[] path;

    // This method is now called by the WaveGenerator to assign a path and speed to this wave.
    public void Initialize(Vector3[] pathPoints, float waveSpeed)
    {
        if (pathPoints == null || pathPoints.Length < 2)
        {
            Debug.LogError("A wave was initialized with an invalid path. Destroying object.");
            Destroy(gameObject);
            return;
        }

        this.path = pathPoints;
        this.speed = waveSpeed; // Set speed from the new parameter
        StartCoroutine(FollowPath());
    }

    IEnumerator FollowPath()
    {
        for (int i = 1; i < path.Length; i++)
        {
            Vector3 targetPosition = path[i];
            
            while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
                yield return null;
            }
            
            transform.position = targetPosition;
        }

        Destroy(gameObject);
    }
}