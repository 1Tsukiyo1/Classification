using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using WPFApplication.Models;

namespace WPFApplication.Services
{
    public static class MlNetCommandBuilder
    {
        public static string BuildArguments(TrainingConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.DatasetPath) || !File.Exists(config.DatasetPath))
                throw new ArgumentException("Укажите корректный файл датасета.");

            if (config.LabelCol < 0)
                throw new ArgumentException("Индекс колонки метки не может быть отрицательным.");

            if (config.TrainTime <= 0)
                throw new ArgumentException("Время обучения должно быть больше 0 секунд.");

            string modelName = string.IsNullOrWhiteSpace(config.ModelName) ? "SentimentModel" : config.ModelName.Trim();
            string outputDir = string.IsNullOrWhiteSpace(config.OutputPath)
                ? Path.GetDirectoryName(config.DatasetPath)!
                : config.OutputPath.Trim();

            var args = new List<string>
        {
            "classification",
            $"--dataset \"{config.DatasetPath}\"",
            $"--label-col {config.LabelCol}",
            $"--has-header {(config.HasHeader ? "true" : "false")}",
            $"--name \"{modelName}\"",
            $"--train-time {config.TrainTime}",
            $"--output \"{outputDir}\"",
            $"--verbosity {config.Verbosity}"
        };

            if (!string.IsNullOrWhiteSpace(config.IgnoreCols))
                args.Add($"--ignore-cols \"{config.IgnoreCols.Trim()}\"");

            return string.Join(" ", args);
        }
    }
}
