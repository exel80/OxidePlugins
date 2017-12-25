using Facepunch.Extend;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("EasyVoteResetVotes", "Exel80", "1.0.0")]
    [Description("Reset player votes, when votes a certain time.")]
    class EasyVoteResetVotes : RustPlugin
    {
        // EasyVote is life and ♥
        [PluginReference] private Plugin EasyVote;

        #region Hooks
        void onUserReceiveReward(BasePlayer player, int voted)
        {
            if(voted <= config.voteTh)
            {
                // Call EasyVote hook
                EasyVote.Call("resetPlayerVotedData", player.UserIDString, config.displayEnabled);
            }
        }
        #endregion

        #region Configuration
        private Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "Display reset message to console (true / false)")]
            public bool displayEnabled;

            [JsonProperty(PropertyName = "Reset player votes when vote over.. (number)")]
            public int voteTh;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    displayEnabled = true,
                    voteTh = 3
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
            }
            catch
            {
                PrintWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion
    }
}
