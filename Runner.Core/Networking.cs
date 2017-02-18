using System.Collections.Generic;

namespace Runner.Core
{
    public class Networking
    {
        public List<IPlayer> Players { get; } = new List<IPlayer>();

        public void AddPlayer(IPlayer player)
        {
            this.Players.Add(player);
        }

        public int PlayerCount => this.Players.Count;
    }
}