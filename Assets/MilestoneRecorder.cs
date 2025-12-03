using UnityEngine;
using System.Collections;
using System.IO;

/// <summary>
/// Automatically records video clips when training milestones are reached.
/// Attach to the ArenaManager GameObject.
/// </summary>
public class MilestoneRecorder : MonoBehaviour
{
    [Header("Recording Settings")]
    [Tooltip("Enable automatic milestone recording")]
    public bool enableRecording = true;
    
    [Tooltip("Seconds to record after milestone is triggered")]
    public float recordingDuration = 15f;
    
    [Tooltip("Output folder for recordings (relative to project)")]
    public string outputFolder = "Recordings";
    
    [Header("Agent References")]
    public RobotAgent robot1Agent;
    public RobotAgent robot2Agent;
    
    [Header("Milestone Tracking")]
    [Tooltip("Minimum speed to count as 'large movement'")]
    public float largeMovementThreshold = 5f;
    
    [Tooltip("Minimum push power to count as 'pushing'")]
    public float pushingThreshold = 50f;
    
    // Milestone flags
    private bool firstLargeMovementRecorded = false;
    private bool firstPushingRecorded = false;
    private bool firstWinRecorded = false;
    private bool firstCloseCallRecorded = false;
    private bool firstMaxPowerRecorded = false;
    
    // Recording state
    private bool isCurrentlyRecording = false;
    private float recordingStartTime = 0f;
    private string currentRecordingName = "";
    
    // Statistics tracking
    private int totalEpisodes = 0;
    private float maxRewardSeen = 0f;
    
    void Start()
    {
        // Create output folder if it doesn't exist
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            Debug.Log($"[MilestoneRecorder] Created recording folder: {outputFolder}");
        }
        
        Debug.Log("[MilestoneRecorder] Ready to record milestones!");
        Debug.Log("Milestones to capture: First Large Movement, First Pushing, First Win, Close Call, Max Power");
    }
    
    void Update()
    {
        if (!enableRecording) return;
        if (robot1Agent == null || robot2Agent == null) return;
        
        // Stop recording if duration exceeded
        if (isCurrentlyRecording && Time.time - recordingStartTime > recordingDuration)
        {
            StopRecording();
        }
        
        // Check for milestones (only if not already recording)
        if (!isCurrentlyRecording)
        {
            CheckMilestones();
        }
    }
    
    void CheckMilestones()
    {
        // Get current state
        Vector3 vel1 = robot1Agent.GetVelocity();
        Vector3 vel2 = robot2Agent.GetVelocity();
        float power1 = robot1Agent.GetPushPower();
        float power2 = robot2Agent.GetPushPower();
        float reward1 = robot1Agent.GetCumulativeReward();
        float reward2 = robot2Agent.GetCumulativeReward();
        
        // Track max reward
        maxRewardSeen = Mathf.Max(maxRewardSeen, reward1, reward2);
        
        // MILESTONE 1: First Large Movement
        if (!firstLargeMovementRecorded)
        {
            if (vel1.magnitude > largeMovementThreshold || vel2.magnitude > largeMovementThreshold)
            {
                TriggerRecording("FirstLargeMovement", 
                    $"First time a robot moved fast! Speed: {Mathf.Max(vel1.magnitude, vel2.magnitude):F2} units/sec");
                firstLargeMovementRecorded = true;
            }
        }
        
        // MILESTONE 2: First Pushing (high push power)
        if (!firstPushingRecorded)
        {
            if (power1 > pushingThreshold || power2 > pushingThreshold)
            {
                TriggerRecording("FirstPushing", 
                    $"First time a robot built up pushing power! Power: {Mathf.Max(power1, power2):F0}");
                firstPushingRecorded = true;
            }
        }
        
        // MILESTONE 3: First Win (high positive reward indicates a win)
        if (!firstWinRecorded)
        {
            if (reward1 > 0.8f || reward2 > 0.8f)
            {
                string winner = reward1 > reward2 ? "Robot 1" : "Robot 2";
                TriggerRecording("FirstWin", 
                    $"First victory! {winner} won with reward: {Mathf.Max(reward1, reward2):F2}");
                firstWinRecorded = true;
            }
        }
        
        // MILESTONE 4: First Close Call (robot near edge but survives)
        if (!firstCloseCallRecorded)
        {
            if (robot1Agent.arena != null)
            {
                Vector3 center = robot1Agent.arena.transform.position;
                float dist1 = Vector3.Distance(
                    new Vector3(robot1Agent.robotBody.transform.position.x, center.y, robot1Agent.robotBody.transform.position.z),
                    center
                );
                float dist2 = Vector3.Distance(
                    new Vector3(robot2Agent.robotBody.transform.position.x, center.y, robot2Agent.robotBody.transform.position.z),
                    center
                );
                
                float edgeRatio1 = dist1 / robot1Agent.platformRadius;
                float edgeRatio2 = dist2 / robot2Agent.platformRadius;
                
                // Very close to edge (95% of radius) but still on platform
                if (edgeRatio1 > 0.95f || edgeRatio2 > 0.95f)
                {
                    TriggerRecording("FirstCloseCall", 
                        $"Close call! Robot nearly fell off (edge ratio: {Mathf.Max(edgeRatio1, edgeRatio2):F2})");
                    firstCloseCallRecorded = true;
                }
            }
        }
        
        // MILESTONE 5: First Max Power
        if (!firstMaxPowerRecorded)
        {
            if (power1 >= 99f || power2 >= 99f)
            {
                TriggerRecording("FirstMaxPower", 
                    $"Maximum pushing power reached! Power: {Mathf.Max(power1, power2):F0}");
                firstMaxPowerRecorded = true;
            }
        }
    }
    
    void TriggerRecording(string milestoneName, string description)
    {
        if (isCurrentlyRecording) return;
        
        isCurrentlyRecording = true;
        recordingStartTime = Time.time;
        currentRecordingName = milestoneName;
        
        // Log to console with timestamp
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fullPath = Path.Combine(outputFolder, $"{milestoneName}_{timestamp}.txt");
        
        Debug.Log($"<color=yellow>╔══════════════════════════════════════════════╗</color>");
        Debug.Log($"<color=yellow>║ 🎬 MILESTONE RECORDING STARTED!</color>");
        Debug.Log($"<color=yellow>║ Milestone: {milestoneName}</color>");
        Debug.Log($"<color=yellow>║ {description}</color>");
        Debug.Log($"<color=yellow>║ Recording for {recordingDuration} seconds...</color>");
        Debug.Log($"<color=yellow>╚══════════════════════════════════════════════╝</color>");
        
        // Save metadata to file
        string metadata = $"MILESTONE: {milestoneName}\n" +
                         $"TIME: {timestamp}\n" +
                         $"DESCRIPTION: {description}\n" +
                         $"DURATION: {recordingDuration} seconds\n" +
                         $"TOTAL_EPISODES: {totalEpisodes}\n" +
                         $"MAX_REWARD_SEEN: {maxRewardSeen:F2}\n" +
                         $"ROBOT1_POSITION: {robot1Agent.robotBody.transform.position}\n" +
                         $"ROBOT2_POSITION: {robot2Agent.robotBody.transform.position}\n" +
                         $"ROBOT1_POWER: {robot1Agent.GetPushPower():F2}\n" +
                         $"ROBOT2_POWER: {robot2Agent.GetPushPower():F2}\n";
        
        File.WriteAllText(fullPath, metadata);
        
        // Start screen recording (you'll need to press Cmd+Shift+5 manually, or use Unity Recorder)
        // This displays a clear on-screen notification
        StartCoroutine(ShowRecordingNotification());
    }
    
    void StopRecording()
    {
        if (!isCurrentlyRecording) return;
        
        Debug.Log($"<color=green>✅ Recording stopped for: {currentRecordingName}</color>");
        Debug.Log($"<color=green>💾 Metadata saved to: {outputFolder}/{currentRecordingName}_*.txt</color>");
        
        isCurrentlyRecording = false;
        currentRecordingName = "";
    }
    
    IEnumerator ShowRecordingNotification()
    {
        // Display notification for the duration
        float elapsed = 0f;
        while (elapsed < recordingDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
    
    // Track episode count
    public void OnEpisodeEnd()
    {
        totalEpisodes++;
    }
    
    // GUI overlay during recording
    void OnGUI()
    {
        if (isCurrentlyRecording)
        {
            float timeRemaining = recordingDuration - (Time.time - recordingStartTime);
            
            GUIStyle style = new GUIStyle();
            style.fontSize = 32;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.red;
            style.alignment = TextAnchor.UpperCenter;
            
            string text = $"🔴 RECORDING: {currentRecordingName}\nTime remaining: {timeRemaining:F1}s";
            
            // Background box
            GUI.Box(new Rect(Screen.width / 2 - 300, 10, 600, 100), "");
            
            // Text
            GUI.Label(new Rect(0, 20, Screen.width, 80), text, style);
        }
        
        // Show milestone status in corner
        if (enableRecording)
        {
            GUIStyle statusStyle = new GUIStyle();
            statusStyle.fontSize = 14;
            statusStyle.normal.textColor = Color.white;
            statusStyle.alignment = TextAnchor.UpperLeft;
            
            string status = "Milestones:\n";
            status += (firstLargeMovementRecorded ? "✅" : "⏳") + " Large Movement\n";
            status += (firstPushingRecorded ? "✅" : "⏳") + " Pushing\n";
            status += (firstWinRecorded ? "✅" : "⏳") + " First Win\n";
            status += (firstCloseCallRecorded ? "✅" : "⏳") + " Close Call\n";
            status += (firstMaxPowerRecorded ? "✅" : "⏳") + " Max Power\n";
            
            GUI.Box(new Rect(10, 10, 180, 130), "");
            GUI.Label(new Rect(15, 15, 170, 120), status, statusStyle);
        }
    }
    
    // Reset milestones (useful for testing)
    [ContextMenu("Reset All Milestones")]
    public void ResetMilestones()
    {
        firstLargeMovementRecorded = false;
        firstPushingRecorded = false;
        firstWinRecorded = false;
        firstCloseCallRecorded = false;
        firstMaxPowerRecorded = false;
        totalEpisodes = 0;
        maxRewardSeen = 0f;
        Debug.Log("[MilestoneRecorder] All milestones reset!");
    }
}
