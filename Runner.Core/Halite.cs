using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;

namespace Runner.Core
{
    public class Halite
    {
        private readonly List<int> alive_frame_count;
        private readonly List<int> full_cardinal_count;

        //Full game
        private readonly List<Map> full_frames = new List<Map>(); //All the maps!

        private readonly List<Direction[,]> full_player_moves = new List<Direction[,]>();
        private readonly List<int> full_production_count;
        private readonly List<int> full_still_count;
        private readonly List<int> full_strength_count;
        private readonly List<int> full_territory_count;
        private readonly Map game_map;
        private readonly List<int> last_territory_count;
        private readonly ushort mapHeight;
        private readonly ushort mapWidth;
        private readonly Networking networking;
        private readonly int number_of_players;
        private readonly bool outputHltFile;
        private readonly Dictionary<Location, Direction>[] player_moves;
        private List<string> names;
        private int turn_number;

        public Halite(ushort mapWidth, ushort mapHeight, ulong seed, Networking networking, bool outputHltFile)
        {
            this.mapWidth = mapWidth;
            this.mapHeight = mapHeight;
            this.networking = networking;
            this.outputHltFile = outputHltFile;

            this.number_of_players = this.networking.PlayerCount;

            //Initialize map
            this.game_map = new Map(mapWidth, mapHeight, this.number_of_players, seed);

            //Default initialize
            this.player_moves = new Dictionary<Location, Direction>[this.number_of_players];
            this.turn_number = 0;

            //Add to full game:
            this.full_frames.Add(new Map(this.game_map));

            //Init statistics
            this.alive_frame_count = Enumerable.Repeat(1, this.number_of_players).ToList();
            this.last_territory_count = Enumerable.Repeat(1, this.number_of_players).ToList();
            this.full_territory_count = Enumerable.Repeat(1, this.number_of_players).ToList();
            this.full_strength_count = Enumerable.Repeat(255, this.number_of_players).ToList();
            this.full_production_count = new List<int>(new int[this.number_of_players]);
            this.full_still_count = new List<int>(new int[this.number_of_players]);
            this.full_cardinal_count = new List<int>(new int[this.number_of_players]);
        }

        public GameStatistics runGame(List<string> names, ulong seed, int id)
        {
            this.names = names;

            //For rankings
            bool[] result = Enumerable.Repeat(true, this.number_of_players).ToArray();
            var rankings = new List<int>();

            //Send initial package
            for (var i = 0; i < this.number_of_players; i++)
            {
                this.networking.Players[i].Id = (ushort)(i + 1);
                this.networking.Players[i].Map = this.game_map;
                this.networking.Players[i].GameStart();
            }

            var maxTurnNumber = (int)Math.Sqrt(this.game_map.Width * this.game_map.Height) * 10;

            while (result.Sum(o => o ? 1 : 0) > 1 && this.turn_number < maxTurnNumber)
            {
                //Increment turn number:
                this.turn_number++;

               // Console.WriteLine(this.turn_number);

                //Frame logic.
                bool[] newResult = this.ProcessNextFrame(result);

                //Add to vector of players that should be dead.
                var newRankings = new List<int>();
                for (var a = 0; a < this.number_of_players; a++)
                {
                    if (result[a] && !newResult[a])
                    {
                        newRankings.Add(a);
                    }
                }

                newRankings.Sort((u1, u2) =>
                {
                    if (this.last_territory_count[u1] == this.last_territory_count[u2])
                    {
                        return this.full_territory_count[u1].CompareTo(this.full_territory_count[u2]);
                    }

                    return this.last_territory_count[u1].CompareTo(this.last_territory_count[u2]);
                });

                foreach (var newRanking in newRankings)
                {
                    rankings.Add(newRanking);
                }

                result = newResult;
            }

            var newRankings2 = new List<int>();
            for (var a = 0; a < this.number_of_players; a++)
            {
                if (result[a]) newRankings2.Add(a);
            }

            newRankings2.Sort((u1, u2) =>
            {
                if (this.last_territory_count[u1] == this.last_territory_count[u2])
                {
                    return this.full_territory_count[u1].CompareTo(this.full_territory_count[u2]);
                }

                return this.last_territory_count[u1].CompareTo(this.last_territory_count[u2]);
            });

            foreach (var newRanking in newRankings2)
            {
                rankings.Add(newRanking);
            }

            rankings.Reverse(); //Best player first rather than last.

            for (var i = 0; i < this.number_of_players; i++)
            {
                this.networking.Players[i].GameStop(rankings[0]);
            }

            var stats = new GameStatistics();
            var chunkSize = this.game_map.Width * this.game_map.Height / this.number_of_players;
            for (var a = 0; a < this.number_of_players; a++)
            {
                var p = new PlayerStatistics
                {
                    tag = a + 1,
                    average_territory_count = this.full_territory_count[a]/(double) (chunkSize*this.alive_frame_count[a]),
                    average_strength_count = this.full_strength_count[a]/(double) (chunkSize*this.alive_frame_count[a]),
                    average_production_count =
                        this.alive_frame_count[a] > 1 ? this.full_production_count[a]/(double) (chunkSize*(this.alive_frame_count[a] - 1)) : 0,
                    still_percentage =
                        this.full_cardinal_count[a] + this.full_still_count[a] > 0
                            ? this.full_still_count[a]/(double) (this.full_cardinal_count[a] + this.full_still_count[a])
                            : 0,
                    rank = rankings.IndexOf(a)
                };


                stats.player_statistics.Add(p);
            }

            if (this.outputHltFile)
            {
                stats.output_filename = $"{id}-{seed}.hlt";
                this.Output(stats.output_filename);
            }

            return stats;
        }

        private void Output(string outputFilename)
        {
            if (File.Exists(outputFilename))
            {
                File.Delete(outputFilename);
            }

            using (var fs = new FileStream(outputFilename, FileMode.Create))
            {
                var serializer = new DataContractJsonSerializer(typeof(JsonContainer));

                var json = new JsonContainer();

                json.version = 11;

                //Encode some details about the game that will make it convenient to parse.
                json.width = this.game_map.Width;
                json.height = this.game_map.Height;
                json.num_players = this.names.Count;
                json.num_frames = this.full_frames.Count;

                //Encode player names.
                json.player_names = this.names;

                //Encode the production map.
                var productions = new List<List<int>>();
                for (var i = 0; i < this.game_map.Height; i++)
                {
                    productions.Add(new List<int>(new int[this.game_map.Width]));
                }

                for (var a = 0; a < this.game_map.Height; a++)
                {
                    for (var b = 0; b < this.game_map.Width; b++)
                    {
                        productions[a][b] = this.game_map._sites[b, a].Production;
                    }
                }
                json.productions = productions;


                //Encode the frames. Note that there is no moves field for the last frame.
                var frames = new List<List<List<List<int>>>>();
                var moves = new List<List<List<int>>>();

                for (var a = 0; a < this.full_frames.Count; a++)
                {
                    var frame = new List<List<List<int>>>();
                    for (var i = 0; i < this.game_map.Height; i++)
                    {
                        var collection = new List<int>[this.game_map.Width];
                        for (var j = 0; j < this.game_map.Width; j++)
                        {
                            collection[j] = new List<int>();
                        }

                        frame.Add(new List<List<int>>(collection));
                    }

                    for (var b = 0; b < this.game_map.Height; b++)
                    {
                        for (var c = 0; c < this.game_map.Width; c++)
                        {
                            frame[b][c].Add(this.full_frames[a]._sites[c, b].Owner);
                            frame[b][c].Add(this.full_frames[a]._sites[c, b].Strength);
                        }
                    }

                    frames.Add(frame);
                }

                for (var a = 0; a < this.full_frames.Count - 1; a++)
                {
                    var move_frame = new List<List<int>>();
                    for (var i = 0; i < this.game_map.Height; i++)
                    {
                        move_frame.Add(new List<int>(new int[this.game_map.Width]));
                    }

                    for (var b = 0; b < this.game_map.Height; b++)
                    {
                        for (var c = 0; c < this.game_map.Width; c++)
                        {
                            move_frame[b][c] = (int)this.full_player_moves[a][b, c];
                        }
                    }
                    moves.Add(move_frame);
                }

                json.frames = frames;
                json.moves = moves;

                serializer.WriteObject(fs, json);
            }
        }

        private bool[] ProcessNextFrame(bool[] alive)
        {
            //Update alive frame counts
            for (var a = 0; a < this.number_of_players; a++)
            {
                if (alive[a]) this.alive_frame_count[a]++;
            }

            //Get the messages sent by bots this frame
            for (var a = 0; a < this.number_of_players; a++)
            {
                this.player_moves[a] = this.networking.Players[a].GetMoves().ToDictionary(o => o.Location, o => o.Direction);
            }

            this.full_player_moves.Add(new Direction[this.game_map.Height, this.game_map.Width]);

            var pieces = new Dictionary<Location, int>[this.number_of_players];
            for (var i = 0; i < this.number_of_players; i++)
            {
                pieces[i] = new Dictionary<Location, int>();
            }

            //For each player, use their moves to create the pieces map.
            for (var a = 0; a < this.number_of_players; a++)
            {
                if (alive[a])
                {
                    //Add in pieces according to their moves. Also add in a second piece corresponding to the piece left behind.

                    foreach (KeyValuePair<Location, Direction> b in this.player_moves[a])
                    {
                        Site site = this.game_map.getSite(b.Key);

                        if (this.game_map.inBounds(b.Key) && site.Owner == a + 1)
                        {
                            if (b.Value == Direction.Still)
                            {
                                if (site.Strength + site.Production <= 255)
                                {
                                    site.Strength += site.Production;
                                }
                                else
                                {
                                    site.Strength = 255;
                                }

                                //Update full still count
                                this.full_still_count[a]++;

                                //Add to full production
                                this.full_production_count[a] += site.Production;
                            }
                            else
                            {
                                //Update full caridnal count.
                                this.full_cardinal_count[a]++;
                            }

                            //Update moves
                            this.full_player_moves.Last()[b.Key.Y, b.Key.X] = b.Value;

                            var newLoc = this.game_map.getLocation(b.Key, b.Value);

                            if (pieces[a].ContainsKey(newLoc))
                            {
                                if (pieces[a][newLoc] + site.Strength <= 255)
                                {
                                    pieces[a][newLoc] += site.Strength;
                                }
                                else
                                {
                                    pieces[a][newLoc] = 255;
                                }
                            }
                            else
                            {
                                pieces[a][newLoc] = site.Strength;
                            }

                            //Add in a new piece with a strength of 0 if necessary.
                            if (!pieces[a].ContainsKey(b.Key))
                            {
                                pieces[a][b.Key] = 0;
                            }

                            //Erase from the game map so that the player can't make another move with the same piece.
                            site.Owner = 0;
                            site.Strength = 0;
                        }

                        this.game_map.SetSite(b.Key, site);
                    }
                }
            }

            //Add in all of the remaining pieces whose moves weren't specified.
            for (short a = 0; a < this.game_map.Height; a++)
            {
                for (short b = 0; b < this.game_map.Width; b++)
                {
                    var s = this.game_map._sites[b, a];

                    if (s.Owner != 0)
                    {
                        var l = new Location { X = (ushort)b, Y = (ushort)a };

                        if (s.Strength + s.Production <= 255)
                        {
                            s.Strength += s.Production;
                        }
                        else
                        {
                            s.Strength = 255;
                        }

                        if (pieces[s.Owner - 1].ContainsKey(l))
                        {
                            if (pieces[s.Owner - 1][l] + s.Strength <= 255) pieces[s.Owner - 1][l] += s.Strength;
                            else
                            {
                                pieces[s.Owner - 1][l] = 255;
                            }
                        }
                        else
                        {
                            pieces[s.Owner - 1][l] = s.Strength;
                        }

                        //Add to full production
                        this.full_production_count[s.Owner - 1] += s.Production;

                        //Update full still count
                        this.full_still_count[s.Owner - 1]++;

                        //Erase from game map.
                        s.Owner = 0;
                        s.Strength = 0;
                    }
                }
            }

            var toInjure = new Dictionary<Location, short>[this.number_of_players];
            for (var i = 0; i < this.number_of_players; i++)
            {
                toInjure[i] = new Dictionary<Location, short>();
            }

            var injureMap = new short[this.mapHeight, this.mapWidth];

            for (short a = 0; a < this.game_map.Height; a++)
            {
                for (short b = 0; b < this.game_map.Width; b++)
                {
                    var l = new Location { X = (ushort)b, Y = (ushort)a };

                    for (var c = 0; c < this.number_of_players; c++)
                    {
                        if (alive[c] && pieces[c].ContainsKey(l))
                        {
                            for (var d = 0; d < this.number_of_players; d++)
                            {
                                if (d != c && alive[d])
                                {
                                    var tempLoc = l;

                                    //Check 'STILL' square. We also need to deal with the threshold here:
                                    if (pieces[d].ContainsKey(tempLoc))
                                    {
                                        //Apply damage, but not more than they have strength:
                                        if (toInjure[d].ContainsKey(tempLoc))
                                        {
                                            toInjure[d][tempLoc] += (short)pieces[c][l];
                                        }
                                        else
                                        {
                                            toInjure[d][tempLoc] = (short)pieces[c][l];
                                        }
                                    }

                                    //Check 'NORTH' square:
                                    tempLoc = this.game_map.getLocation(l, Direction.North);
                                    if (pieces[d].ContainsKey(tempLoc))
                                    {
                                        //Apply damage, but not more than they have strength:
                                        if (toInjure[d].ContainsKey(tempLoc))
                                        {
                                            toInjure[d][tempLoc] += (short)pieces[c][l];
                                        }
                                        else
                                        {
                                            toInjure[d][tempLoc] = (short)pieces[c][l];
                                        }
                                    }

                                    //Check 'EAST' square:
                                    tempLoc = this.game_map.getLocation(l, Direction.East);
                                    if (pieces[d].ContainsKey(tempLoc))
                                    {
                                        //Apply damage, but not more than they have strength:
                                        if (toInjure[d].ContainsKey(tempLoc))
                                        {
                                            toInjure[d][tempLoc] += (short)pieces[c][l];
                                        }
                                        else
                                        {
                                            toInjure[d][tempLoc] = (short)pieces[c][l];
                                        }
                                    }

                                    //Check 'SOUTH' square:
                                    tempLoc = this.game_map.getLocation(l, Direction.South);
                                    if (pieces[d].ContainsKey(tempLoc))
                                    {
                                        //Apply damage, but not more than they have strength:
                                        if (toInjure[d].ContainsKey(tempLoc))
                                        {
                                            toInjure[d][tempLoc] += (short)pieces[c][l];
                                        }
                                        else
                                        {
                                            toInjure[d][tempLoc] = (short)pieces[c][l];
                                        }
                                    }

                                    //Check 'WEST' square:
                                    tempLoc = this.game_map.getLocation(l, Direction.West);
                                    if (pieces[d].ContainsKey(tempLoc))
                                    {
                                        //Apply damage, but not more than they have strength:
                                        if (toInjure[d].ContainsKey(tempLoc))
                                        {
                                            toInjure[d][tempLoc] += (short)pieces[c][l];
                                        }
                                        else
                                        {
                                            toInjure[d][tempLoc] = (short)pieces[c][l];
                                        }
                                    }
                                }
                            }

                            Site site = this.game_map.getSite(l);

                            if (site.Strength > 0)
                            {
                                if (toInjure[c].ContainsKey(l))
                                {
                                    toInjure[c][l] += (short)site.Strength;
                                }
                                else
                                {
                                    toInjure[c][l] = (short)site.Strength;
                                }

                                injureMap[l.Y, l.X] += (short)pieces[c][l];
                            }
                        }
                    }
                }
            }

            //Injure and/or delete pieces. Note >= rather than > indicates that pieces with a strength of 0 are killed.
            for (var a = 0; a < this.number_of_players; a++)
            {
                if (alive[a])
                {
                    foreach (KeyValuePair<Location, short> b in toInjure[a])
                    {
                        //Apply damage.
                        if (b.Value >= pieces[a][b.Key])
                        {
                            pieces[a].Remove(b.Key);
                        }
                        else
                        {
                            pieces[a][b.Key] -= b.Value;
                        }
                    }
                }
            }

            //Apply damage to map pieces.
            for (short a = 0; a < this.game_map.Height; a++)
            {
                for (short b = 0; b < this.game_map.Width; b++)
                {
                    if (this.game_map._sites[b, a].Strength < injureMap[a, b])
                    {
                        this.game_map._sites[b, a].Strength = 0;
                    }
                    else
                    {
                        this.game_map._sites[b, a].Strength -= (ushort)injureMap[a, b];
                    }

                    this.game_map._sites[b, a].Owner = 0;
                }
            }

            //Add pieces back into the map.
            for (var a = 0; a < this.number_of_players; a++)
            {
                foreach (KeyValuePair<Location, int> b in pieces[a])
                {
                    this.game_map._sites[b.Key.X, b.Key.Y].Owner = (ushort)(a + 1);
                    this.game_map._sites[b.Key.X, b.Key.Y].Strength = (ushort)b.Value;
                }
            }

            //Add to full game:
            this.full_frames.Add(new Map(this.game_map));

            //Check if the game is over:
            var stillAlive = new bool[this.number_of_players];
            for (var i = 0; i < this.last_territory_count.Count; i++)
            {
                this.last_territory_count[i] = 0;
            }

            for (short a = 0; a < this.game_map.Height; a++)
            {
                for (short b = 0; b < this.game_map.Width; b++)
                {
                    if (this.game_map._sites[b, a].Owner != 0)
                    {
                        this.last_territory_count[this.game_map._sites[b, a].Owner - 1]++;
                        this.full_territory_count[this.game_map._sites[b, a].Owner - 1]++;
                        this.full_strength_count[this.game_map._sites[b, a].Owner - 1] += this.game_map._sites[b, a].Strength;
                        this.full_production_count[this.game_map._sites[b, a].Owner - 1] += this.game_map._sites[b, a].Strength;

                        stillAlive[this.game_map._sites[b, a].Owner - 1] = true;
                    }
                }
            }

            return stillAlive;
        }
    }
}