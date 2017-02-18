using ConvNetSharp;
using System.Collections.Generic;
using System.Linq;

namespace Training
{
    public class Entry : IEntry
    {
        public Entry(IVolume input, int output, params int[] parameters)
        {
            this.Input = new[] { input };
            this.OutputClass = output;
            this.Parameters = parameters;
        }

        public Entry(IEnumerable<IVolume> input, int output, params int[] parameters)
        {
            this.Input = input.ToArray();
            this.OutputClass = output;
            this.Parameters = parameters;
        }
        public IVolume[] Input { get; private set; }

        public int OutputClass { get; set; }

        public bool IsValidation { get; set; }

        public int[] Parameters { get; private set; }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var item = obj as Entry;

            if (item.Parameters.Length != this.Parameters.Length)
            {
                return false;
            }

            for (int i = 0; i < this.Parameters.Length; i++)
            {
                if (this.Parameters[i] != item.Parameters[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            int hash = 17;

            for (int i = 0; i < this.Parameters.Length; i++)
            {
                hash = hash * 23 + this.Parameters[i].GetHashCode();
            }

            return hash;
        }

        public override string ToString()
        {
            return $"O={this.OutputClass} V={this.IsValidation}";
        }
    }
}
