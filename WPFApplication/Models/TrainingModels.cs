using System;
using System.Collections.Generic;
using System.Text;

namespace WPFApplication.Models
{
    public class TrainingConfig
    {
        public string DatasetPath { get; set; } = string.Empty;
        public int LabelCol { get; set; } = 1;
        public bool HasHeader { get; set; } = false;
        public string ModelName { get; set; } = "SentimentModel";
        public int TrainTime { get; set; } = 60;
        public string OutputPath { get; set; } = string.Empty;
        public string Verbosity { get; set; } = "minimal";
        public string IgnoreCols { get; set; } = string.Empty;
    }

    public class MetricItem
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class TrainingResult
    {
        public bool Success { get; set; }
        public string ConsoleProjectPath { get; set; } = string.Empty;
        public string ModelZipPath { get; set; } = string.Empty;
        public string BestAlgorithm { get; set; } = "Не определен";
        public List<MetricItem> Metrics { get; set; } = new();
    }

    public class PredictionResult
    {
        public string PredictedLabel { get; set; } = string.Empty;
        public float? Probability { get; set; }
        public string RawOutput { get; set; } = string.Empty;
    }
}
