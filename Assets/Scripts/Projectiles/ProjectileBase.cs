using Fusion;
using UnityEngine;

namespace Projectiles
{
	public class ProjectileContext
	{
		public NetworkRunner Runner;
		public ObjectCache   Cache;
		public PlayerRef     Owner;
		// Barrel transform represents position from which projectile visuals should fly out
		// (actual projectile fire calculations are usually done from different point, for example camera)
		public Transform[]   BarrelTransforms;
	}

	/// <summary>
	/// A common base for all projectiles.
	/// </summary>
	public abstract class ProjectileBase: MonoBehaviour
	{
		// PUBLIC MEMBERS

		public ProjectileContext Context { get; set; }

		// PRIVATE MEMBERS

		[SerializeField]
		private GameObject _impactEffectPrefab;
		[SerializeField]
		private float _impactEffectReturnTime = 2f;
		[SerializeField, Tooltip("Standalone effect that will be spawned through NetworkRunner")]
		private NetworkObject _impactObjectPrefab;

		private TrailRenderer[] _trails;

		// MONOBEHAVIOR

		protected virtual void Awake()
		{
			_trails = GetComponentsInChildren<TrailRenderer>(true);
		}

		protected void OnDisable()
		{
			for (int i = 0; i < _trails.Length; i++)
			{
				_trails[i].Clear();
			}
		}

		// PROTECTED METHODS

		protected void SpawnImpact(Vector3 position, Vector3 normal)
		{
			if (Context.Runner.Stage == default)
			{
				Debug.LogError("Call SpawnImpact only from simulation(Spawned, FUN) methods!");
				return;
			}

			if (position == Vector3.zero)
				return;

			if (_impactObjectPrefab != null && Context.Runner.IsServer == true)
			{
				Context.Runner.Spawn(_impactObjectPrefab, position, Quaternion.LookRotation(normal), Context.Owner);
			}
		}

		protected void SpawnImpactVisual(Vector3 position, Vector3 normal)
		{
			if (Context.Runner.Stage != default)
			{
				Debug.LogError("Call SpawnImpactVisual only from Render-related methods!");
				return;
			}

			if (position == Vector3.zero)
				return;

			if (_impactEffectPrefab != null)
			{
				var impact = Context.Cache.Get(_impactEffectPrefab);

				impact.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal));
				Context.Runner.MoveToRunnerScene(impact);

				Context.Cache.ReturnDeferred(impact, _impactEffectReturnTime);
			}
		}
	}
}
