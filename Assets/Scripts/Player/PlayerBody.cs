using Fusion;
using UnityEngine;
using UnityEngine.Rendering;

namespace Projectiles
{
	/// <summary>
	/// Component handling all visual/hierarchy related tasks and effects (immortality, death).
	/// </summary>
	public class PlayerBody : ContextBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private GameObject _root;
		[SerializeField]
		private GameObject _visual;
		[SerializeField]
		private GameObject _immortalityEffect;
		[SerializeField]
		private Transform _capTransform;
		[SerializeField]
		private Rigidbody _flyingCapPrefab;
		[SerializeField]
		private float _capImpulse = 10f;
		[SerializeField]
		private GameObject _deathEffectPrefab;

		private PlayerAgent _agent;
		private HitboxRoot _hitboxRoot;

		// ContextBehaviour INTERFACE

		public override void Spawned()
		{
			_root.SetActive(_agent.Health.IsAlive);
			_agent.Health.FatalHitTaken += OnFatalHit;

			// Disable visual for local player
			var renderers = _visual.GetComponentsInChildren<MeshRenderer>();
			for (int i = 0; i < renderers.Length; i++)
			{
				renderers[i].shadowCastingMode = HasInputAuthority ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
			}
		}

		public override void FixedUpdateNetwork()
		{
			// Disable hitbox detection when agent is dead
			_hitboxRoot.HitboxRootActive = _agent.Health.IsAlive;
		}

		public override void Render()
		{
			_immortalityEffect.SetActive(_agent.Health.IsImmortal);
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			_agent.Health.FatalHitTaken -= OnFatalHit;
		}

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_agent = GetComponent<PlayerAgent>();
			_hitboxRoot = GetComponent<HitboxRoot>();
		}

		// PRIVATE METHODS

		private void OnFatalHit(HitData hit)
		{
			_agent.KCC.SetActive(false);
			_root.SetActive(false);

			var deathEffect = Runner.InstantiateInRunnerScene(_deathEffectPrefab);
			deathEffect.transform.position = transform.position + Vector3.up;

			var flyingCap = Runner.InstantiateInRunnerScene(_flyingCapPrefab);
			flyingCap.transform.SetPositionAndRotation(_capTransform.position, _capTransform.rotation);

			var direction = (hit.Direction + 2f * Vector3.up).normalized;
			flyingCap.AddForceAtPosition(direction * _capImpulse, flyingCap.transform.position - hit.Direction * 0.2f, ForceMode.Impulse);

			if (Runner.Config.PeerMode == NetworkProjectConfig.PeerModes.Multiple)
			{
				Runner.AddVisibilityNodes(flyingCap.gameObject);
				Runner.AddVisibilityNodes(deathEffect.gameObject);
			}
		}
	}
}
