using System;
using System.Collections.Generic;
using Runner.Core;


public class DummyBot : IPlayer
{
    private readonly Direction direction;
    private readonly Random random = new Random();
    private Map map;
    private ushort myId;

    public DummyBot(Direction direction)
    {
        this.direction = direction;
    }

    public DummyBot(Map map, ushort myId)
    {
        this.map = map;
        this.myId = myId;

    }

    public Map Map
    {
        set { this.map = value; }
    }

    public ushort Id
    {
        set { this.myId = value; }
    }

    public IEnumerable<Move> GetMoves()
    {
        var moves = new List<Move>();
        for (ushort x = 0; x < this.map.Width; x++)
        {
            for (ushort y = 0; y < this.map.Height; y++)
            {
                if (this.map[x, y].Owner == this.myId)
                {
                    Direction move = (Direction)random.Next(5);

                    moves.Add(new Move
                    {
                        Location = new Location { X = x, Y = y },
                        Direction = move
                    });
                }
            }
        }

        return moves;
    }

    public void GameStart()
    {
    }

    public void GameStop(int winnerId)
    {
    }

    public void JourneyStop()
    {
    }

    public void JourneyStart()
    {

    }

    public string Name { get; set; } = "RandomBot";
}