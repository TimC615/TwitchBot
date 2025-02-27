using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchBot.Utility_Code;
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
                        try
                        {
                            WPFUtility.WriteToLog("toggle webcam triggered");

                            //read in visibility of webcam
                            //if false, tell obs to set to true
                            //else, set to false
                            string currSceneName = GlobalObjects._OBS.GetCurrentProgramScene();
                            int webcamItemID = GlobalObjects._OBS.GetSceneItemId(currSceneName, "iPhone Webcam - Elgato Camera Hub", 0);
                            bool webcamEnabled = GlobalObjects._OBS.GetSceneItemEnabled(currSceneName, webcamItemID);

                            //Log("1: " + e.RewardRedeemed.Redemption.Status);

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
                            //e.RewardRedeemed.Redemption.Status = "FULFILLED";

                            //Log("2: " + e.RewardRedeemed.Redemption.Status);
                        }
                        catch (Exception except)
                        {
                            WPFUtility.WriteToLog("Toggle webcam: " + except.Message);

                            //Log(e.RewardRedeemed.Redemption.Status);
                        }
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
