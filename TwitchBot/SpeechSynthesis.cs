using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;

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

    public class SpeechSynthesis
    {
        public static readonly int SPEECHSYNTH_VOL = 80;
        public static readonly int SPEECHSYNTH_RATE = -2;

        SpeechSynthesizer synth = new SpeechSynthesizer();

        public void SpeechSynth(string input, int customRate = -100)
        {
            Trace.WriteLine("SpeechSynth received: " + input);

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

        //doesn't seem to be needed
        public void SpeechSynthAsync(string input, int customRate = -100)
        {
            Trace.WriteLine("SpeechSynth received: " + input);

            synth.Volume = SPEECHSYNTH_VOL;
            if (customRate == -100)
                synth.Rate = SPEECHSYNTH_RATE;
            else
                synth.Rate = customRate;

            Prompt prompt = new Prompt(input);

            synth.SpeakAsync(prompt);
        }
    }
}
