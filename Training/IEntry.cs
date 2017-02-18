using ConvNetSharp;

namespace Training
{
    public interface IEntry
    {
        IVolume[] Input { get; }

        int OutputClass { get; }

        bool IsValidation { get; set; }
    }
}
