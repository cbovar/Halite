using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;

namespace HltDownloader
{
    class Program
    {
        static void Main(string[] args)
        {
            const string root = @"..\..\..\games\";

            Console.WriteLine("nmalaguti = 2697\tdavid-wu = 3157");
            Console.WriteLine("djma = 1017\t\ttimfoden = 2173");
            Console.WriteLine("erdman = 2609\t\tcdurbin = 2557");
            Console.WriteLine("mzotkiew = 4223");
            Console.Write("UserId:");
            int userId = int.Parse(Console.ReadLine());

            var url = $"https://halite.io/api/web/game?userID={userId}&limit=1000";

            string lastGames = null;
            using (var client = new WebClient())
            {
                lastGames = client.DownloadString(url);
            }

            if (lastGames != null)
            {
                dynamic games = JsonConvert.DeserializeObject(lastGames);

                foreach (var game in games)
                {
                    if (!Directory.Exists($"{root}/{userId}"))
                    {
                        Directory.CreateDirectory($"{root}/{userId}");
                    }

                    var filename = Path.Combine($"{root}/{userId}", game.replayName.Value);

                    if (!File.Exists(filename))
                    {
                        Console.WriteLine(filename);

                        using (var client = new AutomaticDecompressionWebClient())
                        {
                            var hlt = $"https://s3.amazonaws.com/halitereplaybucket/{game.replayName}";
                            client.DownloadFile(hlt, filename);
                        }
                    }
                }
            }
        }
    }
}
