using UnityEngine;

/// <summary>
/// Manages the robot sumo arena - handles episode resets, win detection, and environment setup.
/// Attach this to an empty GameObject in your scene.
/// </summary>
public class SumoArenaManager : MonoBehaviour
{
    [Header("Agent References")]
    [Tooltip("First robot agent")]
    public RobotAgent robot1Agent;
    
    [Tooltip("Second robot agent")]
    public RobotAgent robot2Agent;
    
    [Header("Arena Setup")]
    [Tooltip("The platform GameObject")]
    public GameObject platform;
    
    [Tooltip("Starting position for robot 1")]
    public Transform robot1StartPosition;
    
    [Tooltip("Starting position for robot 2")]
    public Transform robot2StartPosition;
    
    [Tooltip("Platform radius (for out-of-bounds detection)")]
    public float platformRadius = 10f;
    
    [Tooltip("Fall height (below this = fallen off)")]
    public float fallHeight = -2f;
    
    [Header("Episode Settings")]
    [Tooltip("Maximum episode length in seconds")]
    public float maxEpisodeTime = 60f;
    
    [Tooltip("Automatically reset on draw (time limit reached)")]
    public bool autoResetOnDraw = true;
    
    private float episodeStartTime;
    private bool episodeActive = false;
    
    void Start()
    {
        // Validate setup
        if (robot1Agent == null || robot2Agent == null)
        {
            Debug.LogError("[SumoArenaManager] Both robot agents must be assigned!");
            return;
        }
        
        if (platform == null)
        {
            Debug.LogWarning("[SumoArenaManager] Platform not assigned. Searching for 'Platform' GameObject...");
            platform = GameObject.Find("Platform");
            if (platform == null)
            {
                Debug.LogWarning("[SumoArenaManager] No platform found. Using Vector3.zero as center.");
            }
        }
        
        // Create start positions if not assigned
        if (robot1StartPosition == null)
        {
            GameObject startPos1 = new GameObject("Robot1StartPosition");
            startPos1.transform.parent = transform;
            Vector3 platformCenter = platform != null ? platform.transform.position : Vector3.zero;
            startPos1.transform.position = platformCenter + new Vector3(-3f, 0.5f, 0f);
            startPos1.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // Face right
            robot1StartPosition = startPos1.transform;
            Debug.Log("[SumoArenaManager] Created Robot 1 start position");
        }
        
        if (robot2StartPosition == null)
        {
            GameObject startPos2 = new GameObject("Robot2StartPosition");
            startPos2.transform.parent = transform;
            Vector3 platformCenter = platform != null ? platform.transform.position : Vector3.zero;
            startPos2.transform.position = platformCenter + new Vector3(3f, 0.5f, 0f);
            startPos2.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // Face left
            robot2StartPosition = startPos2.transform;
            Debug.Log("[SumoArenaManager] Created Robot 2 start position");
        }
        
        // Set up agent references
        SetupAgents();
        
        // Start first episode
        ResetEpisode();
    }
    
    void SetupAgents()
    {
        // Make sure agents reference each other
        robot1Agent.opponentAgent = robot2Agent;
        robot2Agent.opponentAgent = robot1Agent;
        
        // Set arena reference
        robot1Agent.arena = platform;
        robot2Agent.arena = platform;
        
        // Set platform radius and fall height
        robot1Agent.platformRadius = platformRadius;
        robot1Agent.fallHeight = fallHeight;
        robot2Agent.platformRadius = platformRadius;
        robot2Agent.fallHeight = fallHeight;
        
        Debug.Log("[SumoArenaManager] Agents configured successfully");
    }
    
    void Update()
    {
        if (!episodeActive) return;
        
        // Check for time limit
        if (Time.time - episodeStartTime > maxEpisodeTime)
        {
            if (autoResetOnDraw)
            {
                Debug.Log("[SumoArenaManager] Episode time limit reached - Draw!");
                
                // Small penalty for both agents for not finishing
                robot1Agent.AddReward(-0.1f);
                robot2Agent.AddReward(-0.1f);
                
                ResetEpisode();
            }
        }
    }
    
    public void ResetEpisode()
    {
        episodeStartTime = Time.time;
        episodeActive = true;
        
        // Reset robot 1
        if (robot1Agent != null && robot1Agent.robotBody != null && robot1StartPosition != null)
        {
            robot1Agent.robotBody.transform.position = robot1StartPosition.position;
            robot1Agent.robotBody.transform.rotation = robot1StartPosition.rotation;
        }
        
        // Reset robot 2
        if (robot2Agent != null && robot2Agent.robotBody != null && robot2StartPosition != null)
        {
            robot2Agent.robotBody.transform.position = robot2StartPosition.position;
            robot2Agent.robotBody.transform.rotation = robot2StartPosition.rotation;
        }
        
        Debug.Log("[SumoArenaManager] Episode reset");
    }
    
    // Helper method to check if position is on platform
    public bool IsOnPlatform(Vector3 position)
    {
        if (platform == null) return true;
        
        Vector3 platformCenter = platform.transform.position;
        float distanceFromCenter = Vector3.Distance(
            new Vector3(position.x, platformCenter.y, position.z),
            platformCenter
        );
        
        return distanceFromCenter <= platformRadius && position.y >= fallHeight;
    }
    
    // Visualize the arena boundaries in editor
    void OnDrawGizmos()
    {
        if (platform != null)
        {
            Vector3 center = platform.transform.position;
            
            // Draw platform circle
            Gizmos.color = Color.green;
            DrawCircle(center, platformRadius, 32);
            
            // Draw fall height plane
            Gizmos.color = Color.red;
            DrawCircle(new Vector3(center.x, fallHeight, center.z), platformRadius + 2f, 32);
        }
        
        // Draw start positions
        if (robot1StartPosition != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(robot1StartPosition.position, 0.5f);
            Gizmos.DrawRay(robot1StartPosition.position, robot1StartPosition.forward * 1.5f);
        }
        
        if (robot2StartPosition != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(robot2StartPosition.position, 0.5f);
            Gizmos.DrawRay(robot2StartPosition.position, robot2StartPosition.forward * 1.5f);
        }
    }
    
    void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 previousPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previousPoint, newPoint);
            previousPoint = newPoint;
        }
    }
}
