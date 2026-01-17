using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using dnlib.DotNet;

namespace Il2CppSDK
{
    static class UtilThing
    {
        public const string allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWYZ1234567890_";
        public static string FixIl2CppName(this string name)
        {
            if (string.IsNullOrEmpty(name)) return "_";
            string result = "";
            bool isIntStarted = char.IsDigit(name[0]);

            if (isIntStarted) result = "_";
            for (int i = 0; i < name.Length; ++i)
            {
                if (!allowedChars.Contains(name[i]))
                    result += "$";
                else
                    result += name[i];
            }
            return result;
        }
    }

    class Program
    {
        static Dictionary<string, int> m_DuplicateMethodTable = new();
        static Dictionary<string, List<string>> namespaceIncludes = new();
        static string OUTPUT_DIR = "SDK";
        static ModuleDefMD currentModule;
        static StreamWriter currentFile;
        static int indentLevel;

        static void WriteIndented(string line, bool write = false)
        {
            string indent = new string('\t', indentLevel);
            if (write) currentFile.Write(indent + line);
            else currentFile.WriteLine(indent + line);
        }

        static void ParseFields(TypeDef clazz)
        {
            foreach (var field in clazz.Fields)
            {
                if (field.IsLiteral) continue;
                var fieldName = field.Name.ToString().FixIl2CppName();
                if (fieldName == "auto" || fieldName == "register") fieldName += "_";
                var fieldType = Utils.Il2CppTypeToCppType(field.FieldType, clazz);
                WriteIndented($"template <typename T = {fieldType}>", true);
                currentFile.WriteLine($" {(field.IsStatic ? "static " : "")}T {Utils.FormatInvalidName(fieldName)}() {{");
                WriteIndented($"\tstatic BNM::Field<T> __bnm__field__ = StaticClass().GetField(\"{fieldName}\");");
                if (!field.IsStatic) WriteIndented("\t__bnm__field__.SetInstance((BNM::IL2CPP::Il2CppObject*)this);");
                WriteIndented("\treturn __bnm__field__();");
                WriteIndented("}");
                WriteIndented($"{(field.IsStatic ? "static " : "")}void set_{Utils.FormatInvalidName(fieldName)}({fieldType} value) {{");
                WriteIndented($"\tstatic BNM::Field<{fieldType}> __bnm__field__ = StaticClass().GetField(\"{fieldName}\");");
                if (!field.IsStatic) WriteIndented("\t__bnm__field__.SetInstance((BNM::IL2CPP::Il2CppObject*)this);");
                WriteIndented("\t__bnm__field__.Set(value);");
                WriteIndented("}");
            }
        }

        static void ParseMethods(TypeDef clazz)
        {
            if (clazz.IsStruct()) return;
            foreach (var method in clazz.Methods)
            {
                if (method.IsConstructor || method.IsStaticConstructor) continue;
                var methodName = method.Name.ToString().FixIl2CppName();
                if (methodName == "auto" || methodName == "register") methodName += "_";
                var returnType = Utils.Il2CppTypeToCppType(method.ReturnType, clazz);
                var key = clazz.FullName + method.Name;
                if (m_DuplicateMethodTable.ContainsKey(key)) methodName += "_" + m_DuplicateMethodTable[key]++;
                else m_DuplicateMethodTable[key] = 1;
                var paramTypes = new List<string>();
                var paramNames = new List<string>();
                foreach (var p in method.Parameters.Where(p => p.IsNormalMethodParameter))
                {
                    var t = Utils.Il2CppTypeToCppType(p.Type, clazz);
                    var paramTypeDef = p.Type.ToTypeDefOrRef()?.ResolveTypeDef();
                    if (paramTypeDef != null && paramTypeDef.IsEnum) t = Utils.GetEnumType(paramTypeDef);
                    if (paramTypeDef != null && paramTypeDef.TryGetGenericInstSig() != null) t = "void*";
                    var n = p.Name;
                    if (n == "auto" || n == "register") n += "_";
                    if (p.HasParamDef && p.ParamDef.IsOut) t += "*";
                    paramTypes.Add(t);
                    paramNames.Add(n);
                }
                WriteIndented($"template <typename T = {returnType}>", true);
                currentFile.WriteLine($" {(method.IsStatic ? "static " : "")}T {Utils.FormatInvalidName(methodName)}({string.Join(", ", paramTypes.Zip(paramNames, (t, n) => $"{t} {Utils.FormatInvalidName(n)}"))}) {{");
                WriteIndented($"\tstatic BNM::Method<T> __bnm__method__ = StaticClass().GetMethod(\"{method.Name}\", {paramNames.Count});");
                if (!method.IsStatic) WriteIndented("\treturn __bnm__method__[(BNM::IL2CPP::Il2CppObject*)this](", true);
                else WriteIndented("\treturn __bnm__method__(", true);
                currentFile.Write(string.Join(", ", paramNames.Select(Utils.FormatInvalidName)));
                currentFile.WriteLine(");");
                WriteIndented("}");
            }
        }

        static void ParseClass(TypeDef clazz)
        {
            currentFile.WriteLine("#pragma once");
            currentFile.WriteLine("#include <BNMIncludes.hpp>");
            currentFile.WriteLine();
            indentLevel = 0;
            TypeDef baseType = clazz.BaseType?.ResolveTypeDef();
            string baseIncludeLine = "";
            string baseClassFullName = "";

            if (!clazz.IsStruct() && baseType != null && baseType.FullName != "System.Object")
            {
                string baseNs = string.IsNullOrEmpty(baseType.Namespace) ? "GlobalNamespace" : baseType.Namespace.Replace(".", "::");
                string baseName = Utils.FormatInvalidName(baseType.Name);
                string includePath = string.IsNullOrEmpty(baseType.Namespace) ? baseName : $"{baseType.Namespace}/{baseName}.hpp";
                baseIncludeLine = $"#include <{includePath}>";
                baseClassFullName = $"{baseNs}::{baseName}";
            }

            if (!string.IsNullOrEmpty(baseIncludeLine)) currentFile.WriteLine(baseIncludeLine);
            var nsString = clazz.Namespace.ToString() ?? "";
            var nsParts = nsString.Split('.', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in nsParts)
            {
                WriteIndented($"namespace {p} {{");
                indentLevel++;
            }

            var validClassName = Utils.FormatInvalidName(clazz.Name);
            if (!clazz.IsStruct())
            {
                if (!string.IsNullOrEmpty(baseClassFullName)) WriteIndented($"class {validClassName} : public {baseClassFullName}");
                else WriteIndented($"class {validClassName} : public BNM::IL2CPP::Il2CppObject");
            }
            else WriteIndented($"struct {validClassName}");

            WriteIndented("{");
            indentLevel++;
            WriteIndented("public:");
            WriteIndented("static BNM::Class StaticClass() {");
            WriteIndented($"\treturn BNM::Class(\"{nsString}\", \"{clazz.Name}\", BNM::Image(\"{clazz.Module.Name}\"));");
            WriteIndented("}");
            WriteIndented("");

            if (clazz.IsEnum)
            {
                WriteIndented($"enum class {validClassName} : {Utils.GetEnumType(clazz)}");
                WriteIndented("{");
                indentLevel++;
                var enumFields = clazz.Fields.Where(f => f.IsLiteral && f.IsStatic && f.Constant?.Value != null).ToList();
                for (int i = 0; i < enumFields.Count; i++)
                {
                    var comma = i == enumFields.Count - 1 ? "" : ",";
                    WriteIndented($"{Utils.FormatInvalidName(enumFields[i].Name)} = {enumFields[i].Constant.Value}{comma}");
                }
                indentLevel--;
                WriteIndented("};");
            }
            else
            {
                ParseFields(clazz);
                WriteIndented("");
                ParseMethods(clazz);
            }

            indentLevel--;
            WriteIndented("};");
            while (indentLevel > 0)
            {
                indentLevel--;
                WriteIndented("}");
            }
        }

        static void ParseClasses()
        {
            if (currentModule == null) return;

            foreach (var rid in currentModule.Metadata.GetTypeDefRidList())
            {
                var type = currentModule.ResolveTypeDef(rid);
                if (type == null) continue;

                string rawTypeName = type.Name.ToString();
                if (rawTypeName.StartsWith("<Module>") || rawTypeName.StartsWith("<PrivateImplementationDetails>"))
                    continue;

                var namespaze = type.Namespace.Replace("<", "").Replace(">", "");
                var className = rawTypeName.FixIl2CppName();
                var classFilename = string.Concat(className.Split(Path.GetInvalidFileNameChars()));

                string key = namespaze.Length > 0 ? namespaze : "-";
                string include = namespaze.Length > 0 
                    ? $"#include \"Includes/{namespaze}/{classFilename}.h\"" 
                    : $"#include \"Includes/{classFilename}.h\"";

                if (!namespaceIncludes.ContainsKey(key))
                    namespaceIncludes[key] = new List<string>();

                if (!namespaceIncludes[key].Contains(include))
                    namespaceIncludes[key].Add(include);

                string outputPath = Path.Combine(OUTPUT_DIR, "Includes");
                if (namespaze.Length > 0) outputPath = Path.Combine(outputPath, namespaze);
                
                if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);

                string fullFilePath = Path.Combine(outputPath, classFilename + ".h");
                currentFile = new StreamWriter(fullFilePath);
                ParseClass(type);
                currentFile.Close();
            }
        }

        static void WriteAllNamespaceHeaderFiles()
        {
            foreach (var kvp in namespaceIncludes)
            {
                string path = Path.Combine(OUTPUT_DIR, kvp.Key + ".h");
                File.WriteAllLines(path, kvp.Value);
            }
        }

        static void ParseModule(string path)
        {
            var ctx = ModuleDef.CreateModuleContext();
            currentModule = ModuleDefMD.Load(path, ctx);
            ParseClasses();
        }

        static void Main(string[] args)
        {
            if (args.Length < 1) return;

            if (Directory.Exists(OUTPUT_DIR))
                Directory.Delete(OUTPUT_DIR, true);

            Directory.CreateDirectory(OUTPUT_DIR);

            if (Directory.Exists(args[0]))
            {
                foreach (var f in Directory.GetFiles(args[0], "*.dll"))
                    ParseModule(f);
            }
            else
            {
                ParseModule(args[0]);
            }

            WriteAllNamespaceHeaderFiles();
        }
    }
}