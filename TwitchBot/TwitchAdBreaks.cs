using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using TwitchBot.Utility_Code;
using TwitchLib.PubSub.Events;

namespace TwitchBot
{
    internal class TwitchAdBreaks
    {
        async void OnCommercial_NewThread(object sender, OnCommercialArgs e)
        {
            int commercialBreakLength = e.Length;
            int threadSleepLength = commercialBreakLength * 1000;

            WPFUtility.WriteToLog("EventSub: Ads started for " + commercialBreakLength + " seconds");

            await MainWindow.AppWindow.CheckAccessToken();

            if (commercialBreakLength >= 60)
            {
                double commercialBreakLengthMin = (double)commercialBreakLength / 60;
                GlobalObjects._TwitchClient.SendMessage(GlobalObjects.TwitchChannelName, "Ads have started and will last for " + commercialBreakLengthMin
                    + " minutes. Feel free to stretch a bit, hydrate, or just chill out in chat!");
            }
            else
            {
                GlobalObjects._TwitchClient.SendMessage(GlobalObjects.TwitchChannelName, "Ads have started and will last for " + commercialBreakLength
                    + " seconds. Feel free to stretch a bit, hydrate, or just chill out in chat!");
            }

            Thread.Sleep(threadSleepLength);
            WPFUtility.WriteToLog("PubSub_OnCommercial: Ads have finished");
            //GlobalObjects._TwitchClient.SendMessage(GlobalObjects.TwitchChannelName, "Ad break is now done!");
        }
    }
}
