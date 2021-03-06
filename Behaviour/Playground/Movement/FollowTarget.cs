using UnityEngine;

/// <summary>
/// Follot target script with look at target option.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FollowTarget : Physics2DObject
{
	[Tooltip("This is the target the object is going to move towards")]
	public Transform target;

	[Header("Movement")]
	[Tooltip("Speed used to move towards the target")]
	public float speed = 1f;

	[Tooltip("Used to decide if the object will look at the target while pursuing it")]
	public bool lookAtTarget = false;

	[Tooltip("The direction that will face the target")]
	public Enums.Directions useSide = Enums.Directions.Up;

	// FixedUpdate is called once per frame
	void FixedUpdate()
	{
		//do nothing if the target hasn't been assigned or it was detroyed for some reason
		if (target == null)
			return;

		//look towards the target
		if (lookAtTarget)
		{
			Utils.SetAxisTowards(useSide, transform, target.position - transform.position);
		}

		//Move towards the target
		rigidbody2D.MovePosition(Vector2.Lerp(transform.position, target.position, Time.fixedDeltaTime * speed));
	}
}
