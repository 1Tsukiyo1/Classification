using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace WPFApplication.Services
{
    public static class GeneratedAppModifier
    {
        private const string Marker = "// [AUTO-MODIFIED BY MLNET-GUI]";

        public static bool IsModified(string consoleProjectOrDir)
        {
            string programCs = ResolveProgramCsPath(consoleProjectOrDir);
            if (!File.Exists(programCs)) return false;
            return File.ReadAllText(programCs).Contains(Marker);
        }

        public static void ModifyProgramCs(string consoleProjectOrDir, string modelName, string textColumnName)
        {
            string programCs = ResolveProgramCsPath(consoleProjectOrDir);
            if (!File.Exists(programCs))
                throw new FileNotFoundException("Файл Program.cs не найден в сгенерированном проекте.", programCs);

            string originalCode = File.ReadAllText(programCs);
            if (originalCode.Contains(Marker))
                return;

            string backupPath = programCs + ".bak";
            if (!File.Exists(backupPath))
                File.WriteAllText(backupPath, originalCode);

            var nsMatch = Regex.Match(originalCode, @"namespace\s+([^\s;{]+)");
            string ns = nsMatch.Success ? nsMatch.Groups[1].Value : $"{modelName}.ConsoleApp";

            bool usesConsumeModel = originalCode.Contains("ConsumeModel.Predict");
            string modelClass = usesConsumeModel ? "ConsumeModel" : modelName;
            string inputClass = usesConsumeModel ? "ModelInput" : $"{modelName}.ModelInput";

            string cleanPropName = Regex.Replace(textColumnName, @"[^a-zA-Z0-9_]", "_");
            if (char.IsDigit(cleanPropName[0])) cleanPropName = "_" + cleanPropName;

            string newCode = $@"{Marker}
using System;
using System.Globalization;
using System.Linq;

namespace {ns}
{{
    class Program
    {{
        static void Main(string[] args)
        {{
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {{
                Console.Error.WriteLine(""Error: Input text argument is missing."");
                Environment.Exit(1);
            }}

            var sampleData = new {inputClass}()
            {{
                {cleanPropName} = args[0]
            }};

            var result = {modelClass}.Predict(sampleData);
            float maxScore = result.Score != null && result.Score.Length > 0 ? result.Score.Max() : 0f;

            Console.WriteLine($""Prediction: {{result.PredictedLabel}}, Probability: {{maxScore.ToString(""F4"", CultureInfo.InvariantCulture)}}"");
        }}
    }}
}}
";
            File.WriteAllText(programCs, newCode);
        }

        private static string ResolveProgramCsPath(string consoleProjectOrDir)
        {
            string dir = File.Exists(consoleProjectOrDir)
                ? Path.GetDirectoryName(consoleProjectOrDir)!
                : consoleProjectOrDir;
            return Path.Combine(dir, "Program.cs");
        }
    }
}
