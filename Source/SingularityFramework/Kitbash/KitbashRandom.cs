using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// The dice for one build. Seeded, so the same pawn or weapon always gets the same result, and kept separate from
    /// the game's own random numbers (Verse.Rand), so building a texture never disturbs the game.
    /// </summary>
    public class KitbashRandom
    {
        private readonly Random random;

        public KitbashRandom(int seed)
        {
            random = new Random(seed);
        }

        /// <summary>
        /// A number from minimum up to maximum.
        /// </summary>
        public float Uniform(float minimum, float maximum)
        {
            return minimum + (float)random.NextDouble() * (maximum - minimum);
        }

        public float Value() => (float)random.NextDouble();

        public bool Chance(float probability) => random.NextDouble() < probability;

        /// <summary>
        /// A whole number from minimum to maximum, both included.
        /// </summary>
        public int RangeInclusive(int minimum, int maximum) => random.Next(minimum, maximum + 1);

        public int Index(int count) => random.Next(count);

        public int Sign() => random.Next(2) == 0 ? -1 : 1;

        public int NewSeed() => random.Next();

        public T Choice<T>(IList<T> options) => options[random.Next(options.Count)];

        public T WeightedChoice<T>(IList<T> options, IList<float> weights)
        {
            float total = 0f;
            foreach (float weight in weights) total += weight;
            float roll = Uniform(0f, total);
            for (int index = 0; index < options.Count; index++)
            {
                roll -= weights[index];
                if (roll <= 0f) return options[index];
            }
            return options[options.Count - 1];
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int index = list.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                T held = list[index];
                list[index] = list[swapIndex];
                list[swapIndex] = held;
            }
        }
    }
}
