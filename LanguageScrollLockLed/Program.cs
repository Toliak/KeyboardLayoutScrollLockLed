using Microsoft.Win32.SafeHandles;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace LanguageScrollLockLed
{
    class Program
    {
        unsafe static class FlashLights
        {
            const uint GENERIC_WRITE = 0x40000000;
            const uint OPEN_EXISTING = 0x3;

            const uint IOCTL_KEYBOARD_QUERY_INDICATORS = 0xB0040;
            const uint IOCTL_KEYBOARD_SET_INDICATORS = 0xB0008;

            enum DOS_DEVICES : uint
            {
                RAW_TARGET_PATH = 0x00000001,
                REMOVE_DEFINITION = 0x00000002,
                EXACT_MATCH_ON_REMOVE = 0x00000004,
                NO_BROADCAST_SYSTEM = 0x00000008,
                LUID_BROADCAST_DRIVE = 0x00000010
            }

            public enum KEYBOARD_KEY : uint
            {
                CAPS_LOCK_ON = 4,
                NUM_LOCK_ON = 2,
                SCROLL_LOCK_ON = 1
            }

            struct KEYBOARD_INDICATOR_PARAMETERS
            {
                internal ushort UnitId;     // Unit identifier.
                internal ushort LedFlags;   // LED indicator state.
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool DefineDosDevice(
                [In] DOS_DEVICES dwFlags,
                [In] string lpDeviceName,
                [In] string lpTargetPath
                );

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern IntPtr CreateFile(
                [In] string lpFileName,
                [In] uint dwDesiredAccess,
                [In] uint dwShareMode,
                [In] IntPtr lpSecurityAttributes,
                [In] uint dwCreationDisposition,
                [In] uint dwFlagsAndAttributes,
                [In] IntPtr hTemplateFile
                );

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool CloseHandle([In] IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool DeviceIoControl(
                [In] IntPtr hDevice,
                [In] uint dwIoControlCode,
                [In] KEYBOARD_INDICATOR_PARAMETERS* lpInBuffer,
                [In] uint nInBufferSize,
                [Out] KEYBOARD_INDICATOR_PARAMETERS* lpOutBuffer,
                [In] uint nOutBufferSize,
                [Out] uint* lpBytesReturned,
                [In, Out] IntPtr lpOverlapped
                );

            static IntPtr hKbdDev;
            /// <summary>
            /// Должно вызываться первым для того чтобы подготовить устройство для использования.
            /// </summary>
            /// <returns>Номер ошибки.</returns>
            public static int OpenKeyboardDevice()
            {
                if (!DefineDosDevice(DOS_DEVICES.RAW_TARGET_PATH, "Kbd000001", "\\Device\\KeyboardClass24"))
                    return Marshal.GetLastWin32Error();

                hKbdDev = CreateFile("\\\\.\\Kbd000001", GENERIC_WRITE, 1U, IntPtr.Zero, OPEN_EXISTING, 0U, IntPtr.Zero);

                if (hKbdDev == (IntPtr)(-1))
                    return Marshal.GetLastWin32Error();

                return 0;
            }
            /// <summary>
            /// Закрывает устройство.
            /// </summary>
            /// <returns>Номер ошибки.</returns>
            public static int Close()
            {
                int err = 0;

                if (!DefineDosDevice(DOS_DEVICES.REMOVE_DEFINITION, "Kbd000001", null))
                    err = Marshal.GetLastWin32Error();

                if (!CloseHandle(hKbdDev))
                    err = Marshal.GetLastWin32Error();

                return err;
            }
            /// <summary>
            /// Мигание одной из 3х лампочек: NUM, SCROLL, CAPS.
            /// </summary>
            /// <param name="LightFlag">Индикатор.</param>
            /// <param name="Duration">Промежуток времени между затуханием и появлением.</param>
            /// <returns>Номер ошибки.</returns>
            public static int FlashKeyboardLight(uint LightFlag, int Duration, uint toggle)
            {
                KEYBOARD_INDICATOR_PARAMETERS
                    inBuff = new KEYBOARD_INDICATOR_PARAMETERS(),
                    outBuff = new KEYBOARD_INDICATOR_PARAMETERS();

                uint LedFlag = (uint)LightFlag;
                uint ledFlagsMask;
                //uint toggle;
                uint dataLen = (uint)Marshal.SizeOf(typeof(KEYBOARD_INDICATOR_PARAMETERS));
                uint retLength = 0;

                inBuff.UnitId = 0;
                outBuff.UnitId = 0;

                if (!DeviceIoControl(hKbdDev, IOCTL_KEYBOARD_QUERY_INDICATORS,
                    &inBuff, dataLen, &outBuff, dataLen, &retLength, IntPtr.Zero))
                    return Marshal.GetLastWin32Error();

                ledFlagsMask = (outBuff.LedFlags & (~LedFlag));
                //toggle = (outBuff.LedFlags & LedFlag);

                //for (int i = 0; i < 2; i++)
                //{
                    //toggle ^= 1;
                    inBuff.LedFlags = (ushort)(ledFlagsMask | (LedFlag * toggle));

                    if (!DeviceIoControl(hKbdDev, IOCTL_KEYBOARD_SET_INDICATORS,
                        &inBuff, dataLen, null, 0, &retLength, IntPtr.Zero))
                        return Marshal.GetLastWin32Error();

                    //System.Threading.Thread.Sleep(Duration);
               // }

                return 0;
            }
        }


        // 


        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, IntPtr proccess);
        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);
        static public CultureInfo GetCurrentKeyboardLayout()
        {
            try
            {
                IntPtr foregroundWindow = GetForegroundWindow();
                uint foregroundProcess = GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
                int keyboardLayout = GetKeyboardLayout(foregroundProcess).ToInt32() & 0xFFFF;
                return new CultureInfo(keyboardLayout);
            }
            catch (Exception _)
            {
                return new CultureInfo(1033); // Assume English if something went wrong.
            }
        }

        static void Main(string[] args)
        {
            FlashLights.OpenKeyboardDevice();
            while (true)
            {
                var layout = GetCurrentKeyboardLayout();
                string layoutString = layout.ToString();
                Console.WriteLine(layoutString);

                if (layoutString == "en-US")
                {
                    
                    FlashLights.FlashKeyboardLight(1, 1500, 0);
                }
                else
                {
                    FlashLights.FlashKeyboardLight(1, 1500, 1);
                    
                }
                Thread.Sleep(25);
            }
            FlashLights.Close();
        }
    }
}
