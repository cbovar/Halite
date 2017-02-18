using ConvNetSharp;
using ConvNetSharp.Fluent;
using ConvNetSharp.Training;
using System;
using System.IO;
using System.Linq;

namespace Training
{
    class FluentNetTraining
    {
        public static void Run()
        {
            const string NetName = "net.dat";

            var random = new Random();
            var entryContainer = new EntryContainer();

            #region Load Net

            var convInputWith = 11; // Will extract 11x11 area
            if (convInputWith % 2 == 0)
            {
                throw new ArgumentException("convInputWith must be odd");
            }

            // Load IA or initialize new network if not found - Direction choice
            INet singleNet = NetExtension.LoadOrCreateNet(NetName, () =>
           {
               var net = FluentNet.Create(convInputWith, convInputWith, 3)
                    .Conv(3, 3, 16).Stride(2)
                    .Tanh()
                    .Conv(2, 2, 16)
                    .Tanh()
                    .FullyConn(100)
                    .Relu()
                    .FullyConn(5)
                    .Softmax(5).Build();

               return net;
           });

            #endregion

            #region Load data

            var hltFiles = Directory.EnumerateFiles(@"..\..\..\games\2609\", "*.hlt").ToList(); // erdman games downloaded with HltDownloader
            int total = hltFiles.Count;
            Console.WriteLine($"Loading {total} games...");

            foreach (var file in hltFiles)
            {
                Console.WriteLine(total--);
                HltReader reader = new HltReader(file);

                var playerId = -1;
                var playerToCopy = reader.PlayerNames.FirstOrDefault(o => o.StartsWith("erdman"));

                if (playerToCopy == null)
                {
                    Console.WriteLine("Player not found");
                    continue;
                }

                playerId = reader.PlayerNames.IndexOf(playerToCopy) + 1;

                var width = reader.Width;
                var height = reader.Height;

                int lastmoveCount = 1;

                for (var frame = 0; frame < reader.FrameCount - 1; frame++)
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
                                    var convVolume = map.GetVolume(convInputWith, playerId, x, y); // Input
                                    var direction = moves[y][x]; // Output

                                    var entry1 = new Entry(new[] { convVolume }, direction, x, y, frame, file.GetHashCode());
                                    entryContainer.Add(entry1);

                                    // Data augmentation
                                    var entry2 = new Entry(new[] { convVolume.Flip(VolumeUtilities.FlipMode.LeftRight) }, (int)Helper.FlipLeftRight((Direction)direction), x, y, frame, file.GetHashCode());
                                    entryContainer.Add(entry2);
                                    var entry3 = new Entry(new[] { convVolume.Flip(VolumeUtilities.FlipMode.UpDown) }, (int)Helper.FlipUpDown((Direction)direction), x, y, frame, file.GetHashCode());
                                    entryContainer.Add(entry3);
                                    var entry4 = new Entry(new[] { convVolume.Flip(VolumeUtilities.FlipMode.Both) }, (int)Helper.FlipBothWay((Direction)direction), x, y, frame, file.GetHashCode());
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

            var length = entryContainer.Shuffle();
            Console.WriteLine(entryContainer.Summary);

            #endregion

            #region Training

            var trainer = new AdamTrainer(singleNet) { BatchSize = 1024, LearningRate = 0.1, Beta1 = 0.9, Beta2 = 0.99, Eps = 1e-8 };
            var trainingScheme = new TrainingScheme(singleNet, trainer, entryContainer, "single");
            bool save = true;
            double lastValidationAcc = 0.0;

            do
            {
                for (int i = 0; i < 1000; i++)
                {
                    if (i > 5)
                    {
                        trainer.L2Decay = 0.001;
                    }

                    Console.WriteLine($"Epoch #{i + 1}");

                    if (i % 15 == 0)
                    {
                        trainer.LearningRate = Math.Max(trainer.LearningRate / 10.0, 0.00001);
                    }

                    trainingScheme.RunEpoch();

                    #region Save Nets

                    if (save)
                    {
                        // Save if validation accuracy has improved
                        if (trainingScheme.ValidationAccuracy > lastValidationAcc)
                        {
                            lastValidationAcc = trainingScheme.ValidationAccuracy;
                            singleNet.SaveNet(NetName);
                        }
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
