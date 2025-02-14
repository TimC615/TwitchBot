using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Api;

namespace TwitchBot
{
    public class GlobalObjects
    {
        public static TwitchAPI _TwitchAPI = new TwitchAPI();
        public static string TwitchBroadcasterUserId = "-1";

        public static bool botIsActive = false;
    }
}
