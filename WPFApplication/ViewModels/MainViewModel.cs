using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using WPFApplication.Models;
using WPFApplication.Services;

namespace WPFApplication.ViewModels
{
    public class ColumnOption
    {
        public int Index { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string RawHeader { get; set; } = string.Empty;
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ProcessRunner _processRunner = new();
        private readonly PredictionClient _predictionClient = new();
        private CancellationTokenSource? _cts;

        public TrainingConfig Config { get; } = new();
        public ObservableCollection<ColumnOption> AvailableColumns { get; } = new();
        public ObservableCollection<MetricItem> Metrics { get; } = new();

        private string _datasetPreview = "Выберите файл датасета (*.csv, *.tsv, *.txt)...";
        public string DatasetPreview { get => _datasetPreview; set { _datasetPreview = value; OnPropertyChanged(); } }

        private string _consoleLog = string.Empty;
        public string ConsoleLog { get => _consoleLog; set { _consoleLog = value; OnPropertyChanged(); } }

        private bool _isTraining;
        public bool IsTraining { get => _isTraining; set { _isTraining = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanTrain)); } }
        public bool CanTrain => !IsTraining && File.Exists(Config.DatasetPath);

        private bool _isTrained;
        public bool IsTrained { get => _isTrained; set { _isTrained = value; OnPropertyChanged(); } }

        private string _bestAlgorithm = "—";
        public string BestAlgorithm { get => _bestAlgorithm; set { _bestAlgorithm = value; OnPropertyChanged(); } }

        private string _generatedProjectPath = "—";
        public string GeneratedProjectPath { get => _generatedProjectPath; set { _generatedProjectPath = value; OnPropertyChanged(); } }

        private string _modelZipPath = string.Empty;

        private bool _autoModifyConsoleApp = true;
        public bool AutoModifyConsoleApp { get => _autoModifyConsoleApp; set { _autoModifyConsoleApp = value; OnPropertyChanged(); } }

        private string _inputText = string.Empty;
        public string InputText { get => _inputText; set { _inputText = value; OnPropertyChanged(); } }

        private string _predictedClass = "—";
        public string PredictedClass { get => _predictedClass; set { _predictedClass = value; OnPropertyChanged(); } }

        private string _predictedProbability = "—";
        public string PredictedProbability { get => _predictedProbability; set { _predictedProbability = value; OnPropertyChanged(); } }

        private bool _isPredicting;
        public bool IsPredicting { get => _isPredicting; set { _isPredicting = value; OnPropertyChanged(); } }

        public ICommand BrowseDatasetCommand { get; }
        public ICommand BrowseOutputCommand { get; }
        public ICommand StartTrainingCommand { get; }
        public ICommand StopTrainingCommand { get; }
        public ICommand OpenOutputFolderCommand { get; }
        public ICommand ClassifyCommand { get; }

        public MainViewModel()
        {
            _processRunner.OutputReceived += line => AppendLog(line);
            _processRunner.ErrorReceived += line => AppendLog($"[ERR] {line}");

            BrowseDatasetCommand = new RelayCommand(_ => BrowseDataset());
            BrowseOutputCommand = new RelayCommand(_ => BrowseOutput());
            StartTrainingCommand = new RelayCommand(async _ => await StartTrainingAsync(), _ => CanTrain);
            StopTrainingCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsTraining);
            OpenOutputFolderCommand = new RelayCommand(_ => OpenOutputFolder(), _ => Directory.Exists(Config.OutputPath));
            ClassifyCommand = new RelayCommand(async _ => await ClassifyAsync(), _ => IsTrained && !IsPredicting && !string.IsNullOrWhiteSpace(InputText));
        }

        private void BrowseDataset()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Табличные файлы (*.csv;*.tsv;*.txt)|*.csv;*.tsv;*.txt|Все файлы (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                Config.DatasetPath = dialog.FileName;
                Config.OutputPath = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
                OnPropertyChanged(nameof(Config));
                OnPropertyChanged(nameof(CanTrain));
                LoadPreviewAndColumns();
            }
        }

        private void BrowseOutput()
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                Config.OutputPath = dialog.FolderName;
                OnPropertyChanged(nameof(Config));
            }
        }

        public void LoadPreviewAndColumns()
        {
            if (!File.Exists(Config.DatasetPath)) return;

            var lines = File.ReadLines(Config.DatasetPath, Encoding.UTF8).Take(10).ToList();
            DatasetPreview = string.Join(Environment.NewLine, lines);

            AvailableColumns.Clear();
            if (lines.Count > 0)
            {
                char sep = Config.DatasetPath.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) || lines[0].Contains('\t') ? '\t' : ',';
                string[] parts = lines[0].Split(sep);

                for (int i = 0; i < parts.Length; i++)
                {
                    string colText = parts[i].Trim('"');
                    AvailableColumns.Add(new ColumnOption
                    {
                        Index = i,
                        RawHeader = Config.HasHeader ? colText : $"col{i}",
                        DisplayName = Config.HasHeader ? $"[{i}] {colText}" : $"[{i}] (Пример: {colText[..Math.Min(20, colText.Length)]})"
                    });
                }

                if (Config.LabelCol >= AvailableColumns.Count)
                    Config.LabelCol = 0;
                OnPropertyChanged(nameof(Config));
            }
        }

        private async Task StartTrainingAsync()
        {
            try
            {
                string args = MlNetCommandBuilder.BuildArguments(Config);
                ConsoleLog = $"> mlnet {args}\n{new string('-', 60)}\n";
                Metrics.Clear();
                IsTrained = false;
                IsTraining = true;
                _cts = new CancellationTokenSource();

                var (exitCode, stdOut, stdErr) = await _processRunner.RunAsync("mlnet", args, _cts.Token);

                if (_cts.IsCancellationRequested)
                {
                    AppendLog("\n[ОБУЧЕНИЕ ПРЕРВАНО ПОЛЬЗОВАТЕЛЕМ]");
                    return;
                }

                if (exitCode != 0)
                {
                    AppendLog($"\n[ОШИБКА] Процесс завершился с кодом {exitCode}.");
                    MessageBox.Show($"Ошибка обучения ML.NET:\n{stdErr}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var result = ResultParser.Parse(stdOut, Config);
                BestAlgorithm = result.BestAlgorithm;
                GeneratedProjectPath = result.ConsoleProjectPath;
                _modelZipPath = result.ModelZipPath;

                foreach (var m in result.Metrics)
                    Metrics.Add(m);

                if (AutoModifyConsoleApp && File.Exists(GeneratedProjectPath))
                {
                    var textCol = AvailableColumns.FirstOrDefault(c => c.Index != Config.LabelCol);
                    string textColName = textCol?.RawHeader ?? "col0";
                    GeneratedAppModifier.ModifyProgramCs(GeneratedProjectPath, Config.ModelName, textColName);
                    AppendLog($"\n[INFO] Сгенерированный Program.cs успешно модифицирован для приёма аргументов.");
                }

                IsTrained = true;
                AppendLog("\n[ГОТОВО] Обучение завершено. Можно выполнять классификацию текста.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsTraining = false;
            }
        }

        private async Task ClassifyAsync()
        {
            IsPredicting = true;
            PredictedClass = "Вычисление...";
            PredictedProbability = "—";

            try
            {
                PredictionResult pred;
                bool canUseConsole = File.Exists(GeneratedProjectPath) && GeneratedAppModifier.IsModified(GeneratedProjectPath);

                if (!canUseConsole && File.Exists(GeneratedProjectPath))
                {
                    var ask = MessageBox.Show(
                        "Сгенерированное консольное приложение ещё не модифицировано.\n\nНажмите «Да», чтобы модифицировать его сейчас, или «Нет», чтобы использовать встроенный PredictionEngine.",
                        "Модификация приложения",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (ask == MessageBoxResult.Cancel) return;

                    if (ask == MessageBoxResult.Yes)
                    {
                        var textCol = AvailableColumns.FirstOrDefault(c => c.Index != Config.LabelCol);
                        GeneratedAppModifier.ModifyProgramCs(GeneratedProjectPath, Config.ModelName, textCol?.RawHeader ?? "col0");
                        canUseConsole = true;
                    }
                }

                if (canUseConsole)
                {
                    pred = await _predictionClient.PredictViaConsoleAppAsync(GeneratedProjectPath, InputText);
                }
                else if (File.Exists(_modelZipPath))
                {
                    var textCol = AvailableColumns.FirstOrDefault(c => c.Index != Config.LabelCol);
                    pred = await Task.Run(() => _predictionClient.PredictFallbackWithMlNet(_modelZipPath, InputText, textCol?.RawHeader ?? "col0"));
                }
                else
                {
                    throw new FileNotFoundException("Не найден ни консольный проект, ни файл модели (.zip).");
                }

                PredictedClass = pred.PredictedLabel;
                PredictedProbability = pred.Probability.HasValue ? $"{pred.Probability.Value:P2} ({pred.Probability.Value:F4})" : "Н/Д";
            }
            catch (Exception ex)
            {
                PredictedClass = "Ошибка";
                MessageBox.Show(ex.Message, "Ошибка классификации", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsPredicting = false;
            }
        }

        private void OpenOutputFolder()
        {
            if (Directory.Exists(Config.OutputPath))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Config.OutputPath}\"") { UseShellExecute = true });
        }

        private void AppendLog(string line)
        {
            Application.Current.Dispatcher.Invoke(() => ConsoleLog += line + Environment.NewLine);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
