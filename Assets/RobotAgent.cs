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
    
    [Header("Arena Settings")]
    [Tooltip("The platform/arena GameObject")]
    public GameObject arena;
    
    [Tooltip("Distance from center before robot is considered off platform")]
    public float platformRadius = 10f;
    
    [Tooltip("Height below which robot is considered fallen")]
    public float fallHeight = -2f;
    
    [Header("Movement Settings")]
    [Tooltip("Movement speed")]
    public float moveSpeed = 15f;
    
    [Tooltip("Rotation speed (degrees per second)")]
    public float rotationSpeed = 120f;
    
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
    public float winReward = 1.0f;
    
    [Tooltip("Penalty for falling off")]
    public float losePenalty = -1.0f;
    
    [Tooltip("Small reward per step for staying on platform")]
    public float existenceReward = 0.0f;  // Disabled - encourages passivity
    
    [Tooltip("Reward for being closer to opponent")]
    public float approachReward = 0.02f;
    
    [Tooltip("Reward for facing opponent")]
    public float facingReward = 0.01f;
    
    [Tooltip("Reward for moving forward")]
    public float forwardReward = 0.01f;
    
    [Tooltip("Reward for pushing opponent")]
    public float pushingReward = 0.1f;
    
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
    
    // Track current actions for visualization
    private float currentMoveAction = 0f;
    private float currentRotateAction = 0f;
    
    public override void Initialize()
    {
        // Get or add rigidbody
        rb = robotBody.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = robotBody.AddComponent<Rigidbody>();
        }
        
        // Configure rigidbody
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.mass = 10f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        
        // Ensure collider exists
        if (robotBody.GetComponent<Collider>() == null)
        {
            robotBody.AddComponent<BoxCollider>();
        }
        
        // Store initial transform
        initialPosition = robotBody.transform.position;
        initialRotation = robotBody.transform.rotation;
        
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
    
    public override void OnEpisodeBegin()
    {
        // Reset robot position and rotation
        robotBody.transform.position = initialPosition;
        robotBody.transform.rotation = initialRotation;
        
        // Reset physics
        horizontalVelocity = Vector3.zero;
        rotationVelocity = 0f;
        pushPower = 0f;
        blocked = false;
        
        // Note: Don't set velocities on kinematic rigidbody - not supported
        
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
        sensor.AddObservation(relativePos.x / platformRadius);
        sensor.AddObservation(relativePos.y);
        sensor.AddObservation(relativePos.z / platformRadius);
        
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
            sensor.AddObservation(toOpponent.x / platformRadius);
            sensor.AddObservation(toOpponent.y);
            sensor.AddObservation(toOpponent.z / platformRadius);
            
            // Opponent's velocity relative to agent (3 values)
            Vector3 opponentVel = opponentAgent.GetVelocity();
            sensor.AddObservation(opponentVel.x / moveSpeed);
            sensor.AddObservation(opponentVel.y / moveSpeed);
            sensor.AddObservation(opponentVel.z / moveSpeed);
            
            // Opponent's push power (1 value)
            sensor.AddObservation(opponentAgent.GetPushPower() / 100f);
            
            // Distance to opponent (1 value)
            float distance = toOpponent.magnitude;
            sensor.AddObservation(distance / platformRadius);
            
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
        
        // Convert actions to movement
        Vector3 forward = robotBody.transform.forward;
        
        // Check for obstacles/collisions
        CheckForObstacles(forward, moveAction);
        
        // Apply movement if not blocked
        if (!blocked && Mathf.Abs(moveAction) > 0.01f)
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
        
        // Apply physics
        if (rb != null)
        {
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
            AddReward(losePenalty);
            EndEpisode();
            return;
        }
        
        // Check if outside platform radius
        float distanceFromCenter = Vector3.Distance(
            new Vector3(robotBody.transform.position.x, arenaCenter.y, robotBody.transform.position.z),
            arenaCenter
        );
        
        if (distanceFromCenter > platformRadius)
        {
            AddReward(losePenalty);
            EndEpisode();
            return;
        }
        
        // Check if opponent fell off (we win)
        if (opponentAgent != null && opponentAgent.robotBody != null)
        {
            if (opponentAgent.robotBody.transform.position.y < fallHeight)
            {
                AddReward(winReward);
                EndEpisode();
                return;
            }
            
            float opponentDistance = Vector3.Distance(
                new Vector3(opponentAgent.robotBody.transform.position.x, arenaCenter.y, opponentAgent.robotBody.transform.position.z),
                arenaCenter
            );
            
            if (opponentDistance > platformRadius)
            {
                AddReward(winReward);
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
}
