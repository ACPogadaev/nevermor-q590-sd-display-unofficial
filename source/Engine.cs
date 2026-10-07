using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Serialization;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;

namespace NevermorDisplay {
    public sealed class Settings {
        public string Upper="cpu_load",Lower="cpu_temp",FanId="";
        public string ClockFormat="system";
        public int UpperCustom=1234,LowerCustom=53,GpuIndex=0;
        public double Interval=2;
        public bool StartHidden=true,Paused=false;
        public Settings Copy(){return (Settings)MemberwiseClone();}
        public void Validate() {
            if(Double.IsNaN(Interval))Interval=2;Interval=Math.Max(1,Math.Min(60,Interval));
            UpperCustom=Math.Max(0,Math.Min(9999,UpperCustom));LowerCustom=Math.Max(0,Math.Min(99,LowerCustom));GpuIndex=Math.Max(0,GpuIndex);
            if(!Metrics.All.Any(x=>x.Id==Upper))Upper="cpu_load";
            if(!Metrics.All.Any(x=>x.Id==Lower&&x.Lower))Lower="cpu_temp";
            if(ClockFormat!="system"&&ClockFormat!="12"&&ClockFormat!="24")ClockFormat="system";
        }
    }
    internal static class Store {
        internal static readonly bool IsInstalled=File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"installed.flag"));
        internal static readonly string DirectoryPath=IsInstalled?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NevermorDisplay"):AppDomain.CurrentDomain.BaseDirectory;
        internal static readonly string SettingsPath=Path.Combine(DirectoryPath,"settings.xml");
        internal static Settings Load() {
            try{using(var f=File.OpenRead(SettingsPath)){var s=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(f);s.Validate();return s;}}catch{return new Settings();}
        }
        internal static void Save(Settings s) {
            Directory.CreateDirectory(DirectoryPath);
            s.Validate();string tmp=SettingsPath+".tmp";
            using(var f=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None))new XmlSerializer(typeof(Settings)).Serialize(f,s);
            if(File.Exists(SettingsPath))File.Replace(tmp,SettingsPath,null);else File.Move(tmp,SettingsPath);
        }
    }
    internal sealed class Metric {
        public string Id,Name; public bool Lower;
        public Metric(string id,string name,bool lower){Id=id;Name=name;Lower=lower;}
        public override string ToString(){return Name;}
    }
    internal static class Metrics {
        internal static readonly Metric[] All={
            new Metric("cpu_temp","Температура CPU · °C",true),
            new Metric("cpu_load","Загрузка CPU · %",true),
            new Metric("cpu_clock","Частота CPU · МГц",false),
            new Metric("gpu_temp","Температура GPU · °C",true),
            new Metric("gpu_load","Загрузка GPU · %",true),
            new Metric("gpu_clock","Частота GPU · МГц",false),
            new Metric("ram_load","Использование RAM · %",true),
            new Metric("ram_gb","Занято RAM · ГБ (целые)",true),
            new Metric("fan","Обороты выбранного вентилятора · RPM",false),
            new Metric("clock","Время · ЧЧММ",false),
            new Metric("clock_seconds","Секунды часов · 00–59",true),
            new Metric("date","Дата · ДДММ",false),
            new Metric("custom","Своё число",true)
        };
        internal static bool NeedsGpu(Settings s){return s.Upper.StartsWith("gpu_")||s.Lower.StartsWith("gpu_");}
        internal static bool NeedsCpu(Settings s){return s.Upper=="cpu_temp"||s.Lower=="cpu_temp"||s.Upper=="cpu_clock";}
        internal static bool NeedsFan(Settings s){return s.Upper=="fan";}
    }
    internal static class ClockDisplay {
        internal static bool Is12Hour(string format,CultureInfo culture){
            if(format=="12")return true;if(format=="24")return false;
            char quote='\0';bool escaped=false;
            foreach(char c in culture.DateTimeFormat.ShortTimePattern){
                if(escaped){escaped=false;continue;}if(c=='\\'){escaped=true;continue;}
                if(quote!='\0'){if(c==quote)quote='\0';continue;}
                if(c=='\''||c=='\"'){quote=c;continue;}if(c=='h')return true;if(c=='H')return false;
            }return false;
        }
        internal static int Number(DateTime now,string format,CultureInfo culture){int hour=now.Hour;if(Is12Hour(format,culture)){hour%=12;if(hour==0)hour=12;}return hour*100+now.Minute;}
        internal static string Preview(DateTime now,string format,CultureInfo culture){int number=Number(now,format,culture);bool twelve=Is12Hour(format,culture);return (number/100).ToString(twelve?"0":"D2",CultureInfo.InvariantCulture)+":"+now.Minute.ToString("D2",CultureInfo.InvariantCulture)+(twelve?(now.Hour<12?" AM":" PM"):"");}
    }
    internal sealed class SensorChoice { public string Id,Name; public override string ToString(){return Name;} }
    internal sealed class Sample {
        internal Dictionary<string,double?> Values=new Dictionary<string,double?>();
        internal List<SensorChoice> Fans=new List<SensorChoice>();
        internal List<string> Gpus=new List<string>();
        internal string Note="";
        internal DateTime LocalTime;
    }
    internal sealed class Sensors : IDisposable {
        internal static string DriverStatus(){try{return PawnIo.IsInstalled?"PawnIO "+PawnIo.Version:"PawnIO не установлен";}catch(Exception e){return "PawnIO: "+e.GetBaseException().Message;}}
        internal static bool DriverReady {get{try{return PawnIo.IsInstalled&&PawnIo.Version>=new Version(2,2);}catch{return false;}}}
        private readonly Nvidia nvidia=new Nvidia();
        private Computer computer;
        private bool cpuFlag,fanFlag,gpuFlag;
        private long previousIdle,previousTotal;
        private bool haveTimes;
        private string sensorError="";
        private DateTime nextSensorAttempt=DateTime.MinValue;
        internal Sensors(){PrimeCpu();}
        private void PrimeCpu(){long idle,kernel,user;if(Native.GetSystemTimes(out idle,out kernel,out user)){previousIdle=idle;previousTotal=kernel+user;haveTimes=true;}}
        private double? CpuLoad(){
            long idle,kernel,user;if(!Native.GetSystemTimes(out idle,out kernel,out user))return null;
            long total=kernel+user,dt=total-previousTotal,di=idle-previousIdle;
            double? result=haveTimes&&dt>0?(double?)Math.Max(0,Math.Min(100,100.0*(dt-di)/dt)):null;
            previousTotal=total;previousIdle=idle;haveTimes=true;return result;
        }
        private void Configure(bool cpu,bool fan,bool gpu) {
            if(computer!=null&&cpuFlag==cpu&&fanFlag==fan&&gpuFlag==gpu)return;
            if(computer==null&&cpuFlag==cpu&&fanFlag==fan&&gpuFlag==gpu&&DateTime.UtcNow<nextSensorAttempt)return;
            if(computer!=null){try{computer.Close();}catch{}computer=null;}
            cpuFlag=cpu;fanFlag=fan;gpuFlag=gpu;sensorError="";
            if(!cpu&&!fan&&!gpu)return;
            try {
                computer=new Computer();computer.IsCpuEnabled=cpu;computer.IsMotherboardEnabled=fan;computer.IsGpuEnabled=gpu;
                computer.Open();
            }catch(Exception e){sensorError="Модуль датчиков: "+e.GetBaseException().Message;try{if(computer!=null)computer.Close();}catch{}computer=null;nextSensorAttempt=DateTime.UtcNow.AddSeconds(30);}
        }
        private static List<IHardware> Flatten(IEnumerable<IHardware> hardware) {
            var result=new List<IHardware>();foreach(var h in hardware){result.Add(h);result.AddRange(Flatten(h.SubHardware));}return result;
        }
        private static double? Value(ISensor s,bool positive) {
            if(s==null||!s.Value.HasValue||Single.IsNaN(s.Value.Value)||Single.IsInfinity(s.Value.Value))return null;
            if(positive&&s.Value.Value<=0)return null;return s.Value.Value;
        }
        private static double? Preferred(IEnumerable<ISensor> sensors,SensorType type,string[] names,bool positive,bool maxFallback) {
            var list=sensors.Where(s=>s.SensorType==type).ToList();
            foreach(string name in names){var v=Value(list.FirstOrDefault(s=>s.Name.IndexOf(name,StringComparison.OrdinalIgnoreCase)>=0),positive);if(v.HasValue)return v;}
            var valid=list.Select(s=>Value(s,positive)).Where(v=>v.HasValue).Select(v=>v.Value).ToList();
            if(valid.Count==0)return null;return maxFallback?valid.Max():valid.First();
        }
        internal Sample Read(Settings settings) {
            var sample=new Sample();foreach(var m in Metrics.All)sample.Values[m.Id]=null;
            // Fast Windows counters do not open any hardware-monitoring groups.
            if(settings.Upper=="cpu_load"||settings.Lower=="cpu_load")sample.Values["cpu_load"]=CpuLoad();
            if(settings.Upper.StartsWith("ram_")||settings.Lower.StartsWith("ram_")) {
                var memory=new Native.MemoryStatus();memory.Length=(uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.MemoryStatus));
                if(Native.GlobalMemoryStatusEx(ref memory)){sample.Values["ram_load"]=memory.Load;sample.Values["ram_gb"]=(memory.TotalPhys-memory.AvailPhys)/(1024.0*1024*1024);}
            }
            DateTime now=DateTime.Now;sample.LocalTime=now;sample.Values["clock"]=ClockDisplay.Number(now,settings.ClockFormat,CultureInfo.CurrentCulture);
            sample.Values["clock_seconds"]=now.Second;sample.Values["date"]=now.Day*100+now.Month;
            bool gpu=Metrics.NeedsGpu(settings),nativeGpu=gpu&&nvidia.Open();
            if(nativeGpu) {
                sample.Gpus.AddRange(nvidia.Names);
                foreach(string id in new[]{settings.Upper,settings.Lower}.Distinct())if(id.StartsWith("gpu_"))sample.Values[id]=nvidia.Read(id,settings.GpuIndex);
            }
            bool cpu=Metrics.NeedsCpu(settings)&&Native.Admin&&DriverReady,fan=Metrics.NeedsFan(settings)&&Native.Admin&&DriverReady;
            Configure(cpu,fan,gpu&&!nativeGpu);
            if(computer!=null) {
                var all=Flatten(computer.Hardware);
                var gpus=all.Where(h=>h.HardwareType==HardwareType.GpuNvidia||h.HardwareType==HardwareType.GpuAmd||h.HardwareType==HardwareType.GpuIntel).ToList();
                IHardware chosenGpu=gpus.Count>0?gpus[Math.Min(settings.GpuIndex,gpus.Count-1)]:null;
                foreach(var h in all) {
                    bool update=(cpu&&h.HardwareType==HardwareType.Cpu)||(fan&&(h.HardwareType==HardwareType.SuperIO||h.HardwareType==HardwareType.Motherboard))||h==chosenGpu;
                    if(!update)continue;try{h.Update();}catch(Exception e){sample.Note="Не удалось прочитать "+h.Name+": "+e.GetBaseException().Message;}
                }
                if(cpu) {
                    var list=all.Where(h=>h.HardwareType==HardwareType.Cpu).SelectMany(h=>h.Sensors);
                    sample.Values["cpu_temp"]=Preferred(list,SensorType.Temperature,new[]{"Tctl/Tdie","CPU Package","Package"},true,true);
                    sample.Values["cpu_clock"]=Preferred(list,SensorType.Clock,new[]{"Core #1","CPU Core"},true,true);
                }
                if(gpu&&!nativeGpu&&chosenGpu!=null) {
                    sample.Gpus.AddRange(gpus.Select(h=>h.Name));
                    sample.Values["gpu_temp"]=Preferred(chosenGpu.Sensors,SensorType.Temperature,new[]{"GPU Core","GPU Temperature"},true,false);
                    sample.Values["gpu_load"]=Preferred(chosenGpu.Sensors,SensorType.Load,new[]{"GPU Core","D3D 3D"},false,false);
                    sample.Values["gpu_clock"]=Preferred(chosenGpu.Sensors,SensorType.Clock,new[]{"GPU Core"},true,false);
                }
                if(fan) {
                    var fans=all.Where(h=>h.HardwareType==HardwareType.SuperIO||h.HardwareType==HardwareType.Motherboard).SelectMany(h=>h.Sensors).Where(s=>s.SensorType==SensorType.Fan).ToList();
                    sample.Fans.AddRange(fans.Select(s=>new SensorChoice{Id=s.Identifier.ToString(),Name=s.Hardware.Name+" / "+s.Name}));
                    ISensor chosen=fans.FirstOrDefault(s=>s.Identifier.ToString()==settings.FanId);
                    if(chosen==null&&String.IsNullOrEmpty(settings.FanId))chosen=fans.FirstOrDefault(s=>s.Name.IndexOf("CPU",StringComparison.OrdinalIgnoreCase)>=0);
                    sample.Values["fan"]=Value(chosen,false);
                    if(chosen==null)sample.Note=fans.Count>0?"Выберите датчик вентилятора в настройках.":"Датчики вентиляторов недоступны на этой плате.";
                }
            }
            if(!Native.Admin&&(Metrics.NeedsCpu(settings)||Metrics.NeedsFan(settings)))sample.Note="Для температуры / частоты CPU и оборотов нажмите «Запустить от администратора».";
            if(!String.IsNullOrEmpty(sensorError))sample.Note=sensorError;
            if(Native.Admin&&(Metrics.NeedsCpu(settings)||Metrics.NeedsFan(settings))&&!DriverReady)sample.Note=DriverStatus()+". Для восстановления запустите единый установщик Nevermor Display 1.3.1 повторно, затем перезапустите приложение.";
            else if(Native.Admin&&(settings.Upper=="cpu_temp"||settings.Lower=="cpu_temp")&&!sample.Values["cpu_temp"].HasValue&&String.IsNullOrEmpty(sensorError))sample.Note="Источник датчиков не предоставил температуру CPU. Сохраните диагностику через меню трея; ноль не подставляется.";
            return sample;
        }
        public void Dispose(){if(computer!=null)try{computer.Close();}catch{}computer=null;nvidia.Dispose();}
    }

    internal sealed class Snapshot {
        public string UpperText="—",LowerText="—",Status="Запуск…",SensorNote="",Gpu="",Fan="";
        public int? Upper,Lower;
        public List<SensorChoice> Fans=new List<SensorChoice>();
        public List<string> Gpus=new List<string>();
        public double WorkMilliseconds;
        public bool Sent;
        public DateTime Time;
    }
    internal sealed class Engine : IDisposable {
        private readonly DisplayConnection display=new DisplayConnection();
        private readonly Sensors sensors=new Sensors();
        private readonly System.Threading.Timer timer;
        private readonly bool send;
        private volatile Settings settings;
        private volatile Snapshot snapshot=new Snapshot();
        private int working,disposed;
        private DateTime nextConflictCheck=DateTime.MinValue;
        private bool vendorRunning;
        internal Snapshot Current { get{return snapshot;} }
        internal Engine(Settings settings,bool send) {this.settings=settings.Copy();this.send=send;timer=new System.Threading.Timer(Tick,null,100,(int)(settings.Interval*1000));}
        internal void Configure(Settings s){s.Validate();settings=s.Copy();timer.Change(0,(int)(s.Interval*1000));}
        private static int? Resolve(string id,int custom,int max,Sample sample,out string warning) {
            warning="";double? value=id=="custom"?(double?)custom:sample.Values[id];
            if(!value.HasValue||Double.IsNaN(value.Value)||Double.IsInfinity(value.Value))return null;
            double rounded=Math.Round(value.Value,MidpointRounding.AwayFromZero);
            if(rounded<0||rounded>max)warning="Число "+rounded.ToString(CultureInfo.InvariantCulture)+" не помещается: показан предел "+max+".";
            return (int)Math.Max(0,Math.Min(max,rounded));
        }
        private void Tick(object unused) {
            if(Volatile.Read(ref disposed)!=0||Interlocked.Exchange(ref working,1)!=0)return;
            try {
                Settings s=settings.Copy();var watch=Stopwatch.StartNew();
                if(s.Paused){display.Close();snapshot=new Snapshot{Status="Передача приостановлена",Time=DateTime.Now};return;}
                if(send&&DateTime.UtcNow>=nextConflictCheck) {
                    nextConflictCheck=DateTime.UtcNow.AddSeconds(10);
                    var processes=Process.GetProcessesByName("DeviceDriver");vendorRunning=processes.Length>0;foreach(var p in processes)p.Dispose();
                }
                if(send&&vendorRunning){display.Close();snapshot=new Snapshot{Status="Закройте штатную Digital через её значок в трее. Две программы будут перезаписывать цифры.",Time=DateTime.Now};return;}
                Sample sample=sensors.Read(s);string warning1,warning2;
                int? upper=Resolve(s.Upper,s.UpperCustom,9999,sample,out warning1),lower=Resolve(s.Lower,s.LowerCustom,99,sample,out warning2);
                bool sent=false;string status;
                if(!upper.HasValue||!lower.HasValue)status="Нет данных для выбранного показателя. Предыдущие цифры на дисплее могут оставаться.";
                else if(send){sent=display.Send(lower.Value,upper.Value);status=display.Status;}
                else status="Предпросмотр: USB-команды не отправляются";
                string note=sample.Note;if(warning1!="")note+=(note==""?"":" ")+warning1;if(warning2!="")note+=(note==""?"":" ")+warning2;
                string upperText=upper.HasValue?upper.Value.ToString():"—",lowerText=lower.HasValue?lower.Value.ToString():"—";
                if(s.Upper=="clock")upperText=ClockDisplay.Preview(sample.LocalTime,s.ClockFormat,CultureInfo.CurrentCulture);
                if(s.Upper=="date"&&upper.HasValue)upperText=upper.Value.ToString("D4",CultureInfo.InvariantCulture).Insert(2,".");
                if(s.Upper=="clock_seconds"&&upper.HasValue)upperText=upper.Value.ToString("D2",CultureInfo.InvariantCulture);
                if(s.Lower=="clock_seconds"&&lower.HasValue)lowerText=lower.Value.ToString("D2",CultureInfo.InvariantCulture);
                watch.Stop();snapshot=new Snapshot{Upper=upper,Lower=lower,UpperText=upperText,LowerText=lowerText,Status=status,SensorNote=note,Fans=sample.Fans,Gpus=sample.Gpus,Sent=sent,WorkMilliseconds=watch.Elapsed.TotalMilliseconds,Time=DateTime.Now};
            }catch(Exception e){snapshot=new Snapshot{Status="Ошибка: "+e.GetBaseException().Message,Time=DateTime.Now};}
            finally{Interlocked.Exchange(ref working,0);}
        }
        public void Dispose() {
            if(Interlocked.Exchange(ref disposed,1)!=0)return;timer.Dispose();
            while(Volatile.Read(ref working)!=0)Thread.Sleep(20);
            display.Dispose();sensors.Dispose();
        }
    }
}
