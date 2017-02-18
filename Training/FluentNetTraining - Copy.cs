using ConvNetSharp;
using ConvNetSharp.Fluent;

using ConvNetSharp.Training;
using ConvNetSharp.GPU;
using HltReplayer;
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using ConvNetSharp.GPU.Layers;

namespace Training
{
    class FluentNetTraining
    {
        public static void Run()
        {
            const string NetName = "net.dat";

            var random = new Random(RandomUtilities.Seed);
            var cpuEntryContainer = new EntryContainer();
            
            #region Load Net

            var convInputWith = 9;

            // Load IA - Direction choice
            // INet singleNet = NetExtension.LoadOrCreateNet(NetName, () =>
            //{
            //    var net = FluentNet.Create(convInputWith, convInputWith, 5)
            //                       .Conv(3, 3, 8).Stride(3).Pad(1)
            //                       .Tanh()
            //                       .Conv(2, 2, 4).Stride(2).Pad(1)
            //                       .Tanh()
            //                       .FullyConn(36)
            //                       .FullyConn(5)
            //                       .Softmax(5).Build();

            //    return net;
            //});


            var cpuNet = FluentNet.Create(convInputWith, convInputWith, 5)
                   .Conv(3, 3, 16).Stride(2).Pad(1)
                   .Tanh()
                   .Conv(2, 2, 16).Stride(1).Pad(1)
                   .Tanh()
                   .FullyConn(100)
                   .FullyConn(5)
                   .Softmax(5).Build();

            var gpuNet = FluentNet.Create(convInputWith, convInputWith, 5)
                .Conv(3, 3, 8).Stride(3).Pad(1)
                .Tanh()
                .Conv(2, 2, 4).Stride(2).Pad(1)
                .Tanh()
                .FullyConn(36)
                .FullyConn(5)
                .Softmax(5).Build();
            gpuNet = gpuNet.ToGPU();

            var cpuFilters1 = ((ConvNetSharp.Layers.ConvLayer)cpuNet.Layers[0]).Filters;
            var gpuFilters1 = ((ConvLayerGPU)gpuNet.Layers[0]).Filters;
            gpuFilters1.Clear();

            for (int i = 0; i < cpuFilters1.Count; i++)
            {
                gpuFilters1.Add((Volume)cpuFilters1[i].Clone());
            }

            var cpuFilters2 = ((ConvNetSharp.Layers.ConvLayer)cpuNet.Layers[2]).Filters;
            var gpuFilters2  = ((ConvLayerGPU)gpuNet.Layers[2]).Filters;
            gpuFilters2.Clear();

            for (int i = 0; i < cpuFilters2.Count; i++)
            {
                gpuFilters2.Add((Volume)cpuFilters2[i].Clone());
            }

            var cpuFilters4 = ((ConvNetSharp.Layers.FullyConnLayer)cpuNet.Layers[4]).Filters;
            var gpuFilters4 = ((ConvNetSharp.Layers.FullyConnLayer)gpuNet.Layers[4]).Filters;
            gpuFilters4.Clear();

            for (int i = 0; i < cpuFilters4.Count; i++)
            {
                gpuFilters4.Add((Volume)cpuFilters4[i].Clone());
            }

            var cpuFilters5 = ((ConvNetSharp.Layers.FullyConnLayer)cpuNet.Layers[5]).Filters;
            var gpuFilters5 = ((ConvNetSharp.Layers.FullyConnLayer)gpuNet.Layers[5]).Filters;
            gpuFilters5.Clear();

            for (int i = 0; i < cpuFilters5.Count; i++)
            {
                gpuFilters5.Add((Volume)cpuFilters5[i].Clone());
            }

            #endregion

            #region Load data

            var enumerable = Directory.EnumerateFiles("D:\\Pro\\Temp\\replays\\Test\\", "*.hlt").ToList();
            int total = enumerable.Count;
            foreach (var file in enumerable)
            {
                Console.WriteLine(total--);
                HltReader reader = new HltReader(file);

                var playerId = -1;
                var playerToCopy = reader.PlayerNames.FirstOrDefault(o => o.StartsWith("erdman v17"));

                if (playerToCopy != null)
                {
                    playerId = reader.PlayerNames.IndexOf(playerToCopy) + 1;
                }

                if (playerId != -1)
                {
                    var width = reader.Width;
                    var height = reader.Height;

                    int lastmoveCount = 1;

                    int v = Math.Min(reader.FrameCount - 1, 150);
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

                                    var direction = moves[y][x];
                                    double factor = 0.03 * 15;
                                    if ((direction == 0 && random.NextDouble() < factor * 1.0 / lastmoveCount) || (direction > 0 && random.NextDouble() < factor * 13.0 / lastmoveCount))
                                    {
                                        var convVolume = map.GetVolumeNew(convInputWith, playerId, x, y, VolumeMode.Production | VolumeMode.Strength | VolumeMode.HasEnemy | VolumeMode.HasOwner | VolumeMode.HasNeutral);

                                        var entry1 = new Entry(new[] { convVolume }, direction, x, y, frame, file.GetHashCode());
                                        cpuEntryContainer.Add(entry1);
                                        var entry2 = new Entry(new[] { convVolume.Flip(VolumeUtilities.FlipMode.LeftRight) }, (int)Helper.FlipLeftRight((Direction)direction), x, y, frame, file.GetHashCode());
                                        cpuEntryContainer.Add(entry2);
                                        var entry3 = new Entry(new[] { convVolume.Flip(VolumeUtilities.FlipMode.UpDown) }, (int)Helper.FlipUpDown((Direction)direction), x, y, frame, file.GetHashCode());
                                        cpuEntryContainer.Add(entry3);
                                        var entry4 = new Entry(new[] { convVolume.Flip(VolumeUtilities.FlipMode.Both) }, (int)Helper.FlipBothWay((Direction)direction), x, y, frame, file.GetHashCode());
                                        cpuEntryContainer.Add(entry4);
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

            var length = cpuEntryContainer.Shuffle();
            Console.WriteLine(cpuEntryContainer.Summary);

            #endregion

            #region Training

            var cpuTrainer = new AdamTrainer(cpuNet) { BatchSize = Math.Min(length / 5, 1), LearningRate = 0.1, Beta1 = 0.9, Beta2 = 0.99, Eps = 1e-8 };
            var gpuTrainer = new AdamTrainer(gpuNet) { BatchSize = Math.Min(length / 5, 1), LearningRate = 0.1, Beta1 = 0.9, Beta2 = 0.99, Eps = 1e-8 };

            var trainingScheme = new GpuCpuTrainingScheme(cpuNet, cpuTrainer, gpuNet, gpuTrainer, cpuEntryContainer, "single");

            bool save = false;
            double lastValidationAcc = 0.0;
            do
            {
                for (int i = 0; i < 1000; i++)
                {
                    //if (i > 5)
                    //{
                    //    trainer.L2Decay = 0.001;
                    //}

                    Console.WriteLine($"Epoch #{i + 1}");

                    //if (i % 50 == 0)
                    //{
                    //    trainer.LearningRate = Math.Max(trainer.LearningRate / 2.0, 0.00001);
                    //}

                    var chrono = Stopwatch.StartNew();

                    trainingScheme.RunEpoch();

                    Console.WriteLine($"elapsed:{chrono.Elapsed.TotalMilliseconds}ms");

                    #region Save Nets

                    //if (save)
                    //{
                    //    if (trainingScheme.ValidationAccuracy > lastValidationAcc)
                    //    {
                    //        lastValidationAcc = trainingScheme.ValidationAccuracy;
                    //        // singleNet = ((FluentNet)singleNet).ToNonGPU();
                    //        singleNet.SaveNet(NetName);
                    //        //  singleNet = ((FluentNet)singleNet).ToGPU();
                    //    }
                    //}
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
