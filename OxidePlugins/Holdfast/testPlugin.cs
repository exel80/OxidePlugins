using System;
using System.Linq;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using HoldfastGame;

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

        private void OnPlayerChat(RoundPlayer roundplayer, string message)
        {
            WeaponHolder w;
            w.
            PlayerBase p = roundplayer.PlayerBase;
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
