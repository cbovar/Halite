using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using Runner.Core;


namespace Training
{
    public class HltReader
    {
        private JsonContainer hlt;

        public HltReader(string filename)
        {
            using (var r = new StreamReader(filename))
            {
                var serializer = new DataContractJsonSerializer(typeof(JsonContainer));
                this.hlt = serializer.ReadObject(r.BaseStream) as JsonContainer;
            }
        }

        public List<string> PlayerNames => this.hlt.player_names;

        public int Width => this.hlt.width;

        public int Height => this.hlt.height;

        public int FrameCount => this.hlt.num_frames;

        public int PlayerCount => this.hlt.num_players;

        public (Map map, List<List<int>> moves) GetFrame(int frame)
        {
            var map = new Map(this.hlt.width, this.hlt.height);
            List<List<List<int>>> currentFrame = hlt.frames[frame];

            for (ushort x = 0; x < this.hlt.width; x++)
            {
                for (ushort y = 0; y < this.hlt.height; y++)
                {
                    var site = new Site
                    {
                        Owner = (ushort)currentFrame[y][x][0],
                        Strength = (ushort)currentFrame[y][x][1],
                        Production = (ushort)hlt.productions[y][x]
                    };
                    map._sites[x, y] = site;
                }
            }

            return (map, this.hlt.moves[frame]);
        }
    }
}
