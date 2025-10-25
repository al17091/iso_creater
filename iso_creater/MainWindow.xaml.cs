using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using DiscUtils;
using DiscUtils.Iso9660;
using System.Collections.Generic; // 追加

namespace iso_creater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _cts;
        private bool _isRunning = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BrowseSourceFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "ソースフォルダを選択してください",
                UseDescriptionForTitle = true
            };

            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedPath))
            {
                SourceFolderPathTextBox.Text = dlg.SelectedPath;

                // ボリュームラベルが空の場合は選択したフォルダ名を設定
                if (string.IsNullOrWhiteSpace(VolumeLabelTextBox.Text))
                {
                    try
                    {
                        var folderName = Path.GetFileName(dlg.SelectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        VolumeLabelTextBox.Text = string.IsNullOrEmpty(folderName) ? dlg.SelectedPath : folderName;
                    }
                    catch
                    {
                        VolumeLabelTextBox.Text = dlg.SelectedPath;
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
            if (!_isRunning)
            {
                // 実行開始
                if (string.IsNullOrWhiteSpace(SourceFolderPathTextBox.Text))
                {
                    System.Windows.MessageBox.Show("ソースフォルダを選択してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(SaveFilePathTextBox.Text))
                {
                    System.Windows.MessageBox.Show("保存先を指定してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _cts = new CancellationTokenSource();
                _isRunning = true;
                CreateIsoButton.Content = "キャンセル";
                SetControlsEnabled(false);

                try
                {
                    // 実際の ISO 作成処理はここに実装します。
                    // 例: DiscUtils などのライブラリを使ってディレクトリを ISO 化。
                    // また除外フィルタは .gitignore 様式をパースして適用する必要があります（未実装）。

                    // CreateIsoAsync: 指定フォルダから UDF 形式の ISO を作成します。
                    // 注意: この実装は一般的な DiscUtils の使い方を想定しています。
                    // 実際の API 名やシグネチャは使用中の DiscUtils バージョンによって異なる場合があります。
                    // 必要に応じて Build / AddFile の呼び出しをプロジェクトの DiscUtils バージョンに合わせて調整してください。
                    await CreateIsoAsync(SourceFolderPathTextBox.Text, SaveFilePathTextBox.Text, VolumeLabelTextBox.Text, null, _cts.Token);

                    System.Windows.MessageBox.Show("ISO 作成が完了しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (OperationCanceledException)
                {
                    System.Windows.MessageBox.Show("処理はキャンセルされました。", "キャンセル", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"エラー: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    _isRunning = false;
                    _cts?.Dispose();
                    _cts = null;
                    CreateIsoButton.Content = "ISO作成";
                    SetControlsEnabled(true);
                    CreationProgressBar.Value = 0;
                }
            }
            else
            {
                // キャンセル要求
                _cts?.Cancel();
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            BrowseSourceFolderButton.IsEnabled = enabled;
            BrowseSaveFileButton.IsEnabled = enabled;
            VolumeLabelTextBox.IsEnabled = enabled;
            FileSystemComboBox.IsEnabled = enabled;
            ExcludePatternsTextBox.IsEnabled = enabled;
        }

        // 指定フォルダから UDF 形式の ISO を作成します。
        // 注意: この実装は一般的な DiscUtils の使い方を想定しています。
        // 実際の API 名やシグネチャは使用中の DiscUtils バージョンによって異なる場合があります。
        // 必要に応じて Build / AddFile の呼び出しをプロジェクトの DiscUtils バージョンに合わせて調整してください。
        private async Task CreateIsoAsync(string sourceDirectory, string outputIsoFile, string volumeLabel, IProgress<double> progress, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory)) throw new ArgumentException("sourceDirectory is required.", nameof(sourceDirectory));
            if (string.IsNullOrWhiteSpace(outputIsoFile)) throw new ArgumentException("outputIsoFile is required.", nameof(outputIsoFile));
            if (!Directory.Exists(sourceDirectory)) throw new DirectoryNotFoundException(sourceDirectory);

            var allFiles = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories);
            var fileList = new System.Collections.Generic.List<string>(allFiles);
            int total = fileList.Count;
            int processed = 0;

            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                var outDir = Path.GetDirectoryName(outputIsoFile);
                if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }

                var fileStreams = new List<Stream>(); // ストリームを保持するリスト
                try
                {
                    using (var outStream = File.Create(outputIsoFile))
                    {
                        var isoBuilder = new CDBuilder
                        {
                            VolumeIdentifier = volumeLabel
                        };

                        foreach (var fullPath in fileList)
                        {
                            token.ThrowIfCancellationRequested();
                            var relativePath = Path.GetRelativePath(sourceDirectory, fullPath).Replace(Path.DirectorySeparatorChar, '/');
                            
                            var fs = File.OpenRead(fullPath);
                            fileStreams.Add(fs); // リストに追加して後で破棄する
                            isoBuilder.AddFile(relativePath, fs);
                            
                            processed++;
                            progress?.Report(total == 0 ? 1.0 : (double)processed / total);
                        }

                        token.ThrowIfCancellationRequested();
                        isoBuilder.Build(outStream);
                    }
                }
                finally
                {
                    // すべてのファイルストリームを確実に破棄する
                    foreach (var stream in fileStreams)
                    {
                        stream.Dispose();
                    }
                }

            }, token);
        }

        // TODO: .gitignore 構文パーサーを実装またはライブラリを導入してください。
        // 現在は単純に改行で分割した文字列配列を返します（実運用ではワイルドカードや除外ルール適用が必要）。
        private string[] ParseIgnorePatterns(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].Trim();
            }
            return lines;
        }
    }
}