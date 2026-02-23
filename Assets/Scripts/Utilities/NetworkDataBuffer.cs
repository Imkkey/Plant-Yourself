using System.Collections.Generic;
using Fusion;

namespace Projectiles
{
	public interface IBufferView<T> where T : INetworkStruct
	{
		public bool IsFinished { get; }
		public void Render(ref T data, ref T fromData, float alpha);
	}

	public abstract class NetworkDataBuffer<TData> : NetworkDataBuffer<TData, IBufferView<TData>>
		where TData : INetworkStruct
	{}

	/// <summary>
	/// Generic data ring buffer that pairs networked data TData with visual representation TView.
	/// </summary>
	/// <typeparam name="TData">Data structure representing single entry in the buffer</typeparam>
	/// <typeparam name="TView">View component representing buffer entry in the game scene</typeparam>
	public abstract class NetworkDataBuffer<TData, TView> : ContextBehaviour
		where TData : INetworkStruct
		where TView : IBufferView<TData>
	{
		// PROTECTED MEMBERS

		[Networked]
		protected int _dataCount { get; set; }

		// PRIVATE MEMBERS

		private Dictionary<int, ViewEntry> _views = new(256);
		private List<int> _finishedViews = new(128);

		private int _viewCount;

		private ArrayReader<TData> _dataBufferReader;
		private PropertyReader<int> _dataCountReader;

		// PUBLIC METHODS

		public void AddData(TData data)
		{
			int dataIndex = _dataCount % DataBuffer.Length;
			DataBuffer.Set(dataIndex, data);

			_dataCount++;
		}

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			_viewCount = _dataCount;

			_dataBufferReader = GetArrayReader<TData>(nameof(DataBuffer));
			_dataCountReader = GetPropertyReader<int>(nameof(_dataCount));
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			foreach (var pair in _views)
			{
				ReturnEntry(pair.Value, false);
			}

			_views.Clear();
		}

		public override void FixedUpdateNetwork()
		{
			for (int i = 0; i < DataBuffer.Length; i++)
			{
				TData bufferData = DataBuffer[i];
				UpdateData(ref bufferData);
				DataBuffer.Set(i, bufferData);
			}
		}

		public override void Render()
		{
			// Visuals are not processed on dedicated server at all
			if (Runner.Mode == SimulationModes.Server)
				return;

			if (TryGetSnapshotsBuffers(out var fromNetworkBuffer, out var toNetworkBuffer, out float bufferAlpha) == false)
				return;

			var fromDataBuffer = _dataBufferReader.Read(fromNetworkBuffer);
			var toDataBuffer = _dataBufferReader.Read(toNetworkBuffer);
			int fromDataCount = _dataCountReader.Read(fromNetworkBuffer);
			int toDataCount = _dataCountReader.Read(toNetworkBuffer);

			int bufferLength = DataBuffer.Length;

			// If our predicted views were not confirmed by the server, discard them
			for (int i = fromDataCount; i < _viewCount; i++)
			{
				if (_views.TryGetValue(i, out var viewEntry) == false)
					continue;

				ReturnEntry(viewEntry, true);
				_views.Remove(i);
			}

			// Let's spawn missing views
			for (int i = _viewCount; i < fromDataCount; i++)
			{
				int bufferIndex = i % bufferLength;
				var data = fromDataBuffer[bufferIndex];

				var view = GetView(data);
				if (view == null)
					continue;

				var viewEntry = Pool.Get<ViewEntry>();
				viewEntry.View = view;

				_views.Add(i, viewEntry);
			}

			// At some point the buffer will be overriden
			// by new data (new buffer cycle) so we need to calculate
			// last valid data key in the buffer.
			int minDataKey = toDataCount - bufferLength;

			// Update all visible views
			foreach (var pair in _views)
			{
				var view = pair.Value.View;

				if (pair.Key >= minDataKey)
				{
					int bufferIndex = pair.Key % bufferLength;

					var data = toDataBuffer[bufferIndex];
					var fromData = fromDataBuffer[bufferIndex];

					view.Render(ref data, ref fromData, bufferAlpha);
					pair.Value.LastData = data;
				}
				else
				{
					// Use last data to Render when there are no data available in the buffer
					view.Render(ref pair.Value.LastData, ref pair.Value.LastData, 0f);
				}

				if (view.IsFinished == true)
				{
					ReturnEntry(pair.Value, false);
					_finishedViews.Add(pair.Key);
				}
			}

			for (int i = 0; i < _finishedViews.Count; i++)
			{
				_views.Remove(_finishedViews[i]);
			}

			_finishedViews.Clear();
			_viewCount = fromDataCount;
		}

		// PROTECTED MEMBERS

		protected abstract NetworkArray<TData> DataBuffer { get; }
		protected virtual void UpdateData(ref TData data) { }

		protected abstract TView GetView(TData data);
		protected abstract void ReturnView(TView bufferView, bool misprediction);

		// PRIVATE METHODS

		private void ReturnEntry(ViewEntry entry, bool misprediction)
		{
			ReturnView(entry.View, misprediction);
			Pool.Return(entry);
		}

		// DATA STRUCTURES

		private class ViewEntry
		{
			public TView View;
			public TData LastData;
		}
	}
}
