using ConvNetSharp;
using ConvNetSharp.Layers;
using ConvNetSharp.Training;
using System;
using System.IO;
using System.Linq;

namespace Training
{
    class SingleNetTraining
    {
        public static void Run()
        {
            const string NetName = "net.dat";

            var random = new Random(RandomUtilities.Seed);
            var entryContainer = new EntryContainer();

            #region Load Net

            // Load IA - Direction choice
            Net singleNet = NetExtension.LoadOrCreateNet(NetName, () =>
           {
               var net = new Net();
               net.AddLayer(new InputLayer(19, 19, 3));
               net.AddLayer(new ConvLayer(5, 5, 32) { Stride = 2 });
               net.AddLayer(new ReluLayer());
               net.AddLayer(new FullyConnLayer(5));
               net.AddLayer(new SoftmaxLayer(5));
               return net;
           }) as Net;

            #endregion

            #region Load data

            var enumerable = Directory.EnumerateFiles("D:\\Pro\\Temp\\replays\\test\\", "*.hlt").ToList();
            //var enumerable = Directory.EnumerateFiles("D:\\Pro\\Temp\\replays\\erdman_mnmalaguti\\", "*.hlt").ToList();
            int total = enumerable.Count;
            foreach (var file in enumerable)
            {
                Console.WriteLine(total--);
                HltReader reader = new HltReader(file);

                var playerId = -1;
                //var playerToCopy = reader.PlayerNames.FirstOrDefault(o => o.StartsWith("acouette"));
                //var playerToCopy = reader.PlayerNames.FirstOrDefault(o => o.StartsWith("nmalaguti"));
                var playerToCopy = reader.PlayerNames.FirstOrDefault(o => o.StartsWith("erdman") || o.StartsWith("nmalaguti"));

                //var playerToCopy = reader.PlayerNames.FirstOrDefault(o => o.StartsWith("erdman") || o.StartsWith("timfoden") || o.StartsWith("nmalaguti") || o.StartsWith("djma") || o.StartsWith("david-wu"));
                if (playerToCopy != null)
                {
                    playerId = reader.PlayerNames.IndexOf(playerToCopy) + 1;
                }

                if (playerId != -1)
                {
                    var width = reader.Width;
                    var height = reader.Height;

                    int lastmoveCount = 1;

                    int v = Math.Min(reader.FrameCount - 1, 200);
                    for (var frame = 0; frame < v; frame++)
                    {
                        var currentFrame = reader.GetFrame(frame);
                        var map = currentFrame.map;
                        var moves = currentFrame.moves;

                        var helper = new Helper(map, (ushort)playerId);

                        bool foundInFrame = false;
                        int moveCount = 0;

                        // moves
                        for (ushort x = 0; x < width; x++)
                        {
                            for (ushort y = 0; y < height; y++)
                            {
                                if (map[x, y].Owner == playerId)
                                {
                                    foundInFrame = true;
                                    moveCount++;

                                    if (random.NextDouble() < 1.0 / lastmoveCount)
                                    {
                                        var volume = map.GetVolume(singleNet.Layers[0].InputWidth, playerId, x, y);
                                        var direction = moves[y][x];

                                        var entry1 = new Entry(volume, direction, x, y, frame, file.GetHashCode());
                                        entryContainer.Add(entry1);
                                        var entry2 = new Entry(volume.Flip(VolumeUtilities.FlipMode.LeftRight), (int)Helper.FlipLeftRight((Direction)direction), x, y, frame, file.GetHashCode());
                                        entryContainer.Add(entry2);
                                        var entry3 = new Entry(volume.Flip(VolumeUtilities.FlipMode.UpDown), (int)Helper.FlipUpDown((Direction)direction), x, y, frame, file.GetHashCode());
                                        entryContainer.Add(entry3);
                                        var entry4 = new Entry(volume.Flip(VolumeUtilities.FlipMode.Both), (int)Helper.FlipBothWay((Direction)direction), x, y, frame, file.GetHashCode());
                                        entryContainer.Add(entry4);
                                    }
                                }
                            }
                        }

                        lastmoveCount = moveCount;

                        if (!foundInFrame)
                        {
                            // player has died
                            break;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("not found");
                }
            }

            var length = entryContainer.Shuffle();
            Console.WriteLine(entryContainer.Summary);

            #endregion

            #region Training

            var trainer = new AdamTrainer(singleNet) { BatchSize = 1024, LearningRate = 0.001, Beta1 = 0.9, Beta2 = 0.99, Eps = 1e-8, L2Decay = 0.01 };
            //var trainer = new AdadeltaTrainer(singleNet) { BatchSize = 512, Eps = 1e-8 };
            //var trainer = new SgdTrainer(singleNet) { BatchSize = 512, LearningRate = 0.001 };
            var trainingScheme = new TrainingScheme(singleNet, trainer, entryContainer, "single");
            bool save = false;
            do
            {
                for (int i = 0; i < 1000; i++)
                {
                    //if (i > 2)
                    //{
                    //    trainer.L2Decay = 0.001;
                    //}

                    Console.WriteLine($"Epoch #{i + 1}");

                    if (i % 50 == 0)
                    {
                        trainer.LearningRate = Math.Max(trainer.LearningRate / 10.0, 0.00001);
                    }
                    trainingScheme.RunEpoch();

                    #region Save Nets

                    if (save)
                    {
                        singleNet.SaveNet(NetName);
                    }
                    #endregion

                    if (Console.KeyAvailable)
                    {
                        break;
                    }
                }
            } while (!Console.KeyAvailable);

            #endregion
        }
    }
}
