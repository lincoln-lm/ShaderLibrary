using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShaderLibrary.Sharc
{
    public class SharcUtils
    {
        public static IEnumerable<Dictionary<string, string>> GetAllVariationCombinations(
            List<SharcFile.VariationMacro> variations)
        {
            var result = new List<Dictionary<string, string>>();

            void Recurse(int index, Dictionary<string, string> current)
            {
                if (index >= variations.Count)
                {
                    result.Add(new Dictionary<string, string>(current));
                    return;
                }
                var variation = variations[index];
                foreach (var value in variation.Values)
                {
                    current[variation.Name] = value;
                    Recurse(index + 1, current);
                }
            }

            Recurse(0, new Dictionary<string, string>());
            return result;
        }

        public static int GetVariationIndex(List<SharcFile.VariationMacro> variations, 
            Dictionary<string, string> options)
        {
            int index = 0;
            foreach (var variation in variations)
            {
                if (!options.ContainsKey(variation.Name))
                    continue;

                if (!variation.Values.Contains(options[variation.Name]))
                    throw new Exception($"Invalid option setting on {variation.Name}! {options[variation.Name]}. Valid choices: {string.Join(",", variation.Values.ToArray())}");

                index *= variation.Values.Count;
                index += variation.Values.IndexOf(options[variation.Name]);
            }
            return index;
        }
    }
}
