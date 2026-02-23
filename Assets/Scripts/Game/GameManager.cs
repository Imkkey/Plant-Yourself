using System;
using Fusion.Sockets;

namespace Projectiles
{
	using System.Collections.Generic;
	using UnityEngine;
	using Fusion;

	/// <summary>
	/// Handles player connections (spawning of Player instances) and prepares SceneContext when a gameplay scene load is done.
	/// </summary>
	[RequireComponent(typeof(NetworkRunner))]
	[RequireComponent(typeof(NetworkEvents))]
	[DefaultExecutionOrder(-100)]
	public sealed class GameManager : SimulationBehaviour, INetworkRunnerCallbacks
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private Gameplay _gameplayPrefab;
		[SerializeField]
		private Player _playerPrefab;

		private bool _gameplaySpawned;

		// INetworkRunnerCallbacks INTERFACE

		void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef playerRef)
		{
			if (Runner.IsServer == false)
				return;

			if (_gameplaySpawned == false)
			{
				Runner.Spawn(_gameplayPrefab);
				_gameplaySpawned = true;
			}

			var player = Runner.Spawn(_playerPrefab, inputAuthority: playerRef);
			Runner.SetPlayerObject(playerRef, player.Object);
		}

		void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef playerRef)
		{
			if (Runner.IsServer == false)
				return;

			var player = Runner.GetPlayerObject(playerRef);
			if (player != null)
			{
				Runner.Despawn(player);
			}
		}

		void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
		{
			// Prepare context
			var scene = runner.SimulationUnityScene.GetComponent<Scene>(true);

			var context = scene.Context;
			context.Runner = Runner;

			// Assign context
			var contextBehaviours = runner.SimulationUnityScene.GetComponents<IContextBehaviour>(true);
			foreach (var behaviour in contextBehaviours)
			{
				behaviour.Context = context;
			}

			var objectPool = Runner.GetComponent<NetworkObjectPool>();
			objectPool.Context = context;

			if (runner.Config.PeerMode == NetworkProjectConfig.PeerModes.Multiple)
			{
				// In case of multipeer mode, fix the scene lighting
				var renderSettingsUpdated = scene.GetComponent<RenderSettingsUpdater>();
				renderSettingsUpdated.ApplySettings();
			}
		}

		void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
		void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
		void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) {}
		void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {}
		void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {}
		void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) {}
		void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {}
		void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {}
		void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {}
		void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {}
		void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {}
		void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {}
		void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}
		void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {}
		void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {}
		void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) {}
	}
}
