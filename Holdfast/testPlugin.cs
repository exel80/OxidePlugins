using System;
using System.Linq;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using HoldfastGame;
using uLink;

namespace Oxide.Plugins
{
    [Info("TestPlugin", "Exel80", "1.0.0")]
    [Description("This is really simple and easy plugin.")]
    class testPlugin : HoldfastPlugin
    {
        void Init()
        {
            Puts($"Test successful!");
        }

        private void OnRoundStart() => Puts("! ROUNDSTART !");

        private void OnPlayerJoin(ulong userId, string name) => Puts($"{userId} {name} Join");

        //private void OnPlayerConnected(NetworkPlayer networkPlayer) => Puts($"Connected");

        private void OnPlayerConnected(RoundPlayer roundPlayer) => Puts($"{roundPlayer.PlayerBase.name} Connected");

        private void OnPlayerDisconnected(RoundPlayer roundPlayer) => Puts($"{roundPlayer.PlayerBase.name} Disconnected");

        private void OnPlayerChat(RoundPlayer roundplayer, string message)
        {
            PlayerBase p = roundplayer.PlayerBase;
            IPlayer pp = (IPlayer)roundplayer;

            //pp.Hurt(50f);

            Puts(p.Health.ToString() + " " + roundplayer.WeaponHolder.CanJump.ToString());
            Puts(message);
        }

        private void OnAimCannon(UnityEngine.Vector2 rotationInput) => Puts("!!!");

        [ChatCommand("test")]
        void TestCommand(IPlayer player, string command, string[] args)
        {
            player.Reply("Test successful!");
            Puts("Test successful!");
        }
    }
}
