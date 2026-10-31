using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Companions.DevBridge;

/// <summary>
/// Keyboard/mouse injection for the DevBridge (development tool). It uses the Windows <c>SendInput</c> API because
/// Unity reads the OS input queue - there is no supported way to fake <c>Input.GetKeyDown</c> from inside the game.
///
/// Safety: every send first brings the game window to the foreground and refuses to type when another window ends up
/// focused, so a request can never type into another application.
/// </summary>
internal static class InputSim
{
	private const int InputMouse = 0;
	private const int InputKeyboard = 1;
	private const uint KeyeventfKeyup = 0x0002;
	private const uint KeyeventfUnicode = 0x0004;
	private const uint MouseeventfMove = 0x0001;
	private const uint MouseeventfLeftdown = 0x0002;
	private const uint MouseeventfLeftup = 0x0004;
	private const uint MouseeventfAbsolute = 0x8000;
	private const int SwRestore = 9;

	[StructLayout(LayoutKind.Sequential)]
	private struct MouseInput
	{
		public int Dx;
		public int Dy;
		public uint MouseData;
		public uint Flags;
		public uint Time;
		public IntPtr ExtraInfo;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct KeybdInput
	{
		public ushort Vk;
		public ushort Scan;
		public uint Flags;
		public uint Time;
		public IntPtr ExtraInfo;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct HardwareInput
	{
		public uint Msg;
		public ushort ParamL;
		public ushort ParamH;
	}

	[StructLayout(LayoutKind.Explicit)]
	private struct InputUnion
	{
		[FieldOffset(0)] public MouseInput Mouse;
		[FieldOffset(0)] public KeybdInput Keyboard;
		[FieldOffset(0)] public HardwareInput Hardware;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct InputEvent
	{
		public uint Type;
		public InputUnion Union;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct Rect
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct Point
	{
		public int X;
		public int Y;
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint SendInput(uint count, InputEvent[] events, int size);

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[DllImport("user32.dll")]
	private static extern bool AttachThreadInput(uint attachFrom, uint attachTo, bool attach);

	[DllImport("user32.dll")]
	private static extern IntPtr SetFocus(IntPtr window);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr window);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr window, int command);

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr window, out Rect rect);

	[DllImport("user32.dll")]
	private static extern bool ClientToScreen(IntPtr window, ref Point point);

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int index);

	private static readonly Dictionary<string, ushort> Special = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
	{
		{ "escape", 0x1B }, { "esc", 0x1B }, { "return", 0x0D }, { "enter", 0x0D }, { "space", 0x20 },
		{ "tab", 0x09 }, { "backspace", 0x08 }, { "delete", 0x2E }, { "insert", 0x2D },
		{ "uparrow", 0x26 }, { "downarrow", 0x28 }, { "leftarrow", 0x25 }, { "rightarrow", 0x27 },
		{ "home", 0x24 }, { "end", 0x23 }, { "pageup", 0x21 }, { "pagedown", 0x22 },
		{ "leftshift", 0xA0 }, { "rightshift", 0xA1 }, { "leftcontrol", 0xA2 }, { "rightcontrol", 0xA3 },
		{ "leftalt", 0xA4 }, { "rightalt", 0xA5 },
		{ "f1", 0x70 }, { "f2", 0x71 }, { "f3", 0x72 }, { "f4", 0x73 }, { "f5", 0x74 }, { "f6", 0x75 },
		{ "f7", 0x76 }, { "f8", 0x77 }, { "f9", 0x78 }, { "f10", 0x79 }, { "f11", 0x7A }, { "f12", 0x7B }
	};

	public static IntPtr GameWindow()
	{
		try
		{
			return Process.GetCurrentProcess().MainWindowHandle;
		}
		catch
		{
			return IntPtr.Zero;
		}
	}

	/// <summary>
	/// Brings the game window to the foreground. Windows refuses <c>SetForegroundWindow</c> from a background process
	/// (the foreground lock), so the calling thread is temporarily attached to the foreground window's thread - the
	/// standard way to steal the focus on purpose. Returns true when the game really has the focus.
	/// </summary>
	public static bool FocusGame()
	{
		IntPtr window = GameWindow();
		if (window == IntPtr.Zero)
		{
			return false;
		}
		// The bridge runs on the Unity main thread, which owns the window: SetFocus here is what actually gives the
		// game the KEYBOARD focus. SetForegroundWindow from another process only makes it the foreground window.
		SetFocus(window);
		if (GetForegroundWindow() == window)
		{
			return true;
		}
		IntPtr foreground = GetForegroundWindow();
		uint foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
		uint myThread = GetCurrentThreadId();
		bool attached = foregroundThread != 0 && foregroundThread != myThread && AttachThreadInput(myThread, foregroundThread, attach: true);
		try
		{
			ShowWindow(window, SwRestore);
			SetForegroundWindow(window);
			SetFocus(window);
		}
		finally
		{
			if (attached)
			{
				AttachThreadInput(myThread, foregroundThread, attach: false);
			}
		}
		return GetForegroundWindow() == window;
	}

	public static bool IsGameFocused()
	{
		IntPtr window = GameWindow();
		return window != IntPtr.Zero && GetForegroundWindow() == window;
	}

	public static ushort VirtualKey(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return 0;
		}
		if (Special.TryGetValue(name, out ushort vk))
		{
			return vk;
		}
		if (name.Length == 1)
		{
			char c = char.ToUpperInvariant(name[0]);
			if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
			{
				return c;
			}
		}
		if (name.Length == 6 && name.StartsWith("Alpha", StringComparison.OrdinalIgnoreCase) && char.IsDigit(name[5]))
		{
			return (ushort)('0' + (name[5] - '0'));
		}
		if (name.Length == 7 && name.StartsWith("Keypad", StringComparison.OrdinalIgnoreCase) && char.IsDigit(name[6]))
		{
			return (ushort)(0x60 + (name[6] - '0'));
		}
		return 0;
	}

	public static bool Tap(ushort vk)
	{
		bool legacy = Send(KeyEvent(vk, up: false), KeyEvent(vk, up: true));
		TapNew(vk);
		return legacy;
	}

	public static bool Down(ushort vk)
	{
		bool legacy = Send(KeyEvent(vk, up: false));
		DownNew(vk);
		return legacy;
	}

	public static bool Up(ushort vk)
	{
		bool legacy = Send(KeyEvent(vk, up: true));
		UpNew();
		return legacy;
	}

	// ---- the game's own input (Valheim 1.0 uses the NEW Input System: ZInput.GetKeyDown reads
	// Keyboard.current[key].wasPressedThisFrame, which SendInput alone does not feed) ----
	private static int _releaseFrame = -1;
	private static int _mouseReleaseFrame = -1;
	private static Vector2 _mousePosition;

	/// <summary>Queues the key press into the new Input System and schedules the release for the next frame, so the
	/// game's Update sees the key down for at least one frame.</summary>
	public static void TapNew(ushort vk)
	{
		Key key = ToKey(vk);
		if (key == Key.None || Keyboard.current == null)
		{
			return;
		}
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
		_releaseFrame = Time.frameCount + 1;
	}

	public static void DownNew(ushort vk)
	{
		Key key = ToKey(vk);
		if (key == Key.None || Keyboard.current == null)
		{
			return;
		}
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
		_releaseFrame = -1;
	}

	public static void UpNew()
	{
		if (Keyboard.current == null)
		{
			return;
		}
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
		_releaseFrame = -1;
	}

	/// <summary>Called every frame by the bridge: releases a tap after the game had a frame to see it.</summary>
	public static void PumpNew()
	{
		if (_releaseFrame >= 0 && Time.frameCount >= _releaseFrame && Keyboard.current != null)
		{
			InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
			_releaseFrame = -1;
		}
		if (_mouseReleaseFrame >= 0 && Time.frameCount >= _mouseReleaseFrame && Mouse.current != null)
		{
			InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = _mousePosition });
			_mouseReleaseFrame = -1;
		}
		if (_lookResetFrame >= 0 && Time.frameCount >= _lookResetFrame && Mouse.current != null)
		{
			InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = _mousePosition });
			_lookResetFrame = -1;
		}
	}

	public static void ClickNew(int x, int y)
	{
		if (Mouse.current == null)
		{
			return;
		}
		_mousePosition = new Vector2(x, y);
		InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = _mousePosition });
		InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = _mousePosition }.WithButton(MouseButton.Left));
		_mouseReleaseFrame = Time.frameCount + 1;
	}

	public static void DownNew(int x, int y)
	{
		if (Mouse.current == null)
		{
			return;
		}
		_mousePosition = new Vector2(x, y);
		InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = _mousePosition }.WithButton(MouseButton.Left));
		_mouseReleaseFrame = -1;
	}

	public static void MouseUpNew()
	{
		if (Mouse.current == null)
		{
			return;
		}
		InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = _mousePosition });
		_mouseReleaseFrame = -1;
	}

	private static int _lookResetFrame = -1;

	/// <summary>Rotates the camera: the game reads &lt;Mouse&gt;/delta (ZInput.GetMouseDelta), so the look is a
	/// one-frame delta event. The delta is zeroed on the next frame (PumpNew), otherwise the camera would keep
	/// turning at that speed.</summary>
	public static void MouseMove(int dx, int dy)
	{
		if (Mouse.current == null)
		{
			return;
		}
		InputSystem.QueueStateEvent(Mouse.current, new MouseState
		{
			position = _mousePosition,
			delta = new Vector2(dx, dy)
		});
		_lookResetFrame = Time.frameCount + 1;
	}

	private static Key ToKey(ushort vk)
	{
		switch (vk)
		{
		case 0x20: return Key.Space;
		case 0x0D: return Key.Enter;
		case 0x1B: return Key.Escape;
		case 0x28: return Key.DownArrow;
		case 0x26: return Key.UpArrow;
		case 0x25: return Key.LeftArrow;
		case 0x27: return Key.RightArrow;
		case 0x09: return Key.Tab;
		case 0x08: return Key.Backspace;
		case 0x2E: return Key.Delete;
		case 0x2D: return Key.Insert;
		case 0x24: return Key.Home;
		case 0x23: return Key.End;
		case 0x21: return Key.PageUp;
		case 0x22: return Key.PageDown;
		case 0xA0: return Key.LeftShift;
		case 0xA1: return Key.RightShift;
		case 0xA2: return Key.LeftCtrl;
		case 0xA3: return Key.RightCtrl;
		case 0xA4: return Key.LeftAlt;
		case 0xA5: return Key.RightAlt;
		}
		if (vk >= 0x70 && vk <= 0x7B)
		{
			return (Key)((int)Key.F1 + (vk - 0x70));
		}
		if (vk >= 'A' && vk <= 'Z')
		{
			return (Key)((int)Key.A + (vk - 'A'));
		}
		if (vk == '0')
		{
			return Key.Digit0;
		}
		if (vk >= '1' && vk <= '9')
		{
			return (Key)((int)Key.Digit1 + (vk - '1'));
		}
		return Key.None;
	}

	/// <summary>Types the text as Unicode characters (works for accents and any layout).</summary>
	public static bool Text(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		List<InputEvent> events = new List<InputEvent>(text.Length * 2);
		foreach (char c in text)
		{
			events.Add(UnicodeEvent(c, up: false));
			events.Add(UnicodeEvent(c, up: true));
		}
		return Send(events.ToArray());
	}

	/// <summary>
	/// Clicks at a <b>client</b> coordinate (the same pixels as the screenshot). The point is mapped with
	/// <c>ClientToScreen</c>, so the window border and title bar are accounted for. Down and up are sent in the same
	/// batch - Unity's EventSystem can miss a click whose press and release land in the same frame, so use
	/// <see cref="MouseDown"/> + <see cref="MouseUp"/> (with a small delay between them) when a click must land.
	/// </summary>
	public static bool Click(int x, int y)
	{
		ClickNew(x, y);
		if (!ClientToAbsolute(x, y, out int nx, out int ny))
		{
			return false;
		}
		return Send(MouseEvent(nx, ny, MouseeventfMove | MouseeventfAbsolute),
			MouseEvent(nx, ny, MouseeventfLeftdown),
			MouseEvent(nx, ny, MouseeventfLeftup));
	}

	public static bool MouseDown(int x, int y)
	{
		DownNew(x, y);
		if (!ClientToAbsolute(x, y, out int nx, out int ny))
		{
			return false;
		}
		return Send(MouseEvent(nx, ny, MouseeventfMove | MouseeventfAbsolute),
			MouseEvent(nx, ny, MouseeventfLeftdown));
	}

	public static bool MouseUp(int x, int y)
	{
		MouseUpNew();
		if (!ClientToAbsolute(x, y, out int nx, out int ny))
		{
			return false;
		}
		return Send(MouseEvent(nx, ny, MouseeventfLeftup));
	}

	private static bool ClientToAbsolute(int x, int y, out int nx, out int ny)
	{
		nx = 0;
		ny = 0;
		IntPtr window = GameWindow();
		if (window == IntPtr.Zero)
		{
			return false;
		}
		Point point = new Point { X = x, Y = y };
		if (!ClientToScreen(window, ref point))
		{
			return false;
		}
		int screenWidth = Math.Max(1, GetSystemMetrics(0));
		int screenHeight = Math.Max(1, GetSystemMetrics(1));
		nx = (int)(Math.Max(0, Math.Min(screenWidth - 1, point.X)) * 65535.0 / (screenWidth - 1));
		ny = (int)(Math.Max(0, Math.Min(screenHeight - 1, point.Y)) * 65535.0 / (screenHeight - 1));
		return true;
	}

	private static InputEvent KeyEvent(ushort vk, bool up)
	{
		return new InputEvent
		{
			Type = InputKeyboard,
			Union = new InputUnion
			{
				Keyboard = new KeybdInput
				{
					Vk = vk,
					Flags = up ? KeyeventfKeyup : 0u
				}
			}
		};
	}

	private static InputEvent UnicodeEvent(char c, bool up)
	{
		return new InputEvent
		{
			Type = InputKeyboard,
			Union = new InputUnion
			{
				Keyboard = new KeybdInput
				{
					Scan = c,
					Flags = KeyeventfUnicode | (up ? KeyeventfKeyup : 0u)
				}
			}
		};
	}

	private static InputEvent MouseEvent(int dx, int dy, uint flags)
	{
		return new InputEvent
		{
			Type = InputMouse,
			Union = new InputUnion
			{
				Mouse = new MouseInput
				{
					Dx = dx,
					Dy = dy,
					Flags = flags
				}
			}
		};
	}

	private static bool Send(params InputEvent[] events)
	{
		if (events == null || events.Length == 0)
		{
			return false;
		}
		return SendInput((uint)events.Length, events, Marshal.SizeOf(typeof(InputEvent))) == events.Length;
	}
}
