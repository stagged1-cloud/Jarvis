using Jarvis.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Jarvis.Input;

/// <summary>
/// Input control service using Win32 SendInput API
/// </summary>
public class InputControlService : IInputControlService
{
    private readonly ILogger<InputControlService> _logger;

    public InputControlService(ILogger<InputControlService> logger)
    {
        _logger = logger;
    }

    public void MoveMouse(int x, int y)
    {
        try
        {
            SetCursorPos(x, y);
            _logger.LogDebug($"Moved mouse to ({x}, {y})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error moving mouse to ({x}, {y})");
        }
    }

    public void ClickMouse()
    {
        try
        {
            // Left button down
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            Thread.Sleep(10);
            // Left button up
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            
            _logger.LogDebug("Mouse clicked");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clicking mouse");
        }
    }

    public void TypeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _logger.LogWarning("Attempted to type empty text");
            return;
        }

        try
        {
            foreach (char c in text)
            {
                SendChar(c);
                Thread.Sleep(10); // Small delay between keystrokes
            }
            
            _logger.LogInformation($"Typed text: {text}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error typing text");
        }
    }

    public void PressKey(string key)
    {
        try
        {
            var vk = GetVirtualKeyCode(key);
            if (vk != 0)
            {
                keybd_event((byte)vk, 0, 0, 0); // Key down
                Thread.Sleep(10);
                keybd_event((byte)vk, 0, KEYEVENTF_KEYUP, 0); // Key up
                
                _logger.LogDebug($"Pressed key: {key}");
            }
            else
            {
                _logger.LogWarning($"Unknown key: {key}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error pressing key: {key}");
        }
    }

    private void SendChar(char c)
    {
        short vk = VkKeyScan(c);
        byte virtualKey = (byte)(vk & 0xFF);
        bool shift = (vk & 0x100) != 0;

        if (shift)
        {
            keybd_event(VK_SHIFT, 0, 0, 0);
        }

        keybd_event(virtualKey, 0, 0, 0);
        keybd_event(virtualKey, 0, KEYEVENTF_KEYUP, 0);

        if (shift)
        {
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
        }
    }

    private int GetVirtualKeyCode(string key)
    {
        return key.ToUpperInvariant() switch
        {
            "ENTER" => VK_RETURN,
            "ESC" => VK_ESCAPE,
            "TAB" => VK_TAB,
            "SPACE" => VK_SPACE,
            "BACKSPACE" => VK_BACK,
            "DELETE" => VK_DELETE,
            "LEFT" => VK_LEFT,
            "RIGHT" => VK_RIGHT,
            "UP" => VK_UP,
            "DOWN" => VK_DOWN,
            "HOME" => VK_HOME,
            "END" => VK_END,
            "PAGEUP" => VK_PRIOR,
            "PAGEDOWN" => VK_NEXT,
            _ => 0
        };
    }

    #region Win32 API

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const byte VK_SHIFT = 0x10;
    private const int VK_RETURN = 0x0D;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_TAB = 0x09;
    private const int VK_SPACE = 0x20;
    private const int VK_BACK = 0x08;
    private const int VK_DELETE = 0x2E;
    private const int VK_LEFT = 0x25;
    private const int VK_UP = 0x26;
    private const int VK_RIGHT = 0x27;
    private const int VK_DOWN = 0x28;
    private const int VK_HOME = 0x24;
    private const int VK_END = 0x23;
    private const int VK_PRIOR = 0x21; // Page Up
    private const int VK_NEXT = 0x22; // Page Down

    #endregion
}
