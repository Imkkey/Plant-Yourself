using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Projectile that can move towards its target. It supports more advanced features like target movement prediction
	/// and targeting target's ground position instead of body (rockets should try to hit ground beneath the target).
	/// HomingProjectile needs to update its position in KinematicData and is therefore
	/// the least network efficient projectile type in the sample.
	/// </summary>
	public class HomingProjectile : KinematicProjectile
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private float _damage = 10f;
		[SerializeField]
		private EHomingPosition _homingPosition = EHomingPosition.Body;
		[SerializeField]
		private EHitType _hitType = EHitType.Projectile;
		[SerializeField]
		private LayerMask _hitMask;
		[SerializeField]
		private LayerMask _environmentCheckMask;
		[SerializeField]
		private float _maxSeekDistance = 50f;
		[SerializeField, Tooltip("Specifies max angle between projectile forward and direction to target. "
		                         + "If exceeded, projectile will continue forward or look for other targets.")]
		private float _maxAngleToTarget = 25f;
		[SerializeField]
		private float _distanceWeight = 1f;
		[SerializeField]
		private float _angleWeight = 1f;
		[SerializeField]
		private float _turnSpeed = 8f;
		[SerializeField, Tooltip("0 = Never recalculate")]
		private float _recalculateTargetAfterTime = 0f;
		[SerializeField, Range(0f, 1f)]
		private float _predictTargetPosition = 0f;

		// KinematicProjectile INTERFACE

		public override KinematicData GetFireData(Vector3 firePosition, Vector3 fireDirection)
		{
			var data = base.GetFireData(firePosition, fireDirection);

			data.Velocity = fireDirection; // Homing projectiles use Velocity as direction
			data.Homing.Target = FindTarget(firePosition, fireDirection);

			return data;
		}

		public override void OnFixedUpdate(ref KinematicData data)
		{
			var previousPosition = data.Position;
			var nextPosition = data.Position + (Vector3)data.Velocity * _startSpeed * Context.Runner.DeltaTime;

			var direction = nextPosition - previousPosition;
			float distance = direction.magnitude;

			// Normalize
			direction /= distance;

			if (ProjectileUtility.ProjectileCast(Context.Runner, Context.Owner, previousPosition, direction, distance, _hitMask, out LagCompensatedHit hit) == true)
			{
				HitUtility.ProcessHit(Context.Owner, direction, hit, _damage, _hitType);

				data.Position = hit.Point;
				data.ImpactPosition = hit.Point;
				data.ImpactNormal = (hit.Normal - direction) * 0.5f;
				data.IsFinished = true;

				SpawnImpact(data.ImpactPosition, data.ImpactNormal);
			}
			else
			{
				TryRecalculateTarget(ref data, nextPosition, direction);
				UpdateDirection(ref data);

				data.Position = nextPosition;
			}

			base.OnFixedUpdate(ref data);
		}

		protected override Vector3 GetRenderPosition(ref KinematicData data, ref KinematicData fromData, float alpha)
		{
			return Vector3.Lerp(fromData.Position, data.Position, alpha);
		}

		// PRIVATE MEMBERS

		private NetworkId FindTarget(Vector3 firePosition, Vector3 fireDirection)
		{
			var targets = ListPool.Get<IHitTarget>(64);

			HitUtility.GetAllTargets(Context.Runner, targets);

			float bestValue = float.MinValue;
			IHitTarget bestTarget = default;

			float maxSqrDistance = _maxSeekDistance * _maxSeekDistance;
			float minDot = Mathf.Cos(_maxAngleToTarget * Mathf.Deg2Rad);

			var physicsScene = Context.Runner.GetPhysicsScene();

			for (int i = 0; i < targets.Count; i++)
			{
				var target = targets[i];
				var targetPosition = GetTargetPosition(target);

				var direction = targetPosition - firePosition;
				if (direction.sqrMagnitude > maxSqrDistance)
					continue;

				float distance = direction.magnitude;
				direction /= distance; // Normalize

				float dot = Vector3.Dot(fireDirection, direction);

				if (dot < minDot)
					continue;

				if (physicsScene.Raycast(firePosition, direction, distance, _environmentCheckMask) == true)
					continue; // View to the target is obstructed

				float value = dot * 90f * _angleWeight + distance * -_distanceWeight;

				if (value > bestValue)
				{
					bestValue = value;
					bestTarget = target;
				}
			}

			ListPool.Return(targets);

			return bestTarget is NetworkBehaviour behaviour ? behaviour.Object.Id : default;
		}

		private void TryRecalculateTarget(ref KinematicData data, Vector3 position, Vector3 direction)
		{
			if (_recalculateTargetAfterTime <= 0f)
				return;

			int recalculateTicks = Mathf.RoundToInt(_recalculateTargetAfterTime * Context.Runner.TickRate);
			int elapsedTicks = Context.Runner.Tick - data.FireTick;

			if (elapsedTicks % recalculateTicks == 0)
			{
				data.Homing.Target = FindTarget(position, direction);
			}
		}

		private void UpdateDirection(ref KinematicData data)
		{
			var targetObject = data.Homing.Target.IsValid == true ? Context.Runner.FindObject(data.Homing.Target) : null;
			if (targetObject == null)
				return; // No target, continue in current direction

			var target = targetObject.GetComponent<IHitTarget>();
			if (target.IsActive == false)
			{
				// Target is no longer active (= dead), forget this target
				// and continue in current direction
				data.Homing.Target = default;
				data.Homing.TargetPosition = default;
				return;
			}

			var targetPosition = GetTargetPosition(target);

			var newDirection = (targetPosition - data.Position);
			float distance = newDirection.magnitude;

			newDirection /= distance; // Normalize

			float minDot = Mathf.Cos(_maxAngleToTarget * Mathf.Deg2Rad);

			if (Vector3.Dot(data.Velocity, newDirection) < minDot)
			{
				// Forget this target
				data.Homing.Target = default;
				data.Homing.TargetPosition = default;
				return;
			}

			if (_predictTargetPosition > 0f)
			{
				var previousTargetPosition = (Vector3)data.Homing.TargetPosition;
				data.Homing.TargetPosition = targetPosition;

				if (previousTargetPosition != Vector3.zero)
				{
					var targetVelocity = (targetPosition - previousTargetPosition) * Context.Runner.TickRate;
					float timeToTarget = distance / _startSpeed;

					var predictedTargetPosition = targetPosition + (targetVelocity * timeToTarget * _predictTargetPosition);
					newDirection = (predictedTargetPosition - data.Position).normalized;
				}
			}

			float deltaTime = Context.Runner.DeltaTime;
			data.Velocity = (data.Velocity + newDirection * deltaTime * _turnSpeed).normalized;
		}

		private Vector3 GetTargetPosition(IHitTarget target)
		{
			// For predicted targets without lag compensated hitbox we simply aim for target position on both server and clients
			if (target.BodyHitbox == null)
			{
				switch (_homingPosition)
				{
					case EHomingPosition.Head:
						return target.HeadPivot.position;
					case EHomingPosition.Ground:
						return target.GroundPivot.position;
					default:
						return target.BodyPivot.position;
				}
			}

			// When target is lag compensated (e.g. players and lag comp. moving targets) we use lag compensation position.
			// This ensures the same target position will be used on both local player client and server.
			var positionOffset = Vector3.zero;
			switch (_homingPosition)
			{
				case EHomingPosition.Head:
					positionOffset = target.HeadPivot.position - target.BodyHitbox.Position;
					break;
				case EHomingPosition.Ground:
					positionOffset = target.GroundPivot.position - target.BodyHitbox.Position;
					break;
				default:
					positionOffset = target.BodyPivot.position - target.BodyHitbox.Position;
					break;
			}

			Context.Runner.LagCompensation.PositionRotation(target.BodyHitbox, Context.Owner, out var compensatedPosition, out _, true);
			return compensatedPosition + positionOffset;
		}

		// HELPERS

		public enum EHomingPosition
		{
			Body = 1,
			Head,
			Ground,
		}
	}
}
