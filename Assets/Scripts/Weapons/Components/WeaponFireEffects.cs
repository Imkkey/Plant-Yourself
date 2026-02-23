using System;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Weapon component that is responsible for all fire effects
	/// - camera shake, weapon knockback, fire sound, fire particles.
	/// </summary>
	public class WeaponFireEffects : WeaponComponent
	{
		// PRIVATE MEMBERS

		[Header("Muzzle")]
		[SerializeField]
		private GameObject _fireParticle;
		[SerializeField]
		private float _fireParticleReturnTime = 1f;

		[Header("Sound")]
		[SerializeField]
		private Transform _fireAudioEffectsRoot;
		[SerializeField]
		private AudioSetup _fireSound;

		[Header("Camera")]
		[SerializeField]
		private ShakeSetup _cameraShakePosition;
		[SerializeField]
		private ShakeSetup _cameraShakeRotation;

		[Header("Kickback")]
		[SerializeField]
		private Transform _kickbackTransform;
		[SerializeField]
		private Kickback _positionKickback = new(0.06f, 60f, 20f);
		[SerializeField]
		private Kickback _rotationKickback = new(5f, 30f, 20f);

		private Vector3 _kickbackInitialPosition;
		private Quaternion _kickbackInitialRotation;

		private AudioEffect[] _fireAudioEffects;

		// WeaponComponent INTERFACE

		public override void FireRender()
		{
			_positionKickback.HasFired();
			_rotationKickback.HasFired();

			if (_fireParticle != null)
			{
				var fireParticle = Context.ObjectCache.Get(_fireParticle);
				Context.ObjectCache.ReturnDeferred(fireParticle, _fireParticleReturnTime);

				// When using multipeer, disable renderers for other clients. Can be omitted otherwise.
				if (Runner.Config.PeerMode == NetworkProjectConfig.PeerModes.Multiple)
				{
					Runner.AddVisibilityNodes(fireParticle.gameObject);
				}

				if (fireParticle.gameObject.layer != Weapon.gameObject.layer)
				{
					fireParticle.SetLayer(Weapon.gameObject.layer, true);
				}

				fireParticle.transform.SetParent(BarrelTransform, false);
			}

			_fireAudioEffects.PlaySound(_fireSound, EForceBehaviour.ForceAny);

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
			UpdateKickback();
		}

		// MONOBEHAVIOUR

		protected void Awake()
		{
			if (_fireAudioEffectsRoot != null)
			{
				_fireAudioEffects = _fireAudioEffectsRoot.GetComponentsInChildren<AudioEffect>(true);
			}

			if (_kickbackTransform != null)
			{
				_kickbackInitialPosition = _kickbackTransform.localPosition;
				_kickbackInitialRotation = _kickbackTransform.localRotation;
			}
		}

		// PRIVATE METHODS

		private void UpdateKickback()
		{
			if (_kickbackTransform == null)
				return;

			var weaponTransform = Weapon.transform;

			_positionKickback.UpdateKickback();
			_kickbackTransform.localPosition = _kickbackInitialPosition + new Vector3(0f, 0f, -_positionKickback.Current);

			_rotationKickback.UpdateKickback(0.1f);
			_kickbackTransform.localRotation = _kickbackInitialRotation;
			_kickbackTransform.RotateAround(weaponTransform.position, weaponTransform.right, -_rotationKickback.Current);
		}

		// HELPERS

		[Serializable]
		private class Kickback
		{
			// PUBLIC MEMBERS

			public float Current => _current;

			// PRIVATE MEMBERS

			[SerializeField]
			private float _fireKickback = 0.06f;
			[SerializeField]
			private float _speed = 60f;
			[SerializeField]
			private float _returnSpeed = 20f;

			private float _current;
			private float _target;

			// CONSTRUCTOR

			public Kickback(float fireKickback, float speed, float returnSpeed)
			{
				_fireKickback = fireKickback;
				_speed = speed;
				_returnSpeed = returnSpeed;
			}

			// PUBLIC METHODS

			public void HasFired()
			{
				if (_speed <= 0f)
					return;

				_target += _fireKickback;
			}

			public void UpdateKickback(float zeroThreshold = 0.001f)
			{
				if (_speed <= 0f)
					return;

				if (_target > 0f)
				{
					_target = Mathf.Lerp(_target, 0f, Time.deltaTime * _returnSpeed);

					if (_target < zeroThreshold)
					{
						// Stop lerping
						_target = 0f;
					}
				}

				_current = Mathf.Lerp(_current, _target, Time.deltaTime * _speed);

				if (_current < zeroThreshold)
				{
					_current = 0f;
				}
			}
		}
	}
}
