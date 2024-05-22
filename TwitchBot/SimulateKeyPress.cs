using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using TwitchLib.Client.Events;
using WindowsInput;
using WindowsInput.Native;
using System.Runtime.InteropServices;

namespace TwitchBot
{
    public class SimulateKeyPress
    {
        InputSimulator inputSim = new InputSimulator();

        public void GeneralStart(OnMessageReceivedArgs e, string activeGame)
        {
            Trace.WriteLine("SimulateKeyPress triggered and read: " + e.ChatMessage);

            //inputSim.Keyboard.KeyPress()
            Trace.WriteLine(VirtualKeyCode.VK_Q.GetHashCode().ToString());
        }
    }
}
