using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchBot.Utility_Code
{
    class OBSUtility
    {
        public static void CloseTTSFace(SceneItemDetails sceneItem, string sceneName)
        {
            try
            {
                SpeechSynthesis _SpeechSynth = SpeechSynthesis.GetInstance();

                //ensure TTS is running before starting to check states
                Thread.Sleep(100);

                //check every 100ms if TTS is actively speaking. if finished speaking, disable TTS face
                while (_SpeechSynth.asyncSynth.State.ToString() == "Speaking")
                {
                    Thread.Sleep(100);
                }

                if (GlobalObjects._OBS.IsConnected && sceneItem != null)
                {
                    GlobalObjects._OBS.SetSceneItemEnabled(sceneName, sceneItem.ItemId, false);
                }
            }
            catch (Exception e)
            {
                WPFUtility.WriteToLog("CloseTTSFace Error: " + e.Message);
            }
        }
    }
}
