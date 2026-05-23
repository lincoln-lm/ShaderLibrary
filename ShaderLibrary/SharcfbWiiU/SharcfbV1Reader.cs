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

            ReadSection(reader, sharc, (r, s) =>
            {
                uint binaryCount = reader.ReadUInt32();
                for (int i = 0; i < binaryCount; i++)
                    sharc.Binaries.Add(ReadSection(reader, sharc, ReadShaderBinary));
            });
            ReadSection(reader, sharc, (r, s) =>
            {
                uint programCount = reader.ReadUInt32();
                for (int i = 0; i < programCount; i++)
                    sharc.Programs.Add(ReadSection(reader, sharc, ReadShaderProgram));
            });
            Console.WriteLine("");
        }

        static ShaderBinary ReadShaderBinary(BinaryDataReader reader, SharcfbFileWiiU sharc)
        {
            ShaderBinary bin = new();
            bin.Type = (GX2ShaderType)reader.ReadUInt32();
            uint offset = reader.ReadUInt32();
            Trace.Assert(offset == 0);

            uint size = reader.ReadUInt32();
            bin.Data = reader.ReadBytes((int)size);
            return bin;
        }

        static ShaderProgram ReadShaderProgram(BinaryDataReader reader, SharcfbFileWiiU sharc)
        {
            ShaderProgram program = new();
            uint nameLength = reader.ReadUInt32();
            program.Kind = reader.ReadUInt32();
            program.BaseIndex = reader.ReadInt32();
            program.Name = reader.ReadFixedString((int)nameLength);
            program.VariationMacros = ReadSection(reader, sharc, ReadMacroList);
            program.VariationDefaults = ReadSection(reader, sharc, ReadMacroList);
            program.Uniforms = ReadSection(reader, sharc, ReadSymbolList);
            if (sharc.FileHeader.Version >= 9)
            {
                uint size = reader.ReadUInt32();
                reader.SeekBegin(reader.Position - 4 + size);
            }
            program.UniformBlocks = ReadSection(reader, sharc, ReadSymbolList);
            program.Samplers = ReadSection(reader, sharc, ReadSymbolList);
            program.Attributes = ReadSection(reader, sharc, ReadSymbolList);
            return program;
        }

        static List<VariationMacro> ReadMacroList(BinaryDataReader reader, SharcfbFileWiiU sharc)
        {
            List<VariationMacro> macroList = new();
            uint count = reader.ReadUInt32();
            for (int i = 0; i < count; i++)
                macroList.Add(ReadSection(reader, sharc, ReadMacro));
            return macroList;
        }

        static VariationMacro ReadMacro(BinaryDataReader reader, SharcfbFileWiiU sharc)
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

        static List<Symbol> ReadSymbolList(BinaryDataReader reader, SharcfbFileWiiU sharc)
        {
            List<Symbol> macroList = new();
            uint count = reader.ReadUInt32();
            for (int i = 0; i < count; i++)
                macroList.Add(ReadSection(reader, sharc, ReadSymbol));
            return macroList;
        }

        static Symbol ReadSymbol(BinaryDataReader reader, SharcfbFileWiiU sharc)
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

        static T ReadSection<T>(BinaryDataReader reader, SharcfbFileWiiU sharc, Func<BinaryDataReader, SharcfbFileWiiU, T> section)
        {
            long start = reader.Position;
            var sectionSize = reader.ReadUInt32();
            T value = section.Invoke(reader, sharc);

            reader.SeekBegin(start + sectionSize);
            return value;
        }

        static void ReadSection(BinaryDataReader reader, SharcfbFileWiiU sharc, Action<BinaryDataReader, SharcfbFileWiiU> section)
        {
            long start = reader.Position;
            var sectionSize = reader.ReadUInt32();
            section.Invoke(reader, sharc);

            reader.SeekBegin(start + sectionSize);
        }
    }
}
