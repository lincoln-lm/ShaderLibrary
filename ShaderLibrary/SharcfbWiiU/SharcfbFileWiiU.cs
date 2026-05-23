using ShaderLibrary.IO;
using ShaderLibrary.Sharc;
using ShaderLibrary.SharcWiiU;
using System.Runtime.InteropServices;

namespace ShaderLibrary
{
    public class SharcfbFileWiiU : ISharcFile
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Header
        {
            public uint Magic; // "BAHS";
            public uint Version;
            public uint FileSize;
            public uint ByteOrder; //0 or 1 for big endian
            public uint Zero;
        }

        public class ShaderBinary
        {
            public GX2ShaderType Type;
            public byte[] Data;
        }

        public class ShaderProgram
        {
            public string Name;
            public uint Kind;
            public int BaseIndex;
            public int StageCount => HasGeometryShader() ? 3 : 2;

            public List<SharcFile.VariationMacro> VariationMacros = new();
            public List<SharcFile.VariationMacro> VariationDefaults = new();
            public List<Symbol> Uniforms = new List<Symbol>();
            public List<Symbol> UniformBlocks = new List<Symbol>();
            public List<Symbol> Samplers = new List<Symbol>();
            public List<Symbol> Attributes = new List<Symbol>();

            public int GetBinaryIndex(Dictionary<string, string> options)
            {
                var variant = GetVariationIndex(options);
                return GetBinaryIndex(variant);
            }

            public int GetBinaryIndex(int variant)
                => BaseIndex + variant * StageCount;

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

            public bool HasGeometryShader() => (Kind & 4) != 0;
            public bool HasPixelShader() => (Kind & 2) != 0;
            public bool HasVertexShader() => (Kind & 1) != 0;
        }

        public class VariationMacro
        {
            public string Name { get; set; }
            public List<string> Values { get; set; } = new List<string>();
            public byte[] Data { get; set; }
        }
        public class Symbol
        {
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
        }

        public Header FileHeader;
        public List<ShaderProgram> Programs = new();
        public List<ShaderBinary> Binaries = new();
        public string Name;

        public bool IsSwitch = false;

        public SharcfbFileWiiU(string filePath) {
            using (var reader = new BinaryDataReader(File.OpenRead(filePath)))
                SharcfbV1Reader.Read(this, reader);
        }

        public void Save(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var wr = new BinaryDataWriter(fs))
                SharcfbV1Writer.Write(this, wr);
        }

        public uint GetVersion()
        {
            return this.FileHeader.Version;
        }

        public enum GX2ShaderType
        {
            Vertex,
            Pixel,
            Geometry,
        }
    }
}