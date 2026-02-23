using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Most simple kinematic projectile that travels in a straight line.
	/// </summary>
	public class SimpleKinematicProjectile : KinematicProjectile
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private float _damage = 10f;
		[SerializeField]
		private EHitType _hitType = EHitType.Projectile;
		[SerializeField]
		private LayerMask _hitMask;

		// KinematicProjectile INTERFACE

		public override void OnFixedUpdate(ref KinematicData data)
		{
			var runner = Context.Runner;

			var previousPosition = GetMovePosition(runner, ref data, runner.Tick - 1);
			var nextPosition = GetMovePosition(runner, ref data, runner.Tick);

			var direction = nextPosition - previousPosition;
			float distance = direction.magnitude;

			// Normalize
			direction /= distance;

			if (_length > 0f)
			{
				float elapsedDistanceSqr = (previousPosition - data.Position).sqrMagnitude;
				float projectileLength = elapsedDistanceSqr > _length * _length ? _length : Mathf.Sqrt(elapsedDistanceSqr);

				previousPosition -= direction * projectileLength;
				distance += projectileLength;
			}

			if (ProjectileUtility.ProjectileCast(runner, Context.Owner, previousPosition, direction, distance, _hitMask, out LagCompensatedHit hit) == true)
			{
				HitUtility.ProcessHit(Context.Owner, direction, hit, _damage, _hitType);

				data.ImpactPosition = hit.Point;
				data.ImpactNormal = (hit.Normal - direction) * 0.5f;
				data.IsFinished = true;

				SpawnImpact(data.ImpactPosition, data.ImpactNormal);
			}

			base.OnFixedUpdate(ref data);
		}

		protected override Vector3 GetRenderPosition(ref KinematicData data, ref KinematicData fromData, float alpha)
		{
			var runner = Context.Runner;

			float renderTime = Context.Owner == runner.LocalPlayer ? runner.LocalRenderTime : runner.RemoteRenderTime;
			return GetMovePosition(Context.Runner, ref data, renderTime / runner.DeltaTime);
		}

		// PRIVATE METHODS

		private Vector3 GetMovePosition(NetworkRunner runner, ref KinematicData data, float currentTick)
		{
			float time = (currentTick - data.FireTick) * runner.DeltaTime;

			if (time <= 0f)
				return data.Position;

			return data.Position + (Vector3)data.Velocity * time;
		}
	}
}
