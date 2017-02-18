using System.Collections.Generic;
using ConvNetSharp;
using Runner.Core;
using System;
using ConvNetSharp.Fluent;

namespace MyBot
{
    public class SingleNetBot : IPlayer
    {
        private Helper helper;
        private int frame;
        private double lastElapsed = -1;
        private bool degradedMode = false;
        private Map map;
        private INet net;
        int lastmoveCount = 1;
        private ushort myId;
        private Random random = new Random();
        private int inputWidth;

        public SingleNetBot(Map map, ushort myID)
        {
            this.map = map;
            this.myId = myID;
        }

        public SingleNetBot()
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
            var chrono = System.Diagnostics.Stopwatch.StartNew();

            try
            {
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

                            if (this.map[x, y].Strength > 29)
                            {
                                // Evaluate network if
                                // - not in degraded mode
                                // OR degraded mode and (frame + x + y) % 4 == 0. Used to evaluate only 25% of the tiles
                                // OR strength is 255
                                if (!degradedMode || ((frame + x + y) % 4 == 0) || this.map[x, y].Strength == 255)
                                {
                                    // Create input
                                    var volume = this.map.GetVolume(this.inputWidth, this.myId, x, y);
                                    this.net.Forward(volume);

                                    // Get prediction
                                    move = (Direction)this.net.GetPrediction();
                                }
                            }

                            moves.Add(new Move
                            {
                                Location = new Location { X = x, Y = y },
                                Direction = move
                            });

                            if (chrono.Elapsed.TotalMilliseconds > 900)
                            {
                                // We took too much time -> we exit now
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
            this.net = NetExtension.LoadNet($"{this.Prefix}net.dat");

            FluentNet fluentNet = net as FluentNet;
            this.inputWidth = fluentNet.InputLayers[0].InputWidth;
        }
    }
}