using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Base class for all hitscan projectile types.
	/// </summary>
	public abstract class HitscanProjectile : ProjectileBase, IBufferView<HitscanData>
	{
		// PUBLIC MEMBERS

		public bool IsFinished { get; protected set; }

		// PRIVATE MEMBERS

		[SerializeField]
		private float _damage = 10f;
		[SerializeField]
		private EHitType _hitType = EHitType.Projectile;
		[SerializeField]
		private LayerMask _hitMask;
		[SerializeField]
		protected float _maxDistance = 200f;

		// PUBLIC METHODS

		public virtual HitscanData GetFireData(Vector3 firePosition, Vector3 fireDirection)
		{
			var data = new HitscanData()
			{
				FirePosition = firePosition,
				FireDirection = fireDirection,
			};

			if (ProjectileUtility.ProjectileCast(Context.Runner, Context.Owner, firePosition, fireDirection, _maxDistance, _hitMask, out LagCompensatedHit hit) == true)
			{
				HitUtility.ProcessHit(Context.Owner, data.FireDirection, hit, _damage, _hitType);

				data.ImpactPosition = hit.Point;
				data.ImpactNormal = (hit.Normal - fireDirection) * 0.5f;

				SpawnImpact(hit.Point, data.ImpactNormal);
			}

			return data;
		}

		public virtual void Activate(ref HitscanData data)
		{
			IsFinished = false;
		}

		public virtual void Deactivate()
		{
		}

		// IBufferView INTERFACE

		public virtual void Render(ref HitscanData data, ref HitscanData fromData, float alpha)
		{
		}
	}
}
