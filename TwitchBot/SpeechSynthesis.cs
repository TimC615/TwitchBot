using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace TwitchBot
{
    /*
    public static class GlobalVars_SpeechSynth
    {
        public static readonly int SPEECHSYNTH_VOL = 80;
        public static readonly int SPEECHSYNTH_RATE = -2;
    }
    */

    /*
    use: test.SpeechSynth 
    to call TTS normally (no message overlap)
     
    use:
    new Thread(delegate () {
        SpeechSynthesis skyrimTTS = new SpeechSynthesis();

        skyrimTTS.SpeechSynth("spawning cheese");
    }).Start();
    when messages are preferred to be overlapping. threads will automatically be closed once TTS is finished
    */

    class SpeechSynthesis
    {
        private static SpeechSynthesis Instance;

        static readonly int SPEECHSYNTH_VOL = 80;
        public static readonly int SPEECHSYNTH_RATE = 0;
        public SpeechSynthesizer synth;
        public SpeechSynthesizer asyncSynth;

        public bool asyncIsPaused = false;

        private Queue<Prompt> currAsyncPromptQueue = new Queue<Prompt>();

        private SpeechSynthesis()
        { 
            synth = new SpeechSynthesizer();

            asyncSynth = new SpeechSynthesizer();
            asyncSynth.SpeakStarted += SpeechSynthAsyncPromptStart;
            asyncSynth.SpeakCompleted += SpeechSynthAsyncPromptEnd;
        }

        public static SpeechSynthesis GetInstance()
        {
            if (Instance == null)
            {
                Instance = new SpeechSynthesis();
            }

            return Instance;
        }
        

        //use for general TTS needs
        //default value for customRate set to -100 to signify obvious impossible rate value
        public void SpeechSynth(string input, int customRate = -100)
        {
            //Trace.WriteLine("SpeechSynth received: " + input);

            /*
            foreach (var v in synth.GetInstalledVoices().Select(v => v.VoiceInfo))
            {
                Trace.WriteLine("Name: " + v.Name + ", Desc: " + v.Description + 
                    ", Gender: " + v.Gender + ", Age: " + v.Age + ", Culture: " + v.Culture);
            }
            */

            synth.Volume = SPEECHSYNTH_VOL;
            if (customRate == -100)
                synth.Rate = SPEECHSYNTH_RATE;
            else
                synth.Rate = customRate;

            Prompt prompt = new Prompt(input);

            synth.Speak(prompt);
        }

        //used for TTS points redeem
        public void SpeechSynthAsync(string input, int customRate = -100)
        {
            //Trace.WriteLine("SpeechSynth received: " + input);

            asyncSynth.Volume = SPEECHSYNTH_VOL;
            if (customRate == -100)
                asyncSynth.Rate = SPEECHSYNTH_RATE;
            else
                asyncSynth.Rate = customRate;

            Prompt prompt = new Prompt(input);
            //currAsyncPromptQueue.Enqueue(prompt);



            //Trace.WriteLine($"State: {asyncSynth.State}");




            asyncSynth.SpeakAsync(prompt);


            //Trace.WriteLine($"State: {asyncSynth.State}");
        }

        public void SpeechSynthAsyncPromptStart(object sender, SpeakStartedEventArgs e)
        {
            Trace.WriteLine($"Async Speech Synth: SPEAKING STARTED");
        }

        public void SpeechSynthAsyncPromptEnd(object sender, SpeakCompletedEventArgs e)
        {
            Trace.WriteLine($"Async Speech Synth: SPEAKING ENDED");
        }

        public void SkipCurrentSpeechSynthAsync()
        {
            Prompt currPrompt = asyncSynth.GetCurrentlySpokenPrompt();
            //Trace.WriteLine($"Current speech prompt: {currPrompt.Stringify()}");
            asyncSynth.SpeakAsyncCancel(currPrompt);
        }

        public void ClearAllSpeechSynthAsyncPrompts()
        {
            Trace.WriteLine($"Async Speech Synth: All prompts cancelled");
            asyncSynth.SpeakAsyncCancelAll();
        }

        public void PauseSpeechSynthAsync()
        {
            Trace.WriteLine($"Async Speech Synth: Speech paused");
            asyncSynth.Pause();
            asyncIsPaused = true;
        }

        public void ResumeSpeechSynthAsync()
        {
            Trace.WriteLine($"Async Speech Synth: Speech resumed");
            asyncSynth.Resume();
            asyncIsPaused = false;
        }
    }
}
