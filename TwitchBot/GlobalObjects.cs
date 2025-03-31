using OBSWebsocketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.Client;

namespace TwitchBot
{
    public class GlobalObjects
    {
        public static TwitchAPI _TwitchAPI = new TwitchAPI();
        public static TwitchClient _TwitchClient = null;
        public static string TwitchBroadcasterUserId = "-1";
        public static string TwitchChannelName = "";

        public static EventSubSubscription[] EventSubSubscribedEvents = null;

        public static OBSWebsocket _OBS = null;
        public static readonly string ObsTtsTalkingHeadName = "TTS Talking Head";

        public static bool botIsActive = false;
    }
}
