using ShaderLibrary.IO;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Xml.Linq;
using static ShaderLibrary.SharcFile;

namespace ShaderLibrary.Sharc
{
    internal class SharcReader
    {
        public static void Read(SharcFile sharc, BinaryDataReader reader)
        {
            sharc.FileHeader = reader.ReadStruct<Header>();

            uint nameLength = reader.ReadUInt32();
            sharc.Name = reader.ReadFixedString((int)nameLength);
            ReadSection(reader, sharc, (r, s) =>
            {
                uint programCount = reader.ReadUInt32();
                for (int i = 0; i < programCount; i++)
                    sharc.Programs.Add(ReadSection(reader, sharc, ReadShaderProgram));
            });
            ReadSection(reader, sharc, (r, s) =>
            {
                uint sourceCount = reader.ReadUInt32();
                for (int i = 0; i < sourceCount; i++)
                    sharc.Sources.Add(ReadSection(reader, sharc, ReadShaderSource));
            });
        }

        static ShaderSource ReadShaderSource(BinaryDataReader reader, ISharcFile sharc)
        {
            ShaderSource src = new();
            uint nameLength = reader.ReadUInt32();
            uint codeLength = reader.ReadUInt32();
            uint codeLength2 = reader.ReadUInt32();
            src.Name = reader.ReadFixedString((int)nameLength);
            src.Data = reader.ReadBytes((int)codeLength);
            return src;
        }

        static ShaderProgram ReadShaderProgram(BinaryDataReader reader, ISharcFile sharc)
        {
            ShaderProgram program = new();
            uint nameLength = reader.ReadUInt32();


            if (sharc.GetVersion() >= 13)
            {
                program.VertexShaderIndex = reader.ReadInt16();
                program.ComputeShaderIndex = reader.ReadInt16();
                program.FragmentShaderIndex = reader.ReadInt16();
                program.GeoemetryShaderIndex = reader.ReadInt16();
                reader.ReadInt16();
                reader.ReadInt16();
                reader.ReadInt16();
                reader.ReadInt16();

                // Trace.Assert(reader.ReadInt16() == -1);
                //Trace.Assert(reader.ReadInt16() == -1);
                //Trace.Assert(reader.ReadInt16() == -1);
                //Trace.Assert(reader.ReadInt16() == -1);
            }
            else
            {
                program.VertexShaderIndex = reader.ReadInt32();
                program.FragmentShaderIndex = reader.ReadInt32();
                program.GeoemetryShaderIndex = reader.ReadInt32();
            }

            program.Name = reader.ReadFixedString((int)nameLength);
            program.VertexMacros = ReadSectionList(reader, sharc, ReadMacroDefine);
            program.FragmentMacros = ReadSectionList(reader, sharc, ReadMacroDefine);
            program.GeometryMacros = ReadSectionList(reader, sharc, ReadMacroDefine);
            if (sharc.GetVersion() >= 13)
                program.ComputeMacros = ReadSectionList(reader, sharc, ReadMacroDefine);

            program.VariationMacros = ReadSectionList(reader, sharc, ReadVariationMacro);
            program.VariationDefaults = ReadSectionList(reader, sharc, ReadVariationMacro);
            if (sharc.GetVersion() >= 13)
            {
                program.UniformBlocksV13 = ReadSectionList(reader, sharc, ReadBlockSymbol);
            }
            else
            {
                program.Uniforms = ReadSectionList(reader, sharc, ReadSymbol);
                program.UniformBlocks = ReadSectionList(reader, sharc, ReadSymbol);
                program.Samplers = ReadSectionList(reader, sharc, ReadSymbol);
                program.Attributes = ReadSectionList(reader, sharc, ReadSymbol);
            }
            return program;
        }

        static MacroDefine ReadMacroDefine(BinaryDataReader reader, ISharcFile sharc)
        {
            MacroDefine var = new();
            uint nameLength = reader.ReadUInt32();
            uint valueLength = reader.ReadUInt32();
            var.Name = reader.ReadFixedString((int)nameLength);
            var.Value = reader.ReadFixedString((int)valueLength);
            return var;
        }

        public static VariationMacro ReadVariationMacro(BinaryDataReader reader, ISharcFile sharc)
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

        static SymbolUniformBlock ReadBlockSymbol(BinaryDataReader reader, ISharcFile sharc)
        {
            SymbolUniformBlock var = new();
            var.Size = reader.ReadUInt32();
            var.Name = reader.ReadFixedString((int)reader.ReadUInt32());
            uint subSectionSize = reader.ReadUInt32();
            uint uniformCount = reader.ReadUInt32();
            for (int i = 0; i < uniformCount; i++)
            {
                ReadSection(reader, sharc, (wr, s) =>
                {
                    Symbol uniform = new();
                    uniform.Offset = reader.ReadUInt32();
                    uniform.Name = reader.ReadFixedString((int)reader.ReadUInt32());
                    var.Uniforms.Add(uniform);
                });
            }
            return var;
        }

        static Symbol ReadSymbol(BinaryDataReader reader, ISharcFile sharc)
        {
            Symbol var = new();
            var.Offset = reader.ReadUInt32();
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

        public static List<T> ReadSectionList<T>(BinaryDataReader reader, ISharcFile sharc, Func<BinaryDataReader, ISharcFile, T> sectionReader)
        {
            var list = new List<T>();

            long start = reader.Position;
            var sectionSize = reader.ReadUInt32();
            uint count = reader.ReadUInt32();
            for (int i = 0; i < count; i++)
                list.Add(ReadSection(reader, sharc, sectionReader));

            reader.SeekBegin(start + sectionSize);
            return list;
        }

        public static T ReadSection<T>(BinaryDataReader reader, ISharcFile sharc, Func<BinaryDataReader, ISharcFile, T> sectionReader)
        {
            long start = reader.Position;
            var sectionSize = reader.ReadUInt32();
            T value = sectionReader.Invoke(reader, sharc);

            reader.SeekBegin(start + sectionSize);
            return value;
        }

        public static void ReadSection(BinaryDataReader reader, ISharcFile sharc, Action<BinaryDataReader, ISharcFile> section)
        {
            long start = reader.Position;
            var sectionSize = reader.ReadUInt32();
            section.Invoke(reader, sharc);

            reader.SeekBegin(start + sectionSize);
        }
    }
}
