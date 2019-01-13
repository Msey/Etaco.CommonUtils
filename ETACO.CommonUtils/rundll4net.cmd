powershell -noexit [Reflection.Assembly]::LoadFrom('ETACO.CommonUtils.dll');[ETACO.CommonUtils.AppContext]::Main();Exit
--[ETACO.CommonUtils.AppContext]|Get-Member
--C:\Windows\System32\WindowsPowerShell\v1.0\powershell
--C:\Windows\SysWOW64\WindowsPowerShell\v1.0
public static string AppFullFileName{get { return Path.Combine(Path.GetDirectoryName(GetAssemblyFullFileName(Assembly.GetExecutingAssembly())), "ETACO.CommonUtils.exe"); }}
