using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Simple WASD controller - transform-based movement only.
/// - W/S: move forward/back
/// - A/D: rotate left/right
/// 
/// Attach to the object you want to move, or set Target in inspector.
/// </summary>
public class WASDMovement : MonoBehaviour
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
	public float CollisionDistance = 0.15f;

	[Tooltip("Radius for collision detection")]
	public float CollisionRadius = 0.15f;

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
		Debug.Log($"[WASDMovement] Target: {Target.name}");
		
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
			Debug.Log($"[WASDMovement] Added BoxCollider to {Target.name}");
		}
	}

	void Update()
	{
		if (Target == null) return;

		// Get WASD input
		bool w = false, s = false, a = false, d = false;

#if ENABLE_INPUT_SYSTEM
		var kb = Keyboard.current;
		if (kb != null)
		{
			w = kb.wKey.isPressed;
			s = kb.sKey.isPressed;
			a = kb.aKey.isPressed;
			d = kb.dKey.isPressed;
		}
#else
		w = Input.GetKey(KeyCode.W);
		s = Input.GetKey(KeyCode.S);
		a = Input.GetKey(KeyCode.A);
		d = Input.GetKey(KeyCode.D);
#endif

		// Get forward direction for this frame
		Vector3 forward = GetForwardVector();

	// Check for obstacles ahead
	bool blocked = false;
	if (w || s)
	{
		float move = w ? 1f : -1f;
		Vector3 checkDir = forward * move;
		
		// Check from current position
		Collider[] hits = Physics.OverlapSphere(Target.transform.position, 1.0f);			foreach (Collider col in hits)
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
				// Ignore floor - don't block on colliders below us
				if (col.bounds.max.y < Target.transform.position.y - 0.1f)
				{
					continue;
				}
				
				// Check actual distance and direction
				Vector3 toObstacle = col.transform.position - Target.transform.position;
				float distance = toObstacle.magnitude;
				float dot = Vector3.Dot(toObstacle.normalized, checkDir.normalized);
				
			// Only block if obstacle is in front AND very close
			if (dot > 0.7f && distance < 0.4f)
			{
				// Check if it's another robot with a movement script
				WASDMovement otherWASD = col.GetComponent<WASDMovement>();
				robot2 otherRobot2 = col.GetComponent<robot2>();
				
				if (otherWASD != null || otherRobot2 != null)
				{
					// Get other robot's push power
					float otherPower = otherWASD != null ? otherWASD.GetPushPower() : otherRobot2.GetPushPower();
					
					// Compare push powers
					if (pushPower > otherPower + 10f) // Need at least 10 power advantage
					{
					// We're stronger - push them back continuously
					float pushStrength = (pushPower - otherPower) / 100f; // 0 to 1
					Vector3 pushDirection = checkDir; // Push them in our direction of movement
					
					if (otherWASD != null)
						otherWASD.ApplyPushForce(pushDirection, MaxSpeed * pushStrength * 8f);
					else
						otherRobot2.ApplyPushForce(pushDirection, MaxSpeed * pushStrength * 8f);						// Block to prevent pass-through
						blocked = true;
					}
					else
					{
						// They're stronger or equal - we get blocked
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
		if ((w || s) && !blocked)
		{
			float move = w ? 1f : -1f;
			Vector3 inputDir = forward * move;
			
			// Gain push power when moving forward
			if (w)
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
			// Decay push power when not pressing forward (faster decay based on current power)
			pushPower = Mathf.Max(0f, pushPower - pushPower * PushPowerDecayRate * Time.deltaTime);
			// Stop when blocked to prevent merging, otherwise apply friction
			if (blocked)
			{
				horizontalVelocity = Vector3.zero;
			}
			else
			{
				horizontalVelocity = Vector3.Lerp(horizontalVelocity, Vector3.zero, Friction * Time.deltaTime);
			}
		}

		// Check if on ground
		bool isOnGround = CheckGroundBelow();

		// Apply gravity (downward) - but stop at ground
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
		if (a || d)
		{
			float turn = a ? -1f : 1f;
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
		// Apply pending push first (overrides blocked movement)
		if (pendingPush != Vector3.zero)
		{
			// Apply push more directly for harder pushing
			horizontalVelocity = Vector3.Lerp(horizontalVelocity, pendingPush, 0.8f);
			pendingPush = Vector3.zero; // Clear after applying
		}
		
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

	public void ApplyPushForce(Vector3 direction, float speed)
	{
		// Store push to apply in FixedUpdate
		pendingPush = direction.normalized * speed;
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
				style.normal.textColor = Color.yellow;
				style.alignment = TextAnchor.MiddleCenter;
				
				// Draw push power
				GUI.Label(new Rect(screenPos.x - 50, screenPos.y - 15, 100, 30), ((int)pushPower).ToString(), style);
			}
		}
	}
}