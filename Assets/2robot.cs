using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Simple IJKL controller - transform-based movement only.
/// - I/K: move forward/back
/// - J/L: rotate left/right
/// 
/// Attach to the object you want to move, or set Target in inspector.
/// </summary>
public class robot2 : MonoBehaviour
{
	[Tooltip("If set, control this GameObject. If null, control the attached GameObject.")]
	public GameObject Target;

	[Tooltip("If Target is null, find this GameObject by name at Start.")]
	public string TargetName = "a5937a451209_a_square__low__box_shaped_ba_0_glb";

	[Tooltip("Movement speed (units per second)")]
	public float MoveSpeed = 5f;

	[Tooltip("Maximum rotation speed (degrees per second)")]
	public float MaxRotationSpeed = 120f;

	[Tooltip("Rotation acceleration")]
	public float RotationAcceleration = 180f;

	[Tooltip("Rotation friction/deceleration")]
	public float RotationFriction = 150f;

	public enum ForwardAxis
	{
		ZPlus,
		ZMinus,
		XPlus,
		XMinus,
		YPlus,
		YMinus
	}

	[Tooltip("Which local axis is forward")]
	public ForwardAxis Forward = ForwardAxis.ZPlus;

	[Tooltip("Gravity acceleration (units per second squared)")]
	public float GravityForce = 9.8f;

	[Tooltip("Distance to check for ground below")]
	public float GroundCheckDistance = 1.0f;

	[Tooltip("Collision detection distance (extends from object center)")]
	public float CollisionDistance = 0.3f;

	[Tooltip("Radius for collision detection")]
	public float CollisionRadius = 0.3f;

	[Tooltip("Acceleration when pressing movement keys")]
	public float Acceleration = 10f;

	[Tooltip("Friction/deceleration when no input")]
	public float Friction = 15f;

	[Tooltip("Maximum speed")]
	public float MaxSpeed = 15f;

	[Tooltip("Push power gain rate per second")]
	public float PushPowerGainRate = 50f;

	[Tooltip("Push power decay rate (multiplied by current power)")]
	public float PushPowerDecayRate = 2f;

	private float pushPower = 0f;
	private float verticalVelocity = 0f;
	private Vector3 horizontalVelocity = Vector3.zero;
	private float rotationVelocity = 0f;
	private Rigidbody targetRb;
	private Vector3 pendingPush = Vector3.zero;
	private bool isBeingPushed = false;
	private bool blocked = false;

	void Start()
	{
		if (Target == null && !string.IsNullOrEmpty(TargetName))
		{
			Target = GameObject.Find(TargetName);
		}
		if (Target == null)
		{
			Target = gameObject;
		}
		Debug.Log($"[robot2] Target: {Target.name}");
		
		// Ensure target has a Rigidbody - NON-kinematic so Unity handles collisions
		Rigidbody rb = Target.GetComponent<Rigidbody>();
		if (rb == null)
		{
			rb = Target.AddComponent<Rigidbody>();
		}
		rb.isKinematic = true;
		rb.useGravity = false; // Manual gravity in FixedUpdate
		rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
		rb.linearDamping = 0f;
		rb.mass = 10f;
		rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
		rb.interpolation = RigidbodyInterpolation.Interpolate;
		targetRb = rb;
		
		// Ensure target has a collider
		if (Target.GetComponent<Collider>() == null)
		{
			Target.AddComponent<BoxCollider>();
			Debug.Log($"[robot2] Added BoxCollider to {Target.name}");
		}
	}

	void Update()
	{
		if (Target == null) return;

		// Get IJKL input
		bool i = false, k = false, j = false, l = false;

#if ENABLE_INPUT_SYSTEM
		var kb = Keyboard.current;
		if (kb != null)
		{
			i = kb.iKey.isPressed;
			k = kb.kKey.isPressed;
			j = kb.jKey.isPressed;
			l = kb.lKey.isPressed;
		}
#else
		i = Input.GetKey(KeyCode.I);
		k = Input.GetKey(KeyCode.K);
		j = Input.GetKey(KeyCode.J);
		l = Input.GetKey(KeyCode.L);
#endif

	// Get forward direction for this frame
	Vector3 forward = GetForwardVector();

	// Check for obstacles ahead
	// Don't reset blocked if we were pushed - preserve SetBlocked state
	if (!isBeingPushed)
	{
		blocked = false;
	}
	{
		// Determine movement direction: prefer input, otherwise use current velocity direction
		float move;
		if (i || k) move = i ? 1f : -1f;
		else move = Vector3.Dot(horizontalVelocity, forward) >= 0f ? 1f : -1f;
		Vector3 checkDir = forward * move;
		
		// Consider this robot "active" for pushing if player is pressing keys or it's already moving
		bool activeMover = (i || k) || horizontalVelocity.magnitude > 0.01f;
		
		// Check from current position
		Collider[] hits = Physics.OverlapSphere(Target.transform.position, 0.6f);
		foreach (Collider col in hits)
		{
			// Ignore triggers and our own collider (including children)
			if (col.isTrigger) continue;
			
			// Check if this collider belongs to our own robot - check multiple ways
			bool isSelf = col.gameObject == Target || 
			              col.gameObject == gameObject ||
			              col.transform.IsChildOf(Target.transform) || 
			              Target.transform.IsChildOf(col.transform) ||
			              col.transform.IsChildOf(transform) ||
			              transform.IsChildOf(col.transform);
			
			if (!isSelf)
			{
				// Ignore floor - don't block on colliders below us
				if (col.bounds.max.y < Target.transform.position.y - 0.1f)
				{
					continue;
				}
				
				// Make sure the obstacle is actually in front, not just nearby
				Vector3 toObstacle = col.transform.position - Target.transform.position;
				float distance = toObstacle.magnitude;
				float dot = Vector3.Dot(toObstacle.normalized, checkDir.normalized);
				
			// Only block if obstacle is in the forward direction (dot > 0.5 means within ~60 degrees)
			if (dot > 0.7f && distance < 0.5f)
			{
				// Check if it's another robot with a movement script
				WASDMovement otherWASD = col.GetComponent<WASDMovement>();
				robot2 otherRobot2 = col.GetComponent<robot2>();
				
				if (otherWASD != null || otherRobot2 != null)
				{
					// Get other robot's push power
					float otherPower = otherWASD != null ? otherWASD.GetPushPower() : otherRobot2.GetPushPower();
					
			// Compare push powers
			if (pushPower > otherPower) // Any power advantage pushes
			{
				Debug.Log($"[2ROBOT] Pushing! myPower={pushPower:F1} otherPower={otherPower:F1} distance={distance:F2} myVel={horizontalVelocity.magnitude:F2}");
				// We're stronger - push them back continuously
				float pushStrength = (pushPower - otherPower) / 100f; // 0 to 1
				Vector3 pushDirection = checkDir; // Push them in our direction of movement
				// Push them at our current speed (not faster)
				float pushSpeed = horizontalVelocity.magnitude;
				Vector3 targetPushVelocity = pushDirection.normalized * pushSpeed;
				if (activeMover)
				{
					if (otherWASD != null)
					{
						otherWASD.SetVelocity(targetPushVelocity);
						otherWASD.SetBlocked(true);
					}
					else
					{
						otherRobot2.SetVelocity(targetPushVelocity);
						otherRobot2.SetBlocked(true);
					}
				}
				else
				{
					// Not actively moving - enforce block on the other to prevent passive overlap
					if (otherWASD != null)
						otherWASD.SetBlocked(true);
					else if (otherRobot2 != null)
						otherRobot2.SetBlocked(true);
					blocked = true;
				}
				// Immediately enforce minimum separation to avoid pass-through (independent of Update ordering)
				{
					float minSeparation = 0.4f;
					if (distance < minSeparation)
					{
						float pushAway = (minSeparation - distance) + 0.01f;
						Vector3 sep = pushDirection.normalized * pushAway;
						if (otherWASD != null && otherWASD.Target != null)
						{
							otherWASD.Target.transform.position += sep;
						}
						else if (otherRobot2 != null && otherRobot2.Target != null)
						{
							otherRobot2.Target.transform.position += sep;
						}
					}
				}
			}
			else
			{
				// They're stronger or equal - we get blocked
				Debug.Log($"[2ROBOT] Blocked! myPower={pushPower:F1} otherPower={otherPower:F1} dist={distance:F2}");
				blocked = true;
			}
			}
			else
				{
					// It's a wall or obstacle - always block
					blocked = true;
				}
				break;
			}
			}
		}
	}

	// Apply acceleration/deceleration to horizontal velocity
	if ((i || k) && !blocked && !isBeingPushed)
	{
		float move = i ? 1f : -1f;
		Vector3 inputDir = forward * move;
		
		// Gain push power when moving forward
		if (i)
		{
			pushPower = Mathf.Min(100f, pushPower + PushPowerGainRate * Time.deltaTime);
		}
		else
		{
			// Decay when moving backward
			pushPower = Mathf.Max(0f, pushPower - pushPower * PushPowerDecayRate * Time.deltaTime);
		}
		
		// Accelerate toward input direction
		horizontalVelocity = Vector3.Lerp(horizontalVelocity, inputDir * MaxSpeed, Acceleration * Time.deltaTime);
	}
		else
		{
		// Decay push power when not pressing forward
		pushPower = Mathf.Max(0f, pushPower - pushPower * PushPowerDecayRate * Time.deltaTime);
		// Apply friction or stop when blocked
		if (blocked)
		{
			horizontalVelocity = Vector3.zero;
		}
		else
		{
			horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, Friction * Time.deltaTime);
		}
	}
	
	// Zero very small velocities to prevent drift
	if (horizontalVelocity.magnitude < 0.01f)
	{
		horizontalVelocity = Vector3.zero;
	}
	
	// Check if we're too close to another robot and moving towards it
	// Only apply this when not actively pushing or being pushed
	if (!isBeingPushed && !(i || k))
	{
		Collider[] nearbyHits = Physics.OverlapSphere(Target.transform.position, 0.6f);
		foreach (Collider col in nearbyHits)
		{
			if (col.isTrigger) continue;
			bool isSelf = col.gameObject == Target || col.gameObject == gameObject ||
			              col.transform.IsChildOf(Target.transform) || Target.transform.IsChildOf(col.transform);
			if (isSelf) continue;
			
			WASDMovement otherWASD = col.GetComponent<WASDMovement>();
			robot2 otherRobot2 = col.GetComponent<robot2>();
			
			if (otherWASD != null || otherRobot2 != null)
			{
				Vector3 toOther = col.transform.position - Target.transform.position;
				float dist = toOther.magnitude;
				
				// If very close and velocity would move us closer, stop
				if (dist < 0.5f && Vector3.Dot(horizontalVelocity.normalized, toOther.normalized) > 0)
				{
					horizontalVelocity = Vector3.zero;
					break;
				}
			}
		}
	}
	
	// If was being pushed last frame but not moving, zero velocity
	if (isBeingPushed && blocked)
	{
		horizontalVelocity = Vector3.zero;
	}
	
	// Reset flags at end of frame
	// CRITICAL: Only reset blocked if we weren't just pushed this frame
	// Otherwise SetBlocked(true) from another robot gets immediately cleared
	if (!isBeingPushed)
	{
		blocked = false;
	}
	isBeingPushed = false;	// Check if on ground
	bool isOnGround = CheckGroundBelow();		// Apply gravity (downward) - but stop at ground
		if (isOnGround)
		{
			if (verticalVelocity < 0)
				verticalVelocity = 0; // Stop falling when on ground
		}
		else
		{
			verticalVelocity -= GravityForce * Time.deltaTime;
		}

		// Rotation
		if (j || l)
		{
			float turn = j ? -1f : 1f;
			// Accelerate rotation
			rotationVelocity = Mathf.Lerp(rotationVelocity, turn * MaxRotationSpeed, RotationAcceleration * Time.deltaTime);
		}
		else
		{
			// Apply rotation friction
			rotationVelocity = Mathf.Lerp(rotationVelocity, 0f, RotationFriction * Time.deltaTime);
		}
	}

	void FixedUpdate()
	{
		// Apply movement as velocity to rigidbody
		if (targetRb != null)
		{
			// Apply gravity
			Vector3 verticalMovement = Vector3.up * verticalVelocity * Time.fixedDeltaTime;
			
			// Use MovePosition for kinematic rigidbody
			Vector3 newPos = Target.transform.position + horizontalVelocity * Time.fixedDeltaTime + verticalMovement;
			targetRb.MovePosition(newPos);
			
			// Apply rotation
			Quaternion deltaRot = Quaternion.Euler(0f, rotationVelocity * Time.fixedDeltaTime, 0f);
			targetRb.MoveRotation(targetRb.rotation * deltaRot);
		}
	}

	bool CheckGroundBelow()
	{
		Vector3 rayStart = Target.transform.position;
		Vector3 rayDirection = Vector3.down;

		// Cast a ray downward
		if (Physics.Raycast(rayStart, rayDirection, GroundCheckDistance))
		{
			return true;
		}
		return false;
	}

	bool CheckGroundAtPosition(Vector3 position)
	{
		if (Physics.Raycast(position, Vector3.down, GroundCheckDistance))
		{
			return true;
		}
		return false;
	}

	Vector3 SnapToGround()
	{
		Vector3 pos = Target.transform.position;
		RaycastHit hit;
		
		if (Physics.Raycast(pos, Vector3.down, out hit, GroundCheckDistance))
		{
			// Move object to just above the surface
			pos.y = hit.point.y;
		}
		
		return pos;
	}

	Vector3 GetForwardVector()
	{
		switch (Forward)
		{
			case ForwardAxis.ZPlus: return Target.transform.forward;
			case ForwardAxis.ZMinus: return -Target.transform.forward;
			case ForwardAxis.XPlus: return Target.transform.right;
			case ForwardAxis.XMinus: return -Target.transform.right;
			case ForwardAxis.YPlus: return Target.transform.up;
			case ForwardAxis.YMinus: return -Target.transform.up;
			default: return Target.transform.forward;
		}
	}

	public float GetPushPower()
	{
		return pushPower;
	}

	public void ZeroVelocity()
	{
		// Stop all movement to prevent bumping back
		horizontalVelocity = Vector3.zero;
		pendingPush = Vector3.zero;
	}

	public void SetVelocity(Vector3 velocity)
	{
		// Set velocity - will be applied in next FixedUpdate
		horizontalVelocity = velocity;
		isBeingPushed = true; // Mark that we're being pushed
	}

	public Vector3 GetVelocity()
	{
		return horizontalVelocity;
	}

	public void SetBlocked(bool block)
	{
		blocked = block;
	}

	public void ApplyPushForce(Vector3 direction, float speed)
	{
		// Directly set velocity - no pending, immediate application
		horizontalVelocity = direction.normalized * speed;
	}

	void OnGUI()
	{
		if (Target != null && Camera.main != null)
		{
			// Convert 3D position to screen position
			Vector3 screenPos = Camera.main.WorldToScreenPoint(Target.transform.position + Vector3.up * 2f);
			
			if (screenPos.z > 0) // Only draw if in front of camera
			{
				// Flip Y coordinate (GUI uses top-left origin)
				screenPos.y = Screen.height - screenPos.y;
				
				// Create label style
				GUIStyle style = new GUIStyle();
				style.fontSize = 24;
				style.fontStyle = FontStyle.Bold;
				style.normal.textColor = Color.cyan;
				style.alignment = TextAnchor.MiddleCenter;
				
				// Draw push power
				GUI.Label(new Rect(screenPos.x - 50, screenPos.y - 15, 100, 30), ((int)pushPower).ToString(), style);
			}
		}
	}
}