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

        private static readonly float reactionTimeBotProMin = 0.1f;
        private static readonly float reactionTimeBotProMax = 0.3f;

        private static readonly float reactionTimeBotAvgMin = 0.5f;
        private static readonly float reactionTimeBotAvgMax = 1.0f;

        private static readonly float reactionTimeBotNoobMin = 1.2f;
        private static readonly float reactionTimeBotNoobMax = 1.8f;

        private static readonly float disobedienceBotProMin = 0.00f;
        private static readonly float disobedienceBotProMax = 0.03f;

        private static readonly float disobedienceBotAvgMin = 0.04f;
        private static readonly float disobedienceBotAvgMax = 0.08f;

        private static readonly float disobedienceBotNoobMin = 0.10f;
        private static readonly float disobedienceBotNoobMax = 0.20f;
        public PlayerHandler myPlayer;

        public BotSkillType mySkillLevel;
        private bool botReady;
        private Coroutine currentRunCorou;

        private float myDisobedience, myReactionTime;

        private Vector3? pathfindTargetPos;
        private bool thinkingAboutActing;
        private bool tryingToRun;


        public void Update()
        {
            if (!botReady)
            {
                if (myPlayer is not null) myPlayer.inputZ = 0;
                return;
            }

            if (pathfindTargetPos.HasValue)
            {
                var directionToTarget = (pathfindTargetPos.Value - transform.position).normalized;
                directionToTarget.y = 0;

                if (directionToTarget != Vector3.zero)
                {
                    var targetRotation = Quaternion.LookRotation(directionToTarget);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
                }
                
                if (Vector3.Distance(transform.position, pathfindTargetPos.Value) < 0.5f)
                {
                    //tryingToRun = false;
                    pathfindTargetPos = null;
                    //myPlayer.inputZ = 0; // Stop moving
                }
            }
            
            if(tryingToRun)
                myPlayer.inputZ = 1;
            
            
        }

        private void OnTriggerEnter(Collider other)
        {
            // Do not process new triggers if the bot is not ready or is in an uncontrollable state (e.g., after a dive).
            if (!botReady || myPlayer.skipTick != PlayerHandler.SkipTickReason.None)
            {
                //Debug.Log("skipping bot turn");
                return;
            }

            var botWaypointTrigger = other.gameObject.GetComponent<BotWaypoint>();
            if (botWaypointTrigger is null)
                return;

            // Handle waypoint based on its type (move or action)
            if (botWaypointTrigger.botAction == BotAction.None)
            {
                // If we are not already processing a move command, start a new one.
                if (currentRunCorou != null) StopCoroutine(currentRunCorou);

                currentRunCorou = StartCoroutine(HandleMoveCommand(botWaypointTrigger));
            }
            else
            {
                // If we are not already processing an action, start a new one.
                // This allows an action (like a jump) to occur while the bot is moving.
                if (!thinkingAboutActing) StartCoroutine(HandleActionCommand(botWaypointTrigger));
            }
        }

        /// <summary>
        ///     Handles the logic for a movement waypoint after the bot's reaction time delay.
        /// </summary>
        private IEnumerator HandleMoveCommand(BotWaypoint waypoint)
        {
            tryingToRun = true;
            yield return new WaitForSeconds(myReactionTime);

            // Decide whether to obey or disobey the move command
            var isDisobedient = Random.Range(0f, 1f) < myDisobedience;
            var finalTarget = waypoint.targetPosition.transform.position;

            if (isDisobedient)
            {
                // Pick a random point near the intended target
                var randomOffset = new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f));
                finalTarget += randomOffset;
            }

            // Set the target and start moving towards it
            pathfindTargetPos = finalTarget;
            

            currentRunCorou = null;
        }

        /// <summary>
        ///     Handles the logic for an action waypoint (Jump/Dive) after the bot's reaction time delay.
        /// </summary>
        private IEnumerator HandleActionCommand(BotWaypoint waypoint)
        {
            thinkingAboutActing = true;
            yield return new WaitForSeconds(myReactionTime);

            if (waypoint.botAction == BotAction.Jump)
            {
                StartCoroutine(PerformAction(true, false));
            }
            else if (waypoint.botAction == BotAction.Dive)
            {
                // When diving, we stop the bot's regular movement since the dive is an impulse
                // and the player will be uncontrollable afterwards.
                tryingToRun = false;
                myPlayer.inputZ = 0;
                StartCoroutine(PerformAction(false, true));
            }

            // A small cooldown before the bot can process another action.
            yield return new WaitForSeconds(0.5f);
            thinkingAboutActing = false;
        }

        /// <summary>
        ///     Performs a single-frame action like a jump or dive by setting the key for one frame.
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
            // 16% chance to get Pro
            // 56% chance to get Avg
            // 28% chance to get Noob
            var skillLevelRandom = Random.Range(0f, 100f);
            if (skillLevelRandom <= 16f)
                mySkillLevel = BotSkillType.Pro;
            else if (skillLevelRandom <= 72f)
                mySkillLevel = BotSkillType.Avg;
            else
                mySkillLevel = BotSkillType.Noob;
            switch (mySkillLevel)
            {
                case BotSkillType.Noob:
                    myDisobedience = Random.Range(disobedienceBotNoobMin, disobedienceBotNoobMax);
                    myReactionTime = Random.Range(reactionTimeBotNoobMin, reactionTimeBotNoobMax);
                    break;
                case BotSkillType.Avg:
                    myDisobedience = Random.Range(disobedienceBotAvgMin, disobedienceBotAvgMax);
                    myReactionTime = Random.Range(reactionTimeBotAvgMin, reactionTimeBotAvgMax);
                    break;
                case BotSkillType.Pro:
                    myDisobedience = Random.Range(disobedienceBotProMin, disobedienceBotProMax);
                    myReactionTime = Random.Range(reactionTimeBotProMin, reactionTimeBotProMax);
                    break;
            }

            botReady = true;
        }
    }
}