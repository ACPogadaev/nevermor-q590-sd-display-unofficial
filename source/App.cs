using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace NevermorDisplay {
    internal sealed class SettingsForm : Form {
        private readonly Engine engine;
        private Settings settings;
        private readonly NotifyIcon tray;
        private readonly System.Windows.Forms.Timer uiTimer;
        private ComboBox upperChoice,lowerChoice,gpuChoice,fanChoice,clockFormat;
        private NumericUpDown upperCustom,lowerCustom,interval;
        private CheckBox startup,startHidden;
        private Label upperPreview,lowerPreview,status,note,performance,startupNote;
        private Button pause;
        private bool exiting,loading=true,previousStartup;
        private string lastFans="",lastGpus="";
        private DateTime lastSnapshot=DateTime.MinValue;
        private readonly EventWaitHandle showSignal;
        private RegisteredWaitHandle showWait;
        internal SettingsForm(Settings settings,Engine engine,EventWaitHandle showSignal,bool preview) {
            this.settings=settings.Copy();this.engine=engine;this.showSignal=showSignal;
            Text="Nevermor Display 1.3.1 · Q590 · Неофициальное ПО";Font=new Font("Segoe UI",9.5f);BackColor=Color.FromArgb(247,248,251);
            ClientSize=new Size(740,694);MinimumSize=new Size(756,733);MaximumSize=new Size(1050,950);StartPosition=FormStartPosition.CenterScreen;
            AutoScaleMode=AutoScaleMode.Dpi;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20,16,20,14),ColumnCount=1,RowCount=10};
            foreach(int h in new[]{34,28,95,111,125,105,38,30,40})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(root);
            root.Controls.Add(new Label{Text="Дисплей кулера — ваши показатели",Font=new Font("Segoe UI",16,FontStyle.Bold),AutoSize=true,Dock=DockStyle.Fill},0,0);
            root.Controls.Add(new Label{Text=preview?"Режим предпросмотра: команды USB не отправляются.":"Самостоятельное приложение · драйвер датчиков установлен вместе с программой.",ForeColor=Color.FromArgb(90,97,110),Dock=DockStyle.Fill},0,1);
            var previewPanel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Margin=new Padding(0,4,0,8)};
            previewPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));previewPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            previewPanel.Controls.Add(Preview("ВЕРХНЕЕ ПОЛЕ · 4 ЦИФРЫ",out upperPreview),0,0);
            previewPanel.Controls.Add(Preview("НИЖНЕЕ ПОЛЕ · 2 ЦИФРЫ",out lowerPreview),1,0);root.Controls.Add(previewPanel,0,2);
            var fields=new GroupBox{Text="Что выводить",Dock=DockStyle.Fill};root.Controls.Add(fields,0,3);
            upperChoice=Combo(174,23,388);lowerChoice=Combo(174,60,388);
            upperChoice.Items.AddRange(Metrics.All.Cast<object>().ToArray());lowerChoice.Items.AddRange(Metrics.All.Where(x=>x.Lower).Cast<object>().ToArray());
            upperChoice.SelectedItem=Metrics.All.First(x=>x.Id==settings.Upper);lowerChoice.SelectedItem=Metrics.All.First(x=>x.Id==settings.Lower);
            upperCustom=Number(574,23,98,0,9999,settings.UpperCustom);lowerCustom=Number(574,60,98,0,99,settings.LowerCustom);
            fields.Controls.AddRange(new Control[]{TextLabel("Верхнее поле",16,27,152),TextLabel("Нижнее поле",16,64,152),upperChoice,lowerChoice,upperCustom,lowerCustom});
            upperChoice.SelectedIndexChanged+=delegate{EnableSources();};lowerChoice.SelectedIndexChanged+=delegate{EnableSources();};
            var sources=new GroupBox{Text="Источники и формат времени",Dock=DockStyle.Fill};root.Controls.Add(sources,0,4);
            gpuChoice=Combo(174,22,498);fanChoice=Combo(174,55,498);gpuChoice.Items.Add("Авто: основная NVIDIA / доступная GPU");gpuChoice.SelectedIndex=0;
            fanChoice.Items.Add(new SensorChoice{Id="",Name="Авто: CPU Fan, если датчик подписан"});fanChoice.SelectedIndex=0;
            sources.Controls.AddRange(new Control[]{TextLabel("Видеокарта",16,26,152),TextLabel("Вентилятор",16,59,152),gpuChoice,fanChoice});
            clockFormat=Combo(174,88,498);clockFormat.Items.AddRange(new object[]{"Как в Windows","12 часов · 1:15 PM","24 часа · 13:15"});clockFormat.SelectedIndex=settings.ClockFormat=="12"?1:settings.ClockFormat=="24"?2:0;
            sources.Controls.AddRange(new Control[]{TextLabel("Формат времени",16,92,152),clockFormat});
            var options=new GroupBox{Text="Обновление и запуск",Dock=DockStyle.Fill};root.Controls.Add(options,0,5);
            interval=Number(174,23,83,1,60,(decimal)settings.Interval);interval.DecimalPlaces=1;interval.Increment=0.5m;
            options.Controls.AddRange(new Control[]{TextLabel("Обновлять каждые",16,27,150),interval,TextLabel("сек. · 2–5 сек. обычно достаточно",265,27,400)});
            startup=new CheckBox{Text="Автозапуск при входе в Windows",Location=new Point(18,58),Size=new Size(323,25)};
            startHidden=new CheckBox{Text="При автозапуске сразу в трей",Location=new Point(350,58),Size=new Size(320,25),Checked=settings.StartHidden};
            string mode=Startup.CurrentMode();previousStartup=mode!="выключен"&&mode!="недоступен";startup.Checked=previousStartup;startup.Enabled=mode!="недоступен";
            startupNote=TextLabel("Сейчас: "+mode+(Native.Admin?" · приложение запущено от администратора":""),18,82,650);startupNote.Font=new Font("Segoe UI",8);startupNote.ForeColor=Color.FromArgb(90,97,110);
            options.Controls.AddRange(new Control[]{startup,startHidden,startupNote});
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,Margin=new Padding(0,5,0,0)};
            var apply=Button("Сохранить",108);pause=Button(settings.Paused?"Продолжить":"Пауза",100);var admin=Button("Запустить от администратора",252);var exit=Button("Выход",90);
            buttons.Controls.AddRange(new Control[]{apply,pause,admin,exit});root.Controls.Add(buttons,0,6);
            apply.Click+=delegate{Apply();};pause.Click+=delegate{TogglePause();};exit.Click+=delegate{ExitApp();};
            admin.Enabled=!Native.Admin;admin.Click+=delegate{Elevate();};
            status=new Label{Dock=DockStyle.Fill,ForeColor=Color.FromArgb(22,90,106),AutoEllipsis=true};root.Controls.Add(status,0,7);
            note=new Label{Dock=DockStyle.Fill,ForeColor=Color.FromArgb(142,89,22),AutoEllipsis=true,Font=new Font("Segoe UI",8.5f)};root.Controls.Add(note,0,8);
            performance=new Label{Dock=DockStyle.Fill,ForeColor=Color.FromArgb(90,97,110),Font=new Font("Segoe UI",8.5f)};root.Controls.Add(performance,0,9);
            var menu=new ContextMenuStrip();menu.Items.Add("Настройки",null,delegate{ShowSettings();});menu.Items.Add("Пауза / продолжить",null,delegate{TogglePause();});
            menu.Items.Add("Сохранить диагностику",null,delegate{SaveDiagnostics();});menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Выход",null,delegate{ExitApp();});
            tray=new NotifyIcon{Icon=SystemIcons.Information,Text="Nevermor Display 1.3.1 · Q590 · Неофициальное ПО",Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=delegate{ShowSettings();};
            uiTimer=new System.Windows.Forms.Timer{Interval=1000};uiTimer.Tick+=delegate{RefreshState();};uiTimer.Start();
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!exiting&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}};
            Resize+=delegate{if(WindowState==FormWindowState.Minimized){Hide();WindowState=FormWindowState.Normal;}};
            VisibleChanged+=delegate{if(Visible){uiTimer.Start();RefreshState();}else uiTimer.Stop();};
            loading=false;EnableSources();RefreshState();
            showWait=ThreadPool.RegisterWaitForSingleObject(showSignal,delegate(object o,bool timeout){try{BeginInvoke((Action)ShowSettings);}catch{}},null,Timeout.Infinite,false);
            FormClosed+=delegate{Application.ExitThread();};
        }
        protected override void Dispose(bool disposing){if(disposing){if(showWait!=null)showWait.Unregister(null);uiTimer.Dispose();tray.Visible=false;tray.Dispose();}base.Dispose(disposing);}
        private static Label TextLabel(string text,int x,int y,int width){return new Label{Text=text,Location=new Point(x,y),Size=new Size(width,23)};}
        private static ComboBox Combo(int x,int y,int width){return new ComboBox{Location=new Point(x,y),Size=new Size(width,28),DropDownStyle=ComboBoxStyle.DropDownList};}
        private static NumericUpDown Number(int x,int y,int width,decimal min,decimal max,decimal value){return new NumericUpDown{Location=new Point(x,y),Size=new Size(width,28),Minimum=min,Maximum=max,Value=value};}
        private static Button Button(string text,int width){return new Button{Text=text,Size=new Size(width,30),Margin=new Padding(0,0,9,0),UseVisualStyleBackColor=true};}
        private static Control Preview(string title,out Label digits) {
            var box=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(26,36,50),Margin=new Padding(0,0,8,0)};
            box.Controls.Add(new Label{Text=title,ForeColor=Color.FromArgb(154,187,208),Location=new Point(15,9),Size=new Size(300,21),Font=new Font("Segoe UI",8.5f)});
            digits=new Label{Text="—",ForeColor=Color.White,Location=new Point(13,28),Size=new Size(280,48),Font=new Font("Consolas",29,FontStyle.Bold)};box.Controls.Add(digits);return box;
        }
        private void EnableSources(){if(loading)return;var up=upperChoice.SelectedItem as Metric;var low=lowerChoice.SelectedItem as Metric;if(up==null||low==null)return;
            upperCustom.Enabled=up.Id=="custom";lowerCustom.Enabled=low.Id=="custom";gpuChoice.Enabled=up.Id.StartsWith("gpu_")||low.Id.StartsWith("gpu_");fanChoice.Enabled=up.Id=="fan";
            clockFormat.Enabled=up.Id=="clock";
        }
        private Settings Collect(){var s=settings.Copy();s.Upper=((Metric)upperChoice.SelectedItem).Id;s.Lower=((Metric)lowerChoice.SelectedItem).Id;
            s.UpperCustom=(int)upperCustom.Value;s.LowerCustom=(int)lowerCustom.Value;s.Interval=(double)interval.Value;s.StartHidden=startHidden.Checked;
            s.ClockFormat=clockFormat.SelectedIndex==1?"12":clockFormat.SelectedIndex==2?"24":"system";
            if(gpuChoice.Items.Count>0&&!(gpuChoice.Items[0] is string&&((string)gpuChoice.Items[0]).StartsWith("Авто:")))s.GpuIndex=Math.Max(0,gpuChoice.SelectedIndex);
            var fan=fanChoice.SelectedItem as SensorChoice;if(fan!=null&&fanChoice.Items.Count>1)s.FanId=fan.Id;s.Validate();return s;
        }
        private bool Apply(){try{
            var next=Collect();Store.Save(next);
            if(startup.Checked!=previousStartup){Startup.Set(startup.Checked,Application.ExecutablePath);previousStartup=startup.Checked;}
            settings=next;engine.Configure(next);startupNote.Text="Сейчас: "+Startup.CurrentMode()+(Native.Admin?" · приложение от администратора":"");lastSnapshot=DateTime.MinValue;return true;
        }catch(Exception e){MessageBox.Show(this,e.GetBaseException().Message,"Не удалось сохранить",MessageBoxButtons.OK,MessageBoxIcon.Warning);return false;}}
        private void TogglePause(){settings.Paused=!settings.Paused;pause.Text=settings.Paused?"Продолжить":"Пауза";
            try{Store.Save(settings);}catch(Exception e){MessageBox.Show(this,e.Message);}engine.Configure(settings);lastSnapshot=DateTime.MinValue;RefreshState();}
        private void Elevate(){if(!Apply())return;try{
            var info=new ProcessStartInfo(Application.ExecutablePath,"--wait-for "+Process.GetCurrentProcess().Id){UseShellExecute=true,Verb="runas",WorkingDirectory=Store.DirectoryPath};
            Process.Start(info);ExitApp();
        }catch(System.ComponentModel.Win32Exception e){if(e.NativeErrorCode!=1223)MessageBox.Show(this,e.Message,"Повышение прав");}}
        internal void ShowSettings(){Show();WindowState=FormWindowState.Normal;Activate();}
        internal void ExitApp(){exiting=true;Close();Application.Exit();}
        internal void RefreshState(){
            Snapshot s=engine.Current;if(s.Time==lastSnapshot)return;lastSnapshot=s.Time;
            upperPreview.Text=s.UpperText;lowerPreview.Text=s.LowerText;status.Text=s.Status;note.Text=s.SensorNote;
            status.ForeColor=s.Sent?Color.FromArgb(24,119,82):Color.FromArgb(22,90,106);
            string fanKey=String.Join("|",s.Fans.Select(x=>x.Id));
            if(fanKey!=lastFans){lastFans=fanKey;var old=fanChoice.SelectedItem as SensorChoice;string id=old==null||fanChoice.Items.Count<=1?settings.FanId:old.Id;
                fanChoice.Items.Clear();fanChoice.Items.Add(new SensorChoice{Id="",Name="Авто: CPU Fan, если датчик подписан"});fanChoice.Items.AddRange(s.Fans.Cast<object>().ToArray());
                fanChoice.SelectedIndex=0;for(int i=1;i<fanChoice.Items.Count;i++)if(((SensorChoice)fanChoice.Items[i]).Id==id)fanChoice.SelectedIndex=i;
            }
            string gpuKey=String.Join("|",s.Gpus);
            if(gpuKey!=lastGpus&&s.Gpus.Count>0){int index=lastGpus==""?settings.GpuIndex:Math.Max(0,gpuChoice.SelectedIndex);lastGpus=gpuKey;gpuChoice.Items.Clear();gpuChoice.Items.AddRange(s.Gpus.Cast<object>().ToArray());gpuChoice.SelectedIndex=Math.Min(index,gpuChoice.Items.Count-1);}
            using(var p=Process.GetCurrentProcess())performance.Text="Работа одного опроса: "+s.WorkMilliseconds.ToString("0.00")+" мс · интервал "+settings.Interval.ToString("0.#")+" с · память "+(p.WorkingSet64/1048576.0).ToString("0")+" МБ\nЗакрытие окна сворачивает приложение в трей. Нижнее поле ограничено 99; 100% отображается как 99.";
        }
        private void SaveDiagnostics(){try{Directory.CreateDirectory(Store.DirectoryPath);string path=Path.Combine(Store.DirectoryPath,"diagnostics.txt");var s=engine.Current;
            File.WriteAllLines(path,new[]{"Time: "+s.Time.ToString("s"),"Administrator: "+Native.Admin,"Sensors library: "+typeof(LibreHardwareMonitor.Hardware.Computer).Assembly.GetName().Version,"Sensor driver: "+Sensors.DriverStatus(),"Status: "+s.Status,"Upper: "+s.UpperText+"; lower: "+s.LowerText,"Note: "+s.SensorNote,"GPU: "+String.Join(", ",s.Gpus),"Fans: "+String.Join(", ",s.Fans.Select(x=>x.Id+" | "+x.Name)),"Poll ms: "+s.WorkMilliseconds.ToString("0.000"),"Startup: "+Startup.CurrentMode()},System.Text.Encoding.UTF8);
            MessageBox.Show(this,"Сохранено: "+path,"Диагностика");}catch(Exception e){MessageBox.Show(this,e.Message);}}
    }

    internal static class Diagnostics {
        private static void Check(bool condition,string message){if(!condition)throw new Exception("TEST FAILED: "+message);}
        private static Snapshot AwaitSnapshot(Engine engine,DateTime after){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<5000){var s=engine.Current;if(s.Time>after)return s;Thread.Sleep(10);}throw new Exception("TEST FAILED: polling timeout");}
        internal static void SelfTest(string path){
            var b=DisplayConnection.Encode(53,913,64);Check(b.Length==64,"length");Check(b.Take(7).SequenceEqual(new byte[]{7,5,3,0,9,1,3}),"digit order");Check(b.Skip(7).All(x=>x==0),"padding");
            var zero=DisplayConnection.Encode(0,0,64);Check(zero.Skip(1).All(x=>x==0),"zero");var max=DisplayConnection.Encode(99,9999,64);Check(max.Skip(1).Take(6).All(x=>x==9),"max");
            bool threw=false;try{DisplayConnection.Encode(100,10,64);}catch(ArgumentOutOfRangeException){threw=true;}Check(threw,"bounds");
            var s=new Settings{Interval=0.01,Upper="invalid",Lower="fan",UpperCustom=12345};s.Validate();Check(s.Interval==1&&s.Upper=="cpu_load"&&s.Lower=="cpu_temp"&&s.UpperCustom==9999,"validation");
            s.Interval=Double.NaN;s.Validate();Check(s.Interval==2,"invalid interval");
            var american=CultureInfo.GetCultureInfo("en-US");var russian=CultureInfo.GetCultureInfo("ru-RU");
            var afternoon=new DateTime(2026,10,7,13,15,9);Check(ClockDisplay.Number(afternoon,"12",american)==115&&ClockDisplay.Number(afternoon,"24",american)==1315,"13:15 / 1:15 PM");
            Check(ClockDisplay.Number(afternoon.Date,"12",american)==1200&&ClockDisplay.Number(afternoon.Date.AddHours(12),"12",american)==1200,"midnight and noon");
            Check(ClockDisplay.Number(afternoon.Date.AddHours(23).AddMinutes(59),"12",american)==1159,"late evening");
            Check(ClockDisplay.Number(afternoon,"system",american)==115&&ClockDisplay.Number(afternoon,"system",russian)==1315,"Windows culture format");
            var quoted=(CultureInfo)american.Clone();quoted.DateTimeFormat.ShortTimePattern="'h' H:mm";Check(!ClockDisplay.Is12Hour("system",quoted),"quoted time pattern literal");
            Check(ClockDisplay.Preview(afternoon,"12",american)=="1:15 PM"&&ClockDisplay.Preview(afternoon.Date,"12",american)=="12:00 AM"&&ClockDisplay.Preview(afternoon,"24",american)=="13:15","clock preview");
            var clockReport=DisplayConnection.Encode(9,ClockDisplay.Number(afternoon,"12",american),64);Check(clockReport.Skip(3).Take(4).SequenceEqual(new byte[]{0,1,1,5}),"12-hour USB digits");
            using(var dateSensors=new Sensors()){var dateSample=dateSensors.Read(new Settings{Upper="date",Lower="clock_seconds"});Check(dateSample.Values["date"]==dateSample.LocalTime.Day*100+dateSample.LocalTime.Month&&dateSample.Values["clock_seconds"]==dateSample.LocalTime.Second,"date and seconds / same time sample");}
            var expected=new Settings{Upper="clock",Lower="ram_load",Interval=3.5,FanId="/fan/3",GpuIndex=2,StartHidden=false,ClockFormat="12"};
            var serializer=new XmlSerializer(typeof(Settings));using(var stream=new MemoryStream()){serializer.Serialize(stream,expected);stream.Position=0;var actual=(Settings)serializer.Deserialize(stream);Check(actual.Upper==expected.Upper&&actual.Lower==expected.Lower&&actual.Interval==3.5&&actual.FanId==expected.FanId&&!actual.StartHidden&&actual.ClockFormat=="12","settings roundtrip");}
            using(var reader=new StringReader("<Settings><Upper>clock</Upper><Lower>gpu_temp</Lower></Settings>")){var migrated=(Settings)serializer.Deserialize(reader);Check(migrated.ClockFormat=="system"&&migrated.Upper=="clock","old settings migration");}
            string xml=Startup.TaskXml(@"C:\Folder & Name\App.exe","S-1-5-21-123");var document=new XmlDocument();document.LoadXml(xml);var manager=new XmlNamespaceManager(document.NameTable);manager.AddNamespace("t","http://schemas.microsoft.com/windows/2004/02/mit/task");Check(document.SelectSingleNode("/t:Task/t:Actions/t:Exec/t:Command",manager).InnerText==@"C:\Folder & Name\App.exe","startup quoting");
            Check(document.SelectSingleNode("/t:Task/t:Actions/t:Exec/t:Arguments",manager).InnerText=="--autostart","startup respects visibility setting");
            using(var engine=new Engine(new Settings{Upper="custom",Lower="custom",UpperCustom=6789,LowerCustom=45},false)){
                var first=AwaitSnapshot(engine,DateTime.MinValue);Check(first.Upper==6789&&first.Lower==45&&!first.Sent,"preview mapping / no writes");
                engine.Configure(new Settings{Paused=true});var paused=AwaitSnapshot(engine,first.Time);Check(!paused.Sent&&!paused.Upper.HasValue,"pause");
                engine.Configure(new Settings{Upper="clock",Lower="ram_load",Interval=1});var resumed=AwaitSnapshot(engine,paused.Time);Check(resumed.Upper.HasValue&&resumed.Upper<=2359&&resumed.Lower.HasValue&&resumed.Lower<=99,"selection and resume");
                if(!Native.Admin){engine.Configure(new Settings{Upper="fan",Lower="cpu_temp"});var unavailable=AwaitSnapshot(engine,resumed.Time);Check(!unavailable.Upper.HasValue&&!unavailable.Lower.HasValue&&!unavailable.Sent,"unavailable sensors are not zero");}
            }
            File.WriteAllText(path,"PASS: digit encoding, bounds, zero padding, settings validation, XML roundtrip, startup XML escaping and arguments, runtime selection, pause/resume, no preview USB writes, unavailable sensor handling.\r\nPASS 1.1: 12/24-hour format, Windows culture and quoted patterns, midnight/noon/evening, AM/PM preview, 12-hour USB digits, date and seconds, old settings migration.\r\n",System.Text.Encoding.UTF8);
        }
        internal static void Probe(string path){
            var lines=new List<string>{"Nevermor Display 1.3.1","Time: "+DateTime.Now.ToString("s"),"Administrator: "+Native.Admin,"Sensors library: "+typeof(LibreHardwareMonitor.Hardware.Computer).Assembly.GetName().Version,"Sensor driver: "+Sensors.DriverStatus(),"USB writes: NONE"};
            foreach(var device in DisplayConnection.Find())lines.Add("HID: "+device.Name+"; feature length="+device.ReportLength+"; "+device.Path);
            using(var sensors=new Sensors()) {
                var s=new Settings();s.Upper="cpu_load";s.Lower="gpu_temp";Thread.Sleep(250);var sample=sensors.Read(s);
                foreach(var pair in sample.Values.Where(x=>x.Value.HasValue))lines.Add(pair.Key+"="+pair.Value.Value.ToString("0.###",CultureInfo.InvariantCulture));
                lines.Add("GPU: "+String.Join(", ",sample.Gpus));
                s.Upper="ram_gb";s.Lower="ram_load";sample=sensors.Read(s);lines.Add("ram_gb="+sample.Values["ram_gb"]+"; ram_load="+sample.Values["ram_load"]);
                s.Upper="fan";s.Lower="cpu_temp";sample=sensors.Read(s);lines.Add("cpu_temp="+sample.Values["cpu_temp"]+"; fan="+sample.Values["fan"]);lines.Add("Note: "+sample.Note);foreach(var f in sample.Fans)lines.Add("FAN: "+f.Id+" | "+f.Name);
            }
            File.WriteAllLines(path,lines,System.Text.Encoding.UTF8);
        }
        internal static void StartupTest(string path){
            Check(!Native.Admin,"startup test must run unelevated");Check(Startup.CurrentMode()=="выключен","refusing to overwrite existing autostart");
            try{Startup.Set(true,Application.ExecutablePath);Check(Startup.CurrentMode()=="обычный","Run registration");
                using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))Check((string)key.GetValue(Startup.Name)=="\""+Application.ExecutablePath+"\" --autostart","Run command quoting");
            }finally{Startup.Set(false,Application.ExecutablePath);}
            Check(Startup.CurrentMode()=="выключен","Run cleanup");File.WriteAllText(path,"PASS: ordinary autostart registration, quoted command, cleanup. No autostart remains enabled.\r\n",System.Text.Encoding.UTF8);
        }
        internal static void Benchmark(int seconds,string path,bool send){
            Settings s=Store.Load();s.Paused=false;var samples=new List<double>();using(var p=Process.GetCurrentProcess())using(var engine=new Engine(s,send)) {
                Thread.Sleep(3000);TimeSpan before=p.TotalProcessorTime;var clock=Stopwatch.StartNew();DateTime seen=DateTime.MinValue;int sent=0;
                while(clock.Elapsed.TotalSeconds<seconds){Thread.Sleep(250);var current=engine.Current;if(current.Time!=seen){seen=current.Time;samples.Add(current.WorkMilliseconds);if(current.Sent)sent++;}}
                p.Refresh();double cpuSeconds=(p.TotalProcessorTime-before).TotalSeconds;
                var lines=new List<string>{"Mode: "+(send?"USB writes enabled":"No USB writes"),"Interval seconds: "+s.Interval.ToString(CultureInfo.InvariantCulture),"Measured seconds: "+clock.Elapsed.TotalSeconds.ToString("0.000",CultureInfo.InvariantCulture),"Process CPU seconds: "+cpuSeconds.ToString("0.000",CultureInfo.InvariantCulture),"CPU % of one logical core: "+(100*cpuSeconds/clock.Elapsed.TotalSeconds).ToString("0.000",CultureInfo.InvariantCulture),"CPU % of all logical cores: "+(100*cpuSeconds/(clock.Elapsed.TotalSeconds*Environment.ProcessorCount)).ToString("0.000",CultureInfo.InvariantCulture),"Working set MB: "+(p.WorkingSet64/1048576.0).ToString("0.00",CultureInfo.InvariantCulture),"Private bytes MB: "+(p.PrivateMemorySize64/1048576.0).ToString("0.00",CultureInfo.InvariantCulture),"Poll count: "+samples.Count,"Average poll ms: "+(samples.Count>0?samples.Average():0).ToString("0.000",CultureInfo.InvariantCulture),"Max poll ms: "+(samples.Count>0?samples.Max():0).ToString("0.000",CultureInfo.InvariantCulture),"Successful USB sends: "+sent,"Last status: "+engine.Current.Status,"Upper: "+engine.Current.UpperText+"; lower: "+engine.Current.LowerText};
                File.WriteAllLines(path,lines,System.Text.Encoding.UTF8);
            }
        }
    }

    internal static class Program {
        [STAThread] private static int Main(string[] args) {
            try {
                if(args.Length>0&&args[0]=="--self-test"){Diagnostics.SelfTest(args[1]);return 0;}
                if(args.Length>0&&args[0]=="--probe"){Diagnostics.Probe(args[1]);return 0;}
                if(args.Length>0&&args[0]=="--startup-test"){Diagnostics.StartupTest(args[1]);return 0;}
                if(args.Length>0&&args[0]=="--remove-startup"){Startup.Set(false,Application.ExecutablePath);return 0;}
                if(args.Length>0&&args[0]=="--setup-initialize"){
                    Store.Save(Store.Load());string startupMode=Startup.CurrentMode();if(startupMode=="недоступен")throw new InvalidOperationException("Нет доступа к настройкам автозапуска текущего пользователя.");if(startupMode!="выключен")Startup.Set(true,Application.ExecutablePath);return 0;
                }
                if(args.Length>0&&args[0]=="--benchmark"){Diagnostics.Benchmark(Int32.Parse(args[1],CultureInfo.InvariantCulture),args[2],args.Contains("--send"));return 0;}
                if(args.Length>0&&args[0]=="--ui-snapshot"){
                    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                    using(var signal=new EventWaitHandle(false,EventResetMode.AutoReset))using(var engine=new Engine(Store.Load(),false))using(var form=new SettingsForm(Store.Load(),engine,signal,true)){
                        form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();Thread.Sleep(3000);form.RefreshState();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);}
                    }return 0;
                }
                if(args.Contains("--post-install")){
                    Store.Save(Store.Load());
                    string startupMode=Startup.CurrentMode();if(startupMode!="выключен"&&startupMode!="недоступен")Startup.Set(true,Application.ExecutablePath);
                }
                if(!args.Contains("--preview")&&!args.Contains("--quit")&&!args.Contains("--elevated")&&!Native.Admin){
                    Settings initial=Store.Load();
                    if(Metrics.NeedsCpu(initial)||Metrics.NeedsFan(initial))try{
                        string arguments=String.Join(" ",args.Where(x=>x=="--tray"||x=="--autostart"))+" --elevated";
                        Process.Start(new ProcessStartInfo(Application.ExecutablePath,arguments){UseShellExecute=true,Verb="runas",WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory});return 0;
                    }catch(System.ComponentModel.Win32Exception e){if(e.NativeErrorCode!=1223)throw;}
                }
                int waiting=Array.IndexOf(args,"--wait-for");if(waiting>=0&&args.Length>waiting+1)try{using(var parent=Process.GetProcessById(Int32.Parse(args[waiting+1])))parent.WaitForExit(20000);}catch(ArgumentException){}
                string sid=System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;bool owner;
                if(args.Contains("--quit")){try{using(var quit=EventWaitHandle.OpenExisting("Local\\NevermorQ590Quit-"+sid))quit.Set();}catch(WaitHandleCannotBeOpenedException){}return 0;}
                using(var mutex=new Mutex(true,"Local\\NevermorQ590Display-"+sid,out owner))using(var signal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\NevermorQ590Show-"+sid)) {
                    if(!owner){signal.Set();return 0;}
                    try {
                        try{Process.GetCurrentProcess().PriorityClass=ProcessPriorityClass.BelowNormal;}catch{}
                        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                        Settings settings=Store.Load();bool preview=args.Contains("--preview");
                        using(var quit=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\NevermorQ590Quit-"+sid))using(var engine=new Engine(settings,!preview))using(var form=new SettingsForm(settings,engine,signal,preview)) {
                            var quitWait=ThreadPool.RegisterWaitForSingleObject(quit,delegate(object o,bool timeout){try{form.BeginInvoke((Action)form.ExitApp);}catch{}},null,Timeout.Infinite,false);
                            bool hidden=args.Contains("--tray")||(args.Contains("--autostart")&&settings.StartHidden);
                            form.Show();if(hidden)form.Hide();try{Application.Run();}finally{quitWait.Unregister(null);}
                        }
                    }finally{mutex.ReleaseMutex();}
                }
                return 0;
            }catch(Exception e){
                string message=e.ToString();try{File.WriteAllText(Path.Combine(Store.DirectoryPath,"last-error.txt"),message);}catch{}
                if(args.Length>0&&args[0].StartsWith("--"))return 1;
                MessageBox.Show(message,"Nevermor Display: ошибка запуска",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;
            }
        }
    }
}
