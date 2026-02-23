using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Pre-spawns network object in advance so they can be immediately used on Input Authority without a spawn delay.
	/// </summary>
	public class NetworkObjectBuffer : NetworkBehaviour
	{
		// CONSTANTS

		public const int CAPACITY = 8;

		// PRIVATE MEMBERS

		[SerializeField]
		private NetworkObject _prefab;
		[SerializeField, Range(1, CAPACITY)]
		private int _bufferSize = CAPACITY;

		[Networked, Capacity(CAPACITY)]
		private NetworkArray<NetworkObject> _buffer { get; }
		[Networked]
		private int _bufferHead { get; set; }

		private NetworkObject[] _localBuffer = new NetworkObject[CAPACITY];

		// PUBLIC METHODS

		public T Get<T>(Vector3 position, Quaternion rotation, PlayerRef inputAuthority) where T : NetworkBehaviour
		{
			var instance = Get(position, rotation, inputAuthority);
			return instance != null ? instance.GetComponent<T>() : null;
		}

		public NetworkObject Get(Vector3 position, Quaternion rotation, PlayerRef inputAuthority)
		{
			var instance = _buffer[_bufferHead];

			if (instance == null)
				return null;

			Runner.SetIsSimulated(instance, true);
			instance.AssignInputAuthority(inputAuthority);

			instance.transform.SetPositionAndRotation(position, rotation);
			instance.gameObject.SetActive(true);

			_buffer.Set(_bufferHead, null);
			FillBuffer();

			_bufferHead = (_bufferHead + 1) % _bufferSize;

			return instance;
		}

		public override void Spawned()
		{
			FillBuffer();
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			ClearBuffer();
		}

		public override void Render()
		{
			if (HasStateAuthority == true)
				return;

			for (int i = 0; i < _bufferSize; i++)
			{
				var networkInstance = _buffer[i];
				var localInstance = _localBuffer[i];

				if (localInstance == networkInstance)
					continue;

				if (localInstance != null && localInstance.IsValid == true)
				{
					// Network instance was released so we need to activate
					// object on all clients (including proxies) not only
					// on those where Get method was called
					localInstance.gameObject.SetActive(true);

					#if UNITY_EDITOR
						localInstance.name = _prefab.name;
					#endif
				}

				_localBuffer[i] = networkInstance;

				if (networkInstance != null)
				{
					// New instance was added to the buffer, we need to make sure
					// that the object is inactive on all clients (including proxies)
					networkInstance.gameObject.SetActive(false);

					#if UNITY_EDITOR
						networkInstance.name = $"(Buffered) {networkInstance.name}";
					#endif
				}

				//var localName = localInstance != null ? localInstance.Id.ToString(): "null";
				//var remoteName = networkInstance != null ? networkInstance.Id.ToString() : "null";
				//Debug.Log($"{Runner.name} - {Object.InputAuthority} ({Time.frameCount}) - Changing local buffer on index {i} Local {localName} Network {remoteName}");
			}
		}

		// PRIVATE METHODS

		private void FillBuffer()
		{
			if (HasStateAuthority == false)
				return;

			for (int i = 0; i < _bufferSize; i++)
			{
				if (_buffer[i] == null)
				{
					_buffer.Set(i, PrepareInstance());
				}
			}
		}

		private void ClearBuffer()
		{
			if (HasStateAuthority == false)
			{
				_localBuffer.Clear();
				return;
			}

			for (int i = 0; i < _bufferSize; i++)
			{
				if (_buffer[i] != null)
				{
					Runner.Despawn(_buffer[i]);
				}
			}

			_buffer.Clear();
		}

		private NetworkObject PrepareInstance()
		{
			var instance = Runner.Spawn(_prefab, new Vector3(0f, -1000f, 0f));

			Runner.SetIsSimulated(instance, false);
			instance.gameObject.SetActive(false);

			return instance;
		}
	}
}
