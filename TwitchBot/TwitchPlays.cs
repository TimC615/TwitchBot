using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using TwitchLib.Client.Events;
using System.Runtime.InteropServices;
using WindowsInput;
using WindowsInput.Native;

using System.Speech.Synthesis;

namespace TwitchBot
{
    public static class GlobalVars
    {
        public static readonly int SPEECHSYNTH_VOL = 80;
        public static readonly int SPEECHSYNTH_RATE = -2;
    }

    //--------------------------------------------------------------------------------------------------------------------------------
    //ADD POTIONS SPAWN, CHANGE GIANT FROST SPIDERS TO NORMAL FROST SPIDERS, LYDIA SPAWNS TO DEFEND PLAYER (5?), SPAWN 4 BEARS INSTEAD OF 10
    //--------------------------------------------------------------------------------------------------------------------------------

    internal class TwitchPlays
    {
        //SimulateKeyPress SimKeyPress = new SimulateKeyPress();
        InputSimulator inputSim = new InputSimulator();

        public SpeechSynthesis synth = new SpeechSynthesis();

        [DllImport("User32.dll")]
        static extern int SetForegroundWindow(IntPtr point);

        //initial attempt to enter skyrim commands (without using papyrus script)
        void SimulateWordInput(string input)
        {
            Trace.WriteLine(input + "\n");

            foreach (char c in input)
            {
                string command = c.ToString();
                Trace.WriteLine("'" + command + "' ");

                switch (command)
                {
                    case "a":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_A);
                        break;
                    case "b":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_B);
                        break;
                    case "c":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_C);
                        break;
                    case "d":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_D);
                        break;
                    case "e":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_E);
                        break;
                    case "f":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_F);
                        break;
                    case "g":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_G);
                        break;
                    case "h":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_H);
                        break;
                    case "i":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_I);
                        break;
                    case "j":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_J);
                        break;
                    case "k":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_K);
                        break;
                    case "l":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_L);
                        break;
                    case "m":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_M);
                        break;
                    case "n":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_N);
                        break;
                    case "o":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_O);
                        break;
                    case "p":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_P);
                        break;
                    case "q":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_Q);
                        break;
                    case "r":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_R);
                        break;
                    case "s":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_S);
                        break;
                    case "t":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_T);
                        break;
                    case "u":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_U);
                        break;
                    case "v":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_V);
                        break;
                    case "w":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_W);
                        break;
                    case "x":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_X);
                        break;
                    case "y":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_Y);
                        break;
                    case "z":
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.VK_Z);
                        break;

                    case " ":
                        //inputSim.Keyboard.KeyDown(VirtualKeyCode.SPACE);
                        //Thread.Sleep(0);
                        //inputSim.Keyboard.KeyUp(VirtualKeyCode.SPACE);
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.SPACE);
                        break;
                    case ".":
                        inputSim.Keyboard.KeyDown(VirtualKeyCode.OEM_PERIOD);
                        Thread.Sleep(0);
                        inputSim.Keyboard.KeyUp(VirtualKeyCode.OEM_PERIOD);
                        break;
                }
            }
        }

        //Twitch plays Skyrim SE
        public void TwitchPlaysSkyrim(OnMessageReceivedArgs e)
        {
            //MainWindow.Log("TwitchPlays triggered and read: " + e.ChatMessage);
            //Trace.WriteLine("TwitchPlays triggered and read: " + e.ChatMessage);

            //Initial functionality from a video by ParametricCamp
            //https://youtu.be/lKjoetsKLAM?si=5f5fvLkLo18BOgQx

            Process[] processes = Process.GetProcessesByName("TESV");
            Trace.WriteLine(processes.Length);
            Process skyrimProcess = processes.FirstOrDefault();
            Trace.WriteLine(skyrimProcess);

            Random random = new Random();

            Trace.WriteLine("skyrimProcess: " + skyrimProcess);
            Trace.WriteLine("GetCurrentProcess: " + Process.GetCurrentProcess());

            //if(skyrimProcess != null && skyrimProcess == Process.GetCurrentProcess())
            if (skyrimProcess != null)
            {
                IntPtr handle = skyrimProcess.Handle;
                Trace.WriteLine("TwitchPlaysSkyrim received: " + e.ChatMessage.Message);

                string input = e.ChatMessage.Message.ToLower();
                string[] command = e.ChatMessage.Message.ToLower().Split(' ');

                //switch (command[1])
                switch (input)
                {
                    //movement
                    case "forward":
                        SetForegroundWindow(handle);
                        inputSim.Keyboard.KeyDown(VirtualKeyCode.VK_W);
                        break;
                    case "back":
                        SetForegroundWindow(handle);
                        inputSim.Keyboard.KeyDown(VirtualKeyCode.VK_S);
                        break;
                    case "stop":
                        SetForegroundWindow(handle);
                        inputSim.Keyboard.KeyUp(VirtualKeyCode.VK_W);
                        inputSim.Keyboard.KeyUp(VirtualKeyCode.VK_S);
                        break;

                    case "left":
                        SetForegroundWindow(handle);
                        inputSim.Keyboard.KeyDown(VirtualKeyCode.VK_A);
                        Thread.Sleep(1000);
                        inputSim.Keyboard.KeyUp(VirtualKeyCode.VK_A);
                        break;
                    case "right":
                        SetForegroundWindow(handle);
                        inputSim.Keyboard.KeyDown(VirtualKeyCode.VK_D);
                        Thread.Sleep(1000);
                        inputSim.Keyboard.KeyUp(VirtualKeyCode.VK_D);
                        break;
                    
                    //case "walk":
                    //    SetForegroundWindow(handle);
                    //    inputSim.Keyboard.KeyDown(VirtualKeyCode.LSHIFT);
                    //    break;
                    //case "run":
                    //    inputSim.Keyboard.KeyUp(VirtualKeyCode.LSHIFT);
                    //    break;
                    
                    case "jump":
                        SetForegroundWindow(handle);
                        inputSim.Keyboard.KeyPress(VirtualKeyCode.SPACE);
                        break;

                    //------------------------------------------------------
                    //Numpad key triggers (spawn whatever is associated to them) 

                    //spawn cheese
                    case "cheese":
                        if (random.Next(1, 2) == 1)
                        {
                            synth.SpeechSynth("spawning cheese");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD0);
                        }
                        break;
                    //spawn tomato and cabbage soup
                    case "soup":
                        if (random.Next(1, 2) == 1)
                        {
                            synth.SpeechSynth("spawning soup");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD1);
                        }
                        break;
                    //spawn wine
                    case "wine":
                        if (random.Next(1, 2) == 1)
                        {
                            synth.SpeechSynth("spawning wine");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD2);
                        }
                        break;
                    //spawn rabbits
                    case "rabbit":
                    case "rabbits":
                        if (random.Next(1, 3) == 1)
                        {
                            synth.SpeechSynth("spawning rabbits");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD3);
                        }
                        break;
                    //spawn skeevers
                    case "skeever":
                    case "skeevers":
                        if (random.Next(1, 11) == 1)
                        {
                            synth.SpeechSynth("spawning skeevers");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD4);
                        }
                        break;
                    //spawn healing, magicka, and stamina potions
                    case "potion":
                    case "potions":
                        if (random.Next(1, 6) == 1)
                        {
                            synth.SpeechSynth("spawning potions");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD5);
                        }
                        break;
                    //spawn bears
                    case "bear":
                    case "bears":
                        if (random.Next(1, 11) == 1)
                        {
                            synth.SpeechSynth("spawning bears");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD6);
                        }
                        break;
                    //spawn lydias
                    case "lydia":
                    case "lydias":
                        if (random.Next(1, 6) == 1)
                        {
                            synth.SpeechSynth("spawning lydia");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD7);
                        }
                        break;
                    //spawn siders
                    case "spider":
                    case "spiders":
                        if (random.Next(1, 11) == 1)
                        {
                            synth.SpeechSynth("spawning spiders");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD8);
                        }
                        break;
                    //spawn dragons
                    case "dragon":
                    case "dragons":
                        if (random.Next(1, 21) == 1)
                        {
                            synth.SpeechSynth("spawning dragons. good luck");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD9);
                        }
                        break;
                    //spam spawn cheese command
                    case "cheesemageddon":
                        if (random.Next(1, 16) == 1)
                        {
                            synth.SpeechSynth("It's time for calcium!");

                            SetForegroundWindow(handle);
                            for (int x = 1; x <= 100; x++)
                                inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD0);
                        }
                        break;
                    //spam spawn soup command
                    case "soupmageddon":
                        if (random.Next(1, 16) == 1)
                        {
                            synth.SpeechSynth("Blame Buzz");

                            SetForegroundWindow(handle);
                            for (int x = 1; x <= 100; x++)
                                inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD1);
                        }
                        break;

                    //------------------------------------------------------

                }
            }
        }
    }
}
