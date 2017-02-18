using ConvNetSharp;
using ConvNetSharp.Training;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Training
{
    class TrainingScheme
    {
        private Random random = new Random(RandomUtilities.Seed);

        private int stepCount = 0;
        private string label;
        private INet net;
        private TrainerBase trainer;
        private EntryContainer container;

        public TrainingScheme(INet net, TrainerBase trainer, EntryContainer container, string label)
        {
            this.container = container;
            this.label = label;
            this.net = net;
            this.trainer = trainer;

            int n = container.ClassCount;
        }

        public void RunEpoch()
        {
            var length = this.container.Shuffle();
            var trainAccWindow = new List<double>();
            var lossWindow = new List<double>();

            var chrono = Stopwatch.StartNew();
            var trainingSet = this.container.ToList<Entry>();
            foreach (Entry entry in trainingSet)
            {
                this.trainer.Train(entry.OutputClass, entry.Input);
                var loss = this.trainer.CostLoss;
                lossWindow.Add(loss);

                var prediction = this.net.GetPrediction();
                var trainAcc = prediction == entry.OutputClass ? 1.0 : 0.0;
                trainAccWindow.Add(trainAcc);

                this.stepCount++;
            }
            Console.WriteLine($"    Training: elapsed:{chrono.Elapsed.TotalMilliseconds}ms");

            chrono = Stopwatch.StartNew();
            var valAcc = ComputeValidationAccuracy();
            Console.WriteLine($"    Validation: elapsed:{chrono.Elapsed.TotalMilliseconds}ms");

            var trainAvg = trainAccWindow.Count > 0 ? trainAccWindow.Average() : 0;
            var lossAvg = lossWindow.Count > 0 ? lossWindow.Average() : 0;

            this.TrainAccuracy = trainAvg;

            File.AppendAllLines($"Train_Accuracy_{label}.csv", new string[] { trainAvg.ToString() });
            File.AppendAllLines($"Test_Accuracy_{label}.csv", new string[] { valAcc[-1].ToString() });
            File.AppendAllLines($"Loss_{label}.csv", new string[] { lossAvg.ToString() });

            Console.WriteLine("[{3}] Loss: {0} Train accuracy: {1}% Test accuracy: {2}%", Math.Round(lossAvg, 4),
                Math.Round(trainAvg * 100.0, 2), Math.Round(valAcc[-1] * 100.0, 2), this.label);

            Console.WriteLine("[{3}] Example seen: {0} Fwd: {1}ms Bckw: {2}ms", this.stepCount,
                Math.Round(this.trainer.ForwardTime.TotalMilliseconds, 2),
                Math.Round(this.trainer.BackwardTime.TotalMilliseconds, 2), this.label);
        }

        public Dictionary<int, double> ComputeValidationAccuracy()
        {
            var validationList = new List<double>();
            var dico = new Dictionary<int, List<double>>();

            foreach (var entry in this.container.ValidationSet)
            {
                this.net.Forward(entry.Input);
                var prediction = this.net.GetPrediction();
                var acc = prediction == entry.OutputClass ? 1.0 : 0.0;
                validationList.Add(acc);

                List<double> d;
                if (!dico.TryGetValue(entry.OutputClass, out d))
                {
                    d = new List<double>();
                    dico[entry.OutputClass] = d;
                }
                d.Add(acc);
            }

            var result = new Dictionary<int, double>();

            this.ValidationAccuracy = validationList.Any() ? validationList.Average() : 0.0; ;

            result[-1] = this.ValidationAccuracy;
            foreach (var d in dico)
            {
                result[d.Key] = d.Value.Average();
            }

            return result;
        }

        public double TrainAccuracy { get; set; }

        public double ValidationAccuracy { get; set; }
    }
}
