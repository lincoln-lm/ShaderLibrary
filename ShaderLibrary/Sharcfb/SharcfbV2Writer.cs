using ShaderLibrary.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ShaderLibrary.SharcfbFile;

namespace ShaderLibrary.Sharc
{
    internal class SharcfbV2Writer
    {
        private const uint STRING_TABLE_OFFSET = 0x150;

        private static Dictionary<string, List<long>> _savedStrings = new();
        private static Dictionary<byte[], long> _savedControlCodes = new(new ByteArrayComparer());
        private static Dictionary<byte[], List<long>> _savedByteCodes = new(new ByteArrayComparer());

        public static void Write(SharcfbFile sharc, BinaryDataWriter writer)
        {
            _savedStrings.Add(sharc.Name, new List<long>());

            writer.WriteStruct(sharc.FileHeader);
            // For string table at the end
            writer.Seek((int)(STRING_TABLE_OFFSET + sharc.FileHeader.Alignment), SeekOrigin.Begin);

            // variation list
            WriteSection(writer, sharc, (wr, s) =>
            {
                wr.Write(sharc.Variations.Count);
                foreach (var program in sharc.Variations)
                    WriteSection(program, writer, sharc, WriteVariation);
            });
            // programs list
            WriteSection(writer, sharc, (wr, s) =>
            {
                wr.Write(sharc.Programs.Count);
                foreach (var program in sharc.Programs)
                    WriteSection(program, writer, sharc, WriteProgram, 0);
            }, 0);

            // Raw data
            writer.AlignBytes((int)sharc.FileHeader.Alignment);

            var start = writer.Position;
            sharc.FileHeader.ShaderDataOffset = (uint)start;

            foreach (var shader in _savedByteCodes)
            {
                writer.AlignBytes((int)128);

                var value = writer.Position - start;
                writer.Write(shader.Key);
                foreach (var ofs in shader.Value)
                {
                    using (writer.BaseStream.TemporarySeek(ofs, SeekOrigin.Begin))
                        writer.Write((int)value);
                }
            }
            writer.AlignBytes((int)sharc.FileHeader.Alignment);

            sharc.FileHeader.ShaderDataSize = (uint)(writer.Position - start);
            sharc.FileHeader.FileSize = (uint)writer.BaseStream.Length;

            // string table
            writer.Seek((int)STRING_TABLE_OFFSET, SeekOrigin.Begin);
            foreach (var str in _savedStrings)
            {
                var value = writer.Position - STRING_TABLE_OFFSET;

                writer.Write(Encoding.UTF8.GetBytes(str.Key));
                writer.Write((byte)0);

                foreach (var ofs in str.Value)
                {
                    using (writer.BaseStream.TemporarySeek(ofs, SeekOrigin.Begin))
                        writer.Write((int)value);
                }
            }

            writer.Seek(0, SeekOrigin.Begin);
            writer.WriteStruct(sharc.FileHeader);
        }

        static void WriteVariation(SharcfbFile.ShaderVariation variation, BinaryDataWriter wr, SharcfbFile sharc)
        {
            // header
            wr.Write((uint)variation.Type);
            wr.Write((uint)0);

            wr.Write((uint)0); // size set later

            // header data
            var controlShader = variation.ControlShader;
            var start = wr.Position;

            if (!_savedByteCodes.ContainsKey(variation.ByteCode))
                _savedByteCodes.Add(variation.ByteCode, new List<long>());

            _savedByteCodes[variation.ByteCode].Add(wr.Position);

            wr.Write((ulong)0); // byte code offset relative to shader data
            wr.Write((uint)controlShader.Length);
            wr.Write((ulong)0); // control shader offset

            wr.Write(variation.UniformBlocks.Count);
            var uniformBlockOffset = wr.SaveOffset();
            wr.Write(variation.Buffers.Count);
            var bufferOffset = wr.SaveOffset();
            wr.Write(variation.Attributes.Count);
            var attributeOffset = wr.SaveOffset();
            wr.Write(variation.Uniforms.Count);
            var uniformOffset = wr.SaveOffset();
            wr.Write(variation.Samplers.Count);
            var samplerOffset = wr.SaveOffset();

            wr.Write((ulong)0);
            wr.Write((uint)0);

            if (!_savedControlCodes.ContainsKey(controlShader))
            {
                wr.Write((uint)0); // aligns by 8

                WriteRelativeOffset(wr, start + 12, start);
                _savedControlCodes.Add(controlShader, wr.Position);
                wr.Write(controlShader);
            }
            else
                WriteRelativeOffset(wr, _savedControlCodes[controlShader], start + 12, start);

            if (variation.UniformBlocks.Count > 0)
            {
                WriteRelativeOffset(wr, uniformBlockOffset, start);
                foreach (var symbol in variation.UniformBlocks)
                {
                    SaveString(wr, symbol.Name);
                    wr.Write(symbol.Location);
                    wr.Write(symbol.Size);
                }
            }

            if (variation.Attributes.Count > 0)
            {
                WriteRelativeOffset(wr, attributeOffset, start);
                foreach (var symbol in variation.Attributes)
                {
                    SaveString(wr, symbol.Name);
                    wr.Write(symbol.Location);
                }
            }
            if (variation.Buffers.Count > 0)
            {
                WriteRelativeOffset(wr, bufferOffset, start);
                foreach (var symbol in variation.Buffers)
                {
                    SaveString(wr, symbol.Name);
                    wr.Write(symbol.Location);
                }
            }

            if (variation.Uniforms.Count > 0)
            {
                WriteRelativeOffset(wr, uniformOffset, start);
                foreach (var symbol in variation.Uniforms)
                {
                    SaveString(wr, symbol.Name);
                    wr.Write(symbol.Location);
                }
            }
            if (variation.Samplers.Count > 0)
            {
                WriteRelativeOffset(wr, samplerOffset, start);
                foreach (var symbol in variation.Samplers)
                {
                    SaveString(wr, symbol.Name);
                    wr.Write(symbol.Location);
                }
            }

            wr.AlignBytes(8);

            var size = wr.Position - start;
            using (wr.BaseStream.TemporarySeek(start - 4, SeekOrigin.Begin)) {
                wr.Write((uint)size);
            }
        }

        static void WriteProgram(SharcfbFile.ShaderProgram program, BinaryDataWriter writer, SharcfbFile sharc)
        {
            writer.Write((uint)(program.Name.Length + 1));
            writer.Write((uint)program.StageCount);
            writer.Write(program.BaseIndex);
            writer.Write(Encoding.UTF8.GetBytes(program.Name));
            writer.Write((byte)0);

            // macro list
            WriteSection(writer, sharc, (wr, s) =>
            {
                wr.Write(program.Macros.Count);
                foreach (var macro in program.Macros)
                    WriteSection(macro, writer, sharc, WriteMacro, 0);
            }, 0);
            // Unused lists
            WriteSection(writer, sharc, (wr, s) =>
            {
                wr.Write(0);
            }, 0);
            WriteSection(writer, sharc, (wr, s) =>
            {
                wr.Write(0);
            }, 0);
        }

        static void WriteMacro(SharcfbFile.VariationMacro macro, BinaryDataWriter writer, SharcfbFile sharc)
        {
            writer.Write((uint)(macro.Name.Length + 1));
            writer.Write((uint)macro.Values.Count);
            writer.Write((uint)macro.Data.Length);
            writer.Write(Encoding.UTF8.GetBytes(macro.Name));
            writer.Write((byte)0);

            foreach (var v in macro.Values)
            {
                writer.Write(Encoding.UTF8.GetBytes(v));
                writer.Write((byte)0);
            }
            writer.Write(macro.Data);
        }

        static void WriteSection(BinaryDataWriter writer, SharcfbFile sharc, Action<BinaryDataWriter, SharcfbFile> section, int alignment = 4)
        {
            var start = writer.Position;
            writer.Write(0); // size set later
            section.Invoke(writer, sharc);
            if (alignment != 0)
                writer.AlignBytes(alignment);
            var end = writer.Position;
            using (writer.BaseStream.TemporarySeek(start, SeekOrigin.Begin))
            {
                writer.Write((uint)(end - start));
            }
        }

        static void WriteSection<T>(T value, BinaryDataWriter writer, SharcfbFile sharc, Action<T, BinaryDataWriter, SharcfbFile> section, int alignment = 4)
        {
            var start = writer.Position;
            writer.Write(0); // size set later
            section.Invoke(value, writer, sharc);

            if (alignment != 0)
                writer.AlignBytes(alignment);
            var end = writer.Position;
            using (writer.BaseStream.TemporarySeek(start, SeekOrigin.Begin))
            {
                writer.Write((uint)(end - start));
            }
        }


        static void WriteRelativeOffset(BinaryDataWriter writer, long target, long start)
        {
            var offset = writer.Position;
            WriteRelativeOffset(writer, offset, target, start);
        }

        static void WriteRelativeOffset(BinaryDataWriter writer, long offset, long target, long start)
        {
            var target_relative = offset - start;
            //Seek to where to write the offset itself and use relative position
            using (writer.BaseStream.TemporarySeek(target, SeekOrigin.Begin))
            {
                writer.Write(((int)target_relative));
            }
        }


        static void SaveString(BinaryDataWriter writer, string str)
        {
            if (!_savedStrings.ContainsKey(str))
                _savedStrings.Add(str, new List<long>());
            _savedStrings[str].Add(writer.Position);

            writer.Write(0UL);
        }

        class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] left, byte[] right)
            {
                if (left == null || right == null)
                {
                    return left == right;
                }
                return left.SequenceEqual(right);
            }
            public int GetHashCode(byte[] key)
            {
                if (key == null)
                    throw new ArgumentNullException("key");
                return key.Sum(b => b);
            }
        }
    }
}
