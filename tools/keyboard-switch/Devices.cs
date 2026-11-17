using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NevermorKeyboardSwitch
{
    internal enum DeviceState { Unavailable, Started, Disabled }

    internal sealed class Collection
    {
        internal readonly string Name, Instance, UsbDevice;
        internal readonly DeviceState State;

        internal Collection(string name, string instance, string usbDevice, DeviceState state)
        {
            Name = name;
            Instance = instance;
            UsbDevice = usbDevice;
            State = state;
        }
    }

    internal sealed class DeviceScan
    {
        internal readonly Collection[] Keyboards;
        internal readonly bool Complete;
        internal readonly bool DisplayStarted;
        internal readonly string Message;

        internal DeviceScan(Collection[] keyboards, bool complete, bool displayStarted, string message)
        {
            Keyboards = keyboards;
            Complete = complete;
            DisplayStarted = displayStarted;
            Message = message;
        }
    }

    internal static class Devices
    {
        internal static readonly string[] KeyboardNames =
            { "MI_00", "MI_01&COL04", "MI_01&COL05", "MI_01&COL06" };

        private const string HardwareId = "VID_1A2C&PID_4E84";
        private const string DisplayName = "MI_01&COL07";
        private const uint PresentAllClasses = 0x02 | 0x04;
        private const uint SuppressDisableUi = 0x04;
        private const uint PersistDisable = 0x08;
        private static readonly Guid KeyboardClass = new Guid("4d36e96b-e325-11ce-bfc1-08002be10318");
        private static readonly Guid HidClass = new Guid("745a17a0-74d3-11d0-b6fe-00a0c90f57da");
        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        internal static string Match(string instance, Guid deviceClass)
        {
            string[] parts = instance.ToUpperInvariant().Split('\\');
            string prefix = HardwareId + "&";
            if (parts.Length != 3 || parts[0] != "HID" || parts[2].Length == 0 ||
                !parts[1].StartsWith(prefix, StringComparison.Ordinal))
                return null;

            string name = parts[1].Substring(prefix.Length);
            if (deviceClass == KeyboardClass && KeyboardNames.Contains(name))
                return name;
            return deviceClass == HidClass && name == DisplayName ? name : null;
        }

        internal static DeviceScan Scan()
        {
            var collections = new List<Collection>();
            IntPtr set = SetupDiGetClassDevs(IntPtr.Zero, "HID", IntPtr.Zero, PresentAllClasses);
            if (set == InvalidHandle) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                for (uint index = 0; ; index++)
                {
                    var entry = new DeviceInfo { Size = Marshal.SizeOf(typeof(DeviceInfo)) };
                    if (!SetupDiEnumDeviceInfo(set, index, ref entry))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == 259) break; // ERROR_NO_MORE_ITEMS
                        throw new Win32Exception(error);
                    }

                    string instance = InstanceId(entry.Node);
                    string name = Match(instance, entry.Class);
                    if (name == null) continue;

                    uint usbInterface, usbDevice;
                    if (CM_Get_Parent(out usbInterface, entry.Node, 0) != 0 ||
                        CM_Get_Parent(out usbDevice, usbInterface, 0) != 0)
                        continue;
                    string usbDeviceId = InstanceId(usbDevice);
                    string interfacePrefix = "USB\\" + HardwareId + "&" + name.Substring(0, 5) + "\\";
                    if (!InstanceId(usbInterface).StartsWith(interfacePrefix, StringComparison.OrdinalIgnoreCase) ||
                        !usbDeviceId.StartsWith("USB\\" + HardwareId + "\\", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string usage = name == DisplayName ? "HID_DEVICE_UP:FF01_U:0001" : "HID_DEVICE_SYSTEM_KEYBOARD";
                    var hardwareIds = new byte[4096];
                    uint propertyType, length;
                    if (!SetupDiGetDeviceRegistryProperty(set, ref entry, 1, out propertyType,
                        hardwareIds, (uint)hardwareIds.Length, out length) || propertyType != 7 ||
                        !Encoding.Unicode.GetString(hardwareIds, 0, (int)length).Split('\0')
                            .Contains(usage, StringComparer.OrdinalIgnoreCase))
                        continue;

                    collections.Add(new Collection(name, instance, usbDeviceId, StateOf(entry.Node)));
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(set);
            }

            var displays = collections.Where(item => item.Name == DisplayName).ToArray();
            if (displays.Length != 1)
            {
                string message = displays.Length == 0 ? "Дисплей Q590-SD не найден." :
                    "Найдено несколько дисплеев. Подключите один Q590-SD.";
                return new DeviceScan(new Collection[0], false, false, message);
            }

            var display = displays[0];
            var keyboards = KeyboardNames.SelectMany(name => collections.Where(item => item.Name == name &&
                string.Equals(item.UsbDevice, display.UsbDevice, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (keyboards.Length != KeyboardNames.Length || keyboards.Select(item => item.Name).Distinct().Count() != KeyboardNames.Length)
                return new DeviceScan(keyboards, false, display.State == DeviceState.Started, "Для этой ревизии не подтверждены четыре клавиатурные записи.");

            return new DeviceScan(keyboards, true, display.State == DeviceState.Started,
                display.State == DeviceState.Started ? null : "Windows не запустила HID-канал дисплея.");
        }

        internal static string Change(bool enable)
        {
            using (var mutex = new Mutex(false, @"Local\NevermorQ590KeyboardSwitch"))
            {
                try
                {
                    if (!mutex.WaitOne(0)) return "Операция уже выполняется в другом окне.";
                }
                catch (AbandonedMutexException)
                {
                    // The previous owner exited. Read the current device states below.
                }

                try
                {
                    DeviceScan scan = Scan();
                    if (!scan.Complete || (!enable && !scan.DisplayStarted)) return scan.Message;
                    var errors = new List<string>();
                    bool restart = false;
                    foreach (Collection keyboard in scan.Keyboards)
                    {
                        uint node;
                        uint result = CM_Locate_DevNode(out node, keyboard.Instance, 0);
                        if (result == 0)
                        {
                            DeviceState state = StateOf(node);
                            if (state == DeviceState.Unavailable)
                            {
                                errors.Add(keyboard.Name + ": состояние недоступно.");
                                continue;
                            }
                            if (state == (enable ? DeviceState.Started : DeviceState.Disabled))
                                continue;

                            // Use a polite disable and keep it across Windows restarts.
                            result = enable ? CM_Enable_DevNode(node, 0) :
                                CM_Disable_DevNode(node, PersistDisable | SuppressDisableUi);
                        }
                        if (result == 0x22) restart = true; // CR_NEED_RESTART
                        else if (result != 0) errors.Add(keyboard.Name + ": " + FormatError(result));
                    }

                    if (restart) errors.Add("Для завершения операции требуется перезагрузка Windows.");
                    return errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        internal static string StateText(DeviceState state)
        {
            switch (state)
            {
                case DeviceState.Started: return "Включена";
                case DeviceState.Disabled: return "Отключена";
                default: return "Недоступна";
            }
        }

        private static string FormatError(uint result)
        {
            return new Win32Exception((int)CM_MapCrToWin32Err(result, 31)).Message +
                " (CONFIGRET 0x" + result.ToString("X2") + ")";
        }

        private static DeviceState StateOf(uint node)
        {
            uint status, problem;
            if (CM_Get_DevNode_Status(out status, out problem, node, 0) != 0)
                return DeviceState.Unavailable;
            if ((status & 0x400) != 0)
                return problem == 22 ? DeviceState.Disabled : DeviceState.Unavailable;
            return (status & 8) != 0 ? DeviceState.Started : DeviceState.Unavailable;
        }

        private static string InstanceId(uint node)
        {
            var id = new StringBuilder(256);
            return CM_Get_Device_ID(node, id, id.Capacity, 0) == 0 ? id.ToString() : string.Empty;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DeviceInfo
        {
            internal int Size;
            internal Guid Class;
            internal uint Node;
            internal IntPtr Reserved;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref DeviceInfo info);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr set, ref DeviceInfo info, uint property,
            out uint type, [Out] byte[] buffer, uint size, out uint requiredSize);
        [DllImport("setupapi.dll")]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Get_Parent(out uint parent, uint node, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern uint CM_Get_Device_ID(uint node, StringBuilder id, int capacity, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern uint CM_Locate_DevNode(out uint node, string instance, uint flags);
        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Get_DevNode_Status(out uint status, out uint problem, uint node, uint flags);
        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Disable_DevNode(uint node, uint flags);
        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Enable_DevNode(uint node, uint flags);
        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_MapCrToWin32Err(uint result, uint defaultError);
    }
}
