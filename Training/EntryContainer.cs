using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using ConvNetSharp;

namespace Training
{
    /// <summary>
    /// Entry container
    /// - Keep 1 out 10 entries in validation set
    /// - Iterator will yield entries for each class with equi probability
    /// </summary>
    class EntryContainer : IEnumerable<Entry>
    {
        private int n = 0;
        private Dictionary<int, List<Entry>> entriesDico = new Dictionary<int, List<Entry>>();
        private Dictionary<int, double> probas = new Dictionary<int, double>();
        private Dictionary<int, int> counters = new Dictionary<int, int>();
        private bool shuffled = false;
        private Random random = new Random(RandomUtilities.Seed);
        private Dictionary<int, int[]> indices;
        private Dictionary<int, int> k;
        private int longest;

        public List<Entry> ValidationSet { get; set; } = new List<Entry>();

        public int ClassCount
        {
            get
            {
                return this.entriesDico.Count;
            }
        }

        public string Summary
        {
            get
            {
                string result = "";
                foreach (var entry in this.entriesDico)
                {
                    result += $"[{entry.Key}]:{entry.Value.Count} ";
                }

                return result;
            }
        }

        public void Add(Entry entry)
        {
            if (shuffled)
            {
                throw new Exception("Cannot add data after Shuffle.");
            }

            if (this.n++ % 10 == 0)
            {
                this.ValidationSet.Add(entry);
                return;
            }

            if (!counters.ContainsKey(entry.OutputClass))
            {
                this.entriesDico[entry.OutputClass] = new List<Entry>();
                this.counters[entry.OutputClass] = 0;
            }

            // Add to examples
            this.entriesDico[entry.OutputClass].Add(entry);

            // Update probas
            this.counters[entry.OutputClass]++;
            this.probas[entry.OutputClass] = 1.0 / this.probas.Count; // Equi probability for each class
        }

        public int Shuffle(int classId)
        {
            var entry = this.entriesDico[classId];

            int[] entryIndices;
            if (!this.indices.TryGetValue(classId, out entryIndices))
            {
                entryIndices = new int[entry.Count];
                // 1..n
                for (int i = 0; i < entry.Count; i++)
                {
                    entryIndices[i] = i;
                }

                this.indices[classId] = entryIndices;
            }

            this.k[classId] = 0;

            // Shuffle
            for (int i = entry.Count - 1; i > 0; i--)
            {
                var j = random.Next(i);

                var temp = entryIndices[j];
                entryIndices[j] = entryIndices[i];
                entryIndices[i] = temp;
            }

            return entry.Count;
        }

        public int Shuffle()
        {
            this.shuffled = true;
            this.indices = new Dictionary<int, int[]>();
            this.k = new Dictionary<int, int>();
            this.longest = 0;

            for (int i = 0; i < this.entriesDico.Count; i++)
            {
                int count = this.Shuffle(entriesDico.Keys.ToList()[i]);
                this.longest = Math.Max(this.longest, count);
            }

            return this.longest;
        }

        public IEnumerator<Entry> GetEnumerator()
        {
            this.Shuffle();

            for (int i = 0; i < this.longest; i++)
            {
                Entry entry = null;

                // Roulette wheel
                var r = this.random.NextDouble();
                double sum = 0.0;
                foreach (var item in this.probas)
                {
                    sum += item.Value; //  proba
                    if (sum > r)
                    {
                        int chosenClass = item.Key;
                        var index = (this.k[chosenClass] + 1) % this.indices[chosenClass].Length;
                        this.k[chosenClass] = index;

                        if (index == 0)
                        {
                            this.Shuffle(chosenClass);
                        }

                        entry = this.entriesDico[chosenClass][this.indices[chosenClass][index]];
                        yield return entry;
                        break;
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
