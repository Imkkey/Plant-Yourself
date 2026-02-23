using Fusion;
using TMPro;
using UnityEngine;

namespace Projectiles.UI
{
	public class UIGameInfo : UIBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private TextMeshProUGUI _sessionNameText;
		[SerializeField]
		private TextMeshProUGUI _regionText;
		[SerializeField]
		private TextMeshProUGUI _fpsText;
		[SerializeField]
		private TextMeshProUGUI _rttText;
		[SerializeField]
		private TextMeshProUGUI _connectionTypeText;

		private int _lastFPS;
		private int _lastRTT = -1;
		private int _frameCount;
		private float _deltaTime;
		private double _rtt;
		private ConnectionType _connectionType;

		// MONOBEHAVIOUR

		protected void OnEnable()
		{
			UpdateInfo(GameUI.Runner, true);
		}

		protected void Update()
		{
			UpdateInfo(GameUI.Runner);
		}

		// PRIVATE MEMBERS

		private void UpdateInfo(NetworkRunner runner, bool full = false)
		{
			if (runner == null)
				return;

			_frameCount++;

			_deltaTime += Time.deltaTime;
			_rtt += runner.GetPlayerRtt(runner.LocalPlayer);

			if (_deltaTime > 0.25f)
			{
				int fps = Mathf.RoundToInt(_frameCount / _deltaTime);
				if (fps != _lastFPS)
				{
					_lastFPS = fps;
					_fpsText.text = fps.ToString();
				}

				int rtt = (int)(_rtt * 1000.0 / _frameCount);
				if (rtt != _lastRTT)
				{
					_lastRTT = rtt;
					_rttText.text = rtt > 0 ? rtt.ToString() : "---";
				}

				_frameCount = 0;
				_deltaTime = 0f;
				_rtt = 0.0;
			}

			if (_connectionType != runner.CurrentConnectionType)
			{
				_connectionType = runner.CurrentConnectionType;
				_connectionTypeText.text = _connectionType.ToString();
			}

			if (full == true)
			{
				_sessionNameText.text = runner.SessionInfo.Name;
				_regionText.text = runner.SessionInfo.Region;
			}
		}
	}
}
