using Fusion;
using UnityEngine;

namespace Projectiles
{
	public struct HitscanData : INetworkStruct
	{
		public byte PrefabIndex;
		public byte BarrelIndex;
		public Vector3Compressed FirePosition;
		public Vector3Compressed FireDirection;
		public Vector3Compressed ImpactPosition;
		public Vector3Compressed ImpactNormal;
	}

	/// <summary>
	/// Projectile data buffer for hitscan projectiles.
	/// </summary>
	public class HitscanProjectileBuffer : NetworkDataBuffer<HitscanData, HitscanProjectile>
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private HitscanProjectile[] _projectilePrefabs;

		private ProjectileContext _context;

		// PUBLIC METHODS

		public void AddProjectile(HitscanProjectile projectilePrefab, Vector3 firePosition, Vector3 direction, byte barrelIndex = 0)
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

			data.PrefabIndex = (byte)prefabIndex;
			data.BarrelIndex = barrelIndex;

			AddData(data);
		}

		public void UpdateBarrelTransforms(Transform[] barrelTransforms)
		{
			_context.BarrelTransforms = barrelTransforms;
		}

		// NetworkDataBuffer INTERFACE

		[Networked, Capacity(32)]
		protected override NetworkArray<HitscanData> DataBuffer { get; }

		protected override HitscanProjectile GetView(HitscanData data)
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

		protected override void ReturnView(HitscanProjectile projectile, bool misprediction)
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
