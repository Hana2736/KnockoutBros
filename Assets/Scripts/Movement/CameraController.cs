using UnityEngine;

namespace Movement
{
    public class CameraController : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("The object the camera will follow (your player).")]
        public Transform target = null;

        [Header("Camera Control")]
        [Tooltip("How far the camera is from the target.")]
        public float distance = 3.5f;
        [Tooltip("Sensitivity of the mouse for camera rotation.")]
        public float mouseSensitivity = 4.0f;
        [Tooltip("How smoothly the camera follows the target's position. Lower is smoother.")]
        public float positionSmoothTime = 0.1f;
        [Tooltip("The minimum angle the camera can look down.")]
        public float pitchMin = -40.0f;
        [Tooltip("The maximum angle the camera can look up.")]
        public float pitchMax = 80.0f;

        // Private variables to store camera rotation
        private float yaw = 0.0f;   // Rotation around the Y axis (left/right)
        private float pitch = 0.0f; // Rotation around the X axis (up/down)

        // Velocity reference for SmoothDamp
        private Vector3 currentVelocity;

        void Start()
        {
            // Lock and hide the cursor for a better gameplay experience
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // LateUpdate is called after all Update functions.
        // This is the best place for camera logic to avoid jitter.
        void LateUpdate()
        {
            if (target == null)
            {
                //Debug.LogWarning("Camera Controller has no target assigned.");
                return;
            }

            // --- Handle Mouse Input for Rotation ---
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;

            // Clamp the vertical rotation to prevent the camera from flipping over
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

            // Calculate the desired camera rotation based on mouse input
            Quaternion desiredRotation = Quaternion.Euler(pitch, yaw, 0);

            // --- Handle Position ---
            // Calculate the desired position for the camera behind the target
            Vector3 desiredPosition = target.position - (desiredRotation * Vector3.forward * distance); //+ new Vector3(0,3.5f,0);

            // Smoothly move the camera towards its desired position
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, positionSmoothTime);

            // Always make the camera look at the target's position.
            // This is what decouples it from the player's own rotation.
            transform.LookAt(target.position);
        }
    }
}
