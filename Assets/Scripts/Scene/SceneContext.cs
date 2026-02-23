using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Holds scene specific references and common runtime data.
	/// </summary>
	public class SceneContext : MonoBehaviour
	{
		// General

		public ObjectCache      ObjectCache;
		public GeneralInput     GeneralInput;
		public SceneCamera      Camera;

		// Gameplay

		[HideInInspector]
		public Gameplay         Gameplay;
		[HideInInspector]
		public NetworkRunner    Runner;
		[HideInInspector]
		public PlayerAgent      LocalAgent;
	}
}
