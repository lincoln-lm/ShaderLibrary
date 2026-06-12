using System.Diagnostics;
using System.Linq;
using System.Text;
using ShaderLibrary;
using ShaderLibrary.CompileTool;

namespace EffectLibraryTest
{
    public class ShaderExtract
    {
        public static void Export(ShaderLibrary.BnshFile.ShaderCode shaderCode, string filePath)
        {
            if (shaderCode == null)
                return;

            File.WriteAllText(filePath, GetCode(shaderCode));
        }

        public static void Export(ShaderLibrary.BnshFile.ShaderCode shaderCode, ShaderLibrary.BnshFile.ShaderReflectionData reflect, string filePath)
        {
            if (shaderCode == null)
                return;

            File.WriteAllText(filePath, GetCode(shaderCode, reflect));
        }

        public static void ExportPreviewed(ShaderModel shader, ShaderLibrary.BnshFile.ShaderCode shaderCode, ShaderLibrary.BnshFile.ShaderReflectionData reflect, string filePath)
        {
            if (shaderCode == null)
                return;

            File.WriteAllText(filePath, ShaderLabelUtil.PreviewUniforms(GetCode(shaderCode, reflect), shader, reflect));
        }

        public static void ExportPreviewed(string code, ShaderModel shader, ShaderLibrary.BnshFile.ShaderCode shaderCode, ShaderLibrary.BnshFile.ShaderReflectionData reflect, string filePath)
        {
            if (shaderCode == null)
                return;

            var control_code = new ControlShader(shaderCode.ControlCode);
            float[] constants = control_code.GetConstantsAsFloats(shaderCode.ByteCode);
            byte[] raw = control_code.GetConstants(shaderCode.ByteCode);
            File.WriteAllBytes("Constants.bin", raw);

            //Apply the code to be usable with the UAM compiler
            code = ApplyConstants(code, constants);
            code = FixLocations(code);
            code = FixEarlyReturns(code);

            if (reflect != null)
                code = SetReflectionNames(code, reflect);

            File.WriteAllText(filePath, ShaderLabelUtil.PreviewUniforms(code, shader, reflect));
        }

        public static string GetCode(ShaderLibrary.BnshFile.ShaderCode shaderCode, ShaderLibrary.BnshFile.ShaderReflectionData reflect = null)
        {
            var control_code = new ControlShader(shaderCode.ControlCode);

            string code = TegraShaderTranslator.Decompile(shaderCode.ByteCode);
            float[] constants = control_code.GetConstantsAsFloats(shaderCode.ByteCode);

            //Apply the code to be usable with the UAM compiler
            code = ApplyConstants(code, constants);
            code = FixLocations(code);
            code = FixEarlyReturns(code);

            if (reflect != null)
                code = SetReflectionNames(code, reflect);

            return code;
        }

        static string SetReflectionNames(string code, ShaderLibrary.BnshFile.ShaderReflectionData reflect)
        {
            Dictionary<string, string> symbols = new Dictionary<string, string>();
            foreach (var sampler in reflect.Samplers.Keys)
            {
                int location = reflect.GetSamplerLocation(sampler);
                if (location == -1)
                    continue;

                string glsl_string_vertex = "vp_t_tcb_" + ((location * 2) + 8).ToString("X1");
                string glsl_string_pixel  = "fp_t_tcb_" + ((location * 2) + 8).ToString("X1");
                symbols.Add(glsl_string_vertex, sampler);
                symbols.Add(glsl_string_pixel, sampler);
            }

            foreach (var name in reflect.UniformBuffers.Keys)
            {
                int location = reflect.GetConstantBufferLocation(name);
                if (location == -1)
                    continue;

                symbols.Add($"_fp_c{((location) + 3)}", $"_{name}");
                symbols.Add($"_vp_c{((location) + 3)}", $"_{name}");

                symbols.Add($"fp_c{((location) + 3)}", name);
                symbols.Add($"vp_c{((location) + 3)}", name);
            }

            foreach (var name in reflect.Inputs.Keys)
            {
                int location = reflect.GetInputLocation(name);
                if (location == -1)
                    continue;

                string glsl_string_input = $"in_attr{location}";
                symbols.Add(glsl_string_input, name);
            }

            foreach (var name in reflect.Outputs.Keys)
            {
                int location = reflect.GetOutputLocation(name);
                if (location == -1)
                    continue;

                string glsl_string_output = $"out_attr{location}";
                symbols.Add(glsl_string_output, name);
            }

            string line;

            var sb = new StringBuilder();
            using (StringReader reader = new StringReader(code))
            {
                do
                {
                    line = reader.ReadLine();

                    if (line != null)
                    {
                        //input sampler
                        foreach (var sampler in symbols)
                        {
                            if (line.Contains(sampler.Key))
                            {
                                line = line.Replace(sampler.Key, sampler.Value);
                            }
                        }
                        sb.AppendLine(line);
                    }

                } while (line != null);
            }


            return sb.ToString();
        }

        static string FixLocations(string code)
        {
            string line;

            int sampler_bind = 0;
            int block_bind = 0;

            int sampler_base_id = 4;
            int block_base_id = 1;

            var sb = new StringBuilder();
            using (StringReader reader = new StringReader(code))
            {
                do
                {
                    line = reader.ReadLine();

                    if (line != null)
                    {
                        //input sampler
                        if (line.Contains("uniform sampler"))
                        {
                            //get the id in hex
                            string id = line.Split("_").LastOrDefault().Replace(";", "");
                            int slot = Convert.ToInt32($"0x{id}", 16) / 2 - sampler_base_id;

                            //swap binding id with slot id
                            line = line.Replace($"binding = {sampler_bind}", $"binding = {slot}");

                            sampler_bind++;
                        }
                        //input block
                        if (line.Contains("std140) uniform") && line.Contains("_c"))
                        {
                            if (line.EndsWith("_fp_c1") || line.EndsWith("_vp_c1")) //remove constant buffer as the extractor loads these directly
                            {
                                //skip cbuffer lines
                                reader.ReadLine();
                                reader.ReadLine();
                                reader.ReadLine();
                                continue;
                            }

                            //get the id in hex
                            string id = line.Split("_c").LastOrDefault().Replace(";", "");
                            int slot = Convert.ToInt32(id);

                            if (slot != 1) //constant buffer skip
                            {
                                //swap binding id with slot id
                                line = line.Replace($"binding = {slot + 1}", $"binding = {slot - 3}");
                            }

                            block_bind++;
                        }
                        sb.AppendLine(line);
                    }

                } while (line != null);
            }


            return sb.ToString();
        }

        static string FixEarlyReturns(string code)
        {
            var sb = new StringBuilder();
            string line;
            using (StringReader reader = new StringReader(code))
            {
                do
                {
                    line = reader.ReadLine();

                    if (line != null)
                    {
                        if (line.Contains("void main()"))
                        {
                            line = line.Replace("void main()", "void main() { do");
                        }
                        if (line.Contains("break;"))
                        {
                            throw new Exception("Shader contains unsupported break");
                        }
                        if (line.Contains("return") && !line.Contains("return;"))
                        {
                            throw new Exception("Shader contains unsupported return");
                        }
                        if (line.Contains("return;"))
                        {
                            line = line.Replace("return;", "break;");
                        }
                        sb.AppendLine(line);
                    } else {
                        sb.Length -= Environment.NewLine.Length;

                        sb.AppendLine(" while (false);");
                        sb.AppendLine("}");
                    }

                } while (line != null);
            }


            return sb.ToString();
        }

        static string ApplyConstants(string code, float[] constants)
        {
            string fragBlockName = "fp_c1.data";
            string vertBlockName = "vp_c1.data";

            Dictionary<string, float> constant_lookup = new Dictionary<string, float>();

            int index = 0;

            for (int i = 0; i < constants.Length;)
            {
                string swizzle = "x";

                //use each 4 swizzle value
                for (int j = 0; j < 4; j++)
                {
                    if (constants.Length <= i)
                        continue;

                    float value = constants[i];

                    //Expected variable name stored in the block
                    string frag_variable_name = $"{fragBlockName}[{index}].{swizzle}";
                    string vert_variable_name = $"{vertBlockName}[{index}].{swizzle}";
                    constant_lookup.Add(frag_variable_name, value);
                    constant_lookup.Add(vert_variable_name, value);

                    swizzle = SwizzleShift(swizzle);

                    //increase to next constant
                    i++;
                }
                //go to next vec4
                index++;
            }

            string line;
            bool has_variable_constant_index = false;

            var sb = new StringBuilder();
            using (StringReader reader = new StringReader(code))
            {
                do
                {
                    line = reader.ReadLine();

                    if (line != null)
                    {
                        //swap variable with raw constant value
                        if (line.Contains(fragBlockName) || line.Contains(vertBlockName))
                        {
                            //find variable and replace it
                            foreach (var var in constant_lookup)
                            {
                                if (line.Contains(var.Key))
                                    line = line.Replace(var.Key, var.Value.ToString());
                            }
                        }
                        // variable index into constant array
                        if (line.Contains(fragBlockName) || line.Contains(vertBlockName)) {
                            has_variable_constant_index = true;
                        }

                        sb.AppendLine(line);
                    }

                } while (line != null);
            }
            code = sb.ToString();

            if (has_variable_constant_index)
            {
                sb = new StringBuilder();
                using (StringReader reader = new StringReader(code))
                {
                    do
                    {
                        line = reader.ReadLine();

                        if (line != null)
                        {
                            if (line.Contains("const int undef = 0;")) {
                                sb.Append("const vec4 constants[] = vec4[](");
                                for (int i = 0; i < constants.Length;)
                                {
                                    if (i > 0) sb.Append(", ");
                                    sb.Append("vec4(");
                                    for (int j = 0; j < 4; j++)
                                    {
                                        if (j > 0) sb.Append(", ");
                                        if (constants.Length <= i) {
                                            sb.Append("0.0");
                                            continue;
                                        }

                                        float value = constants[i];
                                        sb.Append(value.ToString());
                                        i++;
                                    }
                                    sb.Append(")");
                                    index++;
                                }
                                sb.AppendLine(");");
                            }
                            if (line.Contains(fragBlockName) || line.Contains(vertBlockName))
                            {
                                line = line.Replace(fragBlockName, "constants");
                                line = line.Replace(vertBlockName, "constants");
                            }

                            sb.AppendLine(line);
                        }

                    } while (line != null);
                }
                code = sb.ToString();
            }

            return code;
        }

        static string SwizzleShift(string swizzle)
        {
            if (swizzle == "x") return "y";
            if (swizzle == "y") return "z";
            if (swizzle == "z") return "w";
            return "x";
        }
    }
}
