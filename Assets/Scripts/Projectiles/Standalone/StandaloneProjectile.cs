using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Standalone (spawned) projectile that acts as a container for KinematicData and updates
	/// standard KinematicProjectile script in a similar manner as KinematicProjectileBuffer does for projectile data buffer.
	/// Note: Should be used only in special cases (e.g. very long living projectiles),
	/// otherwise projectile data buffer is much better solution.
	/// </summary>
	public sealed class StandaloneProjectile : ContextBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private KinematicProjectile _projectileVisual;
		[SerializeField]
		private float _despawnTime = 1.5f;

		[Networked]
		private KinematicData _data { get; set; }
		[Networked]
		private Vector3 _barrelPosition { get; set; }
		[Networked]
		private TickTimer _despawnCooldown { get; set; }

		private bool _isActivated;
		private Transform _dummyBarrelTransform;

		private ProjectileContext _projectileContext = new();
		private PropertyReader<KinematicData> _dataReader;

		// PUBLIC METHODS

		public void Fire(Vector3 firePosition, Vector3 fireDirection)
		{
			// Reassign input authority as this object could be from NetworkObjectBuffer
			// and input authority is not known on object spawn
			_projectileContext.Owner = Object.InputAuthority;

			var data = _projectileVisual.GetFireData(firePosition, fireDirection);
			data.FireTick = Runner.Tick;

			_data = data;

			// Save spawned position as barrel position
			_barrelPosition = transform.position;
		}

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			PrepareContext();

			_dataReader = GetPropertyReader<KinematicData>(nameof(_data));

			if (_projectileVisual.gameObject != gameObject)
			{
				// Disable visual until rendering happens
				_projectileVisual.SetActive(false);
			}
			else
			{
				Debug.LogError("Projectile visual should be child of the NetworkObject");
			}

			if (IsProxy == false)
			{
				// Saved the spawned position as barrel position
				_barrelPosition = transform.position;
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (_data.FireTick == 0)
				return;

			if (_data.IsFinished == true)
			{
				if (_despawnCooldown.ExpiredOrNotRunning(Runner) == true)
				{
					Runner.Despawn(Object);
				}
				return;
			}

			var data = _data;
			_projectileVisual.OnFixedUpdate(ref data);
			_data = data;

			if (data.IsFinished == true && _despawnTime > 0f)
			{
				_despawnCooldown = TickTimer.CreateFromSeconds(Runner, _despawnTime);
			}
		}

		public override void Render()
		{
			// Visuals are not processed on dedicated server at all
			if (Runner.Mode == SimulationModes.Server)
				return;

			if (_isActivated == true && _projectileVisual.IsFinished == true)
			{
				_projectileVisual.SetActive(false);
				return;
			}

			if (TryGetSnapshotsBuffers(out var fromNetworkBuffer, out var toNetworkBuffer, out float bufferAlpha) == false)
				return;

			var fromData = _dataReader.Read(fromNetworkBuffer);
			var toData = _dataReader.Read(toNetworkBuffer);

			// In case the projectile comes from NetworkObjectBuffer the network buffers
			// are valid even before the fire data is set. Wait until the data is truly valid.
			if (fromData.FireTick == 0)
				return;

			_dummyBarrelTransform.position = _barrelPosition;

			if (_isActivated == false)
			{
				_projectileVisual.SetActive(true);
				_projectileVisual.Activate(ref fromData);
				_isActivated = true;
			}

			_projectileVisual.Render(ref toData, ref fromData, bufferAlpha);
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			if (_isActivated == true)
			{
				_projectileVisual.Deactivate();
				_isActivated = false;
			}

			_dummyBarrelTransform.localPosition = Vector3.zero;
		}

		// MONOBEHAVIOUR

		private void Awake()
		{
			_projectileVisual.Context = _projectileContext;

			_dummyBarrelTransform = new GameObject("DummyBarrelTransform").transform;
			_dummyBarrelTransform.parent = transform;
		}

		// PRIVATE METHODS

		private void PrepareContext()
		{
			_projectileContext.Runner = Runner;
			_projectileContext.Cache = Context.ObjectCache;
			_projectileContext.Owner = Object.InputAuthority;

			if (_projectileContext.BarrelTransforms == null)
			{
				_projectileContext.BarrelTransforms = new Transform[1];
			}

			// Setting real weapon transform is not safe for standalone projectiles as that can get returned to cache, be destroyed etc.
			// This object is not moving, so it is good enough substitude to use dummy barrel transform child.
			_projectileContext.BarrelTransforms[0] = _dummyBarrelTransform;

			// Set correct barrel position even on proxies
			_dummyBarrelTransform.position = _barrelPosition;
		}
	}
}
