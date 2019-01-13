using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ETACO.CommonUtils
{
    /// <summary> Содержит расширения для работы с процессами </summary>
    public static class ProcessExtensions
    {
        /// <summary> Завершить работу процесса </summary>
        public static void Terminate(this Process process, bool terminateTree = true)
        {
            if (process == null) return;
            if (terminateTree) foreach (var p in process.GetChildList()) using (p) { p.Kill(); }
            process.Kill();
        }

        /// <summary> Получить родительский процесс </summary>
        public static Process GetParent(this Process process)
        {
            try
            {
                return process == null ? null : Process.GetProcessById(ProcessUtils.GetParentProcess(process.Id));
            }
            catch (ArgumentException) {/*process with this id is not running*/ return null; }
        }

        /// <summary> Получить список дочерних процессов </summary>
        public static List<Process> GetChildList(this Process process)
        {
            var result = new List<Process>();
            if (process == null) return result;
            foreach (int id in ProcessUtils.GetChildProcessList(process.Id))
            {
                try
                {
                    result.Add(Process.GetProcessById(id));
                }
                catch (ArgumentException) {/*process with this id is not running*/ }
            }
            return result;
        }
    }
}