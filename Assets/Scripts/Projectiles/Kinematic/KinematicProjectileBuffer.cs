using System.Runtime.InteropServices;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	[StructLayout(LayoutKind.Explicit)]
	public struct KinematicData : INetworkStruct
	{
		public bool              IsFinished        { get { return _state.IsBitSet(0); } set { _state.SetBit(0, value); } }
		public bool              HasStopped        { get { return _state.IsBitSet(1); } set { _state.SetBit(1, value); } }

		[FieldOffset(0)]
		private byte             _state;

		[FieldOffset(1)]
		public byte              PrefabIndex;
		[FieldOffset(2)]
		public byte              BarrelIndex;
		[FieldOffset(3)]
		public int               FireTick;
		[FieldOffset(7)]
		public Vector3Compressed Position;
		[FieldOffset(19)]
		public Vector3Compressed Velocity;
		[FieldOffset(31)]
		public Vector3Compressed ImpactPosition;
		[FieldOffset(43)]
		public Vector3Compressed ImpactNormal;

		// Custom projectile data

		[FieldOffset(55)]
		public AdvancedData      Advanced;
		[FieldOffset(55)]
		public SprayData         Spray;
		[FieldOffset(55)]
		public HomingData        Homing;

		public struct AdvancedData : INetworkStruct
		{
			public int  MoveStartTick;
			public byte BounceCount;
		}

		public struct SprayData : INetworkStruct
		{
			public Vector3 InheritedVelocity;
		}

		public struct HomingData : INetworkStruct
		{
			public NetworkId         Target;
			public Vector3Compressed TargetPosition; // Used for position prediction
		}
	}

	/// <summary>
	/// Projectile data buffer for kinematic projectiles.
	/// </summary>
	public class KinematicProjectileBuffer : NetworkDataBuffer<KinematicData, KinematicProjectile>
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private KinematicProjectile[] _projectilePrefabs;

		private ProjectileContext _context;

		// PUBLIC METHODS

		public void AddProjectile(KinematicProjectile projectilePrefab, Vector3 firePosition, Vector3 direction, byte barrelIndex = 0, Vector3 inheritedVelocity = default)
		{
			int prefabIndex = _projectilePrefabs.IndexOf(projectilePrefab);

			if (prefabIndex < 0)
			{
				Debug.LogError($"Projectile {projectilePrefab} not found. Add it in HitscanProjectiles prefab array.");
				return;
			}

			// Temporarily assign correct context in case it will be needed in GetFireData
			projectilePrefab.Context = _context;
			var data = projectilePrefab.GetFireData(firePosition, direction);
			projectilePrefab.Context = null;

			data.FireTick = Runner.Tick;
			data.PrefabIndex = (byte)prefabIndex;
			data.BarrelIndex = barrelIndex;

			if (inheritedVelocity != Vector3.zero)
			{
				data.Spray.InheritedVelocity = inheritedVelocity;
			}

			AddData(data);
		}

		public void UpdateBarrelTransforms(Transform[] barrelTransforms)
		{
			_context.BarrelTransforms = barrelTransforms;
		}

		// NetworkDataBuffer INTERFACE

		[Networked, Capacity(64)]
		protected override NetworkArray<KinematicData> DataBuffer { get; }

		protected override void UpdateData(ref KinematicData data)
		{
			if (data.IsFinished == true)
				return;
			if (data.FireTick == 0)
				return;

			var prefab = _projectilePrefabs[data.PrefabIndex];

			// Temporarily assign correct context
			prefab.Context = _context;
			prefab.OnFixedUpdate(ref data);
			prefab.Context = null;
		}

		protected override KinematicProjectile GetView(KinematicData data)
		{
			var projectile = Context.ObjectCache.Get(_projectilePrefabs[data.PrefabIndex]);

			Runner.MoveToRunnerScene(projectile);
			if (Runner.Config.PeerMode == NetworkProjectConfig.PeerModes.Multiple)
			{
				Runner.AddVisibilityNodes(projectile.gameObject);
			}

			projectile.Context = _context;
			projectile.Activate(ref data);

			return projectile;
		}

		protected override void ReturnView(KinematicProjectile projectile, bool misprediction)
		{
			if (projectile == null)
				return;

			projectile.Deactivate();

			Context.ObjectCache.Return(projectile);
		}

		public override void Spawned()
		{
			base.Spawned();

			_context.Runner = Runner;
			_context.Cache = Context.ObjectCache;
			_context.Owner = Object.InputAuthority;
		}

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_context = new ProjectileContext();
		}
	}
}
