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
            string programPath = GetProgramPath(consoleProjectOrDir);
            return File.Exists(programPath) && File.ReadAllText(programPath).Contains(Marker, StringComparison.Ordinal);
        }

        public static void ModifyProgramCs(string consoleProjectOrDir, string modelName, string textColumnName)
        {
            string programPath = GetProgramPath(consoleProjectOrDir);
            if (!File.Exists(programPath))
                throw new FileNotFoundException("Файл Program.cs не найден в сгенерированном проекте.", programPath);

            string currentCode = File.ReadAllText(programPath);
            if (currentCode.Contains(Marker, StringComparison.Ordinal))
                return;

            SaveBackupIfNeeded(programPath, currentCode);

            string targetNamespace = FindNamespace(currentCode) ?? $"{modelName}.ConsoleApp";
            bool hasConsumeModel = currentCode.Contains("ConsumeModel.Predict", StringComparison.Ordinal);

            string predictorClass = hasConsumeModel ? "ConsumeModel" : modelName;
            string inputModelClass = hasConsumeModel ? "ModelInput" : $"{modelName}.ModelInput";
            string propertyName = FormatIdentifier(textColumnName);

            string generatedCode = ConsoleProgramTemplate.Render(
                Marker,
                targetNamespace,
                inputModelClass,
                predictorClass,
                propertyName);

            File.WriteAllText(programPath, generatedCode);
        }

        private static void SaveBackupIfNeeded(string programPath, string originalCode)
        {
            string bakPath = programPath + ".bak";
            if (!File.Exists(bakPath))
                File.WriteAllText(bakPath, originalCode);
        }

        private static string? FindNamespace(string code)
        {
            var match = Regex.Match(code, @"namespace\s+([^\s;{]+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string FormatIdentifier(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                return "Text";

            string sanitized = Regex.Replace(columnName, @"[^a-zA-Z0-9_]", "_");
            if (string.IsNullOrWhiteSpace(sanitized))
                return "Text";

            return char.IsDigit(sanitized[0]) ? $"_{sanitized}" : sanitized;
        }

        private static string GetProgramPath(string projectOrDir)
        {
            string folder = File.Exists(projectOrDir)
                ? Path.GetDirectoryName(projectOrDir)!
                : projectOrDir;

            return Path.Combine(folder, "Program.cs");
        }

        private static class ConsoleProgramTemplate
        {
            public static string Render(string marker, string ns, string inputType, string modelType, string propName)
            {
                return string.Concat(
                    marker, "\n",
                    "using System;\n",
                    "using System.Globalization;\n",
                    "using System.Linq;\n\n",
                    "namespace ", ns, ";\n\n",
                    "internal class Program\n",
                    "{\n",
                    "private static void Main(string[] args)\n",
                    "{\n",
                    "if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))\n",
                    "{\n",
                    "Console.Error.WriteLine(\"Error: Input text argument is missing.\");\n",
                    "Environment.Exit(1);\n",
                    "}\n\n",
                    "var sample = new ", inputType, "()\n",
                    "{\n",
                    " ", propName, " = args[0]\n",
                    "};\n\n",
                    "var prediction = ", modelType, ".Predict(sample);\n",
                    "float bestScore = prediction.Score is { Length: > 0 } ? prediction.Score.Max() : 0f;\n",
                    "string formatted = bestScore.ToString(\"F4\", CultureInfo.InvariantCulture);\n",
                    "Console.WriteLine($\"Prediction: {prediction.PredictedLabel}, Probability: {formatted}\");\n",
                    " }\n",
                    "}\n");
            }
        }
    }
}
