using System.Collections.Generic;

namespace Runner.Core
{
    public class GameStatistics
    {
        public string output_filename;
        public List<PlayerStatistics> player_statistics = new List<PlayerStatistics>();
    }

    public class PlayerStatistics
    {
        public double average_production_count;
        public double average_strength_count;
        public double average_territory_count;
        public int rank;
        public double still_percentage;
        public int tag;
    }

    public class JsonContainer
    {
        public List<List<List<int>>> moves { get; set; }

        public List<List<List<List<int>>>> frames { get; set; }

        public int version { get; set; }

        public ushort width { get; set; }

        public ushort height { get; set; }

        public int num_players { get; set; }

        public int num_frames { get; set; }

        public List<string> player_names { get; set; }

        public List<List<int>> productions { get; set; }
    }
}