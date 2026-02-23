using Fusion;
using UnityEngine;
using Fusion.Addons.SimpleKCC;

namespace Projectiles
{
	/// <summary>
	/// Main script handling player agent. It provides access to common components and handles movement input processing and camera.
	/// </summary>
	[DefaultExecutionOrder(-5)]
	[RequireComponent(typeof(Weapons), typeof(Health), typeof(SimpleKCC))]
	public class PlayerAgent : ContextBehaviour
	{
		// PUBLIC MEMBERS

		[Networked]
		public Player      Owner         { get; set; }
		public Weapons     Weapons       { get; private set; }
		public Health      Health        { get; private set; }
		public SimpleKCC   KCC           { get; private set; }
		public PlayerInput Input         { get; private set; }

		public bool        InputBlocked  => Health.IsAlive == false;

		// PRIVATE MEMBERS

		[SerializeField]
		private Transform _cameraPivot;
		[SerializeField]
		private Transform _cameraHandle;

		[Header("Movement")]
		[SerializeField]
		private float _moveSpeed = 6f;
		[SerializeField]
		public float _upGravity = 15f;
		[SerializeField]
		public float _downGravity = 25f;
		[SerializeField]
		private float _maxCameraAngle = 75f;
		[SerializeField]
		private float _jumpImpulse = 6f;
		[SerializeField]
		public float _groundAcceleration = 55f;
		[SerializeField]
		public float _groundDeceleration = 25f;
		[SerializeField]
		public float _airAcceleration = 25f;
		[SerializeField]
		public float _airDeceleration = 1.3f;

		[Networked]
		private Vector3 _moveVelocity { get; set; }

		private Vector2 _lastFUNLookRotation;

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			name = Object.InputAuthority.ToString();

			// Only local player needs networked properties (move velocity).
			// This saves network traffic by not synchronizing networked properties to other clients except local player.
			ReplicateToAll(false);
			ReplicateTo(Object.InputAuthority, true);
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			Owner = null;
		}

		public override void FixedUpdateNetwork()
		{
			if (Owner != null && Health.IsAlive == true)
			{
				ProcessMovementInput();
			}

			// Setting camera pivot rotation
			var pitchRotation = KCC.GetLookRotation(true, false);
			_cameraPivot.localRotation = Quaternion.Euler(pitchRotation);

			_lastFUNLookRotation = KCC.GetLookRotation();
		}

		// MONOBEHAVIOUR

		protected void Awake()
		{
			KCC = GetComponent<SimpleKCC>();
			Weapons = GetComponent<Weapons>();
			Health = GetComponent<Health>();
			Input = GetComponent<PlayerInput>();
		}

		protected void LateUpdate()
		{
			if (HasInputAuthority == true && Owner != null && Health.IsAlive == true)
			{
				// For responsive look experience we use last FUN look + accumulated look rotation delta
				KCC.SetLookRotation(_lastFUNLookRotation + Input.AccumulatedLook, -_maxCameraAngle, _maxCameraAngle);
			}

			// Update camera pitch
			// Camera pivot influences also weapon rotation so it needs to be set on proxies as well
			var pitchRotation = KCC.GetLookRotation(true, false);
			_cameraPivot.localRotation = Quaternion.Euler(pitchRotation);

			if (HasInputAuthority == true)
			{
				var cameraTransform = Context.Camera.transform;

				// Setting base camera transform based on handle
				cameraTransform.position = _cameraHandle.position;
				cameraTransform.rotation = _cameraHandle.rotation;
			}
		}

		// PRIVATE METHODS

		private void ProcessMovementInput()
		{
			if (GetInput(out GameplayInput input) == false)
				return;

			KCC.AddLookRotation(input.LookRotationDelta, -_maxCameraAngle, _maxCameraAngle);

			// It feels better when player falls quicker
			KCC.SetGravity(KCC.RealVelocity.y >= 0f ? _upGravity : _downGravity);

			// Calculate input direction based on recently updated look rotation (the change propagates internally also to KCC.TransformRotation)
			var inputDirection = KCC.TransformRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);

			var desiredMoveVelocity = inputDirection * _moveSpeed;
			float acceleration = 1f;

			if (desiredMoveVelocity == Vector3.zero)
			{
				// No desired move velocity - we are stopping.
				acceleration = KCC.IsGrounded == true ? _groundDeceleration : _airDeceleration;
			}
			else
			{
				acceleration = KCC.IsGrounded == true ? _groundAcceleration : _airAcceleration;
			}

			_moveVelocity = Vector3.Lerp(_moveVelocity, desiredMoveVelocity, acceleration * Runner.DeltaTime);

			float jumpImpulse = input.Buttons.WasPressed(Input.PreviousButtons, EInputButton.Jump) && KCC.IsGrounded ? _jumpImpulse : 0f;
			KCC.Move(_moveVelocity, jumpImpulse);
		}
	}
}
