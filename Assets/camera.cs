using UnityEngine;

public class camera : MonoBehaviour
{
	[Tooltip("First GameObject to follow")]
	public GameObject Target1;

	[Tooltip("Second GameObject to follow")]
	public GameObject Target2;

	[Tooltip("If Target1 is null, find this GameObject by name at Start")]
	public string TargetName1 = "a5937a451209_a_square__low__box_shaped_ba_0_glb";

	[Tooltip("If Target2 is null, find this GameObject by name at Start")]
	public string TargetName2 = "";

	[Tooltip("Offset from target position")]
	public Vector3 Offset = new Vector3(0, 3, -5);

	[Tooltip("Smoothing speed (higher = faster follow)")]
	public float SmoothSpeed = 5f;

	[Tooltip("Minimum distance to keep both objects in frame")]
	public float MinFrameDistance = 5f;

	void Start()
	{
		if (Target1 == null && !string.IsNullOrEmpty(TargetName1))
		{
			Target1 = GameObject.Find(TargetName1);
		}
		if (Target1 == null)
		{
			Debug.LogError("[camera] Target1 not found!");
		}

		if (Target2 == null && !string.IsNullOrEmpty(TargetName2))
		{
			Target2 = GameObject.Find(TargetName2);
		}
	}

	void LateUpdate()
	{
		if (Target1 == null) return;

		// Calculate focus point
		Vector3 focusPoint;
		if (Target2 != null)
		{
			// Both targets exist - focus on midpoint
			focusPoint = (Target1.transform.position + Target2.transform.position) * 0.5f;
		}
		else
		{
			// Only one target
			focusPoint = Target1.transform.position;
		}

		// Calculate distance between targets to adjust camera distance
		float targetDistance = 0f;
		if (Target2 != null)
		{
			targetDistance = Vector3.Distance(Target1.transform.position, Target2.transform.position);
		}

		// Adjust offset distance based on target separation
		Vector3 adjustedOffset = Offset;
		if (targetDistance > MinFrameDistance)
		{
			float scale = targetDistance / MinFrameDistance;
			adjustedOffset *= scale;
		}

		// Position camera behind and above the focus point
		Vector3 desiredPosition = focusPoint + adjustedOffset;

		// Smoothly move camera to desired position
		Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, SmoothSpeed * Time.deltaTime);
		transform.position = smoothedPosition;

		// Look at focus point
		transform.LookAt(focusPoint + Vector3.up * 0.5f);
	}
}
