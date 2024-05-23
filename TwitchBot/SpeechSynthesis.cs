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

    public class SpeechSynthesis
    {
        public static readonly int SPEECHSYNTH_VOL = 80;
        public static readonly int SPEECHSYNTH_RATE = -2;

        SpeechSynthesizer synth = new SpeechSynthesizer();

        public void SpeechSynth(string input, int customRate = -100)
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

        public void SpeechSynthSync(string input, int customRate = -100)
        {
            Trace.WriteLine("SpeechSynth received: " + input);

            synth.Volume = SPEECHSYNTH_VOL;
            if (customRate == -100)
                synth.Rate = SPEECHSYNTH_RATE;
            else
                synth.Rate = customRate;

            Prompt prompt = new Prompt(input);

            synth.Speak(prompt);
        }
    }
}
