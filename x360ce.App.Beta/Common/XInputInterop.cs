using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace x360ce.App
{
    /// <summary>
    /// Direct P/Invoke against the real System32\xinput1_4.dll.
    /// Safe alongside Xidi because Xidi only replaces dinput8.dll.
    /// XInput has no cooperative level, no window handle, no acquisition.
    /// Vibration works regardless of foreground state.
    /// </summary>
    public static class XInputInterop
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct XINPUT_VIBRATION
        {
            public ushort wLeftMotorSpeed;
            public ushort wRightMotorSpeed;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
        private static extern uint _XInputSetState(
            uint dwUserIndex, ref XINPUT_VIBRATION pVibration);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint _XInputGetState(
            uint dwUserIndex, ref XINPUT_STATE pState);

        private const uint ERROR_SUCCESS = 0;

        /// <summary>
        /// Set vibration on physical XInput controller.
        /// Motor values 0-255 (ViGEmBus byte scale).
        /// </summary>
        public static bool SetVibration(uint userIndex,
            byte largeMotor, byte smallMotor)
        {
            var vib = new XINPUT_VIBRATION
            {
                wLeftMotorSpeed = (ushort)(largeMotor * 257),
                wRightMotorSpeed = (ushort)(smallMotor * 257)
            };
            return _XInputSetState(userIndex, ref vib) == ERROR_SUCCESS;
        }

        /// <summary>
        /// Stop all vibration on the given controller.
        /// </summary>
        public static bool StopVibration(uint userIndex)
        {
            return SetVibration(userIndex, 0, 0);
        }

        public static bool IsConnected(uint userIndex)
        {
            var state = new XINPUT_STATE();
            return _XInputGetState(userIndex, ref state) == ERROR_SUCCESS;
        }

        /// <summary>
        /// Detect whether a DirectInput product GUID is an XInput device
        /// exposed through Microsoft's XInput-to-DirectInput shim.
        /// The last 6 bytes of the GUID spell "PIDVID" in ASCII.
        /// </summary>
        public static bool IsXInputDeviceViaProductGuid(Guid productGuid)
        {
            byte[] b = productGuid.ToByteArray();
            return b[10] == 0x50   // P
                && b[11] == 0x49   // I
                && b[12] == 0x44   // D
                && b[13] == 0x56   // V
                && b[14] == 0x49   // I
                && b[15] == 0x44;  // D
        }

        /// <summary>
        /// Returns all connected XInput slot indices (0-3).
        /// </summary>
        public static List<uint> GetConnectedSlots()
        {
            var result = new List<uint>();
            for (uint i = 0; i < 4; i++)
            {
                if (IsConnected(i))
                    result.Add(i);
            }
            return result;
        }
    }
}
