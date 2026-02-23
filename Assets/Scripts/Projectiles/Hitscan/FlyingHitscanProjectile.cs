using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Hitscan projectile with dummy flying visual.
	/// </summary>
	public class FlyingHitscanProjectile : HitscanProjectile
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private float _speed = 40f;

		private Vector3 _startPosition;
		private Vector3 _targetPosition;

		private float _time;
		private float _duration;

		// HitscanProjectile INTERFACE

		public override void Activate(ref HitscanData data)
		{
			base.Activate(ref data);

			_startPosition = Context.BarrelTransforms[data.BarrelIndex].position;
			_targetPosition = (Vector3)data.ImpactPosition != Vector3.zero ? data.ImpactPosition : data.FirePosition + (Vector3)data.FireDirection * _maxDistance;

			transform.position = _startPosition;
			transform.rotation = Quaternion.LookRotation(data.FireDirection);

			_duration = Vector3.Magnitude(_targetPosition - _startPosition) / _speed;
			_time = 0f;
		}

		public override void Render(ref HitscanData data, ref HitscanData fromData, float alpha)
		{
			base.Render(ref data, ref fromData, alpha);

			_time += Time.deltaTime;

			float progress = _time / _duration;
			transform.position = Vector3.Lerp(_startPosition, _targetPosition, progress);

			if (_time >= _duration)
			{
				SpawnImpactVisual(data.ImpactPosition, data.ImpactNormal);

				transform.position = _targetPosition;
				IsFinished = true;
			}
		}
	}
}
