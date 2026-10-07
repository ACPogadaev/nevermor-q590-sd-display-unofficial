using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NevermorSetup {
    internal static class ShellShortcut {
        [ComImport,Guid("00021401-0000-0000-C000-000000000046")] private class ShellLink {}
        [ComImport,InterfaceType(ComInterfaceType.InterfaceIsIUnknown),Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW {
            void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder path,int maximum,IntPtr data,uint flags);
            void GetIDList(out IntPtr list);
            void SetIDList(IntPtr list);
            void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder value,int maximum);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string value);
            void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder value,int maximum);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string value);
            void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder value,int maximum);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string value);
            void GetHotkey(out short value);
            void SetHotkey(short value);
            void GetShowCmd(out int value);
            void SetShowCmd(int value);
            void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder path,int maximum,out int index);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path,int index);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path,uint reserved);
            void Resolve(IntPtr window,uint flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }
        private static void Sta(Action action) {
            if(Thread.CurrentThread.GetApartmentState()==ApartmentState.STA){action();return;}
            Exception failure=null;var thread=new Thread(()=>{try{action();}catch(Exception e){failure=e;}}){IsBackground=true};
            thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
            if(failure!=null)throw new IOException("Не удалось создать или прочитать ярлык Unicode.",failure);
        }
        internal static void Create(string path,string target,string arguments,string description) {
            Sta(delegate{
                object instance=new ShellLink();try{
                    var link=(IShellLinkW)instance;link.SetPath(target);link.SetWorkingDirectory(Path.GetDirectoryName(target));
                    link.SetArguments(arguments??"");link.SetDescription(description??"");link.SetShowCmd(1);
                    ((System.Runtime.InteropServices.ComTypes.IPersistFile)instance).Save(path,true);
                }finally{Marshal.FinalReleaseComObject(instance);}
            });
        }
        internal static string[] Read(string path) {
            string[] result=null;Sta(delegate{
                object instance=new ShellLink();try{
                    ((System.Runtime.InteropServices.ComTypes.IPersistFile)instance).Load(path,0);var link=(IShellLinkW)instance;
                    var target=new StringBuilder(4096);var arguments=new StringBuilder(4096);var description=new StringBuilder(4096);
                    link.GetPath(target,target.Capacity,IntPtr.Zero,4);link.GetArguments(arguments,arguments.Capacity);link.GetDescription(description,description.Capacity);
                    result=new[]{target.ToString(),arguments.ToString(),description.ToString()};
                }finally{Marshal.FinalReleaseComObject(instance);}
            });return result;
        }
    }
    internal static class Package {
        internal const string AppId="9cd89d32-5fa0-4215-b788-30dfedec272a";
        internal const string DriverHash="1f519a22e47187f70a1379a48ca604981c4fcf694f4e65b734aaa74a9fba3032";
        internal static string Hash(Stream stream){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        internal static string HashFile(string path){using(var stream=File.OpenRead(path))return Hash(stream);}
        internal static string SafePath(string root,string relative) {
            if(String.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.IndexOf(':')>=0||relative.IndexOf('\0')>=0)throw new InvalidDataException("Некорректный путь в пакете.");
            string[] parts=relative.Replace('\\','/').Split('/');if(parts.Any(x=>x==".."||x=="."||x==""))throw new InvalidDataException("Путь выходит за пределы пакета.");
            string fullRoot=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string result=Path.GetFullPath(Path.Combine(fullRoot,String.Join(Path.DirectorySeparatorChar.ToString(),parts)));
            if(!result.StartsWith(fullRoot,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Путь выходит за пределы пакета.");return result;
        }
        internal static void NoReparse(string path) {
            var info=new DirectoryInfo(Path.GetFullPath(path));while(info!=null){if(info.Exists&&(info.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Установка в ссылку или junction запрещена: "+info.FullName);info=info.Parent;}
        }
        internal static void Extract(Stream stream,string destination) {
            string expected=BuildInfo.PayloadHash;string actual=Hash(stream);if(actual!=expected)throw new InvalidDataException("Контрольная сумма встроенного пакета не совпадает.");stream.Position=0;
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Read,true)){
                var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
                foreach(var entry in zip.Entries){
                    if(entry.FullName.EndsWith("/"))throw new InvalidDataException("Неожиданная запись каталога.");
                    string target=SafePath(destination,entry.FullName);if(!seen.Add(target))throw new InvalidDataException("Повторяющийся путь в пакете.");
                    if(entry.Length>32*1024*1024||(total+=entry.Length)>64*1024*1024)throw new InvalidDataException("Пакет превышает допустимый размер.");
                    if((entry.ExternalAttributes>>16&0xF000)==0xA000)throw new InvalidDataException("Ссылки в пакете запрещены.");
                    Directory.CreateDirectory(Path.GetDirectoryName(target));NoReparse(Path.GetDirectoryName(target));
                    using(var input=entry.Open())using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(output);
                }
            }
            string driver=SafePath(destination,"driver/PawnIO_setup.exe");if(HashFile(driver)!=DriverHash)throw new InvalidDataException("Неверная контрольная сумма драйвера PawnIO.");
            string marker=SafePath(destination,"app/installed.flag");if(File.ReadAllText(marker).Trim()!=AppId)throw new InvalidDataException("Пакет не принадлежит Nevermor Display.");
        }
        internal static string PrivateTemp() {
            string path=Path.Combine(Path.GetTempPath(),"NevermorSetup-"+Guid.NewGuid().ToString("N"));
            var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);
            var current=WindowsIdentity.GetCurrent().User;security.SetOwner(current);
            foreach(var sid in new[]{current,new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null),new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null)})security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            Directory.CreateDirectory(path,security);return path;
        }
        internal static void CleanTemp(string path){
            string root=Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\')+"\\";
            string full=Path.GetFullPath(path);if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith("NevermorSetup-"))throw new IOException("Неверный временный каталог.");
            NoReparse(full);if(Directory.Exists(full))Directory.Delete(full,true);
        }
    }
    internal static class Signature {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct FileInfo {internal uint Size;[MarshalAs(UnmanagedType.LPWStr)] internal string Path;internal IntPtr Handle,Subject;}
        [StructLayout(LayoutKind.Sequential)] private struct TrustData {internal uint Size;internal IntPtr Policy,Sip;internal uint UI,Revocation,Choice;internal IntPtr File;internal uint StateAction;internal IntPtr StateData,URL;internal uint Flags,Context;}
        [DllImport("wintrust.dll",ExactSpelling=true)] private static extern int WinVerifyTrust(IntPtr window,ref Guid action,ref TrustData data);
        internal static void Check(string path) {
            var file=new FileInfo{Size=(uint)Marshal.SizeOf(typeof(FileInfo)),Path=path};IntPtr memory=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FileInfo)));
            try {Marshal.StructureToPtr(file,memory,false);var data=new TrustData{Size=(uint)Marshal.SizeOf(typeof(TrustData)),UI=2,Choice=1,File=memory,Flags=0x1010};
                Guid action=new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");int error=WinVerifyTrust(new IntPtr(-1),ref action,ref data);
                if(error!=0)throw new InvalidDataException("Windows не подтвердила подпись встроенного установщика PawnIO: 0x"+error.ToString("X8")+". Защита не изменялась.");
            }finally{Marshal.DestroyStructure(memory,typeof(FileInfo));Marshal.FreeHGlobal(memory);}
        }
    }
    internal static class Install {
        internal static readonly string Target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Nevermor Display");
        internal static readonly string DataPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NevermorDisplay");
        internal static readonly string ShortcutFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),"Nevermor Display — Unofficial");
        internal const string RegistryPath=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\NevermorQ590Display";
        internal static Version DriverVersion() {
            using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))using(var key=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO")){
                Version result;return key!=null&&Version.TryParse(key.GetValue("DisplayVersion") as string,out result)?result:null;
            }
        }
        internal static bool Ready(Version version){return version!=null&&version>=new Version(2,2);}
        internal static bool DriverResult(int code){if(code!=0&&code!=183&&code!=3010)throw new InvalidOperationException("Установка драйвера не завершена, код "+code+". Приложение не заменено.");return code==3010;}
        private static int Run(string exe,string arguments,int timeout){
            using(var process=Process.Start(new ProcessStartInfo(exe,arguments){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(exe)})){
                if(!process.WaitForExit(timeout))throw new TimeoutException("Компонент не завершился за отведённое время. Его процесс не остановлен принудительно; дождитесь завершения и повторите установку.");return process.ExitCode;
            }
        }
        private static void CloseApp() {
            string sid=WindowsIdentity.GetCurrent().User.Value;
            try{using(var signal=EventWaitHandle.OpenExisting("Local\\NevermorQ590Quit-"+sid))signal.Set();}catch(WaitHandleCannotBeOpenedException){}
            for(int attempt=0;attempt<100;attempt++){
                try{using(var mutex=Mutex.OpenExisting("Local\\NevermorQ590Display-"+sid)){
                    bool acquired=false;try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
                    if(acquired){mutex.ReleaseMutex();return;}
                }}catch(WaitHandleCannotBeOpenedException){return;}Thread.Sleep(100);
            }throw new IOException("Закройте предыдущую Nevermor Display через меню её значка в трее и повторите установку.");
        }
        internal static void VerifyOwned(string target) {
            Package.NoReparse(target);if(!Directory.Exists(target))return;
            string marker=Path.Combine(target,"installed.flag");if(!File.Exists(marker)||File.ReadAllText(marker).Trim()!=Package.AppId)throw new IOException("В папке установки находятся другие файлы. Установщик не будет их заменять: "+target);
        }
        internal static string Commit(string candidate,string target) {
            VerifyOwned(target);Package.NoReparse(candidate);string backup=target+".backup-"+Guid.NewGuid().ToString("N");bool moved=false;
            try{if(Directory.Exists(target)){Directory.Move(target,backup);moved=true;}Directory.Move(candidate,target);return moved?backup:null;}
            catch{if(moved&&!Directory.Exists(target))Directory.Move(backup,target);throw;}
        }
        internal static void RemoveOwned(string target) {
            VerifyOwned(target);if(!Directory.Exists(target))return;
            string manifest=Path.Combine(target,"installed-files.txt");if(!File.Exists(manifest))throw new IOException("Не найден список установленных файлов.");
            var files=File.ReadAllLines(manifest);foreach(string relative in files){string path=Package.SafePath(target,relative);Package.NoReparse(Path.GetDirectoryName(path));if(File.Exists(path))File.Delete(path);}
            File.Delete(manifest);
            // Remove only empty owned directories. Files added by the user are preserved.
            var directories=files.Select(x=>Path.GetDirectoryName(Package.SafePath(target,x))).Where(x=>x!=target).Distinct().OrderByDescending(x=>x.Length);
            foreach(string directory in directories)if(Directory.Exists(directory)&&!Directory.EnumerateFileSystemEntries(directory).Any())Directory.Delete(directory);
            if(!Directory.EnumerateFileSystemEntries(target).Any())Directory.Delete(target);
        }
        private static void Shortcuts() {
            Package.NoReparse(ShortcutFolder);Directory.CreateDirectory(ShortcutFolder);
            ShellShortcut.Create(Path.Combine(ShortcutFolder,"Nevermor Display.lnk"),Path.Combine(Target,"NevermorDisplay.exe"),"","Nevermor Q590-SD — независимое неофициальное ПО");
            ShellShortcut.Create(Path.Combine(ShortcutFolder,"Удалить Nevermor Display.lnk"),Path.Combine(Target,"Maintenance.exe"),"--uninstall","Удалить Nevermor Display");
        }
        internal static void OptionalShortcuts(Action create,Action<string> progress) {
            try{create();}catch(Exception e){progress("Приложение установлено, но ярлыки не созданы: "+e.GetBaseException().Message+". Запустите его кнопкой ниже или из "+Path.Combine(Target,"NevermorDisplay.exe"));}
        }
        internal static bool Execute(Action<string> progress) {
            VerifyOwned(Target);CloseApp();string stage=Package.PrivateTemp(),candidate=null,backup=null;bool driverWasAdded=false,reboot=false;
            try{
                progress("Проверяем и распаковываем встроенные компоненты…");using(var payload=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))Package.Extract(payload,stage);
                string driver=Path.Combine(stage,"driver","PawnIO_setup.exe");Signature.Check(driver);
                Version version=DriverVersion();driverWasAdded=version==null;
                if(!Ready(version)){
                    progress("Устанавливаем подписанный драйвер датчиков PawnIO…");reboot=DriverResult(Run(driver,"-install -silent",180000));
                    if(!Ready(DriverVersion()))throw new InvalidOperationException("Установщик драйвера завершился, но PawnIO 2.2 не зарегистрирован. Установка приложения не продолжена.");
                }else progress("Совместимый драйвер уже установлен — используем его.");
                progress("Устанавливаем Nevermor Display 1.3.1…");candidate=Target+".pending-"+Guid.NewGuid().ToString("N");Package.NoReparse(candidate);Directory.CreateDirectory(candidate);
                string app=Path.Combine(stage,"app");foreach(string file in Directory.GetFiles(app,"*",SearchOption.AllDirectories)){
                    string relative=file.Substring(app.Length+1);string output=Package.SafePath(candidate,relative);Directory.CreateDirectory(Path.GetDirectoryName(output));File.Copy(file,output,false);
                }
                File.Copy(Application.ExecutablePath,Path.Combine(candidate,"Maintenance.exe"),false);
                if(driverWasAdded||(Directory.Exists(Target)&&File.Exists(Path.Combine(Target,"pawnio-installed-by-nevermor.flag"))))File.WriteAllText(Path.Combine(candidate,"pawnio-installed-by-nevermor.flag"),"PawnIO 2.2.0\r\n");
                var files=Directory.GetFiles(candidate,"*",SearchOption.AllDirectories).Select(x=>x.Substring(candidate.Length+1)).ToArray();File.WriteAllLines(Path.Combine(candidate,"installed-files.txt"),files,Encoding.UTF8);
                backup=Commit(candidate,Target);candidate=null;
                if(Run(Path.Combine(Target,"NevermorDisplay.exe"),"--setup-initialize",30000)!=0)throw new IOException("Не удалось сохранить настройки или обновить существующий автозапуск. Подробности: "+Path.Combine(DataPath,"last-error.txt"));
                using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))using(var key=root.CreateSubKey(RegistryPath)){
                    key.SetValue("DisplayName","Nevermor Q590-SD Display — независимое неофициальное ПО");key.SetValue("DisplayVersion","1.3.1");key.SetValue("Publisher","Independent community project; not affiliated with Nevermor");
                    key.SetValue("InstallLocation",Target);key.SetValue("UninstallString","\""+Path.Combine(Target,"Maintenance.exe")+"\" --uninstall");key.SetValue("DisplayIcon",Path.Combine(Target,"NevermorDisplay.exe"));
                    key.SetValue("NoModify",1,RegistryValueKind.DWord);key.SetValue("NoRepair",1,RegistryValueKind.DWord);key.SetValue("EstimatedSize",(int)(Directory.GetFiles(Target,"*",SearchOption.AllDirectories).Sum(x=>new FileInfo(x).Length)/1024),RegistryValueKind.DWord);
                }
                OptionalShortcuts(Shortcuts,progress);
                if(backup!=null){RemoveOwned(backup);backup=null;}progress(reboot?"Готово. Драйвер запросил перезагрузку Windows; выполните её после сохранения работы.":"Готово. Приложение и драйвер установлены. AIDA64 и Digital для работы не нужны.");return reboot;
            }catch{
                if(backup!=null){try{RemoveOwned(Target);if(!Directory.Exists(Target))Directory.Move(backup,Target);}catch{}}
                throw;
            }finally{if(candidate!=null)try{RemoveCandidate(candidate);}catch{}try{Package.CleanTemp(stage);}catch{}}
        }
        private static void RemoveCandidate(string candidate){
            string prefix=Target+".pending-";if(!candidate.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("Неверный каталог подготовки.");Package.NoReparse(candidate);if(Directory.Exists(candidate))Directory.Delete(candidate,true);
        }
        internal static void Uninstall(bool driver,Action<string> progress) {
            VerifyOwned(Target);CloseApp();progress("Удаляем автозапуск приложения…");string app=Path.Combine(Target,"NevermorDisplay.exe");
            if(File.Exists(app)&&Run(app,"--remove-startup",30000)!=0)throw new IOException("Не удалось удалить автозапуск. Удаление файлов не выполнено.");
            if(driver){
                if(!File.Exists(Path.Combine(Target,"pawnio-installed-by-nevermor.flag")))throw new InvalidOperationException("Этот драйвер установлен другой программой. Его удаление здесь запрещено.");
                progress("Удаляем компонент PawnIO…");string stage=Package.PrivateTemp();try{using(var payload=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))Package.Extract(payload,stage);string setup=Path.Combine(stage,"driver","PawnIO_setup.exe");Signature.Check(setup);DriverResult(Run(setup,"-uninstall -silent",180000));}finally{try{Package.CleanTemp(stage);}catch{}}
            }
            progress("Удаляем файлы приложения…");RemoveOwned(Target);Package.NoReparse(ShortcutFolder);
            foreach(string name in new[]{"Nevermor Display.lnk","Удалить Nevermor Display.lnk"}){string path=Path.Combine(ShortcutFolder,name);if(File.Exists(path))File.Delete(path);}if(Directory.Exists(ShortcutFolder)&&!Directory.EnumerateFileSystemEntries(ShortcutFolder).Any())Directory.Delete(ShortcutFolder);
            using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))root.DeleteSubKeyTree(RegistryPath,false);
            progress("Приложение удалено. Настройки сохранены в "+DataPath+(driver?". Компонент PawnIO удалён.":". Общий драйвер PawnIO сохранён, поскольку им могут пользоваться другие программы."));
        }
    }
    internal sealed class SetupForm:Form {
        private readonly Button start=new Button{Text="Установить",Width=150,Height=34};
        private readonly Button launch=new Button{Text="Запустить приложение",Width=200,Height=34,Enabled=false};
        private readonly TextBox log=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
        private readonly ProgressBar bar=new ProgressBar{Dock=DockStyle.Bottom,Height=16};
        private readonly CheckBox removeDriver=new CheckBox{Dock=DockStyle.Top,Height=45,Text="Удалить также PawnIO — только если он не нужен другим программам",Checked=false};
        private bool busy;private readonly bool uninstall;private readonly List<string> records=new List<string>();
        internal SetupForm(bool uninstall){
            this.uninstall=uninstall;Text="Nevermor Q590-SD Display 1.3.1 — неофициальное ПО";ClientSize=new Size(710,530);MinimumSize=Size;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),RowCount=5,ColumnCount=1};root.RowStyles.Add(new RowStyle(SizeType.Absolute,45));root.RowStyles.Add(new RowStyle(SizeType.Absolute,112));root.RowStyles.Add(new RowStyle(SizeType.Absolute,50));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,50));Controls.Add(root);
            root.Controls.Add(new Label{Dock=DockStyle.Fill,Text=uninstall?"Удаление Nevermor Display":"Приложение и драйвер — одна установка",Font=new Font("Segoe UI",17,FontStyle.Bold)},0,0);
            root.Controls.Add(new Label{Dock=DockStyle.Fill,Text="Самостоятельный контроллер дисплея Nevermor Q590-SD / SD-Q590.\r\nВ комплекте: приложение, библиотека датчиков и подписанный PawnIO 2.2.0. Отдельные установки и интернет для установки не нужны.\r\n\r\nПапка приложения: "+Install.Target},0,1);
            var license=new LinkLabel{Dock=DockStyle.Fill,Text="Независимое неофициальное ПО. Условия использования и лицензии"};license.LinkClicked+=delegate{MessageBox.Show(this,"Проект является независимым неофициальным программным обеспечением и не связан с Nevermor. Все товарные знаки принадлежат их владельцам. Использование осуществляется на собственный риск. Перед применением на реальном оборудовании необходимо проверить совместимость и требования производителя.\r\n\r\nКонтроллер: MIT; LibreHardwareMonitor: MPL 2.0.\r\nPawnIO — сторонний подписанный драйвер namazso; включён официальный неизменённый установщик, распространение которого разрешено. Драйвер не является собственной разработкой проекта.\r\n\r\nУстановка не меняет Defender, Secure Boot, целостность памяти и проверку подписей. Полные лицензии приложены в папке licenses.","Условия использования",MessageBoxButtons.OK,MessageBoxIcon.Information);};root.Controls.Add(license,0,2);
            var panel=new Panel{Dock=DockStyle.Fill};panel.Controls.Add(log);panel.Controls.Add(bar);if(uninstall){removeDriver.Enabled=File.Exists(Path.Combine(Install.Target,"pawnio-installed-by-nevermor.flag"));panel.Controls.Add(removeDriver);}root.Controls.Add(panel,0,3);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0)};buttons.Controls.Add(start);if(!uninstall)buttons.Controls.Add(launch);root.Controls.Add(buttons,0,4);
            start.Text=uninstall?"Удалить":"Установить / обновить";start.Width=200;start.Click+=Begin;
            launch.Click+=delegate{try{Process.Start(new ProcessStartInfo(Path.Combine(Install.Target,"NevermorDisplay.exe"),"--post-install"){UseShellExecute=false,WorkingDirectory=Install.Target});Close();}catch(Exception e){MessageBox.Show(this,e.Message);}};
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy)e.Cancel=true;};
            Write("Готов к "+(uninstall?"удалению":"установке")+". Во время установки предыдущая Nevermor Display будет закрыта. Для проверки CPU/RPM закройте Digital и AIDA64 через их меню выхода.");
        }
        private void Write(string message){if(InvokeRequired){BeginInvoke((Action<string>)Write,message);return;}string line=DateTime.Now.ToString("HH:mm:ss")+" "+message;records.Add(line);log.AppendText(line+Environment.NewLine);}
        private async void Begin(object sender,EventArgs e){
            busy=true;start.Enabled=false;removeDriver.Enabled=false;bar.Style=ProgressBarStyle.Marquee;bool remove=removeDriver.Checked;
            try{bool reboot=false;if(uninstall)await Task.Run(()=>Install.Uninstall(remove,Write));else reboot=await Task.Run(()=>Install.Execute(Write));launch.Enabled=!uninstall&&!reboot;start.Text="Готово";}
            catch(Exception error){Write("Ошибка: "+error.GetBaseException().Message);Write("Если компонент драйвера уже был установлен, он сохранён. Повторная установка безопасно проверяет его наличие.");start.Enabled=true;start.Text="Повторить";}
            finally{bar.Style=ProgressBarStyle.Blocks;busy=false;try{Directory.CreateDirectory(Install.DataPath);File.WriteAllLines(Path.Combine(Install.DataPath,"setup.log"),records,Encoding.UTF8);}catch{}}
        }
    }
    internal static class Program {
        [STAThread] private static int Main(string[] args){
            try{
                bool uninstall=args.Contains("--uninstall");
                if(uninstall&&String.Equals(Path.GetDirectoryName(Application.ExecutablePath),Install.Target,StringComparison.OrdinalIgnoreCase)){
                    string stage=Package.PrivateTemp();string worker=Path.Combine(stage,"Nevermor-maintenance.exe");File.Copy(Application.ExecutablePath,worker,false);
                    Process.Start(new ProcessStartInfo(worker,"--uninstall"){UseShellExecute=false,WorkingDirectory=stage});return 0;
                }
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new SetupForm(uninstall));return 0;
            }catch(Exception e){MessageBox.Show(e.GetBaseException().Message,"Nevermor Setup",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
        }
    }
}
