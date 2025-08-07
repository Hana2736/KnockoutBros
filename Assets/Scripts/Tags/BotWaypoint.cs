using UnityEngine;

namespace Tags
{
    public class BotWaypoint : MonoBehaviour
    {
        public GameObject targetPosition;
        public BotAction botAction;
    }

    public enum BotAction
    {
        None,
        Jump,
        Dive
    }
}