using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace iso_creater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private readonly IsoCreationService _isoCreationService;

        public MainWindow()
        {
            InitializeComponent();
            _isoCreationService = new IsoCreationService();
        }

        private void BrowseSourceFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "ソースフォルダを選択してください"
            };

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrEmpty(dlg.FileName))
            {
                SourceFolderPathTextBox.Text = dlg.FileName;

                if (string.IsNullOrWhiteSpace(VolumeLabelTextBox.Text))
                {
                    try
                    {
                        var folderName = Path.GetFileName(dlg.FileName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        VolumeLabelTextBox.Text = string.IsNullOrEmpty(folderName) ? dlg.FileName : folderName;
                    }
                    catch
                    {
                        VolumeLabelTextBox.Text = dlg.FileName;
                    }
                }
            }
        }

        private void BrowseSaveFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "ISO を保存",
                Filter = "ISO イメージ (*.iso)|*.iso|すべてのファイル (*.*)|*.*",
                DefaultExt = "iso",
                AddExtension = true,
                FileName = "image.iso"
            };

            if (dlg.ShowDialog(this) == true)
            {
                SaveFilePathTextBox.Text = dlg.FileName;
            }
        }

        private async void CreateIsoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                _cts?.Cancel();
                return;
            }

            if (!ValidateInput()) return;

            _cts = new CancellationTokenSource();
            _isRunning = true;
            UpdateUiForRunningState(true);

            try
            {
                var sourceDirectory = SourceFolderPathTextBox.Text;
                var outputFile = SaveFilePathTextBox.Text;
                var volumeLabel = VolumeLabelTextBox.Text;
                var fileSystem = (IsoFileSystem)FileSystemComboBox.SelectedIndex;
                var excludePatterns = ParseIgnorePatterns(ExcludePatternsTextBox.Text);

                var progress = new Progress<double>(p => CreationProgressBar.Value = p);

                await _isoCreationService.CreateIsoAsync(
                    sourceDirectory,
                    outputFile,
                    volumeLabel,
                    fileSystem,
                    excludePatterns,
                    progress,
                    _cts.Token);

                MessageBox.Show("ISO 作成が完了しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("処理はキャンセルされました。", "キャンセル", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"エラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isRunning = false;
                _cts?.Dispose();
                _cts = null;
                UpdateUiForRunningState(false);
                CreationProgressBar.Value = 0;
            }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(SourceFolderPathTextBox.Text))
            {
                MessageBox.Show("ソースフォルダを選択してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(SaveFilePathTextBox.Text))
            {
                MessageBox.Show("保存先を指定してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void UpdateUiForRunningState(bool isRunning)
        {
            CreateIsoButton.Content = isRunning ? "キャンセル" : "ISO作成";
            BrowseSourceFolderButton.IsEnabled = !isRunning;
            BrowseSaveFileButton.IsEnabled = !isRunning;
            VolumeLabelTextBox.IsEnabled = !isRunning;
            FileSystemComboBox.IsEnabled = !isRunning;
            ExcludePatternsTextBox.IsEnabled = !isRunning;
        }

        private string[] ParseIgnorePatterns(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();

            return text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(line => line.Trim())
                       .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                       .ToArray();
        }
    }
}