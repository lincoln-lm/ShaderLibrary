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

        static ShaderSource ReadShaderSource(BinaryDataReader reader, SharcFile sharc)
        {
            ShaderSource src = new();
            uint nameLength = reader.ReadUInt32();
            uint codeLength = reader.ReadUInt32();
            uint codeLength2 = reader.ReadUInt32();
            src.Name = reader.ReadFixedString((int)nameLength);
            src.Data = reader.ReadBytes((int)codeLength);
            return src;
        }

        static ShaderProgram ReadShaderProgram(BinaryDataReader reader, SharcFile sharc)
        {
            ShaderProgram program = new();
            uint nameLength = reader.ReadUInt32();


            if (sharc.FileHeader.Version >= 13)
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
            program.VertexMacros = ReadSection(reader, sharc, ReadMacroDefinesList);
            program.FragmentMacros = ReadSection(reader, sharc, ReadMacroDefinesList);
            program.GeometryMacros = ReadSection(reader, sharc, ReadMacroDefinesList);
            if (sharc.FileHeader.Version >= 13)
                program.ComputeMacros = ReadSection(reader, sharc, ReadMacroDefinesList);

            program.VariationMacros = ReadSection(reader, sharc, ReadMacroList);
            program.VariationDefaults = ReadSection(reader, sharc, ReadMacroList);
            if (sharc.FileHeader.Version >= 13)
            {
                program.UniformBlocksV13 = ReadSection(reader, sharc, ReadBlockList);
            }
            else
            {
                program.Uniforms = ReadSection(reader, sharc, ReadSymbolList);
                program.UniformBlocks = ReadSection(reader, sharc, ReadSymbolList);
                program.Samplers = ReadSection(reader, sharc, ReadSymbolList);
                program.Attributes = ReadSection(reader, sharc, ReadSymbolList);
            }
            return program;
        }

        static List<MacroDefine> ReadMacroDefinesList(BinaryDataReader reader, SharcFile sharc)
        {
            List<MacroDefine> macroList = new();
            uint count = reader.ReadUInt32();
            for (int i = 0; i < count; i++)
                macroList.Add(ReadSection(reader, sharc, ReadMacroDefine));
            return macroList;
        }

        static MacroDefine ReadMacroDefine(BinaryDataReader reader, SharcFile sharc)
        {
            MacroDefine var = new();
            uint nameLength = reader.ReadUInt32();
            uint valueLength = reader.ReadUInt32();
            var.Name = reader.ReadFixedString((int)nameLength);
            var.Value = reader.ReadFixedString((int)valueLength);
            return var;
        }

        static List<VariationMacro> ReadMacroList(BinaryDataReader reader, SharcFile sharc)
        {
            List<VariationMacro> macroList = new();
            uint count = reader.ReadUInt32();
            for (int i = 0; i < count; i++)
                macroList.Add(ReadSection(reader, sharc, ReadMacro));
            return macroList;
        }

        static VariationMacro ReadMacro(BinaryDataReader reader, SharcFile sharc)
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

        static List<Symbol> ReadSymbolList(BinaryDataReader reader, SharcFile sharc)
        {
            List<Symbol> macroList = new();
            uint count = reader.ReadUInt32();
            for (int i = 0; i < count; i++)
                macroList.Add(ReadSection(reader, sharc, ReadSymbol));
            return macroList;
        }

        static List<SymbolUniformBlock> ReadBlockList(BinaryDataReader reader, SharcFile sharc)
        {
            List<SymbolUniformBlock> macroList = new();
            uint count = reader.ReadUInt32();
            for (int i = 0; i < count; i++)
                macroList.Add(ReadSection(reader, sharc, ReadBlockSymbol));
            for (int i = 0; i < count; i++)
                macroList[i].Location = i;

            return macroList;
        }

        static SymbolUniformBlock ReadBlockSymbol(BinaryDataReader reader, SharcFile sharc)
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

        static Symbol ReadSymbol(BinaryDataReader reader, SharcFile sharc)
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

        static T ReadSection<T>(BinaryDataReader reader, SharcFile sharc, Func<BinaryDataReader, SharcFile, T> section)
        {
            long start = reader.Position;
            var sectionSize = reader.ReadUInt32();
            T value = section.Invoke(reader, sharc);

            reader.SeekBegin(start + sectionSize);
            return value;
        }

        static void ReadSection(BinaryDataReader reader, SharcFile sharc, Action<BinaryDataReader, SharcFile> section)
        {
            long start = reader.Position;
            var sectionSize = reader.ReadUInt32();
            section.Invoke(reader, sharc);

            reader.SeekBegin(start + sectionSize);
        }
    }
}
