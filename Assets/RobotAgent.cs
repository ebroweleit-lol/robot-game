using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

/// <summary>
/// ML-Agents script for robot sumo game.
/// The agent learns to push the opponent off the platform.
/// </summary>
public class RobotAgent : Agent
{
    [Header("Robot References")]
    [Tooltip("The robot GameObject this agent controls")]
    public GameObject robotBody;
    
    [Tooltip("The opponent robot agent")]
    public RobotAgent opponentAgent;
    
    [Header("Control Mode")]
    [Tooltip("Enable to control this robot manually with WASD keys (disable for AI training)")]
    public bool playerControlled = false;
    
    [Header("Arena Settings")]
    [Tooltip("The platform/arena GameObject")]
    public GameObject arena;
    
    [Tooltip("Half-width of platform (X direction)")]
    public float platformHalfWidth = 3.5f;  // Half of 6.96
    
    [Tooltip("Half-depth of platform (Z direction)")]
    public float platformHalfDepth = 3.2f;  // Half of 6.37
    
    [Tooltip("Height below which robot is considered fallen")]
    public float fallHeight = -1.0f;  // Below platform at Y=-0.01
    
    [Header("Movement Settings")]
    [Tooltip("Movement speed")]
    public float moveSpeed = 10f;
    
    [Tooltip("Rotation speed (degrees per second)")]
    public float rotationSpeed = 300f;  // Increased from 120 for faster turning
    
    [Tooltip("Acceleration rate")]
    public float acceleration = 10f;
    
    [Tooltip("Deceleration/friction rate")]
    public float friction = 15f;
    
    [Tooltip("Push power gain rate")]
    public float pushPowerGainRate = 50f;
    
    [Tooltip("Push power decay rate")]
    public float pushPowerDecayRate = 2f;
    
    [Header("Reward Settings")]
    [Tooltip("Reward for winning (pushing opponent off)")]
    public float winReward = 5.0f;  // Increased from 1.0 to emphasize winning
    
    [Tooltip("Penalty for falling off")]
    public float losePenalty = -5.0f;  // Increased from -1.0 to match win reward
    
    [Tooltip("Small reward per step for staying on platform")]
    public float existenceReward = 0.0f;  // Disabled - encourages passivity
    
    [Tooltip("Reward for being closer to opponent")]
    public float approachReward = 0.05f;
    
    [Tooltip("Reward for facing opponent")]
    public float facingReward = 0.0f;  // Disabled - causes spinning behavior
    
    [Tooltip("Reward for moving forward")]
    public float forwardReward = 0.03f;
    
    [Tooltip("Reward for pushing opponent")]
    public float pushingReward = 0.15f;
    
    // Private movement variables
    private Vector3 horizontalVelocity = Vector3.zero;
    private float rotationVelocity = 0f;
    private float pushPower = 0f;
    private Rigidbody rb;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 arenaCenter;
    private float lastDistanceToOpponent;
    private bool blocked = false;
    private bool outOfBounds = false;  // Flag to stop movement when falling off
    private Vector3 lastPosition;
    private float timeWithoutMovement = 0f;
    private bool hasMovedOnce = false;
    private const float INACTIVITY_TIMEOUT = 5f;
    private bool episodeScored = false;  // Prevent double-counting wins
    
    // Track current actions for visualization
    private float currentMoveAction = 0f;
    private float currentRotateAction = 0f;
    
    // Persistent score tracking
    private static int robot1Wins = 0;
    private static int robot2Wins = 0;
    private bool isRobot1 = false;  // Determined by name or position
    
    public override void Initialize()
    {
        // Get rigidbody (should already exist from Inspector setup)
        rb = robotBody.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError($"No Rigidbody found on {robotBody.name}! Please add one in Inspector.");
            return;
        }
        
        // Configure for robot sumo physics
        rb.useGravity = true;  // Gravity enabled so robots can fall off platform
        rb.constraints = RigidbodyConstraints.FreezeRotationX | 
                        RigidbodyConstraints.FreezeRotationZ;  // Only allow Y rotation
        
        // Determine if this is robot 1 or robot 2 based on position
        // Robot 1 is on left side (negative X), Robot 2 is on right (positive X)
        if (robotBody != null)
        {
            isRobot1 = robotBody.transform.position.x < 0;
        }
        
        // Ensure collider exists
        if (robotBody.GetComponent<Collider>() == null)
        {
            robotBody.AddComponent<BoxCollider>();
        }
        
        // Initial position will be set by SumoArenaManager
        // Don't capture it here as ArenaManager moves robots in Start()
        
        // Get arena center
        if (arena != null)
        {
            arenaCenter = arena.transform.position;
        }
        else
        {
            arenaCenter = Vector3.zero;
        }
        
        lastDistanceToOpponent = float.MaxValue;
    }
    
    /// <summary>
    /// Called by SumoArenaManager to set the spawn position
    /// </summary>
    public void SetSpawnPoint(Vector3 position, Quaternion rotation)
    {
        initialPosition = position;
        initialRotation = rotation;
        robotBody.transform.position = position;
        robotBody.transform.rotation = rotation;
    }
    
    public override void OnEpisodeBegin()
    {
        // Reset robot position and rotation
        robotBody.transform.position = initialPosition;
        robotBody.transform.rotation = initialRotation;
        
        Debug.Log($"[{robotBody.name}] RESET to position {initialPosition}");
        
        // Reset physics
        horizontalVelocity = Vector3.zero;
        rotationVelocity = 0f;
        pushPower = 0f;
        blocked = false;
        outOfBounds = false;
        
        // Reset inactivity tracking
        lastPosition = initialPosition;
        timeWithoutMovement = 0f;
        hasMovedOnce = false;
        episodeScored = false;
        
        // Reset Rigidbody velocities to prevent physics carryover
        Rigidbody rb = robotBody.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Reset distance tracking
        if (opponentAgent != null && opponentAgent.robotBody != null)
        {
            lastDistanceToOpponent = Vector3.Distance(
                robotBody.transform.position, 
                opponentAgent.robotBody.transform.position
            );
        }
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        // Agent's position relative to arena center (3 values)
        Vector3 relativePos = robotBody.transform.position - arenaCenter;
        sensor.AddObservation(relativePos.x / platformHalfWidth);
        sensor.AddObservation(relativePos.y);
        sensor.AddObservation(relativePos.z / platformHalfDepth);
        
        // Agent's rotation (2 values - forward direction in XZ plane)
        Vector3 forward = robotBody.transform.forward;
        sensor.AddObservation(forward.x);
        sensor.AddObservation(forward.z);
        
        // Agent's velocity (3 values)
        sensor.AddObservation(horizontalVelocity.x / moveSpeed);
        sensor.AddObservation(horizontalVelocity.y / moveSpeed);
        sensor.AddObservation(horizontalVelocity.z / moveSpeed);
        
        // Agent's push power (1 value)
        sensor.AddObservation(pushPower / 100f);
        
        if (opponentAgent != null && opponentAgent.robotBody != null)
        {
            // Opponent's position relative to agent (3 values)
            Vector3 toOpponent = opponentAgent.robotBody.transform.position - robotBody.transform.position;
            float maxDist = Mathf.Max(platformHalfWidth, platformHalfDepth) * 2;
            sensor.AddObservation(toOpponent.x / maxDist);
            sensor.AddObservation(toOpponent.y);
            sensor.AddObservation(toOpponent.z / maxDist);
            
            // Opponent's velocity relative to agent (3 values)
            Vector3 opponentVel = opponentAgent.GetVelocity();
            sensor.AddObservation(opponentVel.x / moveSpeed);
            sensor.AddObservation(opponentVel.y / moveSpeed);
            sensor.AddObservation(opponentVel.z / moveSpeed);
            
            // Opponent's push power (1 value)
            sensor.AddObservation(opponentAgent.GetPushPower() / 100f);
            
            // Distance to opponent (1 value)
            float distance = toOpponent.magnitude;
            sensor.AddObservation(distance / maxDist);
            
            // Dot product of forward direction to opponent (1 value)
            float facingDot = Vector3.Dot(forward, toOpponent.normalized);
            sensor.AddObservation(facingDot);
        }
        else
        {
            // Add zeros if no opponent
            for (int i = 0; i < 9; i++)
            {
                sensor.AddObservation(0f);
            }
        }
        
        // Total observations: 3 + 2 + 3 + 1 + 9 = 18
    }
    
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Get continuous actions
        float moveAction = actions.ContinuousActions[0]; // -1 to 1 (backward to forward)
        float rotateAction = actions.ContinuousActions[1]; // -1 to 1 (left to right)
        
        // Store for visualization
        currentMoveAction = moveAction;
        currentRotateAction = rotateAction;
        
        // Convert actions to movement - robot models are rotated 90 degrees
        Vector3 forward = Quaternion.Euler(0, 90, 0) * robotBody.transform.forward;
        
        // Check for obstacles/collisions
        CheckForObstacles(forward, moveAction);
        
        // Apply movement if not blocked and not out of bounds
        if (!blocked && !outOfBounds && Mathf.Abs(moveAction) > 0.01f)
        {
            Vector3 targetVelocity = forward * moveAction * moveSpeed;
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
            
            // Gain push power when moving forward
            if (moveAction > 0)
            {
                pushPower = Mathf.Min(100f, pushPower + pushPowerGainRate * Time.fixedDeltaTime);
                
                // Small reward for moving forward
                AddReward(forwardReward * Time.fixedDeltaTime);
            }
            else
            {
                pushPower = Mathf.Max(0f, pushPower - pushPower * pushPowerDecayRate * Time.fixedDeltaTime);
            }
        }
        else
        {
            // Apply friction
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, friction * Time.fixedDeltaTime);
            pushPower = Mathf.Max(0f, pushPower - pushPower * pushPowerDecayRate * Time.fixedDeltaTime);
        }
        
        // Apply rotation
        if (Mathf.Abs(rotateAction) > 0.01f)
        {
            rotationVelocity = rotateAction * rotationSpeed;
        }
        else
        {
            rotationVelocity = Mathf.Lerp(rotationVelocity, 0f, friction * Time.fixedDeltaTime);
        }
        
        // Apply physics to rigidbody
        if (rb != null && !rb.isKinematic)
        {
            // Dynamic rigidbody - set velocities directly
            rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
            
            // Use AddTorque for rotation instead of setting angular velocity
            // This works better with small colliders
            float torque = rotationVelocity * rb.mass * 0.05f;  // Fine-tuned for controlled rotation
            rb.AddTorque(0, torque, 0, ForceMode.Force);
        }
        else if (rb != null && rb.isKinematic)
        {
            // Kinematic rigidbody - use MovePosition/MoveRotation
            Vector3 newPos = robotBody.transform.position + horizontalVelocity * Time.fixedDeltaTime;
            rb.MovePosition(newPos);
            
            Quaternion deltaRot = Quaternion.Euler(0f, rotationVelocity * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * deltaRot);
        }
        
        // Calculate rewards
        CalculateRewards();
        
        // Check win/lose conditions
        CheckEndConditions();
        
        // Reset blocked flag
        blocked = false;
    }
    
    private void CheckForObstacles(Vector3 forward, float moveDirection)
    {
        blocked = false;
        
        if (Mathf.Abs(moveDirection) < 0.01f) return;
        
        Vector3 checkDir = forward * Mathf.Sign(moveDirection);
        Collider[] hits = Physics.OverlapSphere(robotBody.transform.position, 0.6f);
        
        foreach (Collider col in hits)
        {
            if (col.isTrigger) continue;
            
            // Check if it's our own collider
            bool isSelf = col.gameObject == robotBody || 
                         col.transform.IsChildOf(robotBody.transform) || 
                         robotBody.transform.IsChildOf(col.transform);
            
            if (isSelf) continue;
            
            // Ignore floor
            if (col.bounds.max.y < robotBody.transform.position.y - 0.1f)
                continue;
            
            Vector3 toObstacle = col.transform.position - robotBody.transform.position;
            float distance = toObstacle.magnitude;
            float dot = Vector3.Dot(toObstacle.normalized, checkDir.normalized);
            
            if (dot > 0.7f && distance < 0.5f)
            {
                // Check if it's the opponent
                RobotAgent otherAgent = col.GetComponent<RobotAgent>();
                if (otherAgent != null && otherAgent == opponentAgent)
                {
                    // Compare push powers
                    float otherPower = otherAgent.GetPushPower();
                    
                    if (pushPower > otherPower)
                    {
                        // We're stronger - push them
                        Vector3 pushVelocity = checkDir.normalized * horizontalVelocity.magnitude;
                        otherAgent.SetVelocity(pushVelocity);
                        otherAgent.SetBlocked(true);
                        
                        // Reward for pushing
                        AddReward(pushingReward * Time.fixedDeltaTime);
                        
                        // Enforce separation
                        if (distance < 0.4f)
                        {
                            float pushAway = (0.4f - distance) + 0.01f;
                            otherAgent.robotBody.transform.position += checkDir.normalized * pushAway;
                        }
                    }
                    else
                    {
                        // They're stronger - we get blocked
                        blocked = true;
                    }
                }
                else
                {
                    // It's a wall or other obstacle
                    blocked = true;
                }
                break;
            }
        }
    }
    
    private void CalculateRewards()
    {
        // Small reward for staying on platform (if enabled)
        if (existenceReward > 0)
            AddReward(existenceReward);
        
        if (opponentAgent != null && opponentAgent.robotBody != null)
        {
            // Reward for getting closer to opponent
            float currentDistance = Vector3.Distance(
                robotBody.transform.position, 
                opponentAgent.robotBody.transform.position
            );
            
            if (currentDistance < lastDistanceToOpponent)
            {
                AddReward(approachReward * Time.fixedDeltaTime);
            }
            
            lastDistanceToOpponent = currentDistance;
            
            // Reward for facing opponent
            Vector3 toOpponent = opponentAgent.robotBody.transform.position - robotBody.transform.position;
            float facingDot = Vector3.Dot(robotBody.transform.forward, toOpponent.normalized);
            
            if (facingDot > 0.7f) // Facing opponent
            {
                AddReward(facingReward * Time.fixedDeltaTime);
            }
        }
    }
    
    private void CheckEndConditions()
    {
        // Check if this agent fell off
        if (robotBody.transform.position.y < fallHeight)
        {
            Debug.Log($"[{robotBody.name}] FELL OFF! Y={robotBody.transform.position.y:F2} < {fallHeight}");
            AddReward(losePenalty);
            
            // Update score (only once per episode)
            if (!episodeScored)
            {
                if (isRobot1)
                    robot2Wins++;
                else
                    robot1Wins++;
                episodeScored = true;
            }
            
            // End episode for both robots
            if (opponentAgent != null)
            {
                opponentAgent.AddReward(winReward); // Opponent wins
                opponentAgent.EndEpisode();
            }
            EndEpisode();
            return;
        }
        
        // Check if outside platform bounds (square platform)
        // End match immediately when robot center crosses the edge
        Vector3 relativePos = robotBody.transform.position - arenaCenter;
        float absX = Mathf.Abs(relativePos.x);
        float absZ = Mathf.Abs(relativePos.z);
        
        // End as soon as center crosses platform edge (no tolerance for clinging)
        if (absX > platformHalfWidth || absZ > platformHalfDepth)
        {
            Debug.Log($"[{robotBody.name}] OFF PLATFORM! X={absX:F2}/{platformHalfWidth} Z={absZ:F2}/{platformHalfDepth}");
            
            AddReward(losePenalty);
            
            // Update score (only once per episode)
            if (!episodeScored)
            {
                if (isRobot1)
                    robot2Wins++;
                else
                    robot1Wins++;
                episodeScored = true;
            }
            
            // End episode for both robots
            if (opponentAgent != null)
            {
                opponentAgent.AddReward(winReward); // Opponent wins
                opponentAgent.EndEpisode();
            }
            EndEpisode();
            return;
        }
        
        // Check if opponent fell off (we win)
        if (opponentAgent != null && opponentAgent.robotBody != null)
        {
            if (opponentAgent.robotBody.transform.position.y < fallHeight)
            {
                AddReward(winReward);
                
                // End episode for both robots
                opponentAgent.AddReward(losePenalty);
                opponentAgent.EndEpisode();
                EndEpisode();
                return;
            }
            
            // Check if opponent is outside platform bounds
            Vector3 opponentRelativePos = opponentAgent.robotBody.transform.position - arenaCenter;
            float opponentAbsX = Mathf.Abs(opponentRelativePos.x);
            float opponentAbsZ = Mathf.Abs(opponentRelativePos.z);
            
            if (opponentAbsX > platformHalfWidth || opponentAbsZ > platformHalfDepth)
            {
                // Push opponent further off if center is over edge (prevents clinging)
                Rigidbody opponentRb = opponentAgent.robotBody.GetComponent<Rigidbody>();
                if (opponentRb != null)
                {
                    Vector3 pushDirection = opponentRelativePos.normalized;
                    opponentRb.AddForce(pushDirection * 2000f + Vector3.down * 500f, ForceMode.Impulse);
                }
                
                AddReward(winReward);
                
                // End episode for both robots
                opponentAgent.AddReward(losePenalty);
                opponentAgent.EndEpisode();
                EndEpisode();
                return;
            }
        }
        
        // Check for inactivity (not moving for 5 seconds after initial movement)
        Vector3 currentPosition = robotBody.transform.position;
        float distanceMoved = Vector3.Distance(currentPosition, lastPosition);
        
        if (distanceMoved > 0.1f)  // Moved more than 0.1 units
        {
            hasMovedOnce = true;
            timeWithoutMovement = 0f;
            lastPosition = currentPosition;
        }
        else if (hasMovedOnce)  // Only check inactivity after they've moved at least once
        {
            timeWithoutMovement += Time.fixedDeltaTime;
            
            if (timeWithoutMovement >= INACTIVITY_TIMEOUT)
            {
                Debug.Log($"[{robotBody.name}] INACTIVITY TIMEOUT! No movement for {INACTIVITY_TIMEOUT} seconds");
                AddReward(losePenalty);
                
                // Update score (only once per episode)
                if (!episodeScored)
                {
                    if (isRobot1)
                        robot2Wins++;
                    else
                        robot1Wins++;
                    episodeScored = true;
                }
                
                // End episode for both robots
                if (opponentAgent != null)
                {
                    opponentAgent.AddReward(winReward);
                    opponentAgent.EndEpisode();
                }
                EndEpisode();
                return;
            }
        }
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // For manual testing with keyboard
        var continuousActions = actionsOut.ContinuousActions;
        
        // Default: no movement
        continuousActions[0] = 0f;
        continuousActions[1] = 0f;
        
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) continuousActions[0] = 1f;
            if (kb.sKey.isPressed) continuousActions[0] = -1f;
            if (kb.aKey.isPressed) continuousActions[1] = -1f;
            if (kb.dKey.isPressed) continuousActions[1] = 1f;
        }
#else
        if (Input.GetKey(KeyCode.W)) continuousActions[0] = 1f;
        if (Input.GetKey(KeyCode.S)) continuousActions[0] = -1f;
        if (Input.GetKey(KeyCode.A)) continuousActions[1] = -1f;
        if (Input.GetKey(KeyCode.D)) continuousActions[1] = 1f;
#endif
    }
    
    // Public methods for interaction
    public void SetVelocity(Vector3 velocity)
    {
        horizontalVelocity = velocity;
    }
    
    public Vector3 GetVelocity()
    {
        return horizontalVelocity;
    }
    
    public float GetPushPower()
    {
        return pushPower;
    }
    
    public void SetBlocked(bool block)
    {
        blocked = block;
    }
    
    // Visualization
    void OnGUI()
    {
        // Draw score at top of screen (only draw once, from robot 1)
        if (isRobot1)
        {
            GUIStyle scoreStyle = new GUIStyle();
            scoreStyle.fontSize = 32;
            scoreStyle.fontStyle = FontStyle.Bold;
            scoreStyle.normal.textColor = Color.white;
            scoreStyle.alignment = TextAnchor.MiddleCenter;
            
            string scoreText = $"Robot 1: {robot1Wins}  |  Robot 2: {robot2Wins}";
            GUI.Label(new Rect(Screen.width/2 - 200, 20, 400, 40), scoreText, scoreStyle);
        }
        
        if (robotBody != null && Camera.main != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(robotBody.transform.position + Vector3.up * 2.5f);
            
            if (screenPos.z > 0)
            {
                screenPos.y = Screen.height - screenPos.y;
                
                // Main stats style
                GUIStyle style = new GUIStyle();
                style.fontSize = 18;
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.green;
                style.alignment = TextAnchor.MiddleCenter;
                
                // Action display style
                GUIStyle actionStyle = new GUIStyle();
                actionStyle.fontSize = 14;
                actionStyle.fontStyle = FontStyle.Normal;
                actionStyle.alignment = TextAnchor.MiddleCenter;
                
                // Build action visualization
                string moveArrow = "";
                if (currentMoveAction > 0.1f) moveArrow = "↑ Forward";
                else if (currentMoveAction < -0.1f) moveArrow = "↓ Backward";
                else moveArrow = "○ Still";
                
                string rotateArrow = "";
                if (currentRotateAction > 0.1f) rotateArrow = "→ Right";
                else if (currentRotateAction < -0.1f) rotateArrow = "← Left";
                else rotateArrow = "| Straight";
                
                // Color based on action intensity
                float moveIntensity = Mathf.Abs(currentMoveAction);
                float rotateIntensity = Mathf.Abs(currentRotateAction);
                
                Color moveColor = Color.Lerp(Color.gray, Color.cyan, moveIntensity);
                Color rotateColor = Color.Lerp(Color.gray, Color.yellow, rotateIntensity);
                
                // Main text
                string text = $"Power: {(int)pushPower}\nReward: {GetCumulativeReward():F2}";
                GUI.Label(new Rect(screenPos.x - 70, screenPos.y - 60, 140, 50), text, style);
                
                // Action inputs
                actionStyle.normal.textColor = moveColor;
                GUI.Label(new Rect(screenPos.x - 70, screenPos.y - 15, 140, 20), moveArrow, actionStyle);
                
                actionStyle.normal.textColor = rotateColor;
                GUI.Label(new Rect(screenPos.x - 70, screenPos.y + 5, 140, 20), rotateArrow, actionStyle);
                
                // Action bar visualization
                DrawActionBar(screenPos.x - 50, screenPos.y + 25, 100, 8, currentMoveAction, Color.cyan);
                DrawActionBar(screenPos.x - 50, screenPos.y + 38, 100, 8, currentRotateAction, Color.yellow);
            }
        }
    }
    
    void DrawActionBar(float x, float y, float width, float height, float value, Color color)
    {
        // Background
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(x - 2, y - 2, width + 4, height + 4), Texture2D.whiteTexture);
        
        // Center line
        GUI.color = Color.gray;
        GUI.DrawTexture(new Rect(x + width/2 - 1, y, 2, height), Texture2D.whiteTexture);
        
        // Value bar
        GUI.color = color;
        if (value > 0)
        {
            GUI.DrawTexture(new Rect(x + width/2, y, (width/2) * value, height), Texture2D.whiteTexture);
        }
        else
        {
            GUI.DrawTexture(new Rect(x + width/2 + (width/2) * value, y, (width/2) * -value, height), Texture2D.whiteTexture);
        }
        
        GUI.color = Color.white; // Reset
    }
    
    // Draw forward direction in Scene view
    void OnDrawGizmos()
    {
        if (robotBody != null)
        {
            // Draw forward direction arrow (corrected for robot model orientation)
            Vector3 pos = robotBody.transform.position;
            Vector3 forward = Quaternion.Euler(0, 90, 0) * robotBody.transform.forward;  // Same correction as movement
            
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(pos, pos + forward * 1.5f);
            
            // Arrow head
            Vector3 arrowTip = pos + forward * 1.5f;
            Vector3 right = Quaternion.Euler(0, 90, 0) * robotBody.transform.right;
            Gizmos.DrawLine(arrowTip, arrowTip - forward * 0.3f + right * 0.2f);
            Gizmos.DrawLine(arrowTip, arrowTip - forward * 0.3f - right * 0.2f);
            
            // Label
            UnityEditor.Handles.Label(pos + forward * 1.8f, "FORWARD", new GUIStyle() 
            { 
                normal = new GUIStyleState() { textColor = Color.blue },
                fontSize = 12,
                fontStyle = FontStyle.Bold
            });
        }
    }
}
