using ShaderLibrary.IO;
using System;
using System.Diagnostics;
using System.Xml.Linq;
using static ShaderLibrary.SharcfbFileWiiU;

namespace ShaderLibrary.Sharc
{
    internal class SharcfbV1Reader
    {
        public static void Read(SharcfbFileWiiU sharc, BinaryDataReader reader)
        {
            sharc.FileHeader = reader.ReadStruct<Header>();

            uint nameLength = reader.ReadUInt32();
            sharc.Name = reader.ReadFixedString((int)nameLength);

            SharcReader.ReadSection(reader, sharc, (r, s) =>
            {
                uint binaryCount = reader.ReadUInt32();
                for (int i = 0; i < binaryCount; i++)
                    sharc.Binaries.Add(SharcReader.ReadSection(reader, sharc, ReadShaderBinary));
            });
            SharcReader.ReadSection(reader, sharc, (r, s) =>
            {
                uint programCount = reader.ReadUInt32();
                for (int i = 0; i < programCount; i++)
                    sharc.Programs.Add(SharcReader.ReadSection(reader, sharc, ReadShaderProgram));
            });
        }

        static ShaderBinary ReadShaderBinary(BinaryDataReader reader, ISharcFile sharc)
        {
            ShaderBinary bin = new();
            bin.Type = (GX2ShaderType)reader.ReadUInt32();
            uint offset = reader.ReadUInt32();
           // Trace.Assert(offset == 0);

            uint size = reader.ReadUInt32();
            bin.Data = reader.ReadBytes((int)size);
            return bin;
        }

        static ShaderProgram ReadShaderProgram(BinaryDataReader reader, ISharcFile sharc)
        {
            ShaderProgram program = new();
            uint nameLength = reader.ReadUInt32();
            program.Kind = reader.ReadUInt32();
            program.BaseIndex = reader.ReadInt32();
            program.Name = reader.ReadFixedString((int)nameLength);
            program.VariationMacros = SharcReader.ReadSectionList(reader, sharc, SharcReader.ReadVariationMacro);
            program.VariationDefaults = SharcReader.ReadSectionList(reader, sharc, SharcReader.ReadVariationMacro);
            program.Uniforms = SharcReader.ReadSectionList(reader, sharc, ReadSymbol);
            if (sharc.GetVersion() >= 9)
            {
                uint size = reader.ReadUInt32();
                reader.SeekBegin(reader.Position - 4 + size);
            }
            program.UniformBlocks = SharcReader.ReadSectionList(reader, sharc, ReadSymbol);
            program.Samplers = SharcReader.ReadSectionList(reader, sharc, ReadSymbol);
            program.Attributes = SharcReader.ReadSectionList(reader, sharc, ReadSymbol);
            return program;
        }

        static Symbol ReadSymbol(BinaryDataReader reader, ISharcFile sharc)
        {
            Symbol var = new();
            var.Size = reader.ReadUInt32();
            uint variableNameLength = reader.ReadUInt32();
            uint symbolNameLength = reader.ReadUInt32();
            uint defaultSize = reader.ReadUInt32();
            uint numVariations = reader.ReadUInt32();

            var.Name = reader.ReadFixedString((int)variableNameLength);
            var.SymbolName = reader.ReadFixedString((int)symbolNameLength);
            var.DefaultData = reader.ReadBytes((int)defaultSize);
            var.UsedVariants = reader.ReadBytes((int)numVariations);
            return var;
        }
    }
}
