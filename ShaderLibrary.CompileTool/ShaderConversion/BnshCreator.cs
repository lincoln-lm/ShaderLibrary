using ShaderLibrary.CompileTool;

// https://github.com/KillzXGaming/BFRES-Shader-Maker/blob/main/ShaderBuilder/src/BnshCreator.cs

namespace EffectLibraryTest
{
    public class BnshCreator
    {
        public class Args
        {
            public List<VariantArg> Variants = new();
            // Versions:
            // MK8, ARMS, SMM2, BOTW 2.1.2
            // Pokemon Sword/Shield 2.1.11
            // SMO, SP2, SMP, MPS, MTA, CTTT 2.1.5
            // SP3 Pokemon Scarlet/Violet 2.1.12
            // TOTK, SMW 2.2.1
            // Ounce, SMPJ 2.3.1
            public ushort VersionMajor = 2;
            public byte VersionMinor = 1;
            public byte VersionMicro = 2;

            public string Name = "dummy";

            public ushort ApiType = 4; // Always 4
            public ushort ApiVersion = 0; // 200 for Ounce

            public uint CompilerVersion = 131330;
            public ulong Unknown = 4785147618590735;
            public string uamPath = "";
        }

        public class VariantArg
        {
            public string VertexShader;
            public string FragmentShader;
            public string GeometryShader;
            public string ComputeShader;
            public string TessellationControlShader;
            public string TessellationEvalShader;

            public uint ShaderVersionMajor = 1;
            public uint ShaderVersionMinor = 9;
            public uint Flag = 2;
        }

        public class VariantOutput
        {
            public ShaderLibrary.BnshFile.ShaderVariation Variation;

            public ShaderBuilderTool.UAMShaderCompiler.ShaderOutput CompilerVertex;
            public ShaderBuilderTool.UAMShaderCompiler.ShaderOutput CompilerFragment;
            public ShaderBuilderTool.UAMShaderCompiler.ShaderOutput CompilerGeometry;
            public ShaderBuilderTool.UAMShaderCompiler.ShaderOutput CompilerCompute;
            public ShaderBuilderTool.UAMShaderCompiler.ShaderOutput CompilerTessE;
            public ShaderBuilderTool.UAMShaderCompiler.ShaderOutput CompilerTessC;
        }

        public static ShaderLibrary.BnshFile Create(Args args)
        {
            ShaderLibrary.BnshFile bnsh = new();
            bnsh.BinHeader.VersionMajor = args.VersionMajor;
            bnsh.BinHeader.VersionMicro = args.VersionMicro;
            bnsh.BinHeader.VersionMinor = args.VersionMinor;
            bnsh.Header.ApiType = args.ApiType;
            bnsh.Header.ApiVersion = args.ApiVersion;
            bnsh.Name = args.Name;
            bnsh.Header.CompilerVersion = args.CompilerVersion;
            bnsh.Header.Unknown2 = args.Unknown;

            foreach (var variant in args.Variants)
                bnsh.Variations.Add(CreateVariation(args.uamPath, variant).Variation);
            return bnsh;
        }

        public static VariantOutput CreateVariation(string uamPath, VariantArg args)
        {
            VariantOutput output = new VariantOutput();

            ShaderLibrary.BnshFile.ShaderVariation shaderVariation = new ShaderLibrary.BnshFile.ShaderVariation();
            shaderVariation.BinaryProgram = new ShaderLibrary.BnshFile.BnshShaderProgram(); 
            shaderVariation.BinaryProgram.header.Flags = (byte)args.Flag;
            // Compile stages that are used
            // Store compile info for symbol data
            output.CompilerVertex = CompileStage(uamPath, shaderVariation, args.VertexShader, args, ShaderBuilderTool.UAMShaderCompiler.Kind.vert);
            // Early fail check
            if (output.CompilerVertex == null && !string.IsNullOrEmpty(args.VertexShader))
                return output;

            output.CompilerFragment = CompileStage(uamPath, shaderVariation, args.FragmentShader, args, ShaderBuilderTool.UAMShaderCompiler.Kind.frag);
            output.CompilerGeometry = CompileStage(uamPath, shaderVariation, args.GeometryShader, args, ShaderBuilderTool.UAMShaderCompiler.Kind.geom);
            output.CompilerCompute = CompileStage(uamPath, shaderVariation, args.ComputeShader, args, ShaderBuilderTool.UAMShaderCompiler.Kind.comp);
            output.CompilerTessC = CompileStage(uamPath, shaderVariation, args.TessellationControlShader, args, ShaderBuilderTool.UAMShaderCompiler.Kind.tesc);
            output.CompilerTessE = CompileStage(uamPath, shaderVariation, args.TessellationEvalShader, args, ShaderBuilderTool.UAMShaderCompiler.Kind.tese);
            output.Variation = shaderVariation;
            return output;
        }

        public static ShaderBuilderTool.UAMShaderCompiler.ShaderOutput CompileStage(string uamPath, ShaderLibrary.BnshFile.ShaderVariation variation,
            string code, VariantArg args, ShaderBuilderTool.UAMShaderCompiler.Kind kind)
        {
            if (string.IsNullOrEmpty(code))
                return null;

            var compiled = ShaderBuilderTool.UAMShaderCompiler.CompileByText(uamPath, code, kind);
            if (compiled.ShaderCode == null) // Failed, return
                return null;

            ShaderLibrary.ControlShader controlCode = new ShaderLibrary.ControlShader(compiled.Control);
            controlCode.MajorVer = args.ShaderVersionMajor;
            controlCode.MinorVer = args.ShaderVersionMinor;

            var binShaderCode = new ShaderLibrary.BnshFile.ShaderCode()
            {
                ControlCode = controlCode.ToBytes(),
                ByteCode = compiled.ShaderCode,
            };
            var reflection = SetReflection(compiled.Symbols);
            switch (kind)
            {
                case ShaderBuilderTool.UAMShaderCompiler.Kind.vert:
                    variation.BinaryProgram.VertexShader = binShaderCode;
                    variation.BinaryProgram.VertexShaderReflection = reflection;
                    break;
                case ShaderBuilderTool.UAMShaderCompiler.Kind.frag:
                    variation.BinaryProgram.FragmentShader = binShaderCode;
                    variation.BinaryProgram.FragmentShaderReflection = reflection;
                    break;
                case ShaderBuilderTool.UAMShaderCompiler.Kind.geom:
                    variation.BinaryProgram.GeometryShader = binShaderCode;
                    variation.BinaryProgram.GeometryShaderReflection = reflection;
                    break;
                case ShaderBuilderTool.UAMShaderCompiler.Kind.comp:
                    variation.BinaryProgram.ComputeShader = binShaderCode;
                    variation.BinaryProgram.ComputeShaderReflection = reflection;
                    break;
                case ShaderBuilderTool.UAMShaderCompiler.Kind.tesc:
                    variation.BinaryProgram.TessellationControlShader = binShaderCode;
                    variation.BinaryProgram.TessellationControlShaderReflection = reflection;
                    break;
                case ShaderBuilderTool.UAMShaderCompiler.Kind.tese:
                    variation.BinaryProgram.TessellationEvalShader = binShaderCode;
                    variation.BinaryProgram.TessellationEvalShaderReflection = reflection;
                    break;
            }
            return compiled;
        }

        // Prepares location mapping and reflection data via shader symbols
        static ShaderLibrary.BnshFile.ShaderReflectionData SetReflection(ShaderBuilderTool.UAMShaderCompiler.ShaderSymbolData symbols)
        {
            ShaderLibrary.BnshFile.ShaderReflectionData reflect = new();
            foreach (var sampler in symbols.samplers.Where(x => x.location != -1))
                reflect.Samplers.TryAdd(sampler.name, new ShaderLibrary.ResUint32((uint)sampler.location));
            foreach (var input in symbols.inputs.Where(x => x.location != -1))
                reflect.Inputs.Add(input.name, new ShaderLibrary.ResUint32((uint)input.location));
            foreach (var output in symbols.outputs.Where(x => x.location != -1))
                reflect.Outputs.Add(output.name, new ShaderLibrary.ResUint32((uint)output.location));
            foreach (var buffer in symbols.uniformBlocks.Where(x => x.binding != 0))
                reflect.UniformBuffers.Add(buffer.name, new ShaderLibrary.ResUint32((uint)(buffer.binding - 1)));
            foreach (var buffer in symbols.storageBlocks.Where(x => x.binding != 0))
                reflect.StorageBuffers.Add(buffer.name, new ShaderLibrary.ResUint32((uint)buffer.binding - 1));
            reflect.UpdateSlots();
            return reflect; 
        }
    }
}