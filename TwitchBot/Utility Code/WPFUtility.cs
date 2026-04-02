using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
//using System.Windows.Forms;
using System.Windows.Threading;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace TwitchBot.Utility_Code
{
    internal class WPFUtility
    {
        public static void WriteToLog(string message)
        {
            try
            {
                if (String.IsNullOrEmpty(message))
                    throw new Exception("Tried to write to wpf log with either a null or empty string");

                if (MainWindow.AppWindow == null)
                    throw new Exception("Tried to write to wpf log before MainWindow.AppWindow was initialized");


                Trace.WriteLine(DateTime.Now.ToString() + "\t" + message);

                MainWindow.AppWindow.Dispatcher.BeginInvoke(() =>
                {
                    MainWindow.AppWindow.ConsoleLog.AppendText("\n" + DateTime.Now.ToString() + "\t" + message);
                    MainWindow.AppWindow.ConsoleLog.ScrollToEnd();
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"WPFUtility Error - {ex.ToString()}");
            }
        }
    }
}
