using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NevermorSetup {
    internal static class Tests {
        private static void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);}
        private static void Reject(Action action,string name){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,name);}
        public static int Main(string[] args){
            try{
                if(args.Length==2&&args[0]=="--snapshot"){
                    System.Windows.Forms.Application.EnableVisualStyles();System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                    using(var form=new SetupForm(false)){
                        form.ShowInTaskbar=false;form.StartPosition=System.Windows.Forms.FormStartPosition.Manual;form.Location=new System.Drawing.Point(-20000,-20000);form.Show();System.Windows.Forms.Application.DoEvents();
                        using(var bitmap=new System.Drawing.Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new System.Drawing.Rectangle(System.Drawing.Point.Empty,form.Size));bitmap.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);}
                    }return 0;
                }
                string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
                foreach(string path in new[]{"../outside.txt","/absolute.txt",@"C:\outside.txt",@"..\outside.txt","folder/../outside.txt","file:stream","folder//file.txt"})Reject(()=>Package.SafePath(root,path),"reject "+path);
                Check(Package.SafePath(root,"source/App.cs")==Path.Combine(root,"source","App.cs"),"safe relative path");
                string unicodeFolder=Path.Combine(root,"Меню Пуск — проверка кириллицы");Directory.CreateDirectory(unicodeFolder);
                string unicodeTarget=Path.Combine(unicodeFolder,"Программа тест.exe");File.WriteAllText(unicodeTarget,"shortcut target fixture; never executed");
                string shortcut=Path.Combine(unicodeFolder,"Удалить Nevermor Display.lnk");string description="Неофициальная программа — проверка";
                ShellShortcut.Create(shortcut,unicodeTarget,"--uninstall",description);
                var linkValues=ShellShortcut.Read(shortcut);Check(File.Exists(shortcut)&&String.Equals(linkValues[0],unicodeTarget,StringComparison.OrdinalIgnoreCase)&&linkValues[1]=="--uninstall"&&linkValues[2]==description,"Unicode shortcut saves and reloads filename, target, arguments and description from MTA worker");
                bool warned=false;Install.OptionalShortcuts(()=>{throw new IOException("simulated Start menu access failure");},message=>{warned=true;});Check(warned,"shortcut error is reported without aborting installation");
                Check(Install.Ready(new Version(2,2,0))&&Install.Ready(new Version(3,0))&&!Install.Ready(new Version(2,1))&&!Install.Ready(null),"driver version gating");
                Check(!Install.DriverResult(0)&&!Install.DriverResult(183)&&Install.DriverResult(3010),"success/already exists/reboot codes");Reject(()=>Install.DriverResult(5),"driver error aborts install");
                string payload=Path.Combine(root,"payload");Directory.CreateDirectory(payload);
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))Package.Extract(stream,payload);
                string driver=Path.Combine(payload,"driver","PawnIO_setup.exe");Check(Package.HashFile(driver)==Package.DriverHash,"official driver hash");Signature.Check(driver);
                string app=Path.Combine(payload,"app");Check(File.Exists(Path.Combine(app,"NevermorDisplay.exe"))&&File.Exists(Path.Combine(app,"LibreHardwareMonitorLib.dll")),"application and sensor library included");
                Check(File.ReadAllText(Path.Combine(app,"installed.flag")).Trim()==Package.AppId,"owned installation marker");
                Check(!Directory.GetFiles(app,"*",SearchOption.AllDirectories).Any(x=>Path.GetFileName(x)=="settings.xml"||Path.GetFileName(x)=="diagnostics.txt"||x.EndsWith(".sys",StringComparison.OrdinalIgnoreCase)),"no personal settings or obsolete sys drivers in application files");
                byte[] corrupted;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))using(var memory=new MemoryStream()){stream.CopyTo(memory);corrupted=memory.ToArray();}corrupted[corrupted.Length/2]^=1;
                Reject(()=>{using(var stream=new MemoryStream(corrupted))Package.Extract(stream,Path.Combine(root,"corrupt"));},"corrupted payload rejected before extraction");
                string target=Path.Combine(root,"deployment");Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"user.txt"),"preserve");Reject(()=>Install.VerifyOwned(target),"foreign directory protected");
                File.Delete(Path.Combine(target,"user.txt"));File.WriteAllText(Path.Combine(target,"installed.flag"),Package.AppId);File.WriteAllText(Path.Combine(target,"version.txt"),"old");
                string candidate=Path.Combine(root,"candidate");Directory.CreateDirectory(candidate);File.WriteAllText(Path.Combine(candidate,"installed.flag"),Package.AppId);File.WriteAllText(Path.Combine(candidate,"version.txt"),"new");
                string backup=Install.Commit(candidate,target);Check(File.ReadAllText(Path.Combine(target,"version.txt"))=="new"&&File.ReadAllText(Path.Combine(backup,"version.txt"))=="old","atomic deployment keeps old backup");
                string invalidCandidate=Path.Combine(root,"missing-candidate");Reject(()=>Install.Commit(invalidCandidate,target),"failed deployment");Check(File.ReadAllText(Path.Combine(target,"version.txt"))=="new","failed deployment restores previous application");
                File.WriteAllText(Path.Combine(target,"user-notes.txt"),"preserve");File.WriteAllLines(Path.Combine(target,"installed-files.txt"),new[]{"version.txt","installed.flag"});Install.RemoveOwned(target);
                Check(File.Exists(Path.Combine(target,"user-notes.txt"))&&!File.Exists(Path.Combine(target,"version.txt")),"uninstall removes only owned files and preserves user additions");
                File.WriteAllText(Path.Combine(root,"PASS.txt"),"PASS: Unicode ShellLink saves and reloads Russian filename and target path, arguments and description from worker thread; Start menu failure does not abort installed application; safe extraction paths, driver version gating, driver exit codes, embedded complete offline payload, official driver hash and Windows signature, rejection of tampered payload, protection of unrelated directories, atomic deployment and rollback, removal of owned files only.\r\nNo drivers installed, no startup registered, no USB writes. Real elevated installation and CPU/RPM still require verification on the user's account.\r\n",Encoding.UTF8);return 0;
            }catch(Exception e){File.WriteAllText(Path.Combine(args[0],"FAIL.txt"),e.ToString());return 1;}
        }
    }
}
