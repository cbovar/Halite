using ConvNetSharp;
using ConvNetSharp.Fluent;
using ConvNetSharp.Training;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Training
{
    class TripleFluentNetTraining
    {
        public static void Run()
        {
            var random = new Random(RandomUtilities.Seed);

            int normalInputWidth = 19;
            int earlyInputWidth = 19;
            int strongInputWidth = 19;

            string NetName = $"net.dat";
            string NetName_early = $"net_early.dat";
            string NetName_strong = $"net_strong.dat";

            var entryContainer = new EntryContainer();
            var entryContainer_early = new EntryContainer();
            var entryContainer_strong = new EntryContainer();

            #region Load Net

            INet singleNet = NetExtension.LoadOrCreateNet(NetName, () =>
            {
                var net = FluentNet.Create(normalInputWidth, normalInputWidth, 3)
                     .Conv(5, 5, 16).Stride(5).Pad(2)
                     .Tanh()
                     .Conv(3, 3, 16).Stride(1).Pad(1)
                     .Tanh()
                     .FullyConn(100)
                     .Relu()
                     .FullyConn(5)
                     .Softmax(5).Build();

                return net;
            });

            INet singleNet_early = NetExtension.LoadOrCreateNet(NetName_early, () =>
            {
                var net = FluentNet.Create(earlyInputWidth, earlyInputWidth, 3)
                    .Conv(5, 5, 16).Stride(5).Pad(2)
                    .Tanh()
                    .Conv(3, 3, 16).Stride(1).Pad(1)
                    .Tanh()
                    .FullyConn(100)
                    .Relu()
                    .FullyConn(5)
                    .Softmax(5).Build();

                return net;
            });

            INet singleNet_strong = NetExtension.LoadOrCreateNet(NetName_strong, () =>
             {
                 var net = FluentNet.Create(strongInputWidth, strongInputWidth, 3)
                    .Conv(5, 5, 16).Stride(5).Pad(2)
                    .Tanh()
                    .Conv(3, 3, 16).Stride(1).Pad(1)
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

                if (playerToCopy != null)
                {
                    playerId = reader.PlayerNames.IndexOf(playerToCopy) + 1;
                }

                if (playerId != -1)
                {
                    var width = reader.Width;
                    var height = reader.Height;

                    int lastmoveCount = 1;

                    for (var frame = 0; frame < reader.FrameCount - 1; frame++)
                    {
                        bool earlyGame = lastmoveCount < 25;

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
                                    bool strong = map[x, y].Strength > 200;
                                    foundInFrame = true;
                                    moveCount++;

                                    if ((earlyGame && random.NextDouble() < 1.5 / lastmoveCount) || (strong && random.NextDouble() < 1.5 / lastmoveCount) || random.NextDouble() < 1.0 / lastmoveCount)
                                    {
                                        var w = normalInputWidth;
                                        var container = entryContainer;

                                        if (earlyGame)
                                        {
                                            w = earlyInputWidth;
                                            container = entryContainer_early;
                                        }
                                        else if (strong)
                                        {
                                            w = strongInputWidth;
                                            container = entryContainer_strong;
                                        }

                                        var convVolume = map.GetVolume(w, playerId, x, y);

                                        var direction = moves[y][x];

                                        var entry1 = new Entry(new[] { convVolume }, direction, x, y, frame, file.GetHashCode());
                                        container.Add(entry1);
                                        var entry2 = new Entry(new[] { convVolume.Flip(VolumeUtilities.FlipMode.LeftRight) }, (int)Helper.FlipLeftRight((Direction)direction), x, y, frame, file.GetHashCode());
                                        container.Add(entry2);
                                        var entry3 = new Entry(new[] { convVolume.Flip(VolumeUtilities.FlipMode.UpDown) }, (int)Helper.FlipUpDown((Direction)direction), x, y, frame, file.GetHashCode());
                                        container.Add(entry3);
                                        var entry4 = new Entry(new[] { convVolume.Flip(VolumeUtilities.FlipMode.Both) }, (int)Helper.FlipBothWay((Direction)direction), x, y, frame, file.GetHashCode());
                                        container.Add(entry4);
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
            Console.WriteLine("normal: " + entryContainer.Summary);
            length = entryContainer_early.Shuffle();
            Console.WriteLine("early: " + entryContainer_early.Summary);
            length = entryContainer_strong.Shuffle();
            Console.WriteLine("strong " + entryContainer_strong.Summary);

            #endregion

            #region Training

            var trainer = new AdamTrainer(singleNet) { BatchSize = 1024, LearningRate = 0.01, Beta1 = 0.9, Beta2 = 0.99, Eps = 1e-8 };
            var trainingScheme = new TrainingScheme(singleNet, trainer, entryContainer, "single");

            var trainer_early = new AdamTrainer(singleNet_early) { BatchSize = 1024, LearningRate = 0.01, Beta1 = 0.9, Beta2 = 0.99, Eps = 1e-8 };
            var trainingScheme_early = new TrainingScheme(singleNet_early, trainer_early, entryContainer_early, "single_early");

            var trainer_strong = new AdamTrainer(singleNet_strong) { BatchSize = 1024, LearningRate = 0.01, Beta1 = 0.9, Beta2 = 0.99, Eps = 1e-8 };
            var trainingScheme_strong = new TrainingScheme(singleNet_strong, trainer_strong, entryContainer_strong, "single_strong");

            bool save = true;
            double lastValidationAcc = 0.0;
            double lastValidationAcc_early = 0.0;
            double lastValidationAcc_strong = 0.0;
            double lastTrainAcc = 0.0;
            double lastTrainAcc_early = 0.0;
            double lastTrainAcc_strong = 0.0;
            do
            {
                var normal = Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < 50; i++)
                    {
                        if (i > 5)
                        {
                            trainer.L2Decay = 0.05;
                        }

                        Console.WriteLine($"[normal] Epoch #{i + 1}");

                        if (i % 50 == 0)
                        {
                            trainer.LearningRate = Math.Max(trainer.LearningRate / 5.0, 0.00001);
                        }

                        trainingScheme.RunEpoch();

                        #region Save Nets

                        if (save)
                        {
                            if (trainingScheme.ValidationAccuracy > lastValidationAcc)
                            {
                                lastValidationAcc = trainingScheme.ValidationAccuracy;
                                lastTrainAcc = trainingScheme.TrainAccuracy;
                                singleNet.SaveNet(NetName);
                            }
                        }
                        #endregion

                        if (Console.KeyAvailable)
                        {
                            break;
                        }
                    }
                });

                var early = Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < 50; i++)
                    {
                        if (i > 5)
                        {
                            trainer_early.L2Decay = 0.05;
                        }

                        Console.WriteLine($"[early] Epoch #{i + 1}");

                        if (i % 50 == 0)
                        {
                            trainer_early.LearningRate = Math.Max(trainer_early.LearningRate / 5.0, 0.00001);
                        }

                        trainingScheme_early.RunEpoch();

                        #region Save Nets

                        if (save)
                        {
                            if (trainingScheme_early.ValidationAccuracy > lastValidationAcc_early)
                            {
                                lastValidationAcc_early = trainingScheme_early.ValidationAccuracy;
                                lastTrainAcc_early = trainingScheme_early.TrainAccuracy;
                                singleNet_early.SaveNet(NetName_early);
                            }
                        }
                        #endregion

                        if (Console.KeyAvailable)
                        {
                            break;
                        }
                    }
                });

                var strong = Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < 50; i++)
                    {
                        if (i > 5)
                        {
                            trainer_strong.L2Decay = 0.05;
                        }

                        Console.WriteLine($"[strong] Epoch #{i + 1}");

                        if (i % 50 == 0)
                        {
                            trainer_strong.LearningRate = Math.Max(trainer_strong.LearningRate / 5.0, 0.00001);
                        }

                        trainingScheme_strong.RunEpoch();

                        #region Save Nets

                        if (save)
                        {
                            if (trainingScheme_strong.ValidationAccuracy > lastValidationAcc_strong)
                            {
                                lastValidationAcc_strong = trainingScheme_strong.ValidationAccuracy;
                                lastTrainAcc_strong = trainingScheme_strong.TrainAccuracy;
                                singleNet_strong.SaveNet(NetName_strong);
                            }
                        }
                        #endregion

                        if (Console.KeyAvailable)
                        {
                            break;
                        }
                    }
                });

                Task.WaitAll(new[] { normal, strong, early });
            }
            while (!Console.KeyAvailable);

            #endregion
        }
    }
}

