using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using WPFApplication.Models;

namespace WPFApplication.Services
{
    public static class ResultParser
    {
        public static TrainingResult Parse(string logOutput, TrainingConfig config)
        {
            var result = new TrainingResult { Success = true };
            var metricPatterns = new[]
            {
            ("Best Trainer / Algorithm", @"Trainer:\s*([^\r\n]+)"),
            ("Accuracy", @"(?<!Micro|Macro)Accuracy:\s*([0-9.,]+)"),
            ("MicroAccuracy", @"MicroAccuracy:\s*([0-9.,]+)"),
            ("MacroAccuracy", @"MacroAccuracy:\s*([0-9.,]+)"),
            ("LogLoss", @"LogLoss:\s*([0-9.,]+)"),
            ("LogLossReduction", @"LogLossReduction:\s*([0-9.,]+)")
        };

            foreach (var (name, pattern) in metricPatterns)
            {
                var matches = Regex.Matches(logOutput, pattern, RegexOptions.IgnoreCase);
                if (matches.Count > 0)
                {
                    string val = matches[^1].Groups[1].Value.Trim();
                    if (name.StartsWith("Best"))
                        result.BestAlgorithm = val;
                    else
                        result.Metrics.Add(new MetricItem { Name = name, Value = val });
                }
            }

            string baseOutput = string.IsNullOrWhiteSpace(config.OutputPath)
                ? Path.GetDirectoryName(config.DatasetPath)!
                : config.OutputPath;

            string modelFolder = Path.Combine(baseOutput, config.ModelName);
            if (Directory.Exists(modelFolder))
            {
                string consoleDir = Path.Combine(modelFolder, $"{config.ModelName}.ConsoleApp");
                if (Directory.Exists(consoleDir))
                {
                    result.ConsoleProjectPath = Directory.GetFiles(consoleDir, "*.csproj").FirstOrDefault() ?? consoleDir;
                }

                string? zipFile = Directory.GetFiles(modelFolder, "*.zip", SearchOption.AllDirectories).FirstOrDefault();
                if (zipFile != null)
                {
                    result.ModelZipPath = zipFile;
                }
            }

            return result;
        }
    }
}
