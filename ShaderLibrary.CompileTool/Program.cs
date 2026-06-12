using EffectLibraryTest;
using ShaderLibrary.CompileTool;
using ShaderLibrary.Test;
using System;
using System.IO;

if (args.Length != 3)
{
    return 1;
}

var uam_path = args[0];

var input_file = args[1];

var shader_name = Path.GetFileNameWithoutExtension(input_file);

// TODO: cmdline args for options
if (input_file.EndsWith(".vert")) {
    var bnsh = BnshCreator.Create(new BnshCreator.Args()
        {
            VersionMajor = 2,
            VersionMinor = 1,
            VersionMicro = 11,
            Name = shader_name,
            CompilerVersion = 67586,
            Variants = new List<BnshCreator.VariantArg>()
            {
                new BnshCreator.VariantArg()
                {
                    VertexShader = File.ReadAllText(input_file),
                    ShaderVersionMajor = 1,
                    ShaderVersionMinor = 15,
                    Flag = 3,
                }
            },
            uamPath = uam_path
    });
    bnsh.Save(args[2]);
} else if (input_file.EndsWith(".frag")) {
    var bnsh = BnshCreator.Create(new BnshCreator.Args()
        {
            VersionMajor = 2,
            VersionMinor = 1,
            VersionMicro = 5,
            Name = shader_name,
            CompilerVersion = 770,
            Variants = new List<BnshCreator.VariantArg>()
            {
                new BnshCreator.VariantArg()
                {
                    FragmentShader = File.ReadAllText(input_file),
                    ShaderVersionMajor = 1,
                    ShaderVersionMinor = 15,
                    Flag = 3,
                },
            },
            uamPath = uam_path
    });
    bnsh.Save(args[2]);
} else if (input_file.EndsWith(".bnsh_vsh")) {
    ShaderLibrary.BnshFile vsh = new ShaderLibrary.BnshFile(input_file);
    ShaderExtract.Export(
        vsh.Variations[0].BinaryProgram.VertexShader,
        vsh.Variations[0].BinaryProgram.VertexShaderReflection,
        args[2]);
} else if (input_file.EndsWith(".bnsh_fsh")) {
    ShaderLibrary.BnshFile fsh = new ShaderLibrary.BnshFile(input_file);
    ShaderExtract.Export(
        fsh.Variations[0].BinaryProgram.FragmentShader,
        fsh.Variations[0].BinaryProgram.FragmentShaderReflection,
        args[2]);
} else {
    return 1;
}


return 0;