using System;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	///  Weapon component that fires a laser beam that damages targets on hit.
	/// </summary>
	[DefaultExecutionOrder(15)]
	public class WeaponBeam : WeaponComponent
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private float _damage = 10f;
		[SerializeField]
		private EHitType _hitType = EHitType.Projectile;
		[SerializeField]
		private LayerMask _hitMask;
		[SerializeField]
		private float _maxDistance = 50f;
		[SerializeField]
		private float _beamRadius = 0.2f;
		[SerializeField, Tooltip("Number of raycast rays fired. First is always in center, other are spread around in the radius distance.")]
		private int _raycastAmount = 5;
		[SerializeField]
		private WeaponTrigger _weaponTrigger;

		[Header("Beam Visuals")]
		[SerializeField]
		private GameObject _beamStart;
		[SerializeField]
		private GameObject _beamEnd;
		[SerializeField]
		private LineRenderer _beam;
		[SerializeField]
		private float _beamEndOffset = 0.5f;
		[SerializeField]
		private bool _updateBeamMaterial;
		[SerializeField]
		private float _textureScale = 3f;
		[SerializeField]
		private float _textureScrollSpeed = -8f;

		[Header("Camera Effect")]
		[SerializeField]
		private ShakeSetup _cameraShakePosition;
		[SerializeField]
		private ShakeSetup _cameraShakeRotation;

		[Networked]
		private float _beamDistance { get; set; }

		// WeaponComponent INTERFACE

		public override void Fire()
		{
			var hit = ProcessBeamHit();
			if (hit.Distance > 0f)
			{
				HitUtility.ProcessHit(Object.InputAuthority, FireTransform.forward, hit, _damage, _hitType);
			}
		}

		// NetworkBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			// Update beam distance only when trigger is firing
			if (_weaponTrigger.IsBusy == true)
			{
				ProcessBeamHit();
			}
			else
			{
				_beamDistance = -1f;
			}
		}

		private void LateUpdate()
		{
			if (Object == null || Object.IsValid == false)
				return;

			// Beam needs to be updated after camera pivot change
			// - after PlayerAgent.LateUpdate
			UpdateBeam();

			if (_beamDistance > 0f && HasInputAuthority == true)
			{
				var cameraShake = Context.Camera.ShakeEffect;
				cameraShake.Play(_cameraShakePosition, EShakeForce.ReplaceSame);
				cameraShake.Play(_cameraShakeRotation, EShakeForce.ReplaceSame);
			}
		}

		// PRIVATE MEMBERS

		private LagCompensatedHit ProcessBeamHit()
		{
			_beamDistance = _maxDistance;

			if (ProjectileUtility.CircleCast(Runner, Object.InputAuthority, FireTransform.position, FireTransform.forward, _maxDistance, _beamRadius, _raycastAmount, _hitMask, out LagCompensatedHit hit) == true)
			{
				_beamDistance = hit.Distance;
				return hit;
			}

			return default;
		}

		private void UpdateBeam()
		{
			bool beamActive = _beamDistance > 0f;

			_beamStart.SetActiveSafe(beamActive);
			_beamEnd.SetActiveSafe(beamActive);
			_beam.gameObject.SetActiveSafe(beamActive);

			if (beamActive == false)
				return;

			var startPosition = _beamStart.transform.position;
			var targetPosition = FireTransform.position + FireTransform.forward * _beamDistance;

			var visualDirection = targetPosition - startPosition;
			float visualDistance = visualDirection.magnitude;

			visualDirection /= visualDistance; // Normalize

			if (_beamEndOffset > 0f)
			{
				// Adjust target position
				visualDistance = visualDistance > _beamEndOffset ? visualDistance - _beamEndOffset : 0f;
				targetPosition = startPosition + visualDirection * visualDistance;
			}

			_beamEnd.transform.SetPositionAndRotation(targetPosition, Quaternion.LookRotation(-visualDirection));

			_beam.SetPosition(0, startPosition);
			_beam.SetPosition(1, targetPosition);

			if (_updateBeamMaterial == true)
			{
				var beamMaterial = _beam.material;

				beamMaterial.mainTextureScale = new Vector2(visualDistance / _textureScale, 1f);
				beamMaterial.mainTextureOffset += new Vector2(Time.deltaTime * _textureScrollSpeed, 0f);
			}
		}
	}
}
