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

    internal class TwitchPlays
    {
        //SimulateKeyPress SimKeyPress = new SimulateKeyPress();
        InputSimulator inputSim = new InputSimulator();

        [DllImport("User32.dll")]
        static extern int SetForegroundWindow(IntPtr point);

        //there is probably a less brute force way to do this
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

        //speech synth test
        public static void SpeechSynth(string input)
        {
            Trace.WriteLine("SpeechSynth received: " + input);

            SpeechSynthesizer synthesizer = new SpeechSynthesizer();
            synthesizer.Volume = GlobalVars.SPEECHSYNTH_VOL;
            synthesizer.Rate = GlobalVars.SPEECHSYNTH_RATE;

            Prompt prompt = new Prompt(input);

            synthesizer.SpeakAsync(prompt);
        }

        public static void SpeechSynth(string input, int customRate = -2)
        {
            Trace.WriteLine("SpeechSynth received: " + input);

            SpeechSynthesizer synthesizer = new SpeechSynthesizer();
            synthesizer.Volume = GlobalVars.SPEECHSYNTH_VOL;
            synthesizer.Rate = customRate;

            Prompt prompt = new Prompt(input);

            synthesizer.SpeakAsync(prompt);
        }

        public static void SpeechSynthSync(string input)
        {
            Trace.WriteLine("SpeechSynth received: " + input);

            SpeechSynthesizer synthesizer = new SpeechSynthesizer();
            synthesizer.Volume = GlobalVars.SPEECHSYNTH_VOL;
            synthesizer.Rate = GlobalVars.SPEECHSYNTH_RATE;

            Prompt prompt = new Prompt(input);

            synthesizer.Speak(prompt);
        }

        public static void SpeechSynthSync(string input, int customRate = -2)
        {
            Trace.WriteLine("SpeechSynth received: " + input);

            SpeechSynthesizer synthesizer = new SpeechSynthesizer();
            synthesizer.Volume = GlobalVars.SPEECHSYNTH_VOL;
            synthesizer.Rate = customRate;

            Prompt prompt = new Prompt(input);

            synthesizer.Speak(prompt);
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
                    case "walk":
                        SetForegroundWindow(handle);
                        inputSim.Keyboard.KeyDown(VirtualKeyCode.LSHIFT);
                        break;
                    case "run":
                        inputSim.Keyboard.KeyUp(VirtualKeyCode.LSHIFT);
                        break;
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
                            SpeechSynth("spawning cheese");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD0);
                        }
                        break;
                    //spawn 5 tomato and 5 cabbage soup
                    case "soup":
                        if (random.Next(1, 2) == 1)
                        {
                            if(random.Next(1, 51) == 1)
                                SpeechSynth("spawning soup", -10);
                            else
                                SpeechSynth("spawning soup");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD1);
                        }
                        break;
                    //spawn 10 wine
                    case "wine":
                        if (random.Next(1, 2) == 1)
                        {
                            SpeechSynth("spawning wine...you alcoholic");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD2);
                        }
                        break;
                    case "num3":
                        if (random.Next(1, 2) == 1)
                        {
                            SpeechSynth("spawning num3");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD3);
                        }
                        break;
                    case "num4":
                        if (random.Next(1, 2) == 1)
                        {
                            SpeechSynth("spawning num4");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD4);
                        }
                        break;
                    case "num5":
                        if (random.Next(1, 2) == 1)
                        {
                            SpeechSynth("spawning num5");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD5);
                        }
                        break;
                    case "num6":
                        if (random.Next(1, 2) == 1)
                        {
                            SpeechSynth("spawning num6");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD6);
                        }
                        break;
                    case "num7":
                        if (random.Next(1, 2) == 1)
                        {
                            SpeechSynth("spawning num7");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD7);
                        }
                        break;
                    //spawn 10 siders
                    case "spider":
                        if (random.Next(1, 11) == 1)
                        {
                            SpeechSynth("spawning spiders");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD8);
                        }
                        break;
                    //spawn 10 dragons
                    case "dragon":
                        if (random.Next(1, 2) == 1)
                        {
                            SpeechSynth("spawning dragons. good luck");

                            SetForegroundWindow(handle);
                            inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD9);
                        }
                        break;
                    case "cheesemageddon":
                        if (random.Next(1, 2) == 1)
                        {
                            SpeechSynth("It's time for calcium!");

                            SetForegroundWindow(handle);
                            for (int x = 1; x <= 100; x++)
                                inputSim.Keyboard.KeyPress(VirtualKeyCode.NUMPAD0);
                        }
                        break;
                    case "soupmageddon":
                        if (random.Next(1, 2) == 1)
                        {
                            SpeechSynth("Blame Buzz");

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
