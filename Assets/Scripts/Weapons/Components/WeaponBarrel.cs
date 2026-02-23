using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Weapon component that is responsible for firing projectiles.
	/// </summary>
	public class WeaponBarrel : WeaponComponent
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private ProjectileBase _projectilePrefab;
		[SerializeField]
		private StandaloneProjectile _standaloneProjectilePrefab;
		[SerializeField]
		private NetworkObjectBuffer _standaloneProjectileBuffer;

		[SerializeField]
		private int _projectilesPerShot = 1;
		[SerializeField]
		private float _dispersion = 1f;
		[SerializeField, Tooltip("When firing ballistic projectiles (grenades) it might be desired to aim a little bit higher than the cursor position.")]
		private float _additionalFirePitch = 0f;

		// WeaponComponent INTERFACE

		public override void Fire()
		{
			if (_dispersion > 0f)
			{
				Random.InitState(Runner.Tick * unchecked((int)Object.Id.Raw));
			}

			var fireRotation = FireTransform.rotation * Quaternion.Euler(-_additionalFirePitch, 0f, 0f);;

			for (int i = 0; i < _projectilesPerShot; i++)
			{
				var projectileRotation = fireRotation;

				if (_dispersion > 0f)
				{
					// We use sphere on purpose -> non-uniform distribution (more projectiles in the center)
					var randomDispersion = Random.insideUnitSphere * _dispersion;
					projectileRotation = Quaternion.Euler(randomDispersion.x, randomDispersion.y, randomDispersion.z) * projectileRotation;
				}

				var projectileDirection = projectileRotation * Vector3.forward;

				if (_projectilePrefab is HitscanProjectile hitscanProjectile)
				{
					WeaponContext.HitscanProjectiles.AddProjectile(hitscanProjectile, FireTransform.position, projectileDirection, WeaponActionId);
				}
				else if (_projectilePrefab is KinematicProjectile kinematicProjectile)
				{
					WeaponContext.KinematicProjectiles.AddProjectile(kinematicProjectile, FireTransform.position, projectileDirection, WeaponActionId);
				}
				else if (_standaloneProjectileBuffer != null)
				{
					// If buffer is available try to fire the projectile with buffer
					var projectile = _standaloneProjectileBuffer.Get<StandaloneProjectile>(BarrelTransform.position, BarrelTransform.rotation, Object.InputAuthority);
					if (projectile != null)
					{
						projectile.Fire(FireTransform.position, projectileDirection);
					}
				}
				else if (_standaloneProjectilePrefab != null && HasStateAuthority == true)
				{
					var projectile = Context.Runner.Spawn(_standaloneProjectilePrefab, BarrelTransform.position, BarrelTransform.rotation, Object.InputAuthority);
					projectile.Fire(FireTransform.position, projectileDirection);
				}
			}
		}
	}
}
