using UnityEngine;
using System.Collections.Generic;
using Fusion;

namespace Projectiles
{
	/// <summary>
	/// An area that deals damage over time to any IHitTarget that stays inside it.
	/// </summary>
	public class DamageArea : NetworkBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]

		private float _damagePerSecond = 20f;
		[SerializeField]
		private int _hitsPerSecond = 4;

		[Networked]
		private TickTimer _cooldown { get; set; }

		private HashSet<IHitTarget> _targets = new();

		// NetworkBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			if (_damagePerSecond <= 0f)
				return;

			// Remove invalid targets
			_targets.RemoveWhere(t => t.IsActive == false);

			if (_cooldown.ExpiredOrNotRunning(Runner) == true)
			{
				Fire();
			}
		}

		// MONOBEHAVIOUR

		private void OnTriggerEnter(Collider other)
		{
			if (HasStateAuthority == false)
				return;

			var target = other.GetComponentInParent<IHitTarget>();
			if (target != null)
			{
				_targets.Add(target);
			}
		}

		private void OnTriggerExit(Collider other)
		{
			if (HasStateAuthority == false)
				return;

			var target = other.GetComponentInParent<IHitTarget>();
			if (target != null)
			{
				_targets.Remove(target);
			}
		}

		// PRIVATE METHODS

		private void Fire()
		{
			// Restart the hit interval
			_cooldown = TickTimer.CreateFromSeconds(Runner, 1f / _hitsPerSecond);

			float damage = _damagePerSecond  / _hitsPerSecond;
			foreach (var target in _targets)
			{
				var targetPosition = (target as MonoBehaviour).transform.position;

				HitData hitData = new HitData();
				hitData.Action           = EHitAction.Damage;
				hitData.Amount           = damage;
				hitData.Position         = targetPosition;
				hitData.InstigatorRef    = Object.InputAuthority;
				hitData.Direction        = (targetPosition - transform.position).normalized;
				hitData.Normal           = Vector3.up;
				hitData.Target           = target;
				hitData.HitType          = EHitType.Suicide;

				HitUtility.ProcessHit(ref hitData);
			}
		}
	}
}
