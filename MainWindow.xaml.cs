using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;

namespace PixelMpPlayer;

public partial class MainWindow : Window
{
    private MotionPhotoData? _currentMotionPhoto;
    private string? _tempVideoFile;
    private bool _isVideoMode;
    
    public MainWindow()
    {
        InitializeComponent();
        SetupKeyboardShortcuts();
        InitializeWebView();
    }
    
    private void SetupKeyboardShortcuts()
    {
        var openCommand = new RoutedCommand();
        openCommand.InputGestures.Add(new KeyGesture(Key.O, ModifierKeys.Control));
        CommandBindings.Add(new CommandBinding(openCommand, OpenFile_Click));
    }

    private async void InitializeWebView()
    {
        try
        {
            await VideoDisplay.EnsureCoreWebView2Async();
            ConfigureWebViewSettings();
            SetupCustomVideoScheme();
        }
        catch (Exception ex)
        {
            ShowError($"WebView2の初期化に失敗しました: {ex.Message}");
        }
    }

    private void ConfigureWebViewSettings()
    {
        var settings = VideoDisplay.CoreWebView2.Settings;
        settings.AreHostObjectsAllowed = true;
        settings.IsPasswordAutosaveEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        
        VideoDisplay.CoreWebView2.PermissionRequested += (sender, args) =>
        {
            args.State = CoreWebView2PermissionState.Allow;
        };
    }

    private void SetupCustomVideoScheme()
    {
        VideoDisplay.CoreWebView2.AddWebResourceRequestedFilter("https://app.local/*", CoreWebView2WebResourceContext.All);
        VideoDisplay.CoreWebView2.WebResourceRequested += HandleVideoResourceRequest;
    }

    private void HandleVideoResourceRequest(object? sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        try
        {
            var uri = args.Request.Uri;
            if (uri.StartsWith("https://app.local/video/"))
            {
                var fileName = uri.Substring("https://app.local/video/".Length);
                var filePath = Path.Combine(Path.GetTempPath(), fileName);
                
                if (File.Exists(filePath))
                {
                    var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    var response = VideoDisplay.CoreWebView2.Environment.CreateWebResourceResponse(
                        fileStream, 200, "OK", "Content-Type: video/mp4\r\nAccess-Control-Allow-Origin: *");
                    args.Response = response;
                }
                else
                {
                    var errorResponse = VideoDisplay.CoreWebView2.Environment.CreateWebResourceResponse(
                        null, 404, "Not Found", "");
                    args.Response = errorResponse;
                }
            }
        }
        catch (Exception ex)
        {
            var errorResponse = VideoDisplay.CoreWebView2.Environment.CreateWebResourceResponse(
                null, 500, "Internal Server Error", $"Content-Type: text/plain\r\n\r\nError: {ex.Message}");
            args.Response = errorResponse;
        }
    }
    
    public void LoadFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            ShowError("ファイルが見つかりません。");
            return;
        }
        
        var motionPhoto = MotionPhotoParser.ParseMotionPhoto(filePath);
        if (motionPhoto == null)
        {
            ShowError("このファイルはMotion Photoではないか、サポートされていない形式です。");
            return;
        }
        
        LoadMotionPhoto(motionPhoto, filePath);
    }

    private void LoadMotionPhoto(MotionPhotoData motionPhoto, string filePath)
    {
        _currentMotionPhoto = motionPhoto;
        
        UpdateUIForMotionPhotoLoaded();
        UpdateFileInfoDisplay(filePath, motionPhoto);
        EnableButtons();
        ShowImage();
        
        Title = $"Pixel Motion Photo Player - {Path.GetFileName(filePath)}";
    }

    private void UpdateUIForMotionPhotoLoaded()
    {
        WelcomePanel.Visibility = Visibility.Collapsed;
        ControlPanel.Visibility = Visibility.Visible;
    }

    private void UpdateFileInfoDisplay(string filePath, MotionPhotoData motionPhoto)
    {
        var fileInfo = new FileInfo(filePath);
        var videoStatus = motionPhoto.HasVideo ? 
            $"動画あり ({FormatFileSize(motionPhoto.Mp4Data.Length)}, {motionPhoto.VideoSource})" : 
            "動画なし";
        FileInfoText.Text = $"{fileInfo.Name} ({FormatFileSize(fileInfo.Length)}) - {videoStatus}";
    }

    private void EnableButtons()
    {
        ShowImageButton.IsEnabled = true;
        ShowVideoButton.IsEnabled = true;
        PlayPauseButton.IsEnabled = false;
        StopButton.IsEnabled = false;
    }
    
    private void ShowImage()
    {
        if (_currentMotionPhoto == null) return;
        
        try
        {
            var bitmap = CreateBitmapFromBytes(_currentMotionPhoto.JpegData);
            DisplayImage(bitmap);
            SetImageModeUI();
        }
        catch (Exception ex)
        {
            ShowError($"画像の表示中にエラーが発生しました: {ex.Message}");
        }
    }

    private BitmapImage CreateBitmapFromBytes(byte[] imageData)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = new MemoryStream(imageData);
        bitmap.EndInit();
        return bitmap;
    }

    private void DisplayImage(BitmapImage bitmap)
    {
        ImageDisplay.Source = bitmap;
        ImageDisplay.Visibility = Visibility.Visible;
        VideoContainer.Visibility = Visibility.Collapsed;
    }

    private void SetImageModeUI()
    {
        _isVideoMode = false;
        ShowImageButton.IsEnabled = false;
        ShowVideoButton.IsEnabled = true;
        SetVideoControlsEnabled(false);
    }

    private void SetVideoModeUI()
    {
        VideoContainer.Visibility = Visibility.Visible;
        ImageDisplay.Visibility = Visibility.Collapsed;
        VideoStatusText.Visibility = Visibility.Visible;
        
        _isVideoMode = true;
        ShowImageButton.IsEnabled = true;
        ShowVideoButton.IsEnabled = false;
        SetVideoControlsEnabled(false);
        PlayPauseButton.Content = "▶️ 再生";
    }

    private void SetVideoControlsEnabled(bool enabled)
    {
        PlayPauseButton.IsEnabled = enabled;
        StopButton.IsEnabled = enabled;
    }
    
    private void ShowVideo()
    {
        if (_currentMotionPhoto == null) return;
        
        try
        {
            PrepareVideoFile();
            var html = CreateVideoPlayerHtml();
            DisplayVideo(html);
            SetVideoModeUI();
        }
        catch (Exception ex)
        {
            ShowError($"動画の表示中にエラーが発生しました: {ex.Message}\n\n" +
                     "このMotion Photoには有効な動画データが含まれていない可能性があります。");
            ShowImage();
        }
    }

    private void PrepareVideoFile()
    {
        CleanupTempFile();
        _tempVideoFile = Path.GetTempFileName() + ".mp4";
        File.WriteAllBytes(_tempVideoFile, _currentMotionPhoto!.Mp4Data);
        
        var fileInfo = new FileInfo(_tempVideoFile);
        if (fileInfo.Length < 50)
        {
            throw new InvalidOperationException($"動画データが無効です（サイズ: {fileInfo.Length} bytes）");
        }
    }

    private string CreateVideoPlayerHtml()
    {
        var fileName = Path.GetFileName(_tempVideoFile);
        var customUrl = $"https://app.local/video/{fileName}";
        
        return $@"
        <!DOCTYPE html>
        <html>
        <head>
            <style>
                html, body {{ 
                    margin: 0; 
                    padding: 0; 
                    height: 100%; 
                    overflow: hidden; 
                    background: transparent; 
                }}
                video {{ 
                    width: 100%; 
                    height: 100vh; 
                    object-fit: contain; 
                    display: block;
                }}
            </style>
        </head>
        <body>
            <video id='videoPlayer' controls preload='metadata' src='{customUrl}'>
                お使いのブラウザは動画の再生をサポートしていません。
            </video>
            <script>
                var video = document.getElementById('videoPlayer');
                
                video.onloadedmetadata = function() {{
                    window.chrome.webview.postMessage('metadata-loaded');
                }};
                
                video.onloadeddata = function() {{
                    window.chrome.webview.postMessage('loaded');
                }};
                
                video.oncanplay = function() {{
                    window.chrome.webview.postMessage('can-play');
                }};
                
                video.onerror = function(e) {{
                    var errorMsg = 'Error: ' + video.error.code + ' - ' + video.error.message;
                    window.chrome.webview.postMessage('error:' + errorMsg);
                }};
                
                video.load();
            </script>
        </body>
        </html>";
    }

    private void DisplayVideo(string html)
    {
        VideoDisplay.NavigateToString(html);
        
        // Ensure event handler is only registered once
        VideoDisplay.WebMessageReceived -= VideoDisplay_WebMessageReceived;
        VideoDisplay.WebMessageReceived += VideoDisplay_WebMessageReceived;
    }

    
    private void CleanupTempFile()
    {
        if (_tempVideoFile != null && File.Exists(_tempVideoFile))
        {
            try
            {
                File.Delete(_tempVideoFile);
            }
            catch { }
            _tempVideoFile = null;
        }
    }
    
    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB" };
        double number = bytes;
        int counter = 0;
        
        while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }
        
        return $"{number:n1}{suffixes[counter]}";
    }

    private void ShowError(string message) => 
        MessageBox.Show(message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);

    private void ShowWarning(string message) => 
        MessageBox.Show(message, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);

    private void ShowInfo(string message) => 
        MessageBox.Show(message, "情報", MessageBoxButton.OK, MessageBoxImage.Information);
    
    // Event Handlers
    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Motion Photo ファイルを選択",
            Filter = "JPEG Files (*.jpg;*.jpeg)|*.jpg;*.jpeg|All Files (*.*)|*.*",
            FilterIndex = 1
        };
        
        if (openFileDialog.ShowDialog() == true)
        {
            LoadFile(openFileDialog.FileName);
        }
    }
    
    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private void About_Click(object sender, RoutedEventArgs e)
    {
        ShowInfo("Pixel Motion Photo Player v1.0\n\n" +
                "Google Pixel の Motion Photo を Windows で表示するアプリケーションです。\n\n" +
                "対応形式：\n" +
                "• 従来のMotion Photo（JPEG+MP4形式）\n" +
                "• 新しいMotion Photo（メタデータ形式）\n\n" +
                "注意：新しい形式では動画が別ファイルとして保存される場合があります。");
    }
    
    private void ShowImage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShowImage();
        }
        catch (Exception ex)
        {
            ShowError($"静止画表示エラー: {ex.Message}");
        }
    }
    
    private void ShowVideo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_currentMotionPhoto == null)
            {
                ShowWarning("Motion Photoが読み込まれていません。");
                return;
            }

            // 動画データが存在するかチェック（より柔軟な条件）
            if (_currentMotionPhoto.Mp4Data.Length > 100)
            {
                ShowVideo();
            }
            else
            {
                var fileName = Path.GetFileName(_currentMotionPhoto.OriginalFilePath ?? "");
                var message = "このMotion Photoには動画データが見つかりません。\n\n";
                
                // デバッグ情報を含める
                message += $"検出された動画データサイズ: {_currentMotionPhoto.Mp4Data.Length} bytes\n";
                message += $"HasVideo フラグ: {_currentMotionPhoto.HasVideo}\n";
                message += $"動画ソース: {_currentMotionPhoto.VideoSource}\n\n";
                
                if (fileName.Contains(".MP.COVER"))
                {
                    message += "このファイルは「.MP.COVER」形式です。\n" +
                              "Google Pixel の新しい Motion Photo では、この形式は静止画（カバー画像）のみを含み、\n" +
                              "動画データは以下の場所に保存されています：\n\n" +
                              "• Google フォトのクラウドストレージ\n" +
                              "• デバイスの別の場所\n" +
                              "• 元のファイルが分割されている\n\n" +
                              "完全な Motion Photo を取得するには、Google フォトアプリから\n" +
                              "「元の品質でダウンロード」を試してください。";
                }
                else
                {
                    message += "考えられる原因：\n" +
                              "• Motion Photo の動画部分が別ファイルとして保存されている\n" +
                              "• ファイルが不完全または破損している\n" +
                              "• サポートされていない形式\n\n" +
                              "デバッグ機能を使用して動画データを確認してください。";
                }
                
                ShowInfo(message);
            }
        }
        catch (Exception ex)
        {
            ShowError($"動画表示エラー: {ex.Message}");
        }
    }
    
    private async void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_isVideoMode || VideoDisplay.CoreWebView2 == null) 
            {
                ShowWarning("動画モードではありません。");
                return;
            }
            
            var script = @"
                var video = document.getElementById('videoPlayer');
                if (video.paused) {
                    video.play();
                    'playing';
                } else {
                    video.pause();
                    'paused';
                }";
            
            var result = await VideoDisplay.CoreWebView2.ExecuteScriptAsync(script);
            
            if (result.Contains("playing"))
            {
                PlayPauseButton.Content = "⏸️ 一時停止";
            }
            else
            {
                PlayPauseButton.Content = "▶️ 再生";
            }
        }
        catch (Exception ex)
        {
            ShowError($"動画の再生中にエラーが発生しました: {ex.Message}");
        }
    }
    
    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_isVideoMode || VideoDisplay.CoreWebView2 == null) 
            {
                ShowWarning("動画モードではありません。");
                return;
            }
            
            var script = @"
                var video = document.getElementById('videoPlayer');
                video.pause();
                video.currentTime = 0;";
            
            await VideoDisplay.CoreWebView2.ExecuteScriptAsync(script);
            PlayPauseButton.Content = "▶️ 再生";
        }
        catch (Exception ex)
        {
            ShowError($"動画の停止中にエラーが発生しました: {ex.Message}");
        }
    }
    
    private void VideoDisplay_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var message = e.TryGetWebMessageAsString();
        
        switch (message)
        {
            case "loaded" or "can-play":
                OnVideoLoaded();
                break;
            case var error when error?.StartsWith("error:") == true:
                OnVideoError(error.Substring(6));
                break;
        }
    }

    private void OnVideoLoaded()
    {
        VideoStatusText.Visibility = Visibility.Collapsed;
        SetVideoControlsEnabled(true);
    }

    private void OnVideoError(string errorDetail)
    {
        ShowError($"動画の読み込みに失敗しました。\n\n詳細: {errorDetail}\n\n" +
                 "WebView2がこの動画形式をサポートしていない可能性があります。");
        ShowImage();
    }
    
    // Drag & Drop
    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? 
            DragDropEffects.Copy : DragDropEffects.None;
    }
    
    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                LoadFile(files[0]);
            }
        }
    }
    
    private void Debug_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMotionPhoto == null)
        {
            ShowWarning("Motion Photoが読み込まれていません。");
            return;
        }
        
        var debug = $"Motion Photo 解析結果:\n\n" +
                   $"JPEG データサイズ: {FormatFileSize(_currentMotionPhoto.JpegData.Length)}\n" +
                   $"動画データサイズ: {FormatFileSize(_currentMotionPhoto.Mp4Data.Length)}\n" +
                   $"動画あり: {_currentMotionPhoto.HasVideo}\n" +
                   $"動画ソース: {_currentMotionPhoto.VideoSource}\n" +
                   $"コンパニオンファイル: {_currentMotionPhoto.CompanionVideoPath ?? "なし"}\n\n";
        
        if (_currentMotionPhoto.Mp4Data.Length > 0)
        {
            try
            {
                var tempVideoFile = Path.GetTempFileName() + ".mp4";
                File.WriteAllBytes(tempVideoFile, _currentMotionPhoto.Mp4Data);
                
                debug += $"動画ファイルを作成しました:\n{tempVideoFile}\n\n" +
                         "このファイルを既定のメディアプレイヤーで開きますか？";
                
                var result = MessageBox.Show(debug, "デバッグ情報", 
                                           MessageBoxButton.YesNo, MessageBoxImage.Information);
                
                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = tempVideoFile,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                debug += $"エラー: {ex.Message}";
                ShowError(debug);
            }
        }
        else
        {
            ShowInfo(debug + "動画データが見つかりません。");
        }
    }
    
    protected override void OnClosed(EventArgs e)
    {
        CleanupTempFile();
        base.OnClosed(e);
    }
}