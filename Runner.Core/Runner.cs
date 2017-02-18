using System;
using System.Collections.Generic;
using System.Linq;

namespace Runner.Core
{
    public static class Runner
    {
        public static IPlayer Run(IPlayer[] players, ushort mapWidth = 25, ushort mapHeight = 25, bool outputHltFile = true)
        {
            var networking = new Networking();

            foreach (var player in players)
            {
                networking.AddPlayer(player);
            }

            // Journey starts
            foreach (var player in networking.Players)
            {
                player.JourneyStart();
            }

            var seed = (ulong)DateTime.Now.Ticks;
            var t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var secondsSinceEpoch = (int)t.TotalSeconds;
            var id = secondsSinceEpoch;

            var game = new Halite(mapWidth, mapHeight, seed, networking, outputHltFile);

            List<string> names = networking.Players.Select(p => p.Name).ToList();
            var stats = game.runGame(names, seed, id);

            // Journey stops
            foreach (var player in networking.Players)
            {
                player.JourneyStop();
            }

            var bestStats = stats.player_statistics.First(o => o.rank == 0);
            return networking.Players[stats.player_statistics.IndexOf(bestStats)];
        }
    }
}
