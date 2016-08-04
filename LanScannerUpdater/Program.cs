using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace LanScannerUpdater
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string process = args[1].Replace(".exe", "");

                Console.Write("Убиваем " + args[1] + "...");
                while (Process.GetProcessesByName(process).Length > 0)
                {
                    Process[] myProcesses2 = Process.GetProcessesByName(process);
                    for (int i = 0; i < myProcesses2.Length; i++) { myProcesses2[i].Kill(); }

                    Thread.Sleep(300);
                }
                Console.WriteLine(" Убит");
    

                if (File.Exists(args[1])) File.Delete(args[1]);

                File.Move(args[0], args[1]);

                Console.WriteLine("Запускаем " + args[1] + "...");
                Process.Start(args[1]);
            }
            catch (Exception) { }
        }
    }
}
