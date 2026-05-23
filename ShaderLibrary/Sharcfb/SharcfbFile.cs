using ShaderLibrary.IO;
using ShaderLibrary.Sharc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ShaderLibrary
{
    public class SharcfbFile : ISharcFile
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class Header
        {
            public uint Magic = 1397244226; // "BAHS";
            public uint Version = 9;
            public uint FileSize;
            public uint Unknown = 5;
            public uint ByteOrder; //0 or 1 for big endian
            public uint Alignment = 4096;
            public uint ShaderDataSize;
            public uint ShaderDataOffset;
        }

        public class ShaderVariation
        {
            public ShaderType Type;

            public byte[] ByteCode;
            public byte[] ControlShader;

            public List<Symbol> Attributes = new List<Symbol>();
            public List<Symbol> Samplers = new List<Symbol>();
            public List<Symbol> Buffers = new List<Symbol>();
            public List<Symbol> Uniforms = new List<Symbol>();
            public List<SymbolUniformBlock> UniformBlocks = new();
        }

        public class ShaderProgram
        {
            public int BaseIndex;
            public int StageCount = 3;
            public string Name;

            public List<SharcFile.VariationMacro> Macros = new();

            public int GetVariationIndex(Dictionary<string, string> options)
            {
                int index = 0;
                foreach (var variation in Macros)
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

            public int GetBinaryIndex(int variation)
                => BaseIndex + variation* StageCount;

            public IEnumerable<Dictionary<string, string>> GetAllVariationCombinations()
            {
                var result = new List<Dictionary<string, string>>();

                void Recurse(int index, Dictionary<string, string> current)
                {
                    if (index >= Macros.Count)
                    {
                        result.Add(new Dictionary<string, string>(current));
                        return;
                    }
                    var variation = Macros[index];
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
            public string Name;
            public int Location;
        }
        public class SymbolUniformBlock
        {
            public string Name;
            public int Location;
            public uint Size;
        }

        public Header FileHeader;
        public List<ShaderVariation> Variations = new();
        public List<ShaderProgram> Programs = new();
        public string Name;

        public bool IsSwitch = false;

        public static bool Identify(Stream stream)
        {
            using (var reader = new BinaryDataReader(stream, false, true)) {
                using (reader.BaseStream.TemporarySeek(0, SeekOrigin.Begin)) {
                    string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    return magic == "BAHS";
                }
            }
        }

        public SharcfbFile() {
            FileHeader = new Header()
            {

            };
        }
        public SharcfbFile(string filePath)
        {
            using (var reader = new BinaryDataReader(File.OpenRead(filePath)))
                SharcfbV2Reader.Read(this, reader);
        }

        public SharcfbFile(Stream stream)
        {
            using (var reader = new BinaryDataReader(stream))
                SharcfbV2Reader.Read(this, reader);
        }

        public void Save(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                Save(fs);
        }
        public void Save(Stream stream)
        {
            using (var wr = new BinaryDataWriter(stream))
            {
                if (IsSwitch)
                    SharcfbV2Writer.Write(this, wr);
                else
                    SharcfbV2Writer.Write(this, wr);
            }
        }

        static bool CheckIsSwitch(BinaryDataReader reader)
        {
            reader.SeekBegin(20);
            //check if this area is a valid alignment value
            //Other versions do not use this value
            bool isAlignment = reader.ReadUInt32() >= 2048;
            reader.BaseStream.Position = 0;
            return isAlignment;
        }

        public uint GetVersion()
        {
            return this.FileHeader.Version;
        }

        public enum ShaderType
        {
            Vertex,
            Pixel,
            Geometry,
        }
    }
}