using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Kinematic projectile that can fall with gravity or bounce in the environment.
	/// </summary>
	public class AdvancedKinematicProjectile : KinematicProjectile
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private float _damage = 10f;
		[SerializeField]
		private EHitType _hitType = EHitType.Projectile;
		[SerializeField]
		private LayerMask _hitMask;
		[SerializeField]
		private float _gravity = 20f;
		[SerializeField]
		private bool _spawnImpactObjectOnTimeout;

		[Header("Bounce")]
		[SerializeField]
		private bool _canBounce = false;
		[SerializeField]
		private LayerMask _bounceMask;
		[SerializeField]
		private float _bounceObjectRadius = 0.1f;
		[SerializeField]
		private float _bounceVelocityMultiplierStart = 0.5f;
		[SerializeField]
		private float _bounceVelocityMultiplierEnd = 0.8f;
		[SerializeField, Tooltip("Number of bounces between velocity multiplier start and end")]
		private int _bounceVelocityScale = 8;
		[SerializeField]
		private float _stopSpeed = 2f;
		[SerializeField]
		private AudioEffect _bounceSound;

		private float _maxBounceVolume;
		private int _visibleBounceCount;

		// KinematicProjectile INTERFACE

		public override void OnFixedUpdate(ref KinematicData data)
		{
			base.OnFixedUpdate(ref data);

			var runner = Context.Runner;

			if (data.IsFinished == true && _spawnImpactObjectOnTimeout == true)
			{
				var position = data.HasStopped == true ? (Vector3)data.ImpactPosition : GetMovePosition(ref data, runner.Tick, runner.DeltaTime);
				SpawnImpact(position, Vector3.up);
			}

			if (data.IsFinished == true || data.HasStopped == true)
				return;

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

			// Ignore self hit but only at the start of projectile lifetime
			// (bouncing projectiles can still hurt the owner)
			bool ignoreInputAuthority = runner.Tick < data.FireTick + 10;

			if (ProjectileUtility.ProjectileCast(runner, Context.Owner, previousPosition - direction * _bounceObjectRadius,
				    direction, distance + 2 * _bounceObjectRadius, _hitMask, out LagCompensatedHit hit, ignoreInputAuthority) == true)
			{
				bool doBounce = _canBounce;

				if (_canBounce == true && hit.GameObject != null)
				{
					// Check bounce layer
					int hitLayer = hit.GameObject.layer;
					doBounce = ((1 << hitLayer) & _bounceMask) != 0;
				}

				if (doBounce == true)
				{
					ProcessBounce(ref data, hit, direction, distance);
				}
				else
				{
					HitUtility.ProcessHit(Context.Owner, direction, hit, _damage, _hitType);

					data.ImpactPosition = hit.Point;
					data.ImpactNormal = (hit.Normal - direction) * 0.5f;
					data.IsFinished = true;

					SpawnImpact(data.ImpactPosition, data.ImpactNormal);
				}
			}
		}

		public override void Activate(ref KinematicData data)
		{
			base.Activate(ref data);

			// Sync visible bounces
			_visibleBounceCount = _canBounce == true ? data.Advanced.BounceCount : 0;
		}

		public override void Render(ref KinematicData data, ref KinematicData fromData, float alpha)
		{
			base.Render(ref data, ref fromData, alpha);

			if (_canBounce == true && data.Advanced.BounceCount != _visibleBounceCount)
			{
				OnBounceRender(ref data);
				_visibleBounceCount = data.Advanced.BounceCount;
			}
		}

		protected override Vector3 GetRenderPosition(ref KinematicData data, ref KinematicData fromData, float alpha)
		{
			var runner = Context.Runner;

			float renderTime = Context.Owner == runner.LocalPlayer ? runner.LocalRenderTime : runner.RemoteRenderTime;
			float floatTick = renderTime / runner.DeltaTime;

			// If projectile has stopped return finished position but not until we are at the stop time (StartTick acts as stop tick here)
			if (data.HasStopped == true && data.Advanced.MoveStartTick <= floatTick)
				return data.ImpactPosition;

			// Choose correct data (matters mainly for bouncing as values are changing after bounce)
			ref var moveData = ref floatTick < data.Advanced.MoveStartTick ? ref fromData : ref data;
			return GetMovePosition(ref moveData, floatTick, runner.DeltaTime);
		}

		// MONOBEHAVIOUR

		protected override void Awake()
		{
			base.Awake();

			_maxBounceVolume = _bounceSound != null ? _bounceSound.DefaultSetup.Volume : 0f;
		}

		// PRIVATE METHODS

		private Vector3 GetMovePosition(ref KinematicData data, float currentTick, float deltaTime)
		{
			int startTick = data.Advanced.MoveStartTick > 0 ? data.Advanced.MoveStartTick : data.FireTick;
			float time = (currentTick - startTick) * deltaTime;

			if (time <= 0f)
				return data.Position;

			return data.Position + (Vector3)data.Velocity * time + new Vector3(0f, -_gravity, 0f) * time * time * 0.5f;
		}

		private void ProcessBounce(ref KinematicData data, LagCompensatedHit hit, Vector3 direction, float distance)
		{
			var runner = Context.Runner;
			var reflectedDirection = Vector3.Reflect(direction, hit.Normal);

			// Stop bouncing when the velocity is small enough
			if (distance < _stopSpeed * runner.DeltaTime)
			{
				// Stop the projectile but do not destroy it yet (wait for timeout)
				data.HasStopped = true;
				data.Advanced.MoveStartTick = runner.Tick;

				data.ImpactPosition = hit.Point + Vector3.Project(hit.Normal * _bounceObjectRadius, reflectedDirection);
				return;
			}

			float bounceMultiplier = _bounceVelocityMultiplierStart;

			if (_bounceVelocityMultiplierStart != _bounceVelocityMultiplierEnd)
			{
				bounceMultiplier = Mathf.Lerp(_bounceVelocityMultiplierStart, _bounceVelocityMultiplierEnd, data.Advanced.BounceCount / (float)_bounceVelocityScale);
			}

			float distanceToHit = Vector3.Distance(hit.Point, transform.position);
			float progressToHit = distanceToHit / distance;

			data.Position = hit.Point + reflectedDirection * _bounceObjectRadius;
			data.Velocity = reflectedDirection * ((Vector3)data.Velocity).magnitude * bounceMultiplier;

			// Simple trick to better align position with ticks. More precise solution would be to remember
			// alpha between ticks (when the bounce happened) but it is good enough here.
			data.Advanced.MoveStartTick = progressToHit > 0.5f ? runner.Tick : runner.Tick - 1;

			data.Advanced.BounceCount++;
		}

		private void OnBounceRender(ref KinematicData data)
		{
			if (_bounceSound == null)
				return;

			var soundSetup = _bounceSound.DefaultSetup;
			soundSetup.Volume = Mathf.Lerp(0f, _maxBounceVolume, ((Vector3)data.Velocity).magnitude / 10f);

			_bounceSound.Play(soundSetup, EForceBehaviour.ForceAny);
		}
	}
}
