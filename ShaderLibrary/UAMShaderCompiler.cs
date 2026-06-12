using ShaderLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShaderBuilderTool
{
    /// <summary>
    /// Represents a shader compiler that will compile Switch nvn shader binaries with the uam tool.
    /// Results in a shader output with byte code, control code, and symbol data.
    /// </summary>
    public class UAMShaderCompiler
    {

        public enum Kind // Type names based on extension and argument for uam tool.
        {
            vert, // Vertex
            frag, // Fragment
            geom, // Geometry
            comp, // Compute
            tesc, // Tess Control
            tese, // Tess Eval
        }

        /// <summary>
        /// Compiles shader code to an nvn binary.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="kind"></param>
        /// <returns></returns>
        public static ShaderOutput CompileByText(string uamPath, string text, Kind kind)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            string inputFile = Path.Combine(tempDir, "input.glsl");
            File.WriteAllText(inputFile, text);
            var compiled = Compile(tempDir, uamPath, "input.glsl", kind);
            Directory.Delete(tempDir, true);
            return compiled;
        }

        public static ShaderOutput CompileByText(string uamPath, string text, Kind kind, Dictionary<string, string> macros)
        {
            return CompileByText(uamPath, GlslUtility.ApplyMacros(macros, text), kind);
        }

        static ShaderOutput Compile(string folder, string uamPath, string shadername, Kind kind)
        {
            bool isSuccess = ExecuteCommand(folder, uamPath, $"--glslcbinds --nvnctrl=control.bin --nvngpu=program.bin -s {kind} {shadername}");
            // Ensure files output to correct directory
            bool filesExist = File.Exists(Path.Combine(folder, "program.bin")) && 
                              File.Exists(Path.Combine(folder, "control.bin"));
            if (!isSuccess || !filesExist)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to compile {shadername}!");
                Console.ResetColor();
                return new ShaderOutput();
            }

            // Raw binary and control shader
            byte[] shader_bin  = File.ReadAllBytes(Path.Combine(folder, "program.bin"));
            byte[] control_bin = File.ReadAllBytes(Path.Combine(folder, "control.bin"));

            // Symbol data should dump on the latest fork
            // Contains names and bind/location info for all the used uniform/input/output/sampler data
            var symbols = new ShaderSymbolData();
            if (File.Exists(Path.Combine(folder, $"symbols.{kind}.json")))
            {
                symbols = JsonSerializer.Deserialize<ShaderSymbolData>(
                    File.ReadAllText(Path.Combine(folder, $"symbols.{kind}.json")));
            }

            foreach (var block in symbols.uniformBlocks)
            {
                if (block.stageMask == 0)
                    block.binding = 0;
            }

            return new ShaderOutput()
            {
                ShaderCode = shader_bin,
                Control = control_bin,
                Symbols = symbols,
            };
        }

        static bool ExecuteCommand(string folder, string exePath, string arguments)
        {
            var info = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = folder, // ensure relative files resolve
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process cmd = new Process();
            cmd.StartInfo = info;
            cmd.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine(e.Data);
            };
            cmd.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine($"");
                    if (e.Data.Contains("warning:"))
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    else
                        Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"{e.Data}");
                    Console.ResetColor();
                }
            };
            cmd.Start();

            cmd.BeginOutputReadLine();
            cmd.BeginErrorReadLine();

            cmd.WaitForExit();

            return cmd.ExitCode == 0;
        }


        public class ShaderOutput
        {
            /// <summary>
            /// The raw shader byte code.
            /// </summary>
            public byte[] ShaderCode;
            /// <summary>
            /// The raw shader control code.
            /// </summary>
            public byte[] Control;
            /// <summary>
            /// Symbol data serialized from json based on used shader data.
            /// </summary>
            public ShaderSymbolData Symbols = new();
        }

        public class ShaderSymbolData
        {
            public List<AttributeSymbol> inputs { get; set; } = new();
            public List<AttributeSymbol> outputs { get; set; } = new();
            public List<SamplerSymbol> samplers { get; set; } = new();
            public List<UniformBlockSymbol> uniformBlocks { get; set; } = new();
            public List<UniformBlockSymbol> storageBlocks { get; set; } = new();

            public int GetSamplerLocation(string name)
            {
                for (int i = 0; i < samplers.Count; i++)
                    if (samplers[i].name == name)
                        return samplers[i].location;
                return -1;
            }
            public int GetUniformBlockLocation(string name)
            {
                for (int i = 0; i < uniformBlocks.Count; i++)
                    if (uniformBlocks[i].name == name)
                        return uniformBlocks[i].binding - 1;
                return -1;
            }
            public int GetStorageBlockLocation(string name)
            {
                for (int i = 0; i < storageBlocks.Count; i++)
                    if (storageBlocks[i].name == name)
                        return storageBlocks[i].binding - 1;
                return -1;
            }
            public bool HasAttribute(string name) => inputs.Any(x => x.name == name);
        }

        public class AttributeSymbol
        {
            public string name { get; set; }
            public int location { get; set; }
        }
        public class SamplerSymbol
        {
            public string name { get; set; }
            public int location { get; set; }
            public int target { get; set; } // 10 == 2D
        }
        public class UniformBlockSymbol
        {
            public string name { get; set; }
            public int index { get; set; }
            public int binding { get; set; }
            public int size { get; set; }
            public int stageMask { get; set; }
            public List<UniformSymbol> uniforms { get; set; } = new();
        }
        public class UniformSymbol
        {
            public string name { get; set; }
            public int index { get; set; }
            public int offset { get; set; }
        }
    }
}
