using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BatteryBar {
public static class Startup {
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static string TaskName { get { return "BatteryBar-Logon-" + WindowsIdentity.GetCurrent().User.Value; } }
    static void Release(object value) { if(value!=null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
    static dynamic Connect() { dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")); service.Connect(); return service; }
    static bool Missing(Exception e) { return e.HResult==unchecked((int)0x80070002) || e.HResult==unchecked((int)0x80070003); }
    static bool Legacy(string path) {
        using(var key=Registry.CurrentUser.OpenSubKey(Key)) return key!=null && string.Equals(key.GetValue("BatteryBar") as string,"\""+path+"\"",StringComparison.OrdinalIgnoreCase);
    }
    public static bool Enabled { get { return IsEnabled(Application.ExecutablePath); } }
    public static bool IsEnabled(string path) {
        dynamic service=null, folder=null, task=null;
        try {
            service=Connect(); folder=service.GetFolder("\\");
            try { task=folder.GetTask(TaskName); } catch(Exception e) { if(!Missing(e)) throw; }
            if(task!=null) {
                var xml=new System.Xml.XmlDocument(); xml.LoadXml((string)task.Xml);
                var ns=new System.Xml.XmlNamespaceManager(xml.NameTable); ns.AddNamespace("t","http://schemas.microsoft.com/windows/2004/02/mit/task");
                var command=xml.SelectSingleNode("/t:Task/t:Actions/t:Exec/t:Command",ns);
                return (bool)task.Enabled && command!=null && string.Equals(command.InnerText,path,StringComparison.OrdinalIgnoreCase);
            }
            return Legacy(path);
        } catch(COMException) { return Legacy(path); }
        catch(IOException) { return Legacy(path); }
        catch(UnauthorizedAccessException) { return Legacy(path); }
        catch(SecurityException) { return Legacy(path); }
        finally { Release(task); Release(folder); Release(service); }
    }
    public static string TaskXml(string path,string sid) {
        return "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">"+
            "<RegistrationInfo><Description>BatteryBar: start at user logon without an intentional delay.</Description></RegistrationInfo>"+
            "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>"+SecurityElement.Escape(sid)+"</UserId><Delay>PT0S</Delay></LogonTrigger></Triggers>"+
            "<Principals><Principal id=\"User\"><UserId>"+SecurityElement.Escape(sid)+"</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>"+
            "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>"+
            "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><AllowHardTerminate>true</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable>"+
            "<RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable><IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>"+
            "<AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><Hidden>false</Hidden><RunOnlyIfIdle>false</RunOnlyIfIdle><WakeToRun>false</WakeToRun>"+
            "<ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Priority>7</Priority></Settings>"+
            "<Actions Context=\"User\"><Exec><Command>"+SecurityElement.Escape(path)+"</Command><WorkingDirectory>"+SecurityElement.Escape(Path.GetDirectoryName(path))+"</WorkingDirectory></Exec></Actions></Task>";
    }
    public static void Set(bool enabled) { Configure(Application.ExecutablePath,enabled); }
    public static void Configure(string path,bool enabled) {
        path=Path.GetFullPath(path);
        if(enabled && !File.Exists(path)) throw new FileNotFoundException("找不到启动程序。",path);
        dynamic service=null, folder=null, task=null;
        try {
            service=Connect(); folder=service.GetFolder("\\");
            if(enabled) {
                string sid=WindowsIdentity.GetCurrent().User.Value;
                task=folder.RegisterTask(TaskName,TaskXml(path,sid),6,sid,null,3,null);
                if(!(bool)task.Enabled) throw new InvalidOperationException("登录启动任务未启用。");
            } else {
                try { folder.DeleteTask(TaskName,0); } catch(Exception e) { if(!Missing(e)) throw; }
            }
            // Only remove the matching legacy entry, after registration succeeded.
            if(Legacy(path)) using(var key=Registry.CurrentUser.OpenSubKey(Key,true)) key.DeleteValue("BatteryBar",false);
        } finally { Release(task); Release(folder); Release(service); }
    }
}
}
