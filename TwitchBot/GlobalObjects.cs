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
        public static TwitchAPI _TwitchAPIBotAccount = new TwitchAPI();
        public static string TwitchBroadcasterUserId = null;
        public static string TwitchChannelName = null;
        public static string TwitchMessageBotUserId = null;
        public static string TwitchMessageBotName = null;

        public static EventSubSubscription[] EventSubSubscribedEvents = null;

        public static OBSWebsocket _OBS = null;
        public static readonly string ObsTtsTalkingHeadName = "TTS Talking Head";

        public static bool botIsActive = false;
        public static bool twitchPlaysActive = false;

        public static TwitchPlays _TwitchPlays = null;
        public static TwitchChatCommands _TwitchChatCommands = null;
    }
}
