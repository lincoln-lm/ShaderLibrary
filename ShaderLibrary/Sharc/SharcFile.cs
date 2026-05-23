using ShaderLibrary.Helpers;
using ShaderLibrary.IO;
using ShaderLibrary.Sharc;
using ShaderLibrary.SharcWiiU;
using System.Runtime.InteropServices;
using System.Text;

namespace ShaderLibrary
{
    public class SharcFile
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Header
        {
            public uint Magic; // "BAHS";
            public uint Version;
            public uint FileSize;
            public uint ByteOrder; //0 or 1 for big endian
        }

        public class ShaderSource
        {
            public string Name;
            public byte[] Data;

            public string GetCode()
            {
                var encoding = TextEncoding.ShiftJIS();
                var code = encoding.GetString(Data);
                code = code.TrimEnd('\0');
                return code.Replace("・ｿ", ""); // remove bom
            }

            public ShaderSource() { }
            public ShaderSource(string source, string name) {
                SetCode(source);
                Name = name;
            }

            public void SetCode(string source)
            {
                source ??= string.Empty;
                var encoding = TextEncoding.ShiftJIS();
                // Add terminator
                if (!source.EndsWith("\0"))
                    source += '\0';
                this.Data = encoding.GetBytes(source);
            }
        }

        public class MacroDefine
        {
            public string Name;
            public string Value;
        }

        public class ShaderProgram
        {
            public string Name;

            public List<MacroDefine> VertexMacros = new List<MacroDefine>();
            public List<MacroDefine> FragmentMacros = new List<MacroDefine>();
            public List<MacroDefine> GeometryMacros = new List<MacroDefine>();
            public List<MacroDefine> ComputeMacros = new List<MacroDefine>();

            public List<VariationMacro> VariationMacros = new List<VariationMacro>();
            public List<VariationMacro> VariationDefaults = new List<VariationMacro>();

            public List<Symbol> Uniforms = new List<Symbol>();
            public List<Symbol> UniformBlocks = new List<Symbol>();
            public List<Symbol> Samplers = new List<Symbol>();
            public List<Symbol> Attributes = new List<Symbol>();

            public List<SymbolUniformBlock> UniformBlocksV13 = new List<SymbolUniformBlock>();

            public int VertexShaderIndex;
            public int FragmentShaderIndex;
            public int GeoemetryShaderIndex;
            public int ComputeShaderIndex;

            public int GetVariationIndex(Dictionary<string, string> options)
            {
                int index = 0;
                foreach (var variation in VariationMacros)
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

            public IEnumerable<Dictionary<string, string>> GetAllVariationCombinations()
            {
                var result = new List<Dictionary<string, string>>();

                void Recurse(int index, Dictionary<string, string> current)
                {
                    if (index >= VariationMacros.Count)
                    {
                        result.Add(new Dictionary<string, string>(current));
                        return;
                    }
                    var variation = VariationMacros[index];
                    foreach (var value in variation.Values)
                    {
                        current[variation.Name] = value;
                        Recurse(index + 1, current);
                    }
                }

                Recurse(0, new Dictionary<string, string>());

                return result;
            }
        }

        public class VariationMacro
        {
            public string Name { get; set; }
            public List<string> Values { get; set; } = new List<string>();
            public byte[] Data { get; set; }
        }

        public class Symbol
        {
            public uint Offset;
            public uint Size;
            public string Name;
            public string SymbolName;
            public List<string> Values = new();
            public byte[] DefaultData;
            public byte[] UsedVariants;
        }
        public class SymbolUniformBlock
        {
            public string Name;
            public int Location;
            public uint Size;

            // For sharc
            public List<Symbol> Uniforms = new();
        }

        public Header FileHeader;
        public List<ShaderProgram> Programs = new();
        public List<ShaderSource> Sources = new();
        public string Name;

        public bool IsSwitch = false;

        public static bool Identify(Stream stream)
        {
            using (var reader = new BinaryDataReader(stream, false, true)) {
                using (reader.BaseStream.TemporarySeek(0, SeekOrigin.Begin)) {
                    string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    return magic == "AAHS";
                }
            }
        }

        public SharcFile() { }
        public SharcFile(string filePath)
        {
            using (var reader = new BinaryDataReader(File.OpenRead(filePath)))
                SharcReader.Read(this, reader);
        }

        public void Save(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var wr = new BinaryDataWriter(fs))
            {
                //SharcfbV1Writer.Write(this, wr);
            }
        }
    }
}