using System.Collections;
using Network;
using Tags;
using UnityEngine;
using Util;

namespace Movement
{
    public class PlayerHandler : MonoBehaviour
    {
        public enum SkipTickReason : byte
        {
            Loading,
            None,
            Dive,
            Dead,
            Stunned,
            Qualify
        }

        private static readonly int idle = Animator.StringToHash("Idle");
        private static readonly int dive = Animator.StringToHash("Dive");
        private static readonly int walk = Animator.StringToHash("Walk");
        private static readonly int jump = Animator.StringToHash("Jump");
        private static readonly int getUp = Animator.StringToHash("GetUp");
        private static readonly int die = Animator.StringToHash("Die");
        private static readonly int qualify = Animator.StringToHash("Qualify");
        public uint playerId;
        private Vector3 networkPositionTarget;
        private Quaternion networkRotationTarget;
        private float timeBetweenPackets;
        public float inputX;
        public float inputZ;
        public bool jumpKey;
        public bool diveKey;

        public int currentAnim;
        public bool localPlayer;
        public Rigidbody myRb;
        public bool readyForUpdates;

        public bool isBotPlayer;
        public BotController myBotController;
        public SkipTickReason skipTick;

        public GameManager parentGameManager;


        public Transform cameraTransform;
        public float rotationSpeed = 10f;

        public Vector3 oldPos, oldAngles;
        private readonly float acceleration = 10f;
        private readonly float walkSpeed = 2.2f;
        private float armsSpeed = .7f;
        private float characterHeight;

        private bool gettingBackUp;
        public bool groundedForJump = true;
        private float jumpCooldown = 0.2f;

        private Animator myAnim;
        public float lastPakUpdate;
        private Collider myCol;
        private bool skipTickWaitingAlready;

        public void Start()
        {
            //localPlayer = true;

            skipTick = SkipTickReason.None;
            skipTickWaitingAlready = false;
            myRb = GetComponent<Rigidbody>();
            myCol = GetComponent<Collider>();
            characterHeight = myCol.bounds.extents.y;
            readyForUpdates = true;
            //Debug.Log("Is bot player? "+isBotPlayer);
            myBotController = GetComponent<BotController>();
            myAnim = GetComponent<Animator>();
            if (!isBotPlayer)
                Destroy(myBotController);
            else
                myBotController.Setup();

            // REMOVE THE OLD CAMERA ASSIGNMENT BLOCK
            // if (localPlayer && cameraTransform is null)
            // {
            //     cameraTransform = Camera.main.transform;
            //     Camera.main.gameObject.GetComponent<CameraController>().target = transform;
            // }

            if (NetServer.BuiltRunningMode == NetServer.RunningMode.Server)
            {
                if (!isBotPlayer)
                    globalSkipCalc = true;
            }
            else if (!localPlayer)
            {
                globalSkipCalc = true;
            }

            if (globalSkipCalc)
                myRb.isKinematic = true;
        }

        public void ReceiveNetworkUpdate(Vector3 position, Quaternion rotation)
        {
            // Calculate the time since the last packet to use as our interpolation duration
            timeBetweenPackets = Time.time - lastPakUpdate;
            lastPakUpdate = Time.time;

            // Set the start of our lerp to the player's current position and rotation
            oldPos = transform.position;
            oldAngles = transform.eulerAngles;

            // Set the target position and rotation from the network packet
            networkPositionTarget = position;
            networkRotationTarget = rotation;
        }


        private bool globalSkipCalc = false;

        public void Update()
        {
            // For the human-controlled local player, we need to make sure we have a camera reference.
            // By putting this in Update, we continuously try to find the camera until it's available.
            if (localPlayer && !isBotPlayer && cameraTransform == null)
            {
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    // Camera found! Assign its transform.
                    cameraTransform = mainCamera.transform;

                    // Now, find the CameraController script on the camera and set this player as its target.
                    var camController = mainCamera.GetComponent<CameraController>();
                    if (camController != null)
                    {
                        camController.target = transform;
                    }
                }
                else
                {
                    // If we still can't find the camera, it's not ready yet. 
                    // We'll skip the rest of this Update frame to prevent the error.
                    return;
                }
            }


            if (globalSkipCalc)
            {
                // --- Interpolation for remote players ---
                float timeSinceLastPacket = Time.time - lastPakUpdate;
                // Use the calculated time between packets for the interpolation period.
                // If timeBetweenPackets is 0 (e.g., on the first packet), we snap to the target.
                float t = (timeBetweenPackets > 0) ? Mathf.Clamp01(timeSinceLastPacket / timeBetweenPackets) : 1f;

                transform.position = Vector3.Lerp(oldPos, networkPositionTarget, t);
                transform.rotation = Quaternion.Slerp(Quaternion.Euler(oldAngles), networkRotationTarget, t);
                // --- End of interpolation ---

                SetAnimState(currentAnim);
                // For remote players, we are done after interpolating.
                return;
            }

            if (SkipTickReason.Loading == skipTick)
            {
                SetAnimState(idle);
                myRb.isKinematic = true;
                inputX = 0;
                inputZ = 0;
                return;
            }
            else
            {
                myRb.isKinematic = false;
            }


            SetAnimState(idle);
            // Add the check for isKinematic to this line
            if (skipTick == SkipTickReason.None && !myRb.isKinematic)
                PlayerMovement();
            var animSet = false;

            if (skipTick == SkipTickReason.Dead)
            {
                SetAnimState(die);
                animSet = true;
            }

            if (skipTick == SkipTickReason.Qualify)
            {
                animSet = true;
                SetAnimState(qualify);
            }

            if (skipTick == SkipTickReason.Dive)
            {
                SetAnimState(dive);
                animSet = true;
            }

            if (gettingBackUp)
            {
                SetAnimState(getUp);
                animSet = true;
            }

            if (!animSet)
            {
                if (groundedForJump && myRb.linearVelocity.magnitude > 0.3f)
                    SetAnimState(walk);
                else if (myRb.linearVelocity.magnitude > 0.3f)
                    SetAnimState(jump);
            }
        }

        private void OnCollisionExit(Collision other)
        {
            if (skipTick == SkipTickReason.None && other.gameObject.GetComponent<Jumpable>() is not null)
                groundedForJump = false;
        }

        private void OnCollisionStay(Collision other)
        {
            if (!((skipTick == SkipTickReason.None || skipTick == SkipTickReason.Dive) && !groundedForJump))
                return;
            if (other.gameObject.GetComponent<Jumpable>() is null)
                return;
            groundedForJump = true;
            if (skipTick == SkipTickReason.Dive)
            {
                myRb.linearVelocity *= 0.2f;
                StartCoroutine(StopTickingForTime(1.5f));
                StartCoroutine(GetBackUp());
            }
        }


        public void OnTriggerEnter(Collider other)
        {
            if (NetServer.BuiltRunningMode != NetServer.RunningMode.Server || skipTick == SkipTickReason.Loading)
                return;
            if (other.GetComponent<DeathZone>() is not null)
            {
                Debug.Log("server-side player dead " + playerId);
                parentGameManager.OnPlayerEliminated(playerId);
                gameObject.SetActive(false);
                return;
            }

            if (other.GetComponent<FinishZone>() is not null)
            {
                Debug.Log("server-side player qualified " + playerId);
                parentGameManager.OnPlayerQualify(playerId);
                gameObject.SetActive(false);
                return;
            }

            var pointZone = other.GetComponent<PointsZone>();
            if (pointZone is not null)
            {
                parentGameManager.OnPlayerScore(pointZone.pointsWorth, playerId);
                Destroy(pointZone.gameObject);
                return;
            }

            if (other.GetComponent<ReadyToPlayZone>() is not null) parentGameManager.readyPlayers.Add(playerId);
        }

        private void SetAnimState(int state)
        {
            if (state == 0)
                return;
            myAnim.SetBool(idle, false);
            myAnim.SetBool(dive, false);
            myAnim.SetBool(walk, false);
            myAnim.SetBool(jump, false);
            myAnim.SetBool(getUp, false);
            myAnim.SetBool(die, false);
            myAnim.SetBool(qualify, false);
            myAnim.SetBool(state, true);
            currentAnim = state;
        }




        private void PlayerMovement()
        {
            jumpCooldown -= Time.deltaTime;
            if (jumpCooldown < 0)
                jumpCooldown = 0;
            var maxSpeed = walkSpeed;

            var moveDirection = transform.forward * inputZ + transform.right * inputX;
            if (localPlayer)
            {
                inputX = Input.GetAxisRaw("Horizontal");
                inputZ = Input.GetAxisRaw("Vertical");
                jumpKey = Input.GetButtonDown("Jump");
                diveKey = Input.GetMouseButtonDown(1);
                var camForward = cameraTransform.forward;
                var camRight = cameraTransform.right;
                camForward.y = 0;
                camRight.y = 0;
                camForward.Normalize();
                camRight.Normalize();

                // We replace the original use of transform.forward/right with our new camera-relative directions.
                moveDirection = camForward * inputZ + camRight * inputX;

                // Rotate the player model to smoothly face the direction of movement.
                if (moveDirection.magnitude > 0.1f)
                {
                    var targetRotation = Quaternion.LookRotation(moveDirection.normalized);
                    myRb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation,
                        rotationSpeed * Time.deltaTime));
                }
                // --- END OF MODIFICATIONS ---
            }


            // Your original velocity logic now works with the new camera-relative moveDirection.
            if (moveDirection.magnitude > 0)
            {
                var velocity = myRb.linearVelocity;
                moveDirection.Normalize(); // Prevents diagonal movement from being faster
                var targetVelocity = Vector3.zero;
                targetVelocity.x += moveDirection.x * maxSpeed;
                targetVelocity.z += moveDirection.z * maxSpeed;

                // Calculate the velocity change needed this frame
                var velocityChange = targetVelocity - new Vector3(velocity.x, 0, velocity.z);

                // Apply acceleration, clamping the *change* in velocity
                velocityChange = Vector3.ClampMagnitude(velocityChange, acceleration * Time.deltaTime);

                // Add the change to the current velocity
                velocity.x += velocityChange.x;
                velocity.z += velocityChange.z;

                // *Only Clamp with Input*
                velocity.x = Mathf.Clamp(velocity.x, -maxSpeed, maxSpeed);
                velocity.z = Mathf.Clamp(velocity.z, -maxSpeed, maxSpeed);
                myRb.linearVelocity = velocity;
            }

            if (jumpKey && jumpCooldown <= .01 && groundedForJump)
            {
                //Debug.Log("try jump...");
                myRb.AddForce(Vector3.up * 4, ForceMode.Impulse);
                jumpCooldown = 0.2f;
                groundedForJump = false;
            }

            if (diveKey)
            {
                myRb.linearVelocity = Vector3.zero;
                myRb.AddForce(Vector3.up * 1.5f, ForceMode.Impulse);
                //Vector3 forcePoint = myCol.bounds.center + new Vector3(0, myCol.bounds.extents.y/2, 0);
                // myRb.AddForceAtPosition(Vector3.forward * 5, forcePoint);
                myRb.constraints = RigidbodyConstraints.None;
                myRb.AddForce(transform.forward * 3f, ForceMode.Impulse);
                myRb.AddTorque(transform.right * 0.145f, ForceMode.Impulse);
                skipTick = SkipTickReason.Dive;
                groundedForJump = false;
            }
        }

        public IEnumerator StopTickingForTime(float seconds)
        {
            if (!skipTickWaitingAlready)
            {
                skipTickWaitingAlready = true;
                yield return new WaitForSeconds(seconds);
                skipTickWaitingAlready = false;
                skipTick = SkipTickReason.None;
            }
        }

        public IEnumerator GetBackUp()
        {
            gettingBackUp = true;
            myRb.freezeRotation = true;
            myRb.angularVelocity = Vector3.zero;

            var elapsedTime = 0f;
            var recoveryDuration = 0.8f;
            var startRotation = transform.rotation;
            var startEuler = startRotation.eulerAngles;

            yield return new WaitForSeconds(1f);

            // Loop until the recovery duration is met
            while (elapsedTime < recoveryDuration)
            {
                // Calculate the interpolation factor (0 to 1)
                var t = elapsedTime / recoveryDuration;
                // Smoothly interpolate between the start and target rotations
                transform.rotation = Quaternion.Slerp(startRotation, Quaternion.Euler(0, startEuler.y, 0), t);

                // Increment elapsed time by the time since the last frame
                elapsedTime += Time.deltaTime;

                // Yield control back to Unity for the next frame
                yield return null;
            }

            gettingBackUp = false;
        }
    }
}