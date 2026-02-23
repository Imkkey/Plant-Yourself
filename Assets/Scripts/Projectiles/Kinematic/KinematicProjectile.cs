using UnityEasing;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Base class for all kinematic projectile types.
	/// </summary>
	public abstract class KinematicProjectile : ProjectileBase, IBufferView<KinematicData>
	{
		// PUBLIC MEMBERS

		public bool IsFinished { get; protected set; }

		// PROTECTED MEMBERS

		[SerializeField]
		protected float _startSpeed = 40f;
		[SerializeField, Tooltip("Projectile length improves hitting moving targets")]
		protected float _length = 0f;

		// PRIVATE MEMBERS

		[SerializeField]
		private float _maxDistance = 200f;
		[SerializeField]
		private float _maxTime = 5f;
		[SerializeField, Tooltip("Time for interpolation between barrel position and actual fire path of the projectile")]
		private float _interpolationDuration = 0.3f;
		[SerializeField]
		private Ease _interpolationEase = Ease.OutSine;

		private Vector3 _startOffset;
		private float _interpolationTime;

		protected int _lifetimeTicks = -1;

		// PUBLIC METHODS

		public virtual KinematicData GetFireData(Vector3 firePosition, Vector3 fireDirection)
		{
			if (_lifetimeTicks < 0)
			{
				int maxDistanceTicks = Mathf.RoundToInt((_maxDistance / _startSpeed) * Context.Runner.TickRate);
				int maxTimeTicks = Mathf.RoundToInt(_maxTime * Context.Runner.TickRate);

				// GetFireData is called on prefab directly, but it is safe to save
				// the value here as it does not change for different instances
				_lifetimeTicks = maxDistanceTicks > 0 && maxTimeTicks > 0 ? Mathf.Min(maxDistanceTicks, maxTimeTicks)
					: (maxDistanceTicks > 0 ? maxDistanceTicks : maxTimeTicks);
			}

			return new KinematicData()
			{
				Position = firePosition,
				Velocity = fireDirection * _startSpeed,
			};
		}

		public virtual void OnFixedUpdate(ref KinematicData data)
		{
			if (Context.Runner.Tick >= data.FireTick + _lifetimeTicks)
			{
				data.IsFinished = true;
			}
		}

		public virtual void Activate(ref KinematicData data)
		{
			var startPosition = Context.BarrelTransforms[data.BarrelIndex].position;

			// Kinematic projectile visual starts at the barrel position and is slowly
			// interpolated to its actual path that starts directly from camera.
			transform.position = startPosition;
			transform.rotation = Quaternion.LookRotation(data.Velocity);

			_startOffset = startPosition - data.Position;
			_interpolationTime = 0f;

			IsFinished = false;
		}

		public virtual void Deactivate()
		{
		}

		// IBufferView INTERFACE

		public virtual void Render(ref KinematicData data, ref KinematicData fromData, float alpha)
		{
			if (data.IsFinished == true)
			{
				SpawnImpactVisual(data.ImpactPosition, data.ImpactNormal);
				IsFinished = true;
				return;
			}

			var targetPosition = GetRenderPosition(ref data, ref fromData, alpha);
			float interpolationProgress = 0f;

			if (targetPosition != (Vector3)data.Position)
			{
				// Do not start interpolation until projectile should actually move
				_interpolationTime += Time.deltaTime;
				interpolationProgress = Mathf.Clamp01(_interpolationTime / _interpolationDuration);
			}

			var offset = Vector3.Lerp(_startOffset, Vector3.zero, _interpolationEase.Get(interpolationProgress));

			var previousPosition = transform.position;
			var nextPosition = targetPosition + offset;
			var direction = nextPosition - previousPosition;

			transform.position = nextPosition;

			if (direction != Vector3.zero)
			{
				transform.rotation = Quaternion.LookRotation(direction);
			}
		}

		// PROTECTED METHODS

		protected abstract Vector3 GetRenderPosition(ref KinematicData data, ref KinematicData fromData, float alpha);
	}
}
