using ConvNetSharp;
using ConvNetSharp.Fluent;
using ConvNetSharp.Layers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

#region Bot
/// <summary>
/// Single net x 3 (normal, early, strong)
/// </summary>
public class IntelligentBotV8
{
    private Helper helper;
    private int frame;
    private double lastElapsed = -1;
    private bool degradedMode = false;
    private Map map;
    private INet singleNet;
    private INet singleNet_strong;
    private INet singleNet_early;

    int lastmoveCount = 1;

    private ushort myId;

    private Random random = new Random();

    public IntelligentBotV8(Map map, ushort myID)
    {
        this.map = map;
        this.myId = myID;

        this.JourneyStart();
        this.GameStart();
    }

    public IntelligentBotV8()
    {
    }

    public string Name { get; set; } = "";

    public string Suffix { get; set; } = "";

    public string Prefix { get; set; } = "";

    public Map Map
    {
        set { this.map = value; }
    }

    public ushort Id
    {
        set { this.myId = value; }
    }

    public IEnumerable<Move> GetMoves()
    {
        var moves = new List<Move>();

        try
        {
            var chrono = System.Diagnostics.Stopwatch.StartNew();

            bool earlyGame = lastmoveCount < 15;
            int moveCount = 0;

            for (ushort x = 0; x < this.map.Width; x++)
            {
                for (ushort y = 0; y < this.map.Height; y++)
                {
                    if (this.map[x, y].Owner == this.myId)
                    {
                        Direction move = Direction.Still;
                        if (lastElapsed > 850)
                        {
                            degradedMode = true;
                        }

                        if (this.map[x, y].Strength > 1)
                        {
                            if (!degradedMode || ((frame + x + y) % 4 == 0) || this.map[x, y].Strength == 255)
                            {
                                FluentNet net = singleNet as FluentNet;
                                if (earlyGame)
                                {
                                    net = singleNet_early as FluentNet; ;
                                }
                                else if (map[x, y].Strength > 200)
                                {
                                    net = singleNet_strong as FluentNet; ;
                                }

                                int inputWidth = net.InputLayers[0].InputWidth;
                                var volume = this.map.GetVolume(inputWidth, this.myId, x, y);
                                net.Forward(false, volume);
                                move = (Direction)net.GetPrediction();
                            }
                        }

                        moves.Add(new Move
                        {
                            Location = new Location { X = x, Y = y },
                            Direction = move
                        });

                        if (chrono.Elapsed.TotalMilliseconds > 900)
                        {
                            return moves;
                        }

                        moveCount++;
                    }
                }
            }

            this.lastmoveCount = moveCount;

            this.frame++;

            lastElapsed = chrono.Elapsed.TotalMilliseconds;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }

        return moves;
    }

    public void GameStart()
    {
        this.frame = 0;
        this.helper = new Helper(this.map, this.myId);
    }

    public void GameStop(int winnerId)
    {
    }

    public void JourneyStop()
    {
    }

    public void JourneyStart()
    {
        this.Load();
    }

    public void Load()
    {
        this.singleNet = NetExtension.LoadNet($"{this.Prefix}net{this.Suffix}.dat");
        this.singleNet_strong = NetExtension.LoadNet($"{this.Prefix}net{this.Suffix}_strong.dat");
        this.singleNet_early = NetExtension.LoadNet($"{this.Prefix}net{this.Suffix}_early.dat");
    }
}

public class MyBot
{
    public const string MyBotName = "MyC#Bot";

    public static void Main(string[] args)
    {
        Console.SetIn(Console.In);
        Console.SetOut(Console.Out);

        ushort myID;
        var map = Networking.getInit(out myID);
        var bot = new IntelligentBotV8(map, myID);

        /* ------
            Do more prep work, see rules for time limit
        ------ */

        Networking.SendInit(MyBotName); // Acknoweldge the init and begin the game

        try
        {
            while (true)
            {
                Networking.getFrame(ref map); // Update the map to reflect the moves before this turn

                IEnumerable<Move> moves = bot.GetMoves();

                Networking.SendMoves(moves); // Send moves
            }
        }
        catch (Exception ex)
        {
        }
    }
}

[Flags]
public enum VolumeMode
{
    Production = 1,
    Strength = 2,
    Territory = 4,
    Only255 = 8,
    HasBorder = 16
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
}

#endregion

#region ConvNetSharp

namespace ConvNetSharp
{
    namespace Fluent
    {
        [Serializable]
        public class FluentNet : INet
        {
            private LastLayerBase lastLayer;
            List<LayerBase> allLayers = new List<LayerBase>();

            public FluentNet(LastLayerBase layer)
            {
                this.lastLayer = layer;

                this.FindLayers(layer, this.InputLayers, this.allLayers);
            }

            public List<InputLayer> InputLayers { get; private set; } = new List<InputLayer>();

            private void FindLayers(LayerBase layer, List<InputLayer> inputLayers, List<LayerBase> allLayers)
            {
                allLayers.Add(layer);

                var inputLayer = layer as InputLayer;
                if (inputLayer != null)
                {
                    inputLayers.Add(inputLayer);
                    return;
                }
                else
                {
                    foreach (var parent in layer.Parents)
                    {
                        this.FindLayers(parent, inputLayers, allLayers);
                    }
                }
            }

            public IVolume Forward(bool isTraining = false, params IVolume[] inputs)
            {
                for (int i = 0; i < inputs.Length; i++)
                {
                    this.InputLayers[i].Forward(inputs[i], isTraining);
                }

                return this.lastLayer.Forward(isTraining);
            }

            public int GetPrediction()
            {
                // this is a convenience function for returning the argmax
                // prediction, assuming the last layer of the net is a softmax
                var softmaxLayer = this.lastLayer as SoftmaxLayer;
                if (softmaxLayer == null)
                {
                    throw new Exception("GetPrediction function assumes softmax as last layer of the net!");
                }

                var maxv = softmaxLayer.OutputActivation.GetWeight(0);
                var maxi = 0;

                for (var i = 1; i < softmaxLayer.OutputActivation.Length; i++)
                {
                    if (softmaxLayer.OutputActivation.GetWeight(i) > maxv)
                    {
                        maxv = softmaxLayer.OutputActivation.GetWeight(i);
                        maxi = i;
                    }
                }

                return maxi; // return index of the class with highest class probability
            }
        }
    }

    [Serializable]
    public class Net : INet
    {
        private readonly List<LayerBase> layers = new List<LayerBase>();

        public List<LayerBase> Layers
        {
            get { return this.layers; }
        }

        public IVolume Forward(bool isTraining = false, params IVolume[] inputs)
        {
            var activation = this.layers[0].Forward(inputs[0], isTraining);

            for (var i = 1; i < this.layers.Count; i++)
            {
                var layerBase = this.layers[i];
                activation = layerBase.Forward(activation, isTraining);
            }

            return activation;
        }

        public int GetPrediction()
        {
            // this is a convenience function for returning the argmax
            // prediction, assuming the last layer of the net is a softmax
            var softmaxLayer = this.layers[this.layers.Count - 1] as SoftmaxLayer;
            if (softmaxLayer == null)
            {
                throw new Exception("GetPrediction function assumes softmax as last layer of the net!");
            }

            var maxv = softmaxLayer.OutputActivation.GetWeight(0);
            var maxi = 0;

            for (var i = 1; i < softmaxLayer.OutputActivation.Length; i++)
            {
                if (softmaxLayer.OutputActivation.GetWeight(i) > maxv)
                {
                    maxv = softmaxLayer.OutputActivation.GetWeight(i);
                    maxi = i;
                }
            }

            return maxi; // return index of the class with highest class probability
        }
    }

    public interface IConvNetSerializable : ISerializable
    {
        bool IsLearning { get; set; }
    }

    public interface INet
    {
        IVolume Forward(bool isTraining = false, params IVolume[] inputs);

        int GetPrediction();
    }

    public interface IVolume : IEnumerable<double>
    {
        void Add(int x, int y, int d, double v);

        void AddFrom(IVolume volume);

        void AddFromScaled(IVolume volume, double a);

        void AddGradient(int x, int y, int d, double v);

        void AddGradientFrom(IVolume volume);

        IVolume Clone();

        IVolume CloneAndZero();

        double Get(int x, int y, int d);

        double GetGradient(int x, int y, int d);

        void Set(int x, int y, int d, double v);

        void SetConst(double c);

        void SetGradient(int x, int y, int d, double v);

        double GetWeight(int i);

        void SetWeight(int i, double v);

        double GetWeightGradient(int i);

        void SetWeightGradient(int i, double v);

        int Width { get; }

        int Height { get; }

        int Depth { get; }

        int Length { get; }

        void ZeroGradients();
    }

    namespace Layers
    {
        public interface ILastLayer
        {
            double Backward(double y);

            double Backward(double[] y);
        }

        public interface IClassificationLayer
        {
            int ClassCount { get; set; }
        }

        [Serializable]
        public abstract class LastLayerBase : LayerBase, ILastLayer
        {
            public abstract double Backward(double[] y);

            public abstract double Backward(double y);
        }

        [Serializable]
        public abstract class LayerBase
        {
            public IVolume InputActivation { get; protected set; }

            public IVolume OutputActivation { get; protected set; }

            public int OutputDepth { get; protected set; }

            public int OutputWidth { get; protected set; }

            public int OutputHeight { get; protected set; }

            public int InputDepth { get; private set; }

            public int InputWidth { get; private set; }

            public int InputHeight { get; private set; }

            public LayerBase Child { get; set; }

            public List<LayerBase> Parents { get; set; } = new List<LayerBase>();

            public abstract IVolume Forward(IVolume input, bool isTraining = false);

            public virtual IVolume Forward(bool isTraining)
            {
                return this.Forward(this.Parents[0].Forward(isTraining), isTraining);
            }

            public virtual void Init(int inputWidth, int inputHeight, int inputDepth)
            {
                this.InputWidth = inputWidth;
                this.InputHeight = inputHeight;
                this.InputDepth = inputDepth;
            }

            internal void ConnectTo(LayerBase layer)
            {
                this.Child = layer;
                layer.Parents.Add(this);

                layer.Init(this.OutputWidth, this.OutputHeight, this.OutputDepth);
            }
        }

        [Serializable]
        public sealed class InputLayer : LayerBase
        {
            public InputLayer(int inputWidth, int inputHeight, int inputDepth)
            {
                this.Init(inputWidth, inputHeight, inputDepth);

                this.OutputWidth = inputWidth;
                this.OutputHeight = inputHeight;
                this.OutputDepth = inputDepth;
            }

            public override IVolume Forward(IVolume input, bool isTraining = false)
            {
                this.InputActivation = input;
                this.OutputActivation = input;
                return this.OutputActivation; // simply identity function for now
            }

            public override IVolume Forward(bool isTraining)
            {
                return this.OutputActivation;
            }
        }

        [Serializable]
        public class SoftmaxLayer : LastLayerBase, IClassificationLayer
        {
            private double[] es;

            public SoftmaxLayer(int classCount)
            {
                this.ClassCount = classCount;
            }

            public int ClassCount { get; set; }

            public override double Backward(double y)
            {
                var yint = (int)y;

                // compute and accumulate gradient wrt weights and bias of this layer
                var x = this.InputActivation;
                x.ZeroGradients(); // zero out the gradient of input Vol

                for (var i = 0; i < this.OutputDepth; i++)
                {
                    var indicator = i == yint ? 1.0 : 0.0;
                    var mul = -(indicator - this.es[i]);
                    x.SetWeightGradient(i, mul);
                }

                // loss is the class negative log likelihood
                return -Math.Log(this.es[yint]);
            }

            public override double Backward(double[] y)
            {
                throw new NotImplementedException();
            }

            public override IVolume Forward(IVolume input, bool isTraining = false)
            {
                this.InputActivation = input;

                var outputActivation = new Volume(1, 1, this.OutputDepth, 0.0);

                // compute max activation
                var amax = input.GetWeight(0);
                for (var i = 1; i < this.OutputDepth; i++)
                {
                    if (input.GetWeight(i) > amax)
                    {
                        amax = input.GetWeight(i);
                    }
                }

                // compute exponentials (carefully to not blow up)
                var es = new double[this.OutputDepth];
                var esum = 0.0;
                for (var i = 0; i < this.OutputDepth; i++)
                {
                    var e = Math.Exp(input.GetWeight(i) - amax);
                    esum += e;
                    es[i] = e;
                }

                // normalize and output to sum to one
                for (var i = 0; i < this.OutputDepth; i++)
                {
                    es[i] /= esum;
                    outputActivation.SetWeight(i, es[i]);
                }

                this.es = es; // save these for backprop
                this.OutputActivation = outputActivation;
                return this.OutputActivation;
            }

            public override void Init(int inputWidth, int inputHeight, int inputDepth)
            {
                base.Init(inputWidth, inputHeight, inputDepth);

                var inputCount = inputWidth * inputHeight * inputDepth;
                this.OutputDepth = inputCount;
                this.OutputWidth = 1;
                this.OutputHeight = 1;
            }
        }

        [Serializable]
        public class ReluLayer : LayerBase
        {
            public override IVolume Forward(IVolume input, bool isTraining = false)
            {
                this.InputActivation = input;
                var output = input.Clone();

                Parallel.For(0, input.Length, i =>
                    {
                        if (output.GetWeight(i) < 0)
                        {
                            output.SetWeight(i, 0); // threshold at 0
                        }
                    });
                this.OutputActivation = output;
                return this.OutputActivation;
            }

            public override void Init(int inputWidth, int inputHeight, int inputDepth)
            {
                base.Init(inputWidth, inputHeight, inputDepth);

                this.OutputDepth = inputDepth;
                this.OutputWidth = inputWidth;
                this.OutputHeight = inputHeight;
            }
        }

        [Serializable]
        public class FullyConnLayer : LayerBase
        {
            private int inputCount;

            public FullyConnLayer(int neuronCount)
            {
                this.NeuronCount = neuronCount;

                this.L1DecayMul = 0.0;
                this.L2DecayMul = 1.0;
            }

            public IVolume Biases { get; private set; }

            public List<IVolume> Filters { get; private set; }

            public double L1DecayMul { get; set; }

            public double L2DecayMul { get; set; }

            public int NeuronCount { get; private set; }

            public double BiasPref { get; set; }

            public override IVolume Forward(IVolume input, bool isTraining = false)
            {
                this.InputActivation = input;
                var outputActivation = new Volume(1, 1, this.OutputDepth, 0.0);

                Parallel.For(0, this.OutputDepth, (int i) =>
                    {
                        var a = 0.0;
                        for (var d = 0; d < this.inputCount; d++)
                        {
                            a += input.GetWeight(d) * this.Filters[i].GetWeight(d); // for efficiency use Vols directly for now
                        }

                        a += this.Biases.GetWeight(i);
                        outputActivation.SetWeight(i, a);
                    });

                this.OutputActivation = outputActivation;
                return this.OutputActivation;
            }

            public override void Init(int inputWidth, int inputHeight, int inputDepth)
            {
                base.Init(inputWidth, inputHeight, inputDepth);

                // required
                // ok fine we will allow 'filters' as the word as well
                this.OutputDepth = this.NeuronCount;

                // computed
                this.inputCount = inputWidth * inputHeight * inputDepth;
                this.OutputWidth = 1;
                this.OutputHeight = 1;

                // initializations
                var bias = this.BiasPref;
                this.Filters = new List<IVolume>();

                for (var i = 0; i < this.OutputDepth; i++)
                {
                    this.Filters.Add(new Volume(1, 1, this.inputCount));
                }

                this.Biases = new Volume(1, 1, this.OutputDepth, bias);
            }
        }

        [Serializable]
        public class ConvLayer : LayerBase
        {
            private int stride = 1;
            private int pad;

            public ConvLayer(int width, int height, int filterCount)
            {
                this.L1DecayMul = 0.0;
                this.L2DecayMul = 1.0;

                this.FilterCount = filterCount;
                this.Width = width;
                this.Height = height;
            }

            public int Width { get; private set; }

            public int Height { get; private set; }

            public Volume Biases { get; private set; }

            public List<Volume> Filters { get; private set; }

            public int FilterCount { get; private set; }

            public double L1DecayMul { get; set; }

            public double L2DecayMul { get; set; }

            public int Stride
            {
                get
                {
                    return this.stride;
                }
                set
                {
                    this.stride = value;
                    this.UpdateOutputSize();
                }
            }

            public int Pad
            {
                get
                {
                    return this.pad;
                }
                set
                {
                    this.pad = value;
                    this.UpdateOutputSize();
                }
            }

            public double BiasPref { get; set; }

            public override IVolume Forward(IVolume input, bool isTraining = false)
            {
                // optimized code by @mdda that achieves 2x speedup over previous version

                this.InputActivation = input;
                var outputActivation = new Volume(this.OutputWidth, this.OutputHeight, this.OutputDepth, 0.0);

                var volumeWidth = input.Width;
                var volumeHeight = input.Height;
                var xyStride = this.Stride;

                Parallel.For(0, this.OutputDepth, depth =>
                    {
                        var filter = this.Filters[depth];
                        var y = -this.Pad;

                        for (var ay = 0; ay < this.OutputHeight; y += xyStride, ay++)
                        {
                            // xyStride
                            var x = -this.Pad;
                            for (var ax = 0; ax < this.OutputWidth; x += xyStride, ax++)
                            {
                                // xyStride

                                // convolve centered at this particular location
                                var a = 0.0;
                                for (var fy = 0; fy < filter.Height; fy++)
                                {
                                    var oy = y + fy; // coordinates in the original input array coordinates
                                    for (var fx = 0; fx < filter.Width; fx++)
                                    {
                                        var ox = x + fx;
                                        if (oy >= 0 && oy < volumeHeight && ox >= 0 && ox < volumeWidth)
                                        {
                                            for (var fd = 0; fd < filter.Depth; fd++)
                                            {
                                                // avoid function call overhead (x2) for efficiency, compromise modularity :(
                                                a += filter.GetWeight((filter.Width * fy + fx) * filter.Depth + fd) *
                                                         input.GetWeight((volumeWidth * oy + ox) * input.Depth + fd);
                                            }
                                        }
                                    }
                                }

                                a += this.Biases.GetWeight(depth);
                                outputActivation.Set(ax, ay, depth, a);
                            }
                        }
                    });

                this.OutputActivation = outputActivation;
                return this.OutputActivation;
            }

            public override void Init(int inputWidth, int inputHeight, int inputDepth)
            {
                base.Init(inputWidth, inputHeight, inputDepth);

                this.UpdateOutputSize();
            }

            private void UpdateOutputSize()
            {
                // required
                this.OutputDepth = this.FilterCount;

                // computed
                // note we are doing floor, so if the strided convolution of the filter doesnt fit into the input
                // volume exactly, the output volume will be trimmed and not contain the (incomplete) computed
                // final application.
                this.OutputWidth = (int)Math.Floor((this.InputWidth + this.Pad * 2 - this.Width) / (double)this.Stride + 1);
                this.OutputHeight = (int)Math.Floor((this.InputHeight + this.Pad * 2 - this.Height) / (double)this.Stride + 1);

                // initializations
                var bias = this.BiasPref;
                this.Filters = new List<Volume>();

                for (var i = 0; i < this.OutputDepth; i++)
                {
                    this.Filters.Add(new Volume(this.Width, this.Height, this.InputDepth));
                }

                this.Biases = new Volume(1, 1, this.OutputDepth, bias);
            }
        }

        [Serializable]
        public class TanhLayer : LayerBase
        {
            public override IVolume Forward(IVolume input, bool isTraining = false)
            {
                this.InputActivation = input;
                var outputActivation = input.CloneAndZero();
                var length = input.Length;


            Parallel.For(0, length, i =>
                {
                    outputActivation.SetWeight(i, Math.Tanh(input.GetWeight(i)));
                });
                this.OutputActivation = outputActivation;
                return this.OutputActivation;
            }

            public override void Init(int inputWidth, int inputHeight, int inputDepth)
            {
                base.Init(inputWidth, inputHeight, inputDepth);

                this.OutputDepth = inputDepth;
                this.OutputWidth = inputWidth;
                this.OutputHeight = inputHeight;
            }
        }

        public static class RandomUtilities
        {
            private static readonly Random Random = new Random(Seed);
            private static double val;
            private static bool returnVal;

            public static int Seed
            {
                get { return (int)DateTime.Now.Ticks; }
            }

            public static double GaussianRandom()
            {
                if (returnVal)
                {
                    returnVal = false;
                    return val;
                }

                var u = 2 * Random.NextDouble() - 1;
                var v = 2 * Random.NextDouble() - 1;
                var r = u * u + v * v;

                if (r == 0 || r > 1)
                {
                    return GaussianRandom();
                }

                var c = Math.Sqrt(-2 * Math.Log(r) / r);
                val = v * c; // cache this
                returnVal = true;

                return u * c;
            }

            public static double Randn(double mu, double std)
            {
                return mu + GaussianRandom() * std;
            }
        }

        [Serializable]
        public class Volume : IVolume
        {
            private double[] WeightGradients;

            private double[] Weights;

            public int Width { get; private set; }

            public int Height { get; private set; }

            public int Depth { get; private set; }

            public int Length { get { return this.Weights.Length; } }

            /// <summary>
            ///     Volume will be filled with random numbers
            /// </summary>
            /// <param name="width">width</param>
            /// <param name="height">height</param>
            /// <param name="depth">depth</param>
            public Volume(int width, int height, int depth)
            {
                // we were given dimensions of the vol
                this.Width = width;
                this.Height = height;
                this.Depth = depth;

                var n = width * height * depth;
                this.Weights = new double[n];
                this.WeightGradients = new double[n];

                // weight normalization is done to equalize the output
                // variance of every neuron, otherwise neurons with a lot
                // of incoming connections have outputs of larger variance
                var scale = Math.Sqrt(1.0 / (width * height * depth));

                for (var i = 0; i < n; i++)
                {
                    this.Weights[i] = RandomUtilities.Randn(0.0, scale);
                }
            }

            /// <summary>
            /// </summary>
            /// <param name="width">width</param>
            /// <param name="height">height</param>
            /// <param name="depth">depth</param>
            /// <param name="c">value to initialize the volume with</param>
            public Volume(int width, int height, int depth, double c)
            {
                // we were given dimensions of the vol
                this.Width = width;
                this.Height = height;
                this.Depth = depth;

                var n = width * height * depth;
                this.Weights = new double[n];
                this.WeightGradients = new double[n];

                if (c != 0)
                {
                    for (var i = 0; i < n; i++)
                    {
                        this.Weights[i] = c;
                    }
                }
            }

            public Volume(IList<double> weights)
            {
                // we were given a list in weights, assume 1D volume and fill it up
                this.Width = 1;
                this.Height = 1;
                this.Depth = weights.Count;

                this.Weights = new double[this.Depth];
                this.WeightGradients = new double[this.Depth];

                for (var i = 0; i < this.Depth; i++)
                {
                    this.Weights[i] = weights[i];
                }
            }

            public double Get(int x, int y, int d)
            {
                var ix = ((this.Width * y) + x) * this.Depth + d;
                return this.Weights[ix];
            }

            public void Set(int x, int y, int d, double v)
            {
                var ix = ((this.Width * y) + x) * this.Depth + d;
                this.Weights[ix] = v;
            }

            public void Add(int x, int y, int d, double v)
            {
                var ix = ((this.Width * y) + x) * this.Depth + d;
                this.Weights[ix] += v;
            }

            public double GetGradient(int x, int y, int d)
            {
                var ix = ((this.Width * y) + x) * this.Depth + d;
                return this.WeightGradients[ix];
            }

            public void SetGradient(int x, int y, int d, double v)
            {
                var ix = ((this.Width * y) + x) * this.Depth + d;
                this.WeightGradients[ix] = v;
            }

            public void AddGradient(int x, int y, int d, double v)
            {
                var ix = ((this.Width * y) + x) * this.Depth + d;
                this.WeightGradients[ix] += v;
            }

            public IVolume CloneAndZero()
            {
                return new Volume(this.Width, this.Height, this.Depth, 0.0);
            }

            public IVolume Clone()
            {
                var volume = new Volume(this.Width, this.Height, this.Depth, 0.0);
                var n = this.Weights.Length;

                for (var i = 0; i < n; i++)
                {
                    volume.Weights[i] = this.Weights[i];
                }

                return volume;
            }

            public void ZeroGradients()
            {
                Array.Clear(this.WeightGradients, 0, this.WeightGradients.Length);
            }

            public void AddFrom(IVolume volume)
            {
                for (var i = 0; i < this.Weights.Length; i++)
                {
                    this.Weights[i] += volume.GetWeight(i);
                }
            }

            public void AddGradientFrom(IVolume volume)
            {
                for (var i = 0; i < this.WeightGradients.Length; i++)
                {
                    this.WeightGradients[i] += volume.GetWeightGradient(i);
                }
            }

            public void AddFromScaled(IVolume volume, double a)
            {
                for (var i = 0; i < this.Weights.Length; i++)
                {
                    this.Weights[i] += a * volume.GetWeight(i);
                }
            }

            public void SetConst(double c)
            {
                for (var i = 0; i < this.Weights.Length; i++)
                {
                    this.Weights[i] += c;
                }
            }

            public double GetWeight(int i)
            {
                return this.Weights[i];
            }

            public double GetWeightGradient(int i)
            {
                return this.WeightGradients[i];
            }

            public void SetWeightGradient(int i, double v)
            {
                this.WeightGradients[i] = v; ;
            }

            public void SetWeight(int i, double v)
            {
                this.Weights[i] = v;
            }

            public IEnumerator<double> GetEnumerator()
            {
                for (int i = 0; i < this.Length; i++)
                {
                    yield return this.Weights[i];
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }
    }

    #endregion

#region NetExtension

    sealed class MyBinder : SerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            File.AppendAllText("log.txt", $"{assemblyName}-{typeName}" + Environment.NewLine);

            if (typeName.Contains("LayerBase"))
            {
                if (typeName.Contains("List"))
                {
                    return typeof(List<LayerBase>);
                }

                return typeof(LayerBase);
            }

            if (typeName.Contains("InputLayer"))
            {
                if (typeName.Contains("List"))
                {
                    return typeof(List<InputLayer>);
                }

                return typeof(InputLayer);
            }

            if (typeName.Contains("SoftmaxLayer"))
            {
                return typeof(SoftmaxLayer);
            }

            if (typeName.Contains("ReluLayer"))
            {
                return typeof(ReluLayer);
            }

            if (typeName.Contains("FullyConnLayer"))
            {
                return typeof(FullyConnLayer);
            }

            if (typeName.Contains("IVolume"))
            {
                if (typeName.Contains("List"))
                {
                    return typeof(List<IVolume>);
                }

                return typeof(IVolume);
            }

            if (typeName.Contains("Volume"))
            {
                if (typeName.Contains("List"))
                {
                    return typeof(List<Volume>);
                }

                return typeof(Volume);
            }

            String exeAssembly = Assembly.GetExecutingAssembly().FullName;
            var typeToDeserialize = Type.GetType(String.Format("{0}, {1}", typeName, exeAssembly));
            return typeToDeserialize;
        }
    }

    public static class SerializationExtensions
    {
        public static INet LoadBinary(Stream stream)
        {
            IFormatter formatter = new BinaryFormatter();
            formatter.Binder = new MyBinder();
            return formatter.Deserialize(stream) as INet;
        }
    }

    public static class NetExtension
    {
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
    }
}

#endregion
