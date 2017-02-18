using ConvNetSharp;
using ConvNetSharp.Layers;
using ConvNetSharp.Training;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Training
{
    class ThreeStageTraining
    {
        public static void Run()
        {
            const string DirectionName = "direction.dat";
            const string MoveName = "move.dat";
            const string DirectionName_early = "direction_early.dat";
            const string MoveName_early = "move_early.dat";
            const string DirectionName_strong = "direction_strong.dat";
            const string MoveName_strong = "move_strong.dat";

            var random = new Random(RandomUtilities.Seed);
            var directionDataContainer_early = new EntryContainer();
            var moveDataContainer_early = new EntryContainer();
            var directionDataContainer_strong = new EntryContainer();
            var moveDataContainer_strong = new EntryContainer();
            var directionDataContainer = new EntryContainer();
            var moveDataContainer = new EntryContainer();

            #region Load Nets

            // Load IA - Direction choice
            Net directionNet = NetExtension.LoadOrCreateNet(DirectionName, () =>
            {
                var net = new Net();
                net.AddLayer(new InputLayer(11, 11, 3));
                net.AddLayer(new FullyConnLayer(11 * 11 * 3));
                net.AddLayer(new ReluLayer());
                net.AddLayer(new FullyConnLayer(11 * 11 * 3));
                net.AddLayer(new ReluLayer());
                net.AddLayer(new FullyConnLayer(11 * 11 * 3));
                net.AddLayer(new ReluLayer());
                net.AddLayer(new FullyConnLayer(4));
                net.AddLayer(new SoftmaxLayer(4));
                return net;
            }) as Net;

            // Load IA - Move choice
            Net moveNet = NetExtension.LoadOrCreateNet(MoveName, () =>
            {
                var net = new Net();
                net.AddLayer(new InputLayer(7, 7, 3));
                net.AddLayer(new FullyConnLayer(7 * 7 * 3));
                net.AddLayer(new FullyConnLayer(2));
                net.AddLayer(new SoftmaxLayer(2));
                return net;
            }) as Net;

            // Load IA - Direction choice
            Net directionNet_early = NetExtension.LoadOrCreateNet(DirectionName_early, () =>
            {
                var net = new Net();
                net.AddLayer(new InputLayer(13, 13, 3));
                net.AddLayer(new FullyConnLayer(13 * 13 * 3));
                net.AddLayer(new ReluLayer());
                net.AddLayer(new FullyConnLayer(13 * 13 * 3));
                net.AddLayer(new ReluLayer());
                net.AddLayer(new FullyConnLayer(13 * 13 * 3));
                net.AddLayer(new ReluLayer());
                net.AddLayer(new FullyConnLayer(4));
                net.AddLayer(new SoftmaxLayer(4));
                return net;
            }) as Net;

            // Load IA - Move choice
            Net moveNet_early = NetExtension.LoadOrCreateNet(MoveName_early, () =>
            {
                var net = new Net();
                net.AddLayer(new InputLayer(13, 13, 3));
                net.AddLayer(new FullyConnLayer(13 * 13 * 3));
                net.AddLayer(new ReluLayer());
                net.AddLayer(new FullyConnLayer(2));
                net.AddLayer(new SoftmaxLayer(2));
                return net;
            }) as Net;

            // Load IA - Direction choice
            Net directionNet_strong = NetExtension.LoadOrCreateNet(DirectionName_strong, () =>
            {
                var net = new Net();
                net.AddLayer(new InputLayer(9, 9, 3));
                net.AddLayer(new FullyConnLayer(9 * 9 * 3));
                net.AddLayer(new ReluLayer());
                net.AddLayer(new FullyConnLayer(9 * 9 * 3));
                net.AddLayer(new ReluLayer());
                //net.AddLayer(new FullyConnLayer(9 * 9 * 3));
                //net.AddLayer(new ReluLayer());
                net.AddLayer(new FullyConnLayer(4));
                net.AddLayer(new SoftmaxLayer(4));
                return net;
            }) as Net;

            // Load IA - Move choice
            Net moveNet_strong = NetExtension.LoadOrCreateNet(MoveName_strong, () =>
            {
                var net = new Net();
                net.AddLayer(new InputLayer(7, 7, 3));
                net.AddLayer(new FullyConnLayer(7 * 7 * 3));
                net.AddLayer(new FullyConnLayer(2));
                net.AddLayer(new SoftmaxLayer(2));
                return net;
            }) as Net;

            #endregion

            #region Load data

            var enumerable = Directory.EnumerateFiles("D:\\Pro\\Temp\\replays\\erdman_200\\", "*.hlt").ToList();
            int total = enumerable.Count;
            foreach (var file in enumerable)
            {
                Console.WriteLine(total--);
                HltReader reader = new HltReader(file);

                var playerId = -1;
                //var playerToCopy = reader.PlayerNames.FirstOrDefault(o => o.StartsWith("nmalaguti"));
                var playerToCopy = reader.PlayerNames.FirstOrDefault(o => o.StartsWith("erdman"));
                //var playerToCopy = reader.PlayerNames.FirstOrDefault(o => o.StartsWith("erdman") || o.StartsWith("timfoden") || o.StartsWith("nmalaguti") || o.StartsWith("djma") || o.StartsWith("david-wu"));
                //var playerToCopy = reader.PlayerNames.FirstOrDefault(o => o.StartsWith("erdman") || o.StartsWith("nmalaguti") || o.StartsWith("djma"));
                if (playerToCopy != null)
                {
                    playerId = reader.PlayerNames.IndexOf(playerToCopy) + 1;
                }

                if (playerId != -1)
                {
                    var width = reader.Width;
                    var height = reader.Height;

                    int lastmoveCount = 1;

                    int v = Math.Min(reader.FrameCount - 1, 300);
                    for (var frame = 0; frame < v; frame++)
                    {
                        var currentFrame = reader.GetFrame(frame);
                        var map = currentFrame.map;
                        var moves = currentFrame.moves;

                        bool foundInFrame = false;
                        int moveCount = 0;


                        // moves
                        for (ushort x = 0; x < width; x++)
                        {
                            for (ushort y = 0; y < height; y++)
                            {
                                if (map[x, y].Owner == playerId)
                                {
                                    var strength = map[x, y].Strength;
                                    var currentMoveNet = lastmoveCount < 10 ? moveNet_early : (strength > 200 ? moveNet_strong : moveNet);
                                    var currentDirectionNet = lastmoveCount < 10 ? directionNet_early : (strength > 200 ? directionNet_strong : directionNet);
                                    var currentMoveContainer = lastmoveCount < 10 ? moveDataContainer_early : (strength > 200 ? moveDataContainer_strong : moveDataContainer);
                                    var currentDirectionContainer = lastmoveCount < 10 ? directionDataContainer_early : (strength > 200 ? directionDataContainer_strong : directionDataContainer);

                                    foundInFrame = true;
                                    moveCount++;

                                    var direction = moves[y][x];

                                    if (random.NextDouble() < 2.0 / lastmoveCount)
                                    {
                                        var volume = map.GetVolume(currentMoveNet.Layers[0].InputWidth, playerId, x, y);

                                        int shouldMove = direction == (int)Direction.Still ? 0 : 1;

                                        var entry1 = new Entry(volume, shouldMove, x, y, frame, file.GetHashCode());
                                        currentMoveContainer.Add(entry1);
                                        var entry2 = new Entry(volume.Flip(VolumeUtilities.FlipMode.LeftRight), shouldMove, x, y, frame, file.GetHashCode());
                                        currentMoveContainer.Add(entry2);
                                        var entry3 = new Entry(volume.Flip(VolumeUtilities.FlipMode.UpDown), shouldMove, x, y, frame, file.GetHashCode());
                                        currentMoveContainer.Add(entry3);
                                        var entry4 = new Entry(volume.Flip(VolumeUtilities.FlipMode.Both), shouldMove, x, y, frame, file.GetHashCode());
                                        currentMoveContainer.Add(entry4);

                                        if (direction != 0)
                                        {
                                            volume = map.GetVolume(currentDirectionNet.Layers[0].InputWidth, playerId, x, y);

                                            entry1 = new Entry(volume, direction - 1, x, y, frame, file.GetHashCode());
                                            currentDirectionContainer.Add(entry1);
                                            entry2 = new Entry(volume.Flip(VolumeUtilities.FlipMode.LeftRight), (int)Helper.FlipLeftRight((Direction)direction) - 1, x, y, frame, file.GetHashCode());
                                            currentDirectionContainer.Add(entry2);
                                            entry3 = new Entry(volume.Flip(VolumeUtilities.FlipMode.UpDown), (int)Helper.FlipUpDown((Direction)direction) - 1, x, y, frame, file.GetHashCode());
                                            currentDirectionContainer.Add(entry3);
                                            entry4 = new Entry(volume.Flip(VolumeUtilities.FlipMode.Both), (int)Helper.FlipBothWay((Direction)direction) - 1, x, y, frame, file.GetHashCode());
                                            currentDirectionContainer.Add(entry4);
                                        }
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
            }

            var dLength = directionDataContainer.Shuffle();
            var mLgenth = moveDataContainer.Shuffle();

            directionDataContainer_early.Shuffle();
            moveDataContainer_early.Shuffle();

            directionDataContainer_strong.Shuffle();
            moveDataContainer_strong.Shuffle();

            Console.WriteLine($"direction:{dLength} move:{mLgenth}");

            #endregion

            #region Training

            var directionTrainer = new AdamTrainer(directionNet) { BatchSize = 1024, LearningRate = 0.001, Beta1 = 0.9, Beta2 = 0.99, Eps = 1e-8 };
            var moveTrainer = new AdamTrainer(moveNet) { BatchSize = 1024, LearningRate = 0.1, Beta1 = 0.9, Beta2 = 0.99, Eps = 1e-8 };

            var directionScheme = new TrainingScheme(directionNet, directionTrainer, directionDataContainer, "direction");
            var moveScheme = new TrainingScheme(moveNet, moveTrainer, moveDataContainer, "move");

            var directionTrainer_early = new AdamTrainer(directionNet_early) { BatchSize = 1024, LearningRate = 0.001, Beta1 = 0.9, Beta2 = 0.99, Eps = 1e-8 };
            var moveTrainer_early = new AdamTrainer(moveNet_early) { BatchSize = 1024, LearningRate = 0.001, Beta1 = 0.9, Beta2 = 0.99, Eps = 1e-8 };

            var directionScheme_early = new TrainingScheme(directionNet_early, directionTrainer_early, directionDataContainer_early, "direction_early");
            var moveScheme_early = new TrainingScheme(moveNet_early, moveTrainer_early, moveDataContainer_early, "move_early");

            var directionTrainer_strong = new AdamTrainer(directionNet_strong) { BatchSize = 1024, LearningRate = 0.001, Beta1 = 0.9, Beta2 = 0.99, Eps = 1e-8 };
            var moveTrainer_strong = new AdamTrainer(moveNet_strong) { BatchSize = 1024, LearningRate = 0.1, Beta1 = 0.9, Beta2 = 0.99, Eps = 1e-8 };

            var directionScheme_strong = new TrainingScheme(directionNet_strong, directionTrainer_strong, directionDataContainer_strong, "direction_strong");
            var moveScheme_strong = new TrainingScheme(moveNet_strong, moveTrainer_strong, moveDataContainer_strong, "move_strong");

            bool save = false;
            double lastDirectionValidationAccuracy = 0.0;
            double lastMoveValidationAccuracy = 0.0;
            double lastDirectionValidationAccuracy_early = 0.0;
            double lastMoveValidationAccuracy_early = 0.0;
            double lastDirectionValidationAccuracy_strong = 0.0;
            double lastMoveValidationAccuracy_strong = 0.0;

            do
            {
                for (int i = 0; i < 1000; i++)
                {
                    //if (i > 100)
                    //{
                    //    directionTrainer.L2Decay = 0.001;
                    //    moveTrainer.L2Decay = 0.001;
                    //}

                    Console.WriteLine($"Epoch #{i + 1}");

                    //if (i % 50 == 0)
                    //{
                    //    directionTrainer.LearningRate = Math.Max(directionTrainer.LearningRate / 2.0, 0.00001);
                    //    moveTrainer.LearningRate = Math.Max(moveTrainer.LearningRate / 2.0, 0.00001);
                    //}
                    //directionScheme.RunEpoch();
                    //moveScheme.RunEpoch();

                    if (i % 50 == 0)
                    {
                        directionTrainer_early.LearningRate = Math.Max(directionTrainer_early.LearningRate / 10.0, 0.00001);
                        moveTrainer_early.LearningRate = Math.Max(moveTrainer_early.LearningRate / 2.0, 0.00001);
                    }
                    //directionScheme_early.RunEpoch(); // overfitted
                    moveScheme_early.RunEpoch();

                    //if (i % 50 == 0)
                    //{
                    //    directionTrainer_strong.LearningRate = Math.Max(directionTrainer_strong.LearningRate / 2.0, 0.00001);
                    //    moveTrainer_strong.LearningRate = Math.Max(moveTrainer_strong.LearningRate / 2.0, 0.00001);
                    //}
                    //directionScheme_strong.RunEpoch();
                    //moveScheme_strong.RunEpoch();

                    #region Save Nets
                    if (save)
                    {
                        if (directionScheme.ValidationAccuracy > lastDirectionValidationAccuracy)
                        {
                            lastDirectionValidationAccuracy = directionScheme.ValidationAccuracy;
                            directionNet.SaveNet(DirectionName);
                        }

                        if (moveScheme.ValidationAccuracy > lastMoveValidationAccuracy)
                        {
                            lastMoveValidationAccuracy = moveScheme.ValidationAccuracy;
                            moveNet.SaveNet(MoveName);
                        }

                        if (directionScheme_early.ValidationAccuracy > lastDirectionValidationAccuracy_early)
                        {
                            lastDirectionValidationAccuracy_early = directionScheme_early.ValidationAccuracy;
                            directionNet_early.SaveNet(DirectionName_early);
                        }

                        if (moveScheme_early.ValidationAccuracy > lastMoveValidationAccuracy_early)
                        {
                            lastMoveValidationAccuracy_early = moveScheme_early.ValidationAccuracy;
                            moveNet_early.SaveNet(MoveName_early);
                        }

                        if (directionScheme_strong.ValidationAccuracy > lastDirectionValidationAccuracy_strong)
                        {
                            lastDirectionValidationAccuracy_strong = directionScheme_strong.ValidationAccuracy;
                            directionNet_strong.SaveNet(DirectionName_strong);
                        }

                        if (moveScheme_strong.ValidationAccuracy > lastMoveValidationAccuracy_strong)
                        {
                            lastMoveValidationAccuracy_strong = moveScheme_strong.ValidationAccuracy;
                            moveNet_strong.SaveNet(MoveName_strong);
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
