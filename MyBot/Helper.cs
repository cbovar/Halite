using System;
using System.Collections.Generic;
using ConvNetSharp;
using System.IO;
using ConvNetSharp.Serialization;

public static class NetExtension
{
    #region ConvNetSharp Custom Binary

    public static void SaveNet(this INet net, string filename)
    {
        using (var fs = new FileStream(filename, FileMode.Create))
        {
            net.SaveBinary(fs);
        }
    }

    public static INet LoadNet(string filename)
    {
        INet result = null;
        if (File.Exists(filename))
        {
            using (var fs = new FileStream(filename, FileMode.Open))
            {
                result = SerializationExtensions.LoadBinary(fs);
            }
        }

        return result;
    }

    #endregion

    public static INet LoadOrCreateNet(string filename, Func<INet> CreateNetFunc)
    {
        INet result;
        if (File.Exists(filename))
        {
            result = LoadNet(filename);
        }
        else
        {
            result = CreateNetFunc();
        }

        return result;
    }
}

[Flags]
public enum VolumeMode
{
    Production = 1,
    Strength = 2,
    Territory = 4,
    Only255 = 8,
    HasBorder = 16,
}

public static class MapExtensions
{
    public static Site GetSite(this Map map, ushort x, ushort y, Direction move)
    {
        switch (move)
        {
            case Direction.Still:
                return map[x, y];
            case Direction.North:
                return map[x, Helper.Mod(y - 1, map.Height)];
            case Direction.East:
                return map[Helper.Mod(x + 1, map.Width), y];
            case Direction.South:
                return map[x, Helper.Mod(y + 1, map.Height)];
            case Direction.West:
                return map[Helper.Mod(x - 1, map.Width), y];
        }

        return map[x, y];
    }

    public static Volume GetVolume(this Map map, int N, int myId, ushort x, ushort y, VolumeMode mode = VolumeMode.Production | VolumeMode.Strength | VolumeMode.Territory)
    {
        int count = 0;
        bool hasProduction = (mode & VolumeMode.Production) == VolumeMode.Production;
        if (hasProduction)
        {
            count++;
        }
        bool hasStrength = (mode & VolumeMode.Strength) == VolumeMode.Strength;
        if (hasStrength)
        {
            count++;
        }
        bool hasTerritory = (mode & VolumeMode.Territory) == VolumeMode.Territory;
        if (hasTerritory)
        {
            count++;
        }
        bool has255 = (mode & VolumeMode.Only255) == VolumeMode.Only255;
        if (has255)
        {
            count++;
        }
        bool hasBorder = (mode & VolumeMode.HasBorder) == VolumeMode.HasBorder;
        if (hasBorder)
        {
            count++;
        }

        var vol = new Volume(N, N, count, 0);

        for (var dx = -N / 2; dx <= N / 2; dx++)
        {
            for (var dy = -N / 2; dy <= N / 2; dy++)
            {
                int n = 0;
                var site = map[Helper.Mod(x + dx, map.Width), Helper.Mod(y + dy, map.Height)];

                if (hasProduction)
                {
                    vol.Set(dx + N / 2, dy + N / 2, n++, site.Production / 20.0 - 0.5);
                }

                if (hasStrength)
                {
                    vol.Set(dx + N / 2, dy + N / 2, n++, site.Strength / 255.0 - 0.5);
                }

                if (hasTerritory)
                {
                    vol.Set(dx + N / 2, dy + N / 2, n++, site.Owner == myId ? 1.0 : (site.Owner != 0 ? -1.0 : 0.0));
                }

                if (has255)
                {
                    vol.Set(dx + N / 2, dy + N / 2, n++, site.Strength == 255 ? 1.0 : 0.0);
                }

                if (hasBorder)
                {
                    bool foundBorder = false;
                    foreach (Direction dir in Enum.GetValues(typeof(Direction)))
                    {
                        var neighbour = map.GetSite((ushort)(dx + N / 2), (ushort)(dy + N / 2), dir);
                        if (neighbour.Owner != myId)
                        {
                            foundBorder = true;
                            break;
                        }
                    }

                    vol.Set(dx + N / 2, dy + N / 2, n++, foundBorder ? 1.0 : 0.0);
                }
            }
        }
        return vol;
    }

    public static void Move(this Map map, ushort x, ushort y, Direction move)
    {
        ushort fx = x, fy = y;
        switch (move)
        {
            case Direction.Still:
                return;
            case Direction.North:
                fy = Helper.Mod(y - 1, map.Height);
                break;
            case Direction.East:
                fx = Helper.Mod(x + 1, map.Width);
                break;
            case Direction.South:
                fy = Helper.Mod(y + 1, map.Height);
                break;
            case Direction.West:
                fx = Helper.Mod(x - 1, map.Width);
                break;
        }

        var origin = map[x, y];
        var site = map[fx, fy];
        if (site.Owner == origin.Owner)
        {
            // Fusion
            site.Strength += origin.Strength;
            if (site.Strength > 255)
            {
                site.Strength = 255;
            }
            origin.Strength = 0;
        }
        else
        {
            if (origin.Strength >= site.Strength)
            {
                site.Owner = origin.Owner;
                site.Strength = (ushort)(origin.Strength - site.Strength);
                origin.Strength = 0;
            }
            else
            {
                site.Strength = (ushort)(site.Strength - origin.Strength);
            }
        }
    }

}

public class Helper
{
    private readonly Map map;
    private readonly ushort myId;

    public Helper(Map map, ushort myId)
    {
        this.map = map;
        this.myId = myId;
    }

    public static Direction Opposite(Direction direction)
    {
        switch (direction)
        {
            case Direction.Still:
                return Direction.Still;
            case Direction.North:
                return Direction.South;
            case Direction.East:
                return Direction.West;
            case Direction.South:
                return Direction.North;
            case Direction.West:
                return Direction.East;
        }

        return Direction.Still;
    }

    public static Direction FlipLeftRight(Direction direction)
    {
        switch (direction)
        {
            case Direction.Still:
                return Direction.Still;
            case Direction.North:
                return Direction.North;
            case Direction.East:
                return Direction.West;
            case Direction.South:
                return Direction.South;
            case Direction.West:
                return Direction.East;
        }

        return Direction.Still;
    }

    public static Direction FlipUpDown(Direction direction)
    {
        switch (direction)
        {
            case Direction.Still:
                return Direction.Still;
            case Direction.North:
                return Direction.South;
            case Direction.East:
                return Direction.East;
            case Direction.South:
                return Direction.North;
            case Direction.West:
                return Direction.West;
        }

        return Direction.Still;
    }

    public static Direction FlipBothWay(Direction direction)
    {
        var dir1 = FlipLeftRight(direction);
        var dir2 = FlipUpDown(dir1);

        return dir2;
    }

    public static ushort Mod(int x, int m)
    {
        var r = x % m;
        return (ushort)(r < 0 ? r + m : r);
    }

    public bool TryGetCenter(int id, out Location location)
    {
        const int maxIteration = 10000;
        var xA = 0;
        int xB = this.map.Width;

        var yA = 0;
        int yB = this.map.Height;

        var iteration = 0;
        int xC;
        int yC;
        do
        {
            xC = (xA + xB) / 2;
            yC = (yA + yB) / 2;
            if ((xB - xA) / 2 <= 1 && (yB - yA) / 2 <= 1) break;

            int xCountLeft = 0, xCountRight = 0;
            int yCountUp = 0, yCountDown = 0;

            var foundId = false;

            for (ushort x = 0; x < this.map.Width; x++)
            {
                for (ushort y = 0; y < this.map.Height; y++)
                {
                    if (this.map[x, y].Owner == id)
                    {
                        foundId = true;
                        if (x >= xC)
                        {
                            xCountRight++;
                        }

                        if (x <= xC)
                        {
                            xCountLeft++;
                        }

                        if (y >= yC)
                        {
                            yCountUp++;
                        }

                        if (y <= yC)
                        {
                            yCountDown++;
                        }
                    }
                }
            }

            if (!foundId)
            {
                location = new Location();
                return false;
            }

            if (xCountRight >= xCountLeft)
            {
                xA = xC;
            }

            if (xCountLeft > xCountRight)
            {
                xB = xC;
            }

            if (yCountUp >= yCountDown)
            {
                yA = yC;
            }

            if (yCountDown > yCountUp)
            {
                yB = yC;
            }

            iteration++;
        } while (iteration < maxIteration);

        location = new Location { X = (ushort)xC, Y = (ushort)yC };

        return true;
    }

    public int DistanceX(int x1, int x2)
    {
        var distanceX = Math.Abs(x1 - x2);
        var w2 = this.map.Width / 2;
        if (distanceX > w2)
        {
            return distanceX - w2;
        }
        return distanceX;
    }

    public int DistanceY(int y1, int y2)
    {
        var distanceY = Math.Abs(y1 - y2);
        var h2 = this.map.Height / 2;
        if (distanceY > h2)
        {
            return distanceY - h2;
        }
        return distanceY;
    }

    public GameStats GetStats()
    {
        var stat = new GameStats();
        var id = this.myId;

        this.ComputeStat(id, stat.MyStat);

        return stat;
    }

    private void ComputeStat(ushort id, GameStat stat)
    {
        int totalProduction = 0;

        for (ushort x = 0; x < this.map.Width; x++)
        {
            for (ushort y = 0; y < this.map.Height; y++)
            {
                var site = this.map[x, y];
                totalProduction += site.Production;


                if (site.Owner == id)
                {
                    stat.Territory++;
                    stat.Production += site.Production;
                    stat.Strength += site.Strength / 255.0;
                }
            }
        }

        int count = this.map.Width * this.map.Height;
        stat.Territory /= count;

        if (totalProduction != 0)
        {
            stat.Production /= totalProduction;
        }

        stat.Strength /= count;
    }

    public class GameStat
    {
        public double Territory { get; set; }

        public double Strength { get; set; }

        public double Production { get; set; }
    }

    public class GameStats
    {
        public GameStats()
        {
            this.MyStat = new GameStat();
            this.OtherStats = new Dictionary<int, GameStat>();
        }

        public GameStat MyStat { get; set; }

        public Dictionary<int, GameStat> OtherStats { get; set; }
    }
}