using ConvNetSharp;
using ConvNetSharp.Fluent;
using System;
using System.Collections.Generic;
using Runner.Core;

namespace MyBot
{
    /// <summary>
    /// Three networks:
    /// - Early game
    /// - Strong pieces (> 200 strength)
    /// - Normal game (the rest)
    /// </summary>
    public class ThreeNetBot : IPlayer
    {
        private Helper helper;
        private int frame;
        private double lastElapsed = -1;
        private bool degradedMode = false;
        private Map map;
        private INet singleNet;
        private INet singleNet_strong;
        private INet singleNet_early;

        int lastmoveCount = 1;

        private ushort myId;

        private Random random = new Random();

        public ThreeNetBot(Map map, ushort myID)
        {
            this.map = map;
            this.myId = myID;

            this.JourneyStart();
            this.GameStart();
        }

        public ThreeNetBot()
        {
        }

        public string Name { get; set; } = "";

        public string Prefix { get; set; } = "";

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

            try
            {
                var chrono = System.Diagnostics.Stopwatch.StartNew();

                bool earlyGame = lastmoveCount < 15;
                int moveCount = 0;

                for (ushort x = 0; x < this.map.Width; x++)
                {
                    for (ushort y = 0; y < this.map.Height; y++)
                    {
                        if (this.map[x, y].Owner == this.myId)
                        {
                            Direction move = Direction.Still;

                            // Check if we are taking too much time
                            if (lastElapsed > 850)
                            {
                                degradedMode = true;
                            }

                            if (this.map[x, y].Strength > 1)
                            {
                                // Evaluate network if
                                // - not in degraded mode
                                // OR degraded mode and (frame + x + y) % 4 == 0. Used to evaluate only 25% of the tiles
                                // OR strength is 255
                                if (!degradedMode || ((frame + x + y) % 4 == 0) || this.map[x, y].Strength == 255)
                                {
                                    // Choose which network to use
                                    FluentNet net = singleNet as FluentNet;
                                    if (earlyGame)
                                    {
                                        net = singleNet_early as FluentNet; ;
                                    }
                                    else if (map[x, y].Strength > 200)
                                    {
                                        net = singleNet_strong as FluentNet; ;
                                    }

                                    // Create input
                                    int inputWidth = net.InputLayers[0].InputWidth;
                                    var volume = this.map.GetVolume(inputWidth, this.myId, x, y);
                                    net.Forward(volume);

                                    // Get prediction
                                    move = (Direction)net.GetPrediction();
                                }
                            }

                            moves.Add(new Move
                            {
                                Location = new Location { X = x, Y = y },
                                Direction = move
                            });

                            if (chrono.Elapsed.TotalMilliseconds > 900)
                            {
                                return moves;
                            }

                            moveCount++;
                        }
                    }
                }

                this.lastmoveCount = moveCount;

                this.frame++;

                lastElapsed = chrono.Elapsed.TotalMilliseconds;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }

            return moves;
        }

        public void GameStart()
        {
            this.frame = 0;
            this.helper = new Helper(this.map, this.myId);
        }

        public void GameStop(int winnerId)
        {
        }

        public void JourneyStop()
        {
        }

        public void JourneyStart()
        {
            this.Load();
        }

        public void Load()
        {
            this.singleNet = NetExtension.LoadNet($"{this.Prefix}net.dat");
            this.singleNet_strong = NetExtension.LoadNet($"{this.Prefix}net_strong.dat");
            this.singleNet_early = NetExtension.LoadNet($"{this.Prefix}net_early.dat");
        }
    }
}
