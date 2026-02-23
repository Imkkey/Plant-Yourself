using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// A projectile that mimics a spray behaviour. To achieve this effect, the projectile can slow down, rise up or down,
	/// or change its diameter over time. When fired with higher dispersion, this projectile can represent the spray-like
	/// behavior of various weapons such as flamethrowers, acid guns, healing sprays, icing guns, and similar devices.
	/// </summary>
	public class SprayProjectile : KinematicProjectile
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private AnimationCurve _damageOverLifetime;
		[SerializeField]
		private AnimationCurve _radiusOverLifetime;
		[SerializeField]
		private EHitType _hitType = EHitType.Projectile;
		[SerializeField]
		private LayerMask _hitMask;
		[SerializeField]
		private float _gravity = 20f;
		[SerializeField]
		private float _drag = 1f;

		// KinematicProjectile INTERFACE

		public override void OnFixedUpdate(ref KinematicData data)
		{
			var runner = Context.Runner;

			var previousPosition = GetMovePosition(ref data, runner.Tick - 1, runner.DeltaTime);
			var nextPosition = GetMovePosition(ref data, runner.Tick, runner.DeltaTime);

			var direction = nextPosition - previousPosition;
			float distance = direction.magnitude;

			if (distance <= 0f)
				return;

			// Normalize
			direction /= distance;

			if (_length > 0f)
			{
				float elapsedDistanceSqr = (previousPosition - data.Position).sqrMagnitude;
				float projectileLength = elapsedDistanceSqr > _length * _length ? _length : Mathf.Sqrt(elapsedDistanceSqr);

				previousPosition -= direction * projectileLength;
				distance += projectileLength;
			}

			int elapsedTicks = runner.Tick - data.FireTick;
			float lifetimeProgress = elapsedTicks / (float)_lifetimeTicks;

			float radius = _radiusOverLifetime.Evaluate(lifetimeProgress);

			if (ProjectileUtility.CircleCast(runner, Context.Owner, previousPosition, direction, distance, radius, 5, _hitMask, out LagCompensatedHit hit) == true)
			{
				float damage = _damageOverLifetime.Evaluate(lifetimeProgress);
				HitUtility.ProcessHit(Context.Owner, direction, hit, damage, _hitType);

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
			return GetMovePosition(ref data, renderTime / runner.DeltaTime, runner.DeltaTime);
		}

		public override void Render(ref KinematicData data, ref KinematicData dataFrom, float alpha)
		{
			base.Render(ref data, ref dataFrom, alpha);

			// Only fire velocity is used for rotating projectile visuals as including also move velocity
			// (data.InheritedVelocity) would cause particles to rotate to one side when firing while strafing
			transform.rotation = Quaternion.LookRotation(data.Velocity);
		}

		// PRIVATE METHODS

		private Vector3 GetMovePosition(ref KinematicData data, float currentTick, float deltaTime)
		{
			float time = (currentTick - data.FireTick) * deltaTime;

			if (time <= 0f)
				return data.Position;

			var velocity = data.Velocity + data.Spray.InheritedVelocity;
			return data.Position + velocity * time - velocity * _drag * time * time * 0.5f + new Vector3(0f, -_gravity, 0f) * time * time * 0.5f;
		}
	}
}
