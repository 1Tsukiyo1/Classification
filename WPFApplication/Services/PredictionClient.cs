using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using WPFApplication.Models;

namespace WPFApplication.Services
{
    public class PredictionClient
    {
        private readonly ProcessRunner _runner = new();

        public async Task<PredictionResult> PredictViaConsoleAppAsync(string csprojPath, string text)
        {
            string escapedText = text.Replace("\"", "\\\"");
            string args = $"run --project \"{csprojPath}\" -- \"{escapedText}\"";

            var (exitCode, stdOut, stdErr) = await _runner.RunAsync("dotnet", args);

            if (exitCode != 0)
                throw new Exception($"Ошибка выполнения консольного приложения:\n{stdErr}");

            return ParsePredictionOutput(stdOut);
        }

        public PredictionResult PredictFallbackWithMlNet(string modelZipPath, string text, string textColumnName)
        {
            var mlContext = new MLContext();
            ITransformer trainedModel = mlContext.Model.Load(modelZipPath, out var inputSchema);

            var inputType = new[] { new TextInput { Text = text } };
            IDataView dataView = mlContext.Data.LoadFromEnumerable(inputType);

            string targetCol = inputSchema
                .FirstOrDefault(c => c.Name != "Label" && c.Type.RawType == typeof(string))
                .Name ?? textColumnName;

            IDataView copiedData = mlContext.Transforms
                .CopyColumns(targetCol, nameof(TextInput.Text))
                .Fit(dataView)
                .Transform(dataView);

            IDataView transformed = trainedModel.Transform(copiedData);

            var predictedLabelCol = transformed.Schema.GetColumnOrNull("PredictedLabel");
            var scoreCol = transformed.Schema.GetColumnOrNull("Score");

            using var cursor = transformed.GetRowCursor(transformed.Schema);
            if (cursor.MoveNext())
            {
                string label = "Unknown";
                if (predictedLabelCol != null)
                {
                    var getter = cursor.GetGetter<ReadOnlyMemory<char>>(predictedLabelCol.Value);
                    ReadOnlyMemory<char> val = default;
                    getter(ref val);
                    label = val.ToString();
                }

                float? prob = null;
                if (scoreCol != null)
                {
                    var scoreGetter = cursor.GetGetter<VBuffer<float>>(scoreCol.Value);
                    VBuffer<float> scores = default;
                    scoreGetter(ref scores);
                    if (scores.Length > 0)
                        prob = scores.DenseValues().Max();
                }

                return new PredictionResult
                {
                    PredictedLabel = label,
                    Probability = prob,
                    RawOutput = $"[Fallback PredictionEngine] Prediction: {label}, Probability: {prob:F4}"
                };
            }

            throw new Exception("Не удалось получить предсказание из модели.");
        }

        private static PredictionResult ParsePredictionOutput(string output)
        {
            var match = Regex.Match(output, @"Prediction:\s*(.+?),\s*Probability:\s*([0-9.,]+)");
            if (match.Success)
            {
                string label = match.Groups[1].Value.Trim();
                string probStr = match.Groups[2].Value.Trim().Replace(',', '.');
                float.TryParse(probStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float prob);

                return new PredictionResult
                {
                    PredictedLabel = label,
                    Probability = prob,
                    RawOutput = output.Trim()
                };
            }

            return new PredictionResult
            {
                PredictedLabel = output.Trim(),
                Probability = null,
                RawOutput = output.Trim()
            };
        }

        private class TextInput
        {
            public string Text { get; set; } = string.Empty;
        }
    }
}