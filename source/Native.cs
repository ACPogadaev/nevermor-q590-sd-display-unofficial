using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace NevermorDisplay {
    internal static class Native {
        [StructLayout(LayoutKind.Sequential)] internal struct InterfaceData { public int Size; public Guid Guid; public int Flags; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)] internal struct HidAttributes { public int Size; public ushort Vid, Pid, Version; }
        [StructLayout(LayoutKind.Sequential)] internal struct HidCaps {
            public ushort Usage, UsagePage, InputLength, OutputLength, FeatureLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst=17)] public ushort[] Reserved;
            public ushort Links, InputButtons, InputValues, InputData, OutputButtons, OutputValues, OutputData, FeatureButtons, FeatureValues, FeatureData;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct MemoryStatus { public uint Length, Load; public ulong TotalPhys, AvailPhys, TotalPage, AvailPage, TotalVirtual, AvailVirtual, AvailExtended; }
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
        [DllImport("hid.dll")] internal static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("hid.dll", SetLastError=true)] [return:MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_GetAttributes(SafeFileHandle handle,ref HidAttributes attributes);
        [DllImport("hid.dll", SetLastError=true)] [return:MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_GetPreparsedData(SafeFileHandle handle,out IntPtr data);
        [DllImport("hid.dll")] [return:MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] internal static extern int HidP_GetCaps(IntPtr data,out HidCaps caps);
        [DllImport("hid.dll")] internal static extern int HidP_GetValueCaps(int type,IntPtr values,ref ushort count,IntPtr data);
        [DllImport("hid.dll", SetLastError=true)] [return:MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_GetProductString(SafeFileHandle handle,IntPtr value,uint length);
        [DllImport("hid.dll", SetLastError=true)] [return:MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_SetFeature(SafeFileHandle handle,byte[] report,int length);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern IntPtr SetupDiGetClassDevs(ref Guid guid,string enumerator,IntPtr window,uint flags);
        [DllImport("setupapi.dll",SetLastError=true)] internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr device,ref Guid guid,uint index,ref InterfaceData iface);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref InterfaceData iface,IntPtr detail,uint length,out uint required,IntPtr device);
        [DllImport("setupapi.dll")] internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll")] internal static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        [DllImport("kernel32.dll")] internal static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern IntPtr OpenFileMapping(uint access,bool inherit,string name);
        [DllImport("kernel32.dll",SetLastError=true)] internal static extern IntPtr MapViewOfFile(IntPtr handle,uint access,uint hi,uint low,UIntPtr length);
        [DllImport("kernel32.dll")] internal static extern bool UnmapViewOfFile(IntPtr address);
        [DllImport("kernel32.dll")] internal static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")] internal static extern bool SetProcessWorkingSetSize(IntPtr process,IntPtr min,IntPtr max);
        internal static bool Admin { get { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } }
    }

    internal sealed class DisplayDevice { public string Path,Name; public int ReportLength; public override string ToString(){return Name;} }

    internal sealed class DisplayConnection : IDisposable {
        private SafeFileHandle handle;
        private int length;
        private DateTime nextScan=DateTime.MinValue;
        public string Status="Дисплей ещё не подключён";
        public static List<DisplayDevice> Find() {
            var results=new List<DisplayDevice>(); Guid guid; Native.HidD_GetHidGuid(out guid);
            IntPtr set=Native.SetupDiGetClassDevs(ref guid,null,IntPtr.Zero,0x12);
            if(set==new IntPtr(-1))return results;
            try {
                for(uint i=0;;i++) {
                    var iface=new Native.InterfaceData();iface.Size=Marshal.SizeOf(typeof(Native.InterfaceData));
                    if(!Native.SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,i,ref iface))break;
                    uint size;Native.SetupDiGetDeviceInterfaceDetail(set,ref iface,IntPtr.Zero,0,out size,IntPtr.Zero);
                    if(size<8 || size>65536)continue;
                    IntPtr buf=Marshal.AllocHGlobal((int)size);
                    try {
                        Marshal.WriteInt32(buf,IntPtr.Size==8?8:6);
                        if(!Native.SetupDiGetDeviceInterfaceDetail(set,ref iface,buf,size,out size,IntPtr.Zero))continue;
                        string path=Marshal.PtrToStringUni(IntPtr.Add(buf,4));
                        if(path==null || path.IndexOf("vid_1a2c&pid_4e84",StringComparison.OrdinalIgnoreCase)<0)continue;
                        using(var h=Native.CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
                            if(h.IsInvalid)continue;
                            var attr=new Native.HidAttributes();attr.Size=Marshal.SizeOf(typeof(Native.HidAttributes));
                            if(!Native.HidD_GetAttributes(h,ref attr)||attr.Vid!=0x1A2C||attr.Pid!=0x4E84)continue;
                            IntPtr pp;
                            if(!Native.HidD_GetPreparsedData(h,out pp))continue;
                            try {
                                Native.HidCaps caps;
                                if(Native.HidP_GetCaps(pp,out caps)!=0x110000||caps.UsagePage!=0xFF01||caps.Usage!=1||caps.FeatureLength<7)continue;
                                ushort count=caps.FeatureValues;
                                if(count==0 || count>64)continue;
                                IntPtr values=Marshal.AllocHGlobal(count*72);
                                bool report7=false;
                                try {
                                    if(Native.HidP_GetValueCaps(2,values,ref count,pp)==0x110000)
                                        for(int n=0;n<count;n++)if(Marshal.ReadByte(values,n*72+2)==7)report7=true;
                                }finally{Marshal.FreeHGlobal(values);}
                                if(!report7)continue;
                                IntPtr name=Marshal.AllocHGlobal(512);
                                string product="USB HID";
                                try{if(Native.HidD_GetProductString(h,name,512))product=Marshal.PtrToStringUni(name);}finally{Marshal.FreeHGlobal(name);}
                                results.Add(new DisplayDevice{Path=path,Name="Q590 · "+product.Trim(),ReportLength=caps.FeatureLength});
                            }finally{Native.HidD_FreePreparsedData(pp);}
                        }
                    }finally{Marshal.FreeHGlobal(buf);}
                }
            }finally{Native.SetupDiDestroyDeviceInfoList(set);}
            return results;
        }
        internal static byte[] Encode(int lower,int upper,int length) {
            if(lower<0||lower>99||upper<0||upper>9999)throw new ArgumentOutOfRangeException("Значение не помещается на дисплее");
            if(length<7||length>256)throw new ArgumentOutOfRangeException("length");
            var report=new byte[length];report[0]=7;
            report[1]=(byte)(lower/10);report[2]=(byte)(lower%10);
            report[3]=(byte)(upper/1000);report[4]=(byte)((upper/100)%10);report[5]=(byte)((upper/10)%10);report[6]=(byte)(upper%10);
            return report;
        }
        public bool Send(int lower,int upper) {
            if(handle==null||handle.IsClosed||handle.IsInvalid) {
                if(DateTime.UtcNow<nextScan)return false;
                nextScan=DateTime.UtcNow.AddSeconds(10);
                List<DisplayDevice> devices=Find();
                if(devices.Count==0){Status="Дисплей не найден. Проверьте USB-подключение.";return false;}
                if(devices.Count!=1){Status="Найдено несколько одинаковых USB-интерфейсов. Передача приостановлена.";return false;}
                handle=Native.CreateFile(devices[0].Path,0xC0000000,3,IntPtr.Zero,3,0,IntPtr.Zero);
                if(handle.IsInvalid){Status="Нет доступа к дисплею: "+new Win32Exception(Marshal.GetLastWin32Error()).Message;Close();return false;}
                length=devices[0].ReportLength;
            }
            byte[] report=Encode(lower,upper,length);
            if(Native.HidD_SetFeature(handle,report,report.Length)){Status="Данные переданы дисплею";return true;}
            int error=Marshal.GetLastWin32Error();
            Status="USB: "+new Win32Exception(error).Message+" ("+error+")";Close();return false;
        }
        public void Close(){if(handle!=null){handle.Dispose();handle=null;}}
        public void Dispose(){Close();}
    }

    internal sealed class Nvidia : IDisposable {
        [StructLayout(LayoutKind.Sequential)] private struct Utilization { public uint Gpu,Memory; }
        [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int nvmlInit_v2();
        [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int nvmlShutdown();
        [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int nvmlDeviceGetCount_v2(out uint count);
        [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int nvmlDeviceGetHandleByIndex_v2(uint index,out IntPtr handle);
        [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Ansi)] private static extern int nvmlDeviceGetName(IntPtr device,StringBuilder name,uint length);
        [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int nvmlDeviceGetTemperature(IntPtr device,int type,out uint value);
        [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int nvmlDeviceGetUtilizationRates(IntPtr device,out Utilization value);
        [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int nvmlDeviceGetClockInfo(IntPtr device,int type,out uint value);
        private bool ready,attempted;
        private readonly List<IntPtr> devices=new List<IntPtr>();
        public readonly List<string> Names=new List<string>();
        public bool Open() {
            if(attempted)return ready;attempted=true;
            try {
                if(nvmlInit_v2()!=0)return false;ready=true;
                uint count;if(nvmlDeviceGetCount_v2(out count)!=0)return false;
                for(uint i=0;i<count;i++) {
                    IntPtr h;if(nvmlDeviceGetHandleByIndex_v2(i,out h)!=0)continue;
                    var name=new StringBuilder(128);nvmlDeviceGetName(h,name,128);devices.Add(h);Names.Add(name.ToString());
                }
                return devices.Count>0;
            }catch(DllNotFoundException){return false;}catch(EntryPointNotFoundException){return false;}
        }
        public double? Read(string metric,int index) {
            if(!Open()||devices.Count==0)return null;
            IntPtr h=devices[Math.Max(0,Math.Min(index,devices.Count-1))];uint v;Utilization u;
            if(metric=="gpu_temp"&&nvmlDeviceGetTemperature(h,0,out v)==0)return v;
            if(metric=="gpu_load"&&nvmlDeviceGetUtilizationRates(h,out u)==0)return u.Gpu;
            if(metric=="gpu_clock"&&nvmlDeviceGetClockInfo(h,0,out v)==0)return v;
            return null;
        }
        public void Dispose(){if(ready){try{nvmlShutdown();}catch{}}ready=false;}
    }

    internal static class Startup {
        internal const string Name="NevermorQ590Display";
        private const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
        internal static string CurrentMode() {
            bool readable=true;
            try{using(var key=Registry.CurrentUser.OpenSubKey(RunKey))if(key!=null&&key.GetValue(Name)!=null)return "обычный";}catch(System.Security.SecurityException){readable=false;}catch(UnauthorizedAccessException){readable=false;}
            try {Type t=Type.GetTypeFromProgID("Schedule.Service");dynamic service=Activator.CreateInstance(t);service.Connect();dynamic task=service.GetFolder("\\").GetTask(Name);return "с правами администратора";}catch{return readable?"выключен":"недоступен";}
        }
        internal static string TaskXml(string exe,string sid) {
            return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>"+
              "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">"+
              "<RegistrationInfo><Description>Лёгкий монитор дисплея Nevermor Q590</Description></RegistrationInfo>"+
              "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>"+SecurityElement.Escape(sid)+"</UserId><Delay>PT10S</Delay></LogonTrigger></Triggers>"+
              "<Principals><Principal id=\"Author\"><UserId>"+SecurityElement.Escape(sid)+"</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>"+
              "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><StartWhenAvailable>true</StartWhenAvailable><RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable><ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Enabled>true</Enabled></Settings>"+
              "<Actions Context=\"Author\"><Exec><Command>"+SecurityElement.Escape(exe)+"</Command><Arguments>--autostart</Arguments><WorkingDirectory>"+SecurityElement.Escape(Path.GetDirectoryName(exe))+"</WorkingDirectory></Exec></Actions></Task>";
        }
        private static bool RemoveTask() {
            try {Type t=Type.GetTypeFromProgID("Schedule.Service");dynamic service=Activator.CreateInstance(t);service.Connect();dynamic folder=service.GetFolder("\\");
                try{folder.GetTask(Name);}catch(COMException e){if(e.ErrorCode==unchecked((int)0x80070002))return true;throw;}folder.DeleteTask(Name,0);return true;
            }catch(Exception e){var failure=e.GetBaseException();if(failure.HResult==unchecked((int)0x80070002)||failure.HResult==unchecked((int)0x80070003))return true;throw new InvalidOperationException("Не удалось проверить автозапуск в планировщике: "+failure.Message,failure);}
        }
        internal static void Set(bool enabled,string exe) {
            if(!enabled) {
                if(!RemoveTask())throw new InvalidOperationException("Для удаления автозапуска с повышенными правами запустите приложение от администратора.");
                using(var key=Registry.CurrentUser.OpenSubKey(RunKey,true))if(key!=null)key.DeleteValue(Name,false);
                return;
            }
            if(Native.Admin) {
                Type t=Type.GetTypeFromProgID("Schedule.Service");dynamic service=Activator.CreateInstance(t);service.Connect();dynamic folder=service.GetFolder("\\");
                folder.RegisterTask(Name,TaskXml(exe,WindowsIdentity.GetCurrent().User.Value),6,null,null,3,null);
                using(var key=Registry.CurrentUser.OpenSubKey(RunKey,true))if(key!=null)key.DeleteValue(Name,false);
            }else {
                if(!RemoveTask())throw new InvalidOperationException("Сначала запустите приложение от администратора, чтобы заменить существующий автозапуск.");
                using(var key=Registry.CurrentUser.CreateSubKey(RunKey))key.SetValue(Name,"\""+exe+"\" --autostart",RegistryValueKind.String);
            }
        }
    }
}
