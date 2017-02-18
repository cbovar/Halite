using System.Collections.Generic;
using System.ComponentModel;

namespace Runner.Core
{
    public interface IPlayer
    {
        string Name { get; }

        Map Map { set; }

        ushort Id { set; }

        IEnumerable<Move> GetMoves();

        void GameStart();

        void GameStop(int winnerId);

        void JourneyStop();

        void JourneyStart();
    }
}