using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Weapon component that handles firing of spray projectiles and showing spray effects.
	/// </summary>
	public class WeaponSpray : WeaponComponent
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private SprayProjectile _projectilePrefab;
		[SerializeField]
		private float _dispersion = 1f;
		[SerializeField, Range(0f, 1f)]
		private float _moveVelocityMultiplier = 0.3f;

		[Header("Effects")]
		[SerializeField]
		private ParticleSystem _fireParticle;
		[SerializeField]
		private ParticleSystem _sparkParticle;
		[SerializeField]
		private AudioSource _fireSound;
		[SerializeField]
		private ShakeSetup _cameraShakePosition;
		[SerializeField]
		private ShakeSetup _cameraShakeRotation;

		private float _fireEffectsCooldown;
		private float _maxVolume;

		// WeaponComponent INTERFACE

		public override void Fire()
		{
			var projectileDirection = FireTransform.forward;

			if (_dispersion > 0f)
			{
				Random.InitState(Runner.Tick * unchecked((int)Object.Id.Raw));

				// We use sphere on purpose -> non-uniform distribution (more projectiles in the center)
				var randomDispersion = Random.insideUnitSphere * _dispersion;
				projectileDirection = Quaternion.Euler(randomDispersion.x, randomDispersion.y, randomDispersion.z) * projectileDirection;
			}

			// For spray projectile add some part of movement velocity so that the sprayed projectile moves with the player
			var inheritedVelocity = WeaponContext.MoveVelocity * _moveVelocityMultiplier;

			WeaponContext.KinematicProjectiles.AddProjectile(_projectilePrefab, FireTransform.position, projectileDirection, WeaponActionId, inheritedVelocity);
		}

		public override void FireRender()
		{
			// Fire effects (volume, particle) will fade out during fire cooldown
			_fireEffectsCooldown = 0.25f;

			if (HasInputAuthority == true)
			{
				var cameraShake = Context.Camera.ShakeEffect;
				cameraShake.Play(_cameraShakePosition, EShakeForce.ReplaceSame);
				cameraShake.Play(_cameraShakeRotation, EShakeForce.ReplaceSame);
			}
		}

		// NetworkBehaviour INTERFACE

		public override void Render()
		{
			_fireEffectsCooldown -= Time.deltaTime;

			float targetVolume = Mathf.Lerp(0f, _maxVolume, _fireEffectsCooldown / 0.25f);
			_fireSound.volume = Mathf.Lerp(_fireSound.volume, targetVolume, Time.deltaTime * 10f);

			if (_fireEffectsCooldown > 0.1f)
			{
				if (_fireParticle.isPlaying == false)
				{
					_fireParticle.Play();
				}

				_sparkParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			}
			else
			{
				if (_sparkParticle.isPlaying == false)
				{
					_sparkParticle.Play();
				}

				_fireParticle.Stop();
			}
		}

		// MONOBEHAVIOUR

		private void Awake()
		{
			_maxVolume = _fireSound.volume;
			_fireSound.volume = 0f;
		}
	}
}
