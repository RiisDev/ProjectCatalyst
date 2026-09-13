using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ProjectCatalyst.Services
{
	public enum GamepadButton
	{
		None,
		DPadLeft,
		DPadRight,
		DPadUp,
		DPadDown,
		Accept,   // A on Xbox pads / Cross on PlayStation pads
		Back,     // B on Xbox pads / Circle on PlayStation pads
		Start
	}

	public sealed class GamepadService : IDisposable
	{
		private const float LeftStickDeadzone = 0.35f;
		private const int MaxConsecutiveFailures = 3;

		[Flags]
		private enum XInputGamepadButtons : ushort
		{
			DPadUp = 0x0001,
			DPadDown = 0x0002,
			DPadLeft = 0x0004,
			DPadRight = 0x0008,
			Start = 0x0010,
			A = 0x1000,
			B = 0x2000,
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct XInputGamepad
		{
			public XInputGamepadButtons wButtons;
			public byte bLeftTrigger;
			public byte bRightTrigger;
			public short sThumbLX;
			public short sThumbLY;
			public short sThumbRX;
			public short sThumbRY;
		}

		// Mirrors native XINPUT_STATE (DWORD + XINPUT_GAMEPAD = 16 bytes).
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct XInputState
		{
			public uint dwPacketNumber;
			public XInputGamepad Gamepad;
		}

		[DllImport("xinput1_4.dll", EntryPoint = "XInputGetState", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
		private static extern uint XInputGetState(uint dwUserIndex, out XInputState pState);

		private readonly DispatcherTimer _timer;
		private XInputGamepadButtons _previousButtons;
		private bool _previousStickLeft;
		private bool _previousStickRight;
		private int _consecutiveFailureCount;

		public event EventHandler<GamepadButton>? ButtonPressed;

		public event EventHandler<string>? PollingDisabled;

		public bool IsControllerConnected { get; private set; }

		public GamepadService(TimeSpan? pollInterval = null)
		{
			_timer = new DispatcherTimer(DispatcherPriority.Background)
			{
				Interval = pollInterval ?? TimeSpan.FromMilliseconds(80)
			};
			_timer.Tick += (_, _) => Poll();
		}

		public void Start() => _timer.Start();

		public void Stop() => _timer.Stop();

		private void Poll()
		{
			uint result;
			XInputState state;

			try
			{
				result = XInputGetState(0, out state);
			}
			catch (DllNotFoundException)
			{
				Disable("xinput1_4.dll was not found on this system.");
				return;
			}
			catch (EntryPointNotFoundException)
			{
				Disable("XInputGetState entry point was not found in xinput1_4.dll.");
				return;
			}
			catch (Exception ex)
			{
				_consecutiveFailureCount++;
				if (_consecutiveFailureCount >= MaxConsecutiveFailures)
				{
					Disable($"XInputGetState failed {MaxConsecutiveFailures} times in a row: {ex.Message}");
				}
				return;
			}

			_consecutiveFailureCount = 0;

			const uint errorSuccess = 0;
			IsControllerConnected = result == errorSuccess;
			if (!IsControllerConnected) return;

			XInputGamepadButtons buttons = state.Gamepad.wButtons;
			RaiseOnEdge(buttons, XInputGamepadButtons.DPadLeft, GamepadButton.DPadLeft);
			RaiseOnEdge(buttons, XInputGamepadButtons.DPadRight, GamepadButton.DPadRight);
			RaiseOnEdge(buttons, XInputGamepadButtons.DPadUp, GamepadButton.DPadUp);
			RaiseOnEdge(buttons, XInputGamepadButtons.DPadDown, GamepadButton.DPadDown);
			RaiseOnEdge(buttons, XInputGamepadButtons.A, GamepadButton.Accept);
			RaiseOnEdge(buttons, XInputGamepadButtons.B, GamepadButton.Back);
			RaiseOnEdge(buttons, XInputGamepadButtons.Start, GamepadButton.Start);

			float normalizedX = state.Gamepad.sThumbLX / 32767f;
			bool stickLeft = normalizedX < -LeftStickDeadzone;
			bool stickRight = normalizedX > LeftStickDeadzone;

			if (stickLeft && !_previousStickLeft) ButtonPressed?.Invoke(this, GamepadButton.DPadLeft);
			if (stickRight && !_previousStickRight) ButtonPressed?.Invoke(this, GamepadButton.DPadRight);
			_previousStickLeft = stickLeft;
			_previousStickRight = stickRight;

			_previousButtons = buttons;
		}

		private void Disable(string reason)
		{
			LogError($"Polling failed: {reason}");
			IsControllerConnected = false;
			_timer.Stop();
			PollingDisabled?.Invoke(this, reason);
		}

		private void RaiseOnEdge(XInputGamepadButtons current, XInputGamepadButtons flag, GamepadButton mapped)
		{
			bool wasDown = _previousButtons.HasFlag(flag);
			bool isDown = current.HasFlag(flag);
			if (isDown && !wasDown)
			{
				ButtonPressed?.Invoke(this, mapped);
			}
		}

		public void Dispose() => _timer.Stop();
	}
}
