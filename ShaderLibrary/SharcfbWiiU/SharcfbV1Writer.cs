using ShaderLibrary.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ShaderLibrary.SharcWiiU
{
    internal class SharcfbV1Writer
    {
        public static void Write(SharcfbFileWiiU sharc, BinaryDataWriter writer)
        {
            writer.WriteStruct(sharc.FileHeader);
            writer.Write((uint)(sharc.Name.Length + 1));
            writer.Write(Encoding.UTF8.GetBytes(sharc.Name));
            writer.Write((byte)0);

            WriteSection(writer, sharc, (wr, s) =>
            {
                wr.Write(sharc.Binaries.Count);
                foreach (var binary in sharc.Binaries)
                    WriteSection(binary, wr, sharc, WriteShaderBinary);
            });
            WriteSection(writer, sharc, (wr, s) =>
            {
                wr.Write(sharc.Programs.Count);
                foreach (var program in sharc.Programs)
                    WriteSection(program, wr, sharc, WriteShaderProgram);
            });
        }

        static void WriteShaderBinary(SharcfbFileWiiU.ShaderBinary binary, BinaryWriter writer, SharcfbFileWiiU sharc)
        {
            writer.Write((uint)binary.Type);
            writer.Write(0); // offset
            writer.Write((uint)binary.Data.Length);
            writer.Write(binary.Data);
        }
        static void WriteShaderProgram(SharcfbFileWiiU.ShaderProgram binary, BinaryDataWriter writer, SharcfbFileWiiU sharc)
        {
            writer.Write((uint)(binary.Name.Length + 1));
            writer.Write(binary.Kind);
            writer.Write(binary.BaseIndex);
            writer.Write(Encoding.UTF8.GetBytes(binary.Name));
            writer.Write((byte)0);

            WriteMacros(binary.VariationMacros, writer, sharc);
            WriteMacros(binary.VariationDefaults, writer, sharc);
            WriteSymbols(binary.Uniforms, writer, sharc);
            WriteSymbols(binary.UniformBlocks, writer, sharc);
            WriteSymbols(binary.Samplers, writer, sharc);
            WriteSymbols(binary.Attributes, writer, sharc);
        }

        static void WriteMacros(List<SharcfbFileWiiU.VariationMacro> macros, BinaryDataWriter writer, SharcfbFileWiiU sharc)
        {
            WriteSection(writer, sharc, (wr, s) =>
            {
                wr.Write(macros.Count);
                foreach (var macro in macros)
                    WriteSection(macro, wr, sharc, WriteMacro);
            });
        }

        static void WriteSymbols(List<SharcfbFileWiiU.Symbol> symbols, BinaryDataWriter writer, SharcfbFileWiiU sharc)
        {
            WriteSection(writer, sharc, (wr, s) =>
            {
                wr.Write(symbols.Count);
                foreach (var symbol in symbols)
                    WriteSection(symbol, wr, sharc, WriteSymbol);
            });
        }

        static void WriteMacro(SharcfbFileWiiU.VariationMacro macro, BinaryWriter writer, SharcfbFileWiiU sharc)
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
        static void WriteSymbol(SharcfbFileWiiU.Symbol symbol, BinaryWriter writer, SharcfbFileWiiU sharc)
        {
            writer.Write(symbol.Size);

            writer.Write((uint)(symbol.Name.Length + 1));
            writer.Write((uint)(symbol.SymbolName.Length + 1));
            writer.Write((uint)(symbol.DefaultData.Length));
            writer.Write((uint)(symbol.UsedVariants.Length));

            writer.Write(Encoding.UTF8.GetBytes(symbol.Name));
            writer.Write((byte)0);
            writer.Write(Encoding.UTF8.GetBytes(symbol.SymbolName));
            writer.Write((byte)0);
            writer.Write(symbol.DefaultData);
            writer.Write(symbol.UsedVariants);
        }

        static void WriteSection(BinaryDataWriter writer, SharcfbFileWiiU sharc, Action<BinaryDataWriter, SharcfbFileWiiU> section)
        { 
            var start = writer.Position;
            writer.Write(0); // size set later
            section.Invoke(writer, sharc);
            var end = writer.Position;
            using (writer.BaseStream.TemporarySeek(start, SeekOrigin.Begin))
            {
                writer.Write((uint)(end - start));
            }
        }

        static void WriteSection<T>(T value, BinaryDataWriter writer, SharcfbFileWiiU sharc, Action<T, BinaryDataWriter, SharcfbFileWiiU> section)
        {
            var start = writer.Position;
            writer.Write(0); // size set later
            section.Invoke(value, writer, sharc);
            var end = writer.Position;
            using (writer.BaseStream.TemporarySeek(start, SeekOrigin.Begin))
            {
                writer.Write((uint)(end - start));
            }
        }
    }
}
