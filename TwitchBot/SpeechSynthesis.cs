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
        static readonly int SPEECHSYNTH_VOL = 80;
        public static readonly int SPEECHSYNTH_RATE = 0;
        public SpeechSynthesizer synth = new SpeechSynthesizer();
        public SpeechSynthesizer asyncSynth = new SpeechSynthesizer();

        private Queue<Prompt> currAsyncPromptQueue = new Queue<Prompt>();

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

        public void StopSpeechSynthAsync()
        {
            if(currAsyncPromptQueue.Count > 0)
            {
                asyncSynth.SpeakAsyncCancel(currAsyncPromptQueue.Dequeue());
            }

            //asyncSynth.Pause();
            //asyncSynth.SpeakAsyncCancelAll();
            //asyncSynth.Resume();
        }

        private void PlayAsyncPromptQueue()
        {
            //Trace.WriteLine asyncSynth.State;
        }
    }
}
