using System;
using System.Collections;
using Network;
using Tags;
using UnityEngine;
using Util;

namespace Movement
{
    public class PlayerHandler : MonoBehaviour
    {
        public uint playerId;

        public float inputX;
        public float inputZ;
        public bool jumpKey;
        public bool diveKey;

        public bool localPlayer;
        public Rigidbody myRb;
        public bool readyForUpdates;

        public bool isBotPlayer;
        public BotController myBotController;
        private readonly float acceleration = 10f;
        private readonly float walkSpeed = 1.2f;
        private float armsSpeed = .7f;
        private float characterHeight;
        private bool groundedForJump = true;
        private float jumpCooldown = 0.2f;
        private Collider myCol;
        public SkipTickReason skipTick;
        private bool skipTickWaitingAlready;

        public GameManager parentGameManager;

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
            if (!isBotPlayer)
                Destroy(myBotController);
            else
                myBotController.Setup();
        }

        public void Update()
        {
            if (skipTick == SkipTickReason.None)
                PlayerMovement();
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
                StartCoroutine(StopTickingForTime(2.5f));
                StartCoroutine(GetBackUp());
            }
        }


        public void OnTriggerEnter(Collider other)
        {
            if(NetServer.BuiltRunningMode != NetServer.RunningMode.Server)
                return;
            if (other.GetComponent<DeathZone>() is not null)
            {
                Debug.Log("server-side player dead "+playerId);
                parentGameManager.OnPlayerEliminated(playerId);
                gameObject.SetActive(false);
                return;
            }

            if (other.GetComponent<FinishZone>() is not null)
            {
                Debug.Log("server-side player qualified "+playerId);
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

            if (other.GetComponent<ReadyToPlayZone>() is not null)
            {
                parentGameManager.readyPlayers.Add(playerId);
            }
        }


        private void PlayerMovement()
        {
            jumpCooldown -= Time.deltaTime;
            if (jumpCooldown < 0)
                jumpCooldown = 0;
            var maxSpeed = walkSpeed;


            if (localPlayer)
            {
                inputX = Input.GetAxisRaw("Horizontal");
                inputZ = Input.GetAxisRaw("Vertical");
                jumpKey = Input.GetButtonDown("Jump");
                diveKey = Input.GetMouseButtonDown(1);
            }


            var moveDirection = transform.forward * inputZ + transform.right * inputX;
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
                myRb.AddForce(Vector3.up * 6, ForceMode.Impulse);
                jumpCooldown = 0.2f;
            }

            if (diveKey)
            {
                myRb.AddForce(Vector3.up * 3f, ForceMode.Impulse);
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
            myRb.freezeRotation = true;
            myRb.angularVelocity = Vector3.zero;

            var elapsedTime = 0f;
            var recoveryDuration = 1.5f;
            var startRotation = transform.rotation;

            yield return new WaitForSeconds(1f);

            // Loop until the recovery duration is met
            while (elapsedTime < recoveryDuration)
            {
                // Calculate the interpolation factor (0 to 1)
                var t = elapsedTime / recoveryDuration;
                // Smoothly interpolate between the start and target rotations
                transform.rotation = Quaternion.Slerp(startRotation, Quaternion.identity, t);

                // Increment elapsed time by the time since the last frame
                elapsedTime += Time.deltaTime;

                // Yield control back to Unity for the next frame
                yield return null;
            }
        }

        public enum SkipTickReason
        {
            None,
            Dive,
            Dead,
            Stunned,
            GameOver
        }
    }
}