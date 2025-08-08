using UnityEngine;

namespace Levels
{
    public class CeilingSpikeFall : MonoBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private Transform CeilingSpike;
        private float limit = 1f;
        void Start()
        {
            CeilingSpike = transform.GetChild(1);
        }

        // Update is called once per frame
        void Update()
        {
            if (limit<=0)
                return;
            limit -= Time.deltaTime;
            CeilingSpike.Translate(Vector3.down * Time.deltaTime * 15);
        }
    }
}
