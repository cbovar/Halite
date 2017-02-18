using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// Helpful for debugging.
/// </summary>
public static class Log
{
    private static string _logPath;

    /// <summary>
    /// File must exist
    /// </summary>
    public static void Setup(string logPath)
    {
        _logPath = logPath;
    }

    public static void Information(string message)
    {
        if (!string.IsNullOrEmpty(_logPath))
            File.AppendAllLines(_logPath, new[] { string.Format("{0}.{1}: {2}", DateTime.Now.ToShortTimeString(), DateTime.Now.Millisecond, message) });
    }

    public static void Error(Exception exception)
    {
        Log.Information(string.Format("ERROR: {0} {1}", exception.Message, exception.StackTrace));
    }
}

public static class Stats
{
    public static void Information(string label, string value)
    {
        if (!string.IsNullOrEmpty($"{label}.csv"))
        {
            File.AppendAllLines($"{label}.csv", new[] { value });
        }
    }
}

public static class Networking
{
    private static string ReadNextLine()
    {
        var str = Console.ReadLine();
        if (str == null) throw new ApplicationException("Could not read next line from stdin");
        return str;
    }

    private static void SendString(string str)
    {
        Console.WriteLine(str);
    }

    /// <summary>
    /// Call once at the start of a game to load the map and player tag from the first four stdin lines.
    /// </summary>
    public static Map getInit(out ushort playerTag)
    {

        // Line 1: Player tag
        if (!ushort.TryParse(ReadNextLine(), out playerTag))
            throw new ApplicationException("Could not get player tag from stdin during init");

        // Lines 2-4: Map
        var map = Map.ParseMap(ReadNextLine(), ReadNextLine(), ReadNextLine());
        return map;
    }

    /// <summary>
    /// Call every frame to update the map to the next one provided by the environment.
    /// </summary>
    public static void getFrame(ref Map map)
    {
        map.Update(ReadNextLine());
    }


    /// <summary>
    /// Call to acknowledge the initail game map and start the game.
    /// </summary>
    public static void SendInit(string botName)
    {
        SendString(botName);
    }

    /// <summary>
    /// Call to send your move orders and complete your turn.
    /// </summary>
    public static void SendMoves(IEnumerable<Move> moves)
    {
        SendString(Move.MovesToString(moves));
    }
}

public enum Direction
{
    Still = 0,
    North = 1,
    East = 2,
    South = 3,
    West = 4
}

public struct Site
{
    public ushort Owner { get; set; }
    public ushort Strength { get; set; }
    public ushort Production { get; set; }

    public override string ToString()
    {
        return $"Owner: {this.Owner}, Strength: {this.Strength}, Production: {this.Production}";
    }
}

[DebuggerDisplay("({X},{Y})")]
public struct Location
{
    public ushort X;
    public ushort Y;

    public bool Equals(Location other)
    {
        return this.X == other.X && this.Y == other.Y;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        return obj is Location && Equals((Location)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (this.X.GetHashCode() * 397) ^ this.Y.GetHashCode();
        }
    }

    public override string ToString()
    {
        return $"({this.X}, {this.Y})";
    }
}

public struct Move
{
    public Location Location;
    public Direction Direction;

    internal static string MovesToString(IEnumerable<Move> moves)
    {
        return string.Join(" ", moves.Select(m => string.Format("{0} {1} {2}", m.Location.X, m.Location.Y, (int)m.Direction)));
    }
}