using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using DirectoryCopier.Models;
using DirectoryCopier.Services;

namespace DirectoryCopier.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _sourcePath = "";
        private string _destinationPath = "";
        private int _threadCount = 4;
        private bool _isRunning;
        private int _progressValue;
        private string _progressText = "0%";
        private string _status = "Ожидание";
        private long _totalBytes;
        private long _copiedBytes;
        private string _currentFile = "";
        private CancellationTokenSource? _cancellationTokenSource;

        private ObservableCollection<LogEntry> _logEntries = new ObservableCollection<LogEntry>();

        public string SourcePath
        {
            get => _sourcePath;
            set
            {
                _sourcePath = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string DestinationPath
        {
            get => _destinationPath;
            set
            {
                _destinationPath = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public int ThreadCount
        {
            get => _threadCount;
            set
            {
                _threadCount = Math.Clamp(value, 1, 16);
                OnPropertyChanged();
            }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                _isRunning = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public int ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); }
        }

        public string ProgressText
        {
            get => _progressText;
            set { _progressText = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public long TotalBytes
        {
            get => _totalBytes;
            set { _totalBytes = value; OnPropertyChanged(); }
        }

        public long CopiedBytes
        {
            get => _copiedBytes;
            set { _copiedBytes = value; OnPropertyChanged(); }
        }

        public string CurrentFile
        {
            get => _currentFile;
            set { _currentFile = value; OnPropertyChanged(); }
        }

        public ObservableCollection<LogEntry> LogEntries
        {
            get => _logEntries;
            set { _logEntries = value; OnPropertyChanged(); }
        }

        public ICommand StartCommand { get; }
        public ICommand BrowseSourceCommand { get; }
        public ICommand BrowseDestinationCommand { get; }
        public ICommand CancelCommand { get; }

        public MainViewModel()
        {
            StartCommand = new RelayCommand(_ => StartCopy(), _ => CanStart());
            BrowseSourceCommand = new RelayCommand(_ => BrowseSource());
            BrowseDestinationCommand = new RelayCommand(_ => BrowseDestination());
            CancelCommand = new RelayCommand(_ => CancelCopy(), _ => IsRunning);
        }

        private bool CanStart()
        {
            if (IsRunning) return false;
            if (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(DestinationPath)) return false;
            if (!Directory.Exists(SourcePath)) return false;

            var src = Path.GetFullPath(SourcePath).TrimEnd('\\', '/');
            var dst = Path.GetFullPath(DestinationPath).TrimEnd('\\', '/');

            if (string.Equals(src, dst, StringComparison.OrdinalIgnoreCase)) return false;
            if (dst.StartsWith(src + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;

            return true;
        }

        private void BrowseSource()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Выберите исходную директорию"
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SourcePath = dialog.SelectedPath;
            }
        }

        private void BrowseDestination()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Выберите директорию для копирования"
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DestinationPath = dialog.SelectedPath;
            }
        }

        private async void StartCopy()
        {
            if (!CanStart()) return;

            LogEntries.Clear();
            IsRunning = true;
            Status = "Копирование...";
            ProgressValue = 0;
            ProgressText = "0%";
            CopiedBytes = 0;
            TotalBytes = 0;
            CurrentFile = "";

            AddLog("Сканирование файлов...", LogType.Info);
            _cancellationTokenSource = new CancellationTokenSource();

            var progress = new Progress<CopyProgress>(p =>
            {
                if (p.TotalBytes > 0)
                {
                    TotalBytes = p.TotalBytes;
                    CopiedBytes = p.CopiedBytes;
                }

                if (!string.IsNullOrEmpty(p.CurrentFile))
                {
                    CurrentFile = p.CurrentFile;
                    AddLog(p.CurrentFile, LogType.Info);
                }

                if (!string.IsNullOrEmpty(p.Error))
                {
                    AddLog($"Ошибка: {p.Error}", LogType.Error);
                }

                if (p.TotalFiles > 0)
                {
                    ProgressValue = (int)((double)p.CopiedFiles / p.TotalFiles * 100);
                    ProgressText = $"{p.CopiedFiles} / {p.TotalFiles} ({ProgressValue}%)";
                }

                if (p.IsCompleted)
                {
                    IsRunning = false;
                    Status = "Завершено!";
                    ProgressText = "100%";
                    ProgressValue = 100;
                    CurrentFile = "";
                    AddLog("Копирование завершено!", LogType.Success);
                }
            });

            try
            {
                AddLog($"Начало копирования. Потоков: {ThreadCount}", LogType.Info);
                await CopyEngine.CopyDirectoryAsync(
                    SourcePath,
                    DestinationPath,
                    ThreadCount,
                    progress,
                    _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                IsRunning = false;
                Status = "Отменено";
                AddLog("Копирование отменено пользователем", LogType.Warning);
            }
            catch (Exception ex)
            {
                IsRunning = false;
                Status = "Ошибка";
                AddLog($"Ошибка: {ex.Message}", LogType.Error);
            }
        }

        private void CancelCopy()
        {
            _cancellationTokenSource?.Cancel();
            Status = "Отмена...";
        }

        private void AddLog(string message, LogType type)
        {
            if (LogEntries.Count > 200)
                LogEntries.RemoveAt(LogEntries.Count - 1);
            LogEntries.Insert(0, new LogEntry(message, type));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
