using System;
using System.Collections.Generic;
using System.Linq;
using Runner.Core;
using System.IO;
using MyBot;

namespace CompareRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            // List of bots
            var bots = new List<Func<IPlayer>>
            {
                {() => new ThreeNetBot{Prefix="../../../networks/V45/", Name = "V45"} },
                {() => new SingleNetBot{Prefix="../../../networks/V28/", Name = "V28"} },
            };

            var random = new Random();
            var scores = new Dictionary<string, int>();

            Console.WriteLine("Running...");

            RunTournament(bots);
        }

        static void RunTournament(List<Func<IPlayer>> bots)
        {
            var n = bots.Count;
            var scores = new Dictionary<string, int>();

            do
            {
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (i != j)
                        {
                            // bot i agains bot j
                            var boti = bots[i]();
                            var botj = bots[j]();
                            Console.Write($"{boti.Name} VS {botj.Name} ");
                            var winner = Runner.Core.Runner.Run(new[] { boti, botj }, 25, 25, true); // 25x25 maps, outputs hlt file

                            IncrementScore(winner.Name, scores);

                            Console.WriteLine($"Winner: {winner.Name}");

                            if (File.Exists("scores.csv"))
                            {
                                File.Delete("scores.csv");
                            }
                            File.AppendAllLines("scores.csv", scores.Select(o => o.Key + "," + o.Value));

                            if (Console.KeyAvailable)
                            {
                                break;
                            }
                        }

                        if (Console.KeyAvailable)
                        {
                            return;
                        }
                    }
                }
            } while (!Console.KeyAvailable);
        }

        static void IncrementScore(string name, Dictionary<string, int> scores)
        {
            int score = 0;
            if (!scores.TryGetValue(name, out score))
            {
                scores[name] = 0;
            }

            scores[name]++;
        }
    }
}
