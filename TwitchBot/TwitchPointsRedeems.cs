using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchBot.Utility_Code;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Helix.Models.Channels.GetChannelInformation;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.PubSub.Events;

namespace TwitchBot
{
    internal class TwitchPointsRedeems
    {
        
        //
        //----------------------PubSub Event Hookups----------------------
        //
        /*
        private void PubSub_OnPubSubServiceConnected(object sender, EventArgs e)
        {
            PubSub.SendTopics(CachedOwnerOfChannelAccessToken);
        }
        */

        /*
        private void PubSub_OnListenResponse(object sender, OnListenResponseArgs e)
        {
            if (!e.Successful)
            {
                Log($"Failed to listen! Response: {e.Topic} + {e.Response.Error}");
                throw new Exception($"Failed to listen! Response: {e.Topic} + {e.Response.Error}");
            }
            else
            {
                Log("PubSub Listen: " + e.Topic);
                Log("PubSub Connected");
            }
        }
        */

        /*
        private void PubSub_OnLog(object sender, TwitchLib.PubSub.Events.OnLogArgs e)
        {
            Log("PubSub Log: " + e.Data);
        }
        */

        /*
        private void PubSub_OnServiceError(object sender, OnPubSubServiceErrorArgs e)
        {
            Log($"PubSub Service Error: {e.Exception.Message} {e.Exception}");
        }
        */

        
        public static void OnChannelPointsRewardRedeemed(ChannelPointsCustomRewardRedemptionArgs e)
        {
            //Log("PubSub: " + e.RewardRedeemed.Redemption.Reward.Title);
            var pointsRedemption = e.Notification.Payload.Event;
            string redeemTitle = pointsRedemption.Reward.Title.ToLower();
            WPFUtility.WriteToLog("Points Reward: " + redeemTitle);

            switch (redeemTitle)
            {
                case "toggle cake face":
                    //if (obsConnected)
                    if(GlobalObjects._OBS != null && GlobalObjects._OBS.IsConnected)
                    {
                        //check if webcam and reactive image scene items exist
                        string currSceneName = GlobalObjects._OBS.GetCurrentProgramScene();

                        List<OBSWebsocketDotNet.Types.SceneItemDetails> currScenesList =  GlobalObjects._OBS.GetSceneItemList(currSceneName);
                        List<string> currSceneItemIDs = new List<string>();

                        //probably a far more elegant way to do a .contains for specific parameter values but this makes sense and i can do it
                        foreach (OBSWebsocketDotNet.Types.SceneItemDetails sceneItem in currScenesList)
                            currSceneItemIDs.Add(sceneItem.SourceName);

                        if(currSceneItemIDs.Contains(Properties.Settings.Default.OBSWebcamSceneID) && currSceneItemIDs.Contains(Properties.Settings.Default.OBSReactiveImageSceneID))
                        {
                            try
                            {
                                //read in visibility of webcam
                                //if false, tell obs to set to true
                                //else, set to false
                                int webcamItemID = GlobalObjects._OBS.GetSceneItemId(currSceneName, Properties.Settings.Default.OBSWebcamSceneID, 0);
                                bool webcamEnabled = GlobalObjects._OBS.GetSceneItemEnabled(currSceneName, webcamItemID);

                                int reactiveImageID = GlobalObjects._OBS.GetSceneItemId(currSceneName, "Reactive Images - Myself", 0);
                                bool reactiveImageEnabled = GlobalObjects._OBS.GetSceneItemEnabled(currSceneName, reactiveImageID);

                                if (!webcamEnabled)
                                {
                                    GlobalObjects._OBS.SetSceneItemEnabled(currSceneName, webcamItemID, true);
                                    GlobalObjects._OBS.SetSceneItemEnabled(currSceneName, reactiveImageID, false);
                                    WPFUtility.WriteToLog("Toggle Webcam: Webcam enabled");
                                }
                                else
                                {
                                    GlobalObjects._OBS.SetSceneItemEnabled(currSceneName, webcamItemID, false);
                                    GlobalObjects._OBS.SetSceneItemEnabled(currSceneName, reactiveImageID, true);
                                    WPFUtility.WriteToLog("Toggle Webcam: Webcam disabled");
                                }
                            }
                            catch (Exception except)
                            {
                                WPFUtility.WriteToLog("Toggle webcam: " + except.Message);
                            }
                        }
                        else
                            WPFUtility.WriteToLog("Toggle webcam: The OBS webcam and/or reactive image names saved in settings don't match up to items in the current OBS scene. Please check to make sure you entered the right names.");
                    }
                    else
                    {
                        WPFUtility.WriteToLog("Toggle webcam: Couldn't run due to OBS not running or not being able to connect to the OBS websocket");
                    }
                        break;

                case "tts (random speech rate)":
                    Random random = new Random();
                    int randRate = random.Next(1, 21) - 10;

                    TwitchUtility.TtsRedeem(pointsRedemption, randRate);
                    break;

                case "tts (normal speech rate)":
                    TwitchUtility.TtsRedeem(pointsRedemption, SpeechSynthesis.SPEECHSYNTH_RATE);
                    break;

                case "1st":
                    TwitchUtility.FirstRedeem(pointsRedemption);
                    break;
            }
        }
        
        //Handled in TwitchAdBreaks.cs
        //private void PubSub_OnCommercial(object sender, OnCommercialArgs e)
        //{
        //    //do actual OnCommercial handling on new thread (method will sleep the thread)
        //    new Thread(delegate () {
        //        OnCommercial_NewThread(sender, e);
        //    }).Start();
        //}
        

        //
        //----------------------End of PubSub Event Hookups----------------------
        //
    }
}
