using ShaderLibrary.IO;
using System;
using static ShaderLibrary.SharcfbFile;

namespace ShaderLibrary.Sharc
{
    internal class SharcfbV2Reader
    {
        private const uint STRING_TABLE_OFFSET = 0x150;

        public static void Read(SharcfbFile sharc, BinaryDataReader reader)
        {
            sharc.FileHeader = reader.ReadStruct<Header>();

            reader.SeekBegin(STRING_TABLE_OFFSET);
            sharc.Name = reader.ReadZeroTerminatedString();

            reader.SeekBegin(STRING_TABLE_OFFSET + sharc.FileHeader.Alignment);
            long startPos = reader.Position;

            uint shaderVariationSectionSize = reader.ReadUInt32();
            uint shaderVariationCount = reader.ReadUInt32();

            for (int i = 0; i < shaderVariationCount; i++)
                sharc.Variations.Add(ReadSection(reader, sharc, ReadVariation));

            reader.SeekBegin(startPos + shaderVariationSectionSize);
            uint shaderProgramSectionSize = reader.ReadUInt32();
            uint shaderProgramCount = reader.ReadUInt32();

            for (int i = 0; i < shaderProgramCount; i++)
                sharc.Programs.Add(ReadSection(reader, sharc, ReadProgram));
        }

        static ShaderVariation ReadVariation(BinaryDataReader reader, SharcfbFile sharc)
        {
            ShaderVariation variation = new();

            variation.Type = (ShaderType)reader.ReadUInt32();
            reader.ReadUInt32(); //0

            uint sectionSize = reader.ReadUInt32();
            var pos = reader.Position;

            long byteCodeOffset = ReadOffset(reader);
            uint controlCodeSize = reader.ReadUInt32();
            long controlCodeOffset = ReadOffset(reader);
            uint numUniformBlocks = reader.ReadUInt32();
            var uniformBlockOffset = ReadOffset(reader);

            uint numBuffers = 0;
            long bufferOffset = 0;

            numBuffers = reader.ReadUInt32();
            bufferOffset = ReadOffset(reader);

            uint numAttributes = reader.ReadUInt32();
            var attributeOffset = ReadOffset(reader);
            uint numUniforms = reader.ReadUInt32();
            var uniformOffset = ReadOffset(reader);
            uint numSamplers = reader.ReadUInt32();
            var samplerOffset = ReadOffset(reader);

            reader.SeekBegin(pos + uniformBlockOffset);
            for (int i = 0; i < numUniformBlocks; i++)
            {
                variation.UniformBlocks.Add(new SymbolUniformBlock()
                {
                    Name = ReadString(reader),
                    Location = reader.ReadInt32(),
                    Size = reader.ReadUInt32(),
                });
            }

            reader.SeekBegin(pos + attributeOffset);
            for (int i = 0; i < numAttributes; i++)
            {
                variation.Attributes.Add(new Symbol()
                {
                    Name = ReadString(reader),
                    Location = reader.ReadInt32(),
                });
            }

            reader.SeekBegin((long)(pos + bufferOffset));
            for (int i = 0; i < numBuffers; i++)
            {
                variation.Buffers.Add(new Symbol()
                {
                    Name = ReadString(reader),
                    Location = reader.ReadInt32(),
                });
            }

            reader.SeekBegin(pos + uniformOffset);
            for (int i = 0; i < numUniforms; i++)
            {
                variation.Uniforms.Add(new Symbol()
                {
                    Name = ReadString(reader),
                    Location = reader.ReadInt32(),
                });
            }

            reader.SeekBegin(pos + samplerOffset);
            for (int i = 0; i < numSamplers; i++)
            {
                variation.Samplers.Add(new Symbol()
                {
                    Name = ReadString(reader),
                    Location = reader.ReadInt32(),
                });
            }

            reader.SeekBegin(pos + controlCodeOffset);
            var bytes = reader.ReadBytes((int)controlCodeSize);
            var controlShader = new ControlShader(bytes);

            variation.ControlShader = bytes;

            reader.SeekBegin(sharc.FileHeader.ShaderDataOffset + byteCodeOffset);
            variation.ByteCode = reader.ReadBytes((int)controlShader.GetByteCodeSize());

            return variation;
        }

        static ShaderProgram ReadProgram(BinaryDataReader reader, SharcfbFile sharc)
        {
            ShaderProgram program = new();
            uint NameLength = reader.ReadUInt32();
            program.StageCount = reader.ReadInt32(); //3
            program.BaseIndex = reader.ReadInt32();
            program.Name = reader.ReadFixedString((int)NameLength);
            program.Macros = ReadSection(reader, sharc, ReadMacroList);
            return program;
        }

        static List<VariationMacro> ReadMacroList(BinaryDataReader reader, SharcfbFile sharc)
        {
            List<VariationMacro> macroList = new ();
            uint count = reader.ReadUInt32();
            for (int i = 0; i < count; i++)
                macroList.Add(ReadSection(reader, sharc, ReadMacro));
            return macroList;
        }

        static VariationMacro ReadMacro(BinaryDataReader reader, SharcfbFile sharc)
        {
            VariationMacro var = new();
            uint nameLength = reader.ReadUInt32();
            uint valueCount = reader.ReadUInt32();
            uint dataLength = reader.ReadUInt32();

            var.Name = reader.ReadFixedString((int)nameLength);
            for (int i = 0; i < valueCount; i++)
                var.Values.Add(reader.ReadZeroTerminatedString());
            var.Data = reader.ReadBytes((int)dataLength); // 0
            return var;
        }

        static T ReadSection<T>(BinaryDataReader reader, SharcfbFile sharc, Func<BinaryDataReader, SharcfbFile, T> section)
        {
            long start = reader.Position;
            var sectionSize = reader.ReadUInt32();
            T value = section.Invoke(reader, sharc);

            reader.SeekBegin(start + sectionSize);
            return value;
        }

        static string ReadString(BinaryDataReader reader)
        {
            var offset = reader.ReadUInt64();
            using (reader.BaseStream.TemporarySeek(STRING_TABLE_OFFSET + (uint)offset, SeekOrigin.Begin)) {
                return reader.ReadZeroTerminatedString();
            }
        }
        static long ReadOffset(BinaryDataReader reader)
        {
            var offset = reader.ReadInt32();
            reader.ReadInt32();
            return offset;
        }
    }
}
