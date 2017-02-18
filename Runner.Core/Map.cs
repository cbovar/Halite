using System;
using System.Collections.Generic;
using Runner.Core;

/// <summary>
///     State of the game at every turn. Use <see cref="GetInitialMap" /> to get the map for a new game from
///     stdin, and use <see cref="NextTurn" /> to update the map after orders for a turn have been executed.
/// </summary>
public class Map
{
    public readonly Site[,] _sites;
    private readonly int map_height;
    private readonly int map_width;
    private readonly MT19937 prg;

   public Map(ushort width, ushort height, int numberOfPlayers, ulong _Seed, bool force = false) : this(width, height)
    {
        //Pseudorandom number generator.
        this.prg = new MT19937();
        this.prg.init_genrand(_Seed);

        //Decides whether to put more players along the horizontal or the vertical.
        var preferHorizontal = this.prg.genrand_int31() % 2 == 1;

        int dw, dh;
        //Find number closest to square that makes the match symmetric.
        if (preferHorizontal)
        {
            dh = (int)Math.Sqrt(numberOfPlayers);
            while (numberOfPlayers % dh != 0) dh--;
            dw = numberOfPlayers / dh;
        }
        else
        {
            dw = (int)Math.Sqrt(numberOfPlayers);
            while (numberOfPlayers % dw != 0) dw--;
            dh = numberOfPlayers / dw;
        }

        //Figure out chunk width and height accordingly.
        //Matches width and height as closely as it can, but is not guaranteed to match exactly.
        //It is guaranteed to be smaller if not the same size, however.
        var cw = width / dw;
        var ch = height / dh;

        //Ensure that we'll be able to move the tesselation by a uniform amount.
        if (preferHorizontal) while (ch % numberOfPlayers != 0) ch--;
        else while (cw % numberOfPlayers != 0) cw--;

        this.map_width = cw * dw;
        this.map_height = ch * dh;


        var prodRegion = new Region(cw, ch, () => this.prg.genrand_real1());
        List<List<double>> prodChunk = prodRegion.GetFactors();

        var strengthRegion = new Region(cw, ch, () => this.prg.genrand_real1());
        List<List<double>> strengthChunk = strengthRegion.GetFactors();


        //We'll first tesselate the map; we'll apply our various translations and transformations later.
        var tesselation = new SiteD[this.map_height, this.map_width];

        for (var a = 0; a < dh; a++)
        {
            for (var b = 0; b < dw; b++)
            {
                for (var c = 0; c < ch; c++)
                {
                    for (var d = 0; d < cw; d++)
                    {
                        tesselation[a * ch + c, b * cw + d].production = prodChunk[c][d];
                        tesselation[a * ch + c, b * cw + d].strength = strengthChunk[c][d];
                    }
                }
                tesselation[a * ch + ch / 2, b * cw + cw / 2].owner = (char)(a * dw + b + 1); //Set owners.
            }
        }


        //We'll now apply the reflections to the map.
        bool reflectVertical = dh % 2 == 0, reflectHorizontal = dw % 2 == 0; //Am I going to reflect in the horizontal vertical directions at all?
        var reflections = new SiteD[this.map_height, this.map_width];

        for (var a = 0; a < dh; a++)
        {
            for (var b = 0; b < dw; b++)
            {
                bool vRef = reflectVertical && a % 2 != 0, hRef = reflectHorizontal && b % 2 != 0; //Do I reflect this chunk at all?
                for (var c = 0; c < ch; c++)
                {
                    for (var d = 0; d < cw; d++)
                    {
                        reflections[a * ch + c, b * cw + d] = tesselation[a * ch + (vRef ? ch - c - 1 : c), b * cw + (hRef ? cw - d - 1 : d)];
                    }
                }
            }
        }

        //Next, let's apply our shifts to create the shifts map.
        var shifts = new SiteD[this.map_height, this.map_height];

        if (preferHorizontal)
        {
            var shift = (this.prg.genrand_int31() % dw == 1 ? 1 : 0) * (this.map_height / dw); //A vertical shift.
            for (var a = 0; a < dh; a++)
            {
                for (var b = 0; b < dw; b++)
                {
                    for (var c = 0; c < ch; c++)
                    {
                        for (var d = 0; d < cw; d++)
                        {
                            shifts[a * ch + c, b * cw + d] = reflections[(a * ch + b * shift + c) % this.map_height, b * cw + d];
                        }
                    }
                }
            }
        }
        else
        {
            var shift = (this.prg.genrand_int31() % dh == 1 ? 1 : 0) * (this.map_width / dh); //A horizontal shift.
            for (var a = 0; a < dh; a++)
            {
                for (var b = 0; b < dw; b++)
                {
                    for (var c = 0; c < ch; c++)
                    {
                        for (var d = 0; d < cw; d++)
                        {
                            shifts[a * ch + c, b * cw + d] = reflections[a * ch + c, (b * cw + a * shift + d) % this.map_width];
                        }
                    }
                }
            }
        }

        //Apply a final blur to create the blur map. This will fix the edges where our transformations have created jumps or gaps.
        const double OWN_WEIGHT = 0.66667;
        var blur = new SiteD[this.map_height, this.map_width];
        Array.Copy(shifts, blur, this.map_height * this.map_width);

        for (var z = 0; z <= 2 * Math.Sqrt(this.map_width * this.map_height) / 10; z++)
        {
            var newBlur = new SiteD[this.map_height, this.map_width];
            Array.Copy(blur, newBlur, this.map_height * this.map_width);

            for (var a = 0; a < this.map_height; a++)
            {
                int mh = a - 1, ph = a + 1;
                if (mh < 0) mh += this.map_height;
                if (ph == this.map_height) ph = 0;
                for (var b = 0; b < this.map_width; b++)
                {
                    int mw = b - 1, pw = b + 1;
                    if (mw < 0) mw += this.map_width;
                    if (pw == this.map_width) pw = 0;

                    newBlur[a, b].production *= OWN_WEIGHT;
                    newBlur[a, b].production += blur[mh, b].production * (1 - OWN_WEIGHT) / 4;
                    newBlur[a, b].production += blur[ph, b].production * (1 - OWN_WEIGHT) / 4;
                    newBlur[a, b].production += blur[a, mw].production * (1 - OWN_WEIGHT) / 4;
                    newBlur[a, b].production += blur[a, pw].production * (1 - OWN_WEIGHT) / 4;
                    newBlur[a, b].strength *= OWN_WEIGHT;
                    newBlur[a, b].strength += blur[mh, b].strength * (1 - OWN_WEIGHT) / 4;
                    newBlur[a, b].strength += blur[ph, b].strength * (1 - OWN_WEIGHT) / 4;
                    newBlur[a, b].strength += blur[a, mw].strength * (1 - OWN_WEIGHT) / 4;
                    newBlur[a, b].strength += blur[a, pw].strength * (1 - OWN_WEIGHT) / 4;
                }
            }
            blur = newBlur;
        }

        //Let's now normalize the map values.
        double maxProduction = 0, maxStrength = 0;
        var normalized = new SiteD[this.map_height, this.map_width];
        Array.Copy(blur, normalized, this.map_height * this.map_width);

        foreach (var b in normalized)
        {
            if (b.production > maxProduction) maxProduction = b.production;
            if (b.strength > maxStrength) maxStrength = b.strength;
        }

        for (var i = 0; i < this.map_height; i++)
        {
            for (var j = 0; j < this.map_width; j++)
            {
                normalized[i, j].production /= maxProduction;
                normalized[i, j].strength /= maxStrength;
            }
        }

        //Finally, fill in the contents vector.
        var TOP_PROD = (int)(this.prg.genrand_int31() % 10 + 6);
        var TOP_STR = (int)(this.prg.genrand_int31() % 106 + 150);

        this._sites = new Site[this.map_width, this.map_height];

        for (var a = 0; a < this.map_height; a++)
        {
            for (var b = 0; b < this.map_width; b++)
            {
                this._sites[b, a].Owner = normalized[a, b].owner;
                this._sites[b, a].Strength = (ushort)Math.Round(normalized[a, b].strength * TOP_STR);
                this._sites[b, a].Production = (ushort)Math.Round(normalized[a, b].production * TOP_PROD);
                if (this._sites[b, a].Owner != 0 && this._sites[b, a].Production == 0) this._sites[b, a].Production = 1;
            }
        }
    }

    public Map(ushort width, ushort height)
    {
        this.map_height = height;
        this.map_width = width;

        this._sites = new Site[width, height];
        for (ushort x = 0; x < width; x++)
        {
            for (ushort y = 0; y < height; y++)
            {
                this._sites[x, y] = new Site();
            }
        }
    }

    public Map(Map otherMap)
    {
        this.map_width = otherMap.map_width;
        this.map_height = otherMap.map_height;

        this._sites = new Site[this.map_width, this.map_height];
        Array.Copy(otherMap._sites, this._sites, this.map_width * this.map_height);
    }

    /// <summary>
    ///     Get a read-only structure representing the current state of the site at the supplied coordinates.
    /// </summary>
    public Site this[ushort x, ushort y]
    {
        get
        {
            if (x >= this.Width)
                throw new IndexOutOfRangeException(string.Format("Cannot get site at ({0},{1}) beacuse width is only {2}", x, y, this.Width));
            if (y >= this.Height)
                throw new IndexOutOfRangeException(string.Format("Cannot get site at ({0},{1}) beacuse height is only {2}", x, y, this.Height));
            return this._sites[x, y];
        }

        set
        {
            this._sites[x, y] = value;
        }
    }

    /// <summary>
    ///     Get a read-only structure representing the current state of the site at the supplied location.
    /// </summary>
    public Site this[Location location] => this[location.X, location.Y];

    /// <summary>
    ///     Returns the width of the map.
    /// </summary>
    public ushort Width => (ushort)this._sites.GetLength(0);

    /// <summary>
    ///     Returns the height of the map.
    /// </summary>
    public ushort Height => (ushort)this._sites.GetLength(1);

    public void Update(string gameMapStr)
    {
        var gameMapValues = new Queue<string>(gameMapStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

        ushort x = 0, y = 0;
        while (y < this.Height)
        {
            ushort counter, owner;
            if (!ushort.TryParse(gameMapValues.Dequeue(), out counter))
                throw new ApplicationException("Could not get some counter from stdin");
            if (!ushort.TryParse(gameMapValues.Dequeue(), out owner))
                throw new ApplicationException("Could not get some owner from stdin");
            while (counter > 0)
            {
                this._sites[x, y].Owner = owner;
                x++;
                if (x == this.Width)
                {
                    x = 0;
                    y++;
                }
                counter--;
            }
        }

        Queue<string> strengthValues = gameMapValues; // Referencing same queue, but using a name that is more clear
        for (y = 0; y < this.Height; y++)
        {
            for (x = 0; x < this.Width; x++)
            {
                ushort strength;
                if (!ushort.TryParse(strengthValues.Dequeue(), out strength))
                    throw new ApplicationException("Could not get some strength value from stdin");
                this._sites[x, y].Strength = strength;
            }
        }
    }

    private static Tuple<ushort, ushort> ParseMapSize(string mapSizeStr)
    {
        ushort width, height;
        string[] parts = mapSizeStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !ushort.TryParse(parts[0], out width) || !ushort.TryParse(parts[1], out height))
            throw new ApplicationException("Could not get map size from stdin during init");
        return Tuple.Create(width, height);
    }

    public static Map ParseMap(string mapSizeStr, string productionMapStr, string gameMapStr)
    {
        Tuple<ushort, ushort> mapSize = ParseMapSize(mapSizeStr);
        var map = new Map(mapSize.Item1, mapSize.Item2);

        var productionValues = new Queue<string>(productionMapStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

        ushort x, y;
        for (y = 0; y < map.Height; y++)
        {
            for (x = 0; x < map.Width; x++)
            {
                ushort production;
                if (!ushort.TryParse(productionValues.Dequeue(), out production))
                    throw new ApplicationException("Could not get some production value from stdin");
                map._sites[x, y].Production = production;
            }
        }

        map.Update(gameMapStr);

        return map;
    }

    public bool inBounds(Location l)
    {
        return l.X < this.map_width && l.Y < this.map_height;
    }

    public Site getSite(Location l, Direction direction = Direction.Still)
    {
        l = this.getLocation(l, direction);
        return this._sites[l.X, l.Y];
    }

    public void SetSite(Location l, Site site, Direction direction = Direction.Still)
    {
        l = this.getLocation(l, direction);
        this._sites[l.X, l.Y] = site;
    }

    public void setStrength(Location l, ushort strength)
    {
        l = this.getLocation(l, Direction.Still);
        this._sites[l.X, l.Y].Strength = strength;
    }

    public void setOwner(Location l, ushort owner)
    {
        l = this.getLocation(l, Direction.Still);
        this._sites[l.X, l.Y].Owner = owner;
    }

    public Location getLocation(Location l, Direction direction)
    {
        if (direction != Direction.Still)
        {
            if (direction == Direction.North)
            {
                if (l.Y == 0) l.Y = (ushort)(this.map_height - 1);
                else l.Y--;
            }
            else if (direction == Direction.East)
            {
                if (l.X == this.map_width - 1) l.X = 0;
                else l.X++;
            }
            else if (direction == Direction.South)
            {
                if (l.Y == this.map_height - 1) l.Y = 0;
                else l.Y++;
            }
            else if (direction == Direction.West)
            {
                if (l.X == 0) l.X = (ushort)(this.map_width - 1);
                else l.X--;
            }
        }

        return l;
    }

    private struct SiteD
    {
        public char owner;
        public double production;
        public double strength;
    }
}