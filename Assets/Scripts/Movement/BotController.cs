using System.Collections;
using Tags;
using UnityEngine;

namespace Movement
{
    public class BotController : MonoBehaviour
    {
        public enum BotSkillType
        {
            Noob,
            Avg,
            Pro
        }

        public PlayerHandler myPlayer;
        public BotSkillType mySkillLevel;
        
        private bool botReady;
        private Coroutine _currentMoveCoroutine;
        private bool _isProcessingAction;

        // --- Bot State ---
        private Vector3? _currentTargetPosition;
        private Vector3? _lastKnownTargetPosition; // Stores the last valid target
        private bool _isTryingToRun;
        private bool _isUnstucking; // Flag to ignore triggers when stuck.
        private Coroutine _patienceCoroutine; // Timer for the current goal.

        public void Update()
        {
            if (!botReady)
            {
                if (myPlayer != null) myPlayer.inputZ = 0;
                return;
            }

            // Check if the bot has arrived at its current destination.
            if (!_isUnstucking && _currentTargetPosition.HasValue && Vector3.Distance(transform.position, _currentTargetPosition.Value) < 1.5f)
            {
                _currentTargetPosition = null; // Arrived, clear the target.
            }

            // If the bot has no target, isn't unstucking, and has a memory of a previous target, try to go back.
            if (!_currentTargetPosition.HasValue && !_isUnstucking && _lastKnownTargetPosition.HasValue)
            {
                _currentTargetPosition = _lastKnownTargetPosition;
                _isTryingToRun = true;
            }
            // If it has no target at all, it will stop.
            else if (!_currentTargetPosition.HasValue)
            {
                _isTryingToRun = false;
            }


            // --- Handle Rotation ---
            if (_currentTargetPosition.HasValue)
            {
                var directionToTarget = (_currentTargetPosition.Value - transform.position).normalized;
                directionToTarget.y = 0;

                if (directionToTarget != Vector3.zero)
                {
                    var targetRotation = Quaternion.LookRotation(directionToTarget);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
                }
            }
            
            // --- Handle Movement ---
            // The bot will always try to run as long as this flag is set.
            myPlayer.inputZ = _isTryingToRun ? 1 : 0;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Do not process new triggers if the bot is not ready, is in an uncontrollable state, or is trying to get unstuck.
            if (!botReady || myPlayer.skipTick != PlayerHandler.SkipTickReason.None || _isUnstucking)
            {
                return;
            }

            var botWaypointTrigger = other.gameObject.GetComponent<BotWaypoint>();
            if (botWaypointTrigger is null) return;

            // Movement commands are handled on OnTriggerEnter to set a new path.
            if (botWaypointTrigger.botAction == BotAction.None)
            {
                if (_currentMoveCoroutine != null) StopCoroutine(_currentMoveCoroutine);
                _currentMoveCoroutine = StartCoroutine(HandleMoveCommand(botWaypointTrigger));
            }
        }

        private void OnTriggerStay(Collider other)
        {
            // Do not process new triggers if the bot is not ready, is in an uncontrollable state, or is trying to get unstuck.
            if (!botReady || myPlayer.skipTick != PlayerHandler.SkipTickReason.None || _isUnstucking)
            {
                return;
            }

            var botWaypointTrigger = other.gameObject.GetComponent<BotWaypoint>();
            if (botWaypointTrigger is null) return;

            // Action commands are handled on OnTriggerStay to allow for repeated attempts.
            if (botWaypointTrigger.botAction != BotAction.None)
            {
                if (!_isProcessingAction)
                {
                    StartCoroutine(HandleActionCommand(botWaypointTrigger));
                }
            }
        }

        /// <summary>
        /// A coroutine that checks if the bot has been stuck on the same task for too long.
        /// </summary>
        private IEnumerator CheckPatience(Vector3 target)
        {
            // Wait for 5 seconds.
            yield return new WaitForSeconds(5.0f);

            // After 5 seconds, if we are not currently in the unstucking routine AND we are still trying to reach the same target...
            if (!_isUnstucking && _currentTargetPosition.HasValue && _currentTargetPosition.Value == target)
            {
                // ...then we are stuck on this task. Time to try something else.
                StartCoroutine(UnstuckAndRetry(target));
            }
        }
        
        /// <summary>
        /// A dedicated failsafe to get the bot out of a stuck situation by overriding all other logic.
        /// </summary>
        private IEnumerator UnstuckAndRetry(Vector3 originalTarget)
        {
            if (_isUnstucking) yield break; // Prevent this from running multiple times.
            _isUnstucking = true;

            // Force stop any other logic that might be controlling the bot.
            if (_currentMoveCoroutine != null)
            {
                StopCoroutine(_currentMoveCoroutine);
                _currentMoveCoroutine = null;
            }
             _isProcessingAction = false; 

            // Clear the current target completely and set run state.
            _currentTargetPosition = null;
            _isTryingToRun = true;

            // Wander in a random direction for a short time to break free.
            Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
            if (randomDirection.sqrMagnitude < 0.1f) randomDirection = transform.forward; // Failsafe for zero vector
            _currentTargetPosition = transform.position + randomDirection * 10f; // Move 10 units away
            
            // During this 5-second period, the bot will ignore triggers, move towards the random point, and jump periodically.
            float unstuckTimer = 5.0f;
            while (unstuckTimer > 0)
            {
                // Jump if grounded
                if (myPlayer.groundedForJump)
                {
                    StartCoroutine(PerformAction(true, false));
                }
                // Wait a second before the next jump attempt
                yield return new WaitForSeconds(1.0f);
                unstuckTimer -= 1.0f;
            }

            // After unstucking, try to return to the original target.
            _currentTargetPosition = originalTarget;
            _isTryingToRun = true;
            
            _isUnstucking = false;

            // After we've reset, start a new patience timer for this retried goal.
            if (_patienceCoroutine != null) StopCoroutine(_patienceCoroutine);
            _patienceCoroutine = StartCoroutine(CheckPatience(originalTarget));
        }

        /// <summary>
        /// Handles the logic for a movement waypoint.
        /// </summary>
        private IEnumerator HandleMoveCommand(BotWaypoint waypoint)
        {
            _isTryingToRun = true;
            
            Vector3 finalTarget = waypoint.targetPosition.transform.position;

            // Set the new target and also store it as the last known good position.
            _currentTargetPosition = finalTarget;
            _lastKnownTargetPosition = finalTarget;

            _currentMoveCoroutine = null;
            
            // Whenever we get a new move command, we reset the patience timer.
            if (_patienceCoroutine != null) StopCoroutine(_patienceCoroutine);
            _patienceCoroutine = StartCoroutine(CheckPatience(finalTarget));

            yield break;
        }

        /// <summary>
        /// Handles the logic for an action waypoint (Jump/Dive).
        /// </summary>
        private IEnumerator HandleActionCommand(BotWaypoint waypoint)
        {
            _isProcessingAction = true;

            if (waypoint.botAction == BotAction.Jump)
            {
                // Only attempt to jump if the bot is actually on the ground.
                if (myPlayer.groundedForJump)
                {
                    StartCoroutine(PerformAction(true, false));
                }
            }
            else if (waypoint.botAction == BotAction.Dive)
            {
                // When diving, we temporarily stop the bot's regular movement since the dive is an impulse.
                _isTryingToRun = false;
                StartCoroutine(PerformAction(false, true));
            }

            // A small cooldown to prevent the action from firing on every single frame.
            yield return new WaitForSeconds(0.2f);
            _isProcessingAction = false;
        }

        /// <summary>
        /// Performs a single-frame action like a jump or dive by setting the key for one frame.
        /// </summary>
        private IEnumerator PerformAction(bool doJump, bool doDive)
        {
            // Set the key to true for one frame
            if (doJump) myPlayer.jumpKey = true;
            if (doDive) myPlayer.diveKey = true;

            // Wait for the next frame
            yield return null;

            // Reset the key on the next frame so it's not held down
            if (doJump) myPlayer.jumpKey = false;
            if (doDive) myPlayer.diveKey = false;
        }

        public void Setup()
        {
            myPlayer = GetComponent<PlayerHandler>();
            // Skill level is still assigned but no longer affects reaction time or disobedience.
            // This could be used for other behaviors in the future.
            var skillLevelRandom = Random.Range(0f, 100f);
            if (skillLevelRandom <= 16f)
                mySkillLevel = BotSkillType.Pro;
            else if (skillLevelRandom <= 72f)
                mySkillLevel = BotSkillType.Avg;
            else
                mySkillLevel = BotSkillType.Noob;

            // The patience checker is now started from HandleMoveCommand, so no need to start a coroutine here.
            botReady = true;
        }
    }
}
