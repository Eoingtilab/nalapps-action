using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace NalaApps.Action.App;

public partial class MainWindow : Window
{
    private const int WmHotKey = 0x0312;
    private const int EmergencyHotKeyId = 0x4E41;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkF12 = 0x7B;
    private const int MaxSteps = 50000;

    private static readonly HashSet<string> SupportedStepTypes = new(StringComparer.Ordinal)
    {
        "MouseMove", "MouseButton", "MouseWheel", "KeyPress", "TextInput", "Wait"
    };

    private readonly ObservableCollection<ActionStepItem> _steps = [];
    private readonly WindowsInputRecorder _recorder;
    private bool _isRecording;
    private bool _isPlaying;
    private bool _isDirty;
    private bool _suppressDirtyTracking;
    private CancellationTokenSource? _playbackCancellation;
    private HwndSource? _windowSource;
    private IntPtr _windowHandle;
    private bool _hotKeyRegistered;

    public MainWindow()
    {
        InitializeComponent();
        StepsList.ItemsSource = _steps;
        _recorder = new WindowsInputRecorder(OnRecordedStep);
        SourceInitialized += MainWindow_SourceInitialized;
        ActionNameTextBox.TextChanged += (_, _) =>
        {
            if (!_suppressDirtyTracking) _isDirty = true;
        };
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(_windowHandle);
        _windowSource?.AddHook(WindowMessageHook);
        _hotKeyRegistered = RegisterHotKey(_windowHandle, EmergencyHotKeyId, ModControl | ModShift | ModNoRepeat, VkF12);
        if (!_hotKeyRegistered)
        {
            SetStatus("준비 · Ctrl+Shift+F12 긴급 중지 단축키를 다른 프로그램이 사용 중입니다.");
        }
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && wParam.ToInt32() == EmergencyHotKeyId)
        {
            StopAll("긴급 중지했습니다.");
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void NewAction_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges()) return;

        StopAll("준비");
        _steps.Clear();
        SetActionName("새 액션");
        _isDirty = false;
        SetStatus("새 액션을 만들었습니다.");
        UpdateStepCount();
    }

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            StopPlayback();
            return;
        }

        if (_isRecording)
        {
            StopRecording();
            return;
        }

        if (_steps.Count >= MaxSteps)
        {
            MessageBox.Show($"액션은 최대 {MaxSteps:N0}단계까지 기록할 수 있습니다.", "날라액션", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _recorder.Start();
            _isRecording = true;
            RecordButton.Content = "■  녹화 중지";
            SetStatus("녹화 중입니다. 마우스 이동·클릭·휠·키보드 입력을 기록합니다.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "녹화 시작 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("녹화를 시작하지 못했습니다.");
        }
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            StopPlayback();
            return;
        }

        if (_isRecording) StopRecording();

        if (_steps.Count == 0)
        {
            MessageBox.Show("실행할 단계가 없습니다.", "날라액션", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            ValidateSteps(_steps);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "액션 검증 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _playbackCancellation?.Cancel();
        _playbackCancellation?.Dispose();
        _playbackCancellation = new CancellationTokenSource();
        _isPlaying = true;
        PlayButton.Content = "■  중지";
        RecordButton.IsEnabled = false;
        SetStatus("2초 후 실행합니다. 대상 프로그램을 활성화하세요.");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), _playbackCancellation.Token);
            await WindowsInputPlayer.PlayAsync(_steps.ToList(), _playbackCancellation.Token, step =>
            {
                Dispatcher.Invoke(() =>
                {
                    StepsList.SelectedItem = step;
                    StepsList.ScrollIntoView(step);
                    SetStatus($"실행 중: {step.Order:00} {step.Name} · {step.BuildDetail()}");
                });
            });
            SetStatus("실행이 완료되었습니다.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("실행을 중지했습니다.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "실행 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("실행 중 오류가 발생했습니다.");
        }
        finally
        {
            _isPlaying = false;
            PlayButton.Content = "▶  실행";
            RecordButton.IsEnabled = true;
            _playbackCancellation?.Dispose();
            _playbackCancellation = null;
        }
    }

    private void StopRecording()
    {
        _recorder.Stop();
        _isRecording = false;
        RecordButton.Content = "●  녹화 시작";
        SetStatus($"녹화를 중지했습니다. {_steps.Count}개 단계가 기록되었습니다.");
    }

    private void StopPlayback()
    {
        _playbackCancellation?.Cancel();
        _isPlaying = false;
        PlayButton.Content = "▶  실행";
        RecordButton.IsEnabled = true;
        SetStatus("실행 중지를 요청했습니다.");
    }

    private void StopAll(string status)
    {
        _recorder.Stop();
        _playbackCancellation?.Cancel();
        _isRecording = false;
        _isPlaying = false;
        RecordButton.Content = "●  녹화 시작";
        PlayButton.Content = "▶  실행";
        RecordButton.IsEnabled = true;
        SetStatus(status);
    }

    private void OnRecordedStep(ActionStepItem step)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_steps.Count >= MaxSteps)
            {
                StopRecording();
                MessageBox.Show($"최대 {MaxSteps:N0}단계에 도달하여 녹화를 중지했습니다.", "날라액션", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            step.Order = _steps.Count + 1;
            step.Detail = step.BuildDetail();
            _steps.Add(step);
            _isDirty = true;
            UpdateStepCount();
            StepsList.SelectedItem = step;
            StepsList.ScrollIntoView(step);
            SetStatus($"기록 중: {step.Name} · {step.Detail}");
        });
    }

    private void AddStep_Click(object sender, RoutedEventArgs e)
    {
        if (_steps.Count >= MaxSteps)
        {
            MessageBox.Show($"액션은 최대 {MaxSteps:N0}단계까지 추가할 수 있습니다.", "날라액션", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ActionStepItem newStep = new()
        {
            Order = _steps.Count + 1,
            Type = "Wait",
            Name = "대기 시간",
            Detail = "1000ms",
            Value = 1000,
            DelayBeforeMs = 0
        };
        StepEditorWindow editor = new(newStep) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            ActionStepItem step = editor.Step;
            step.Order = _steps.Count + 1;
            step.Detail = step.BuildDetail();
            ValidateStep(step);
            _steps.Add(step);
            _isDirty = true;
            UpdateStepCount();
            StepsList.SelectedItem = step;
            SetStatus("새 단계를 추가했습니다.");
        }
    }

    private void EditStep_Click(object sender, RoutedEventArgs e) => EditSelectedStep();
    private void StepsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => EditSelectedStep();

    private void EditSelectedStep()
    {
        if (StepsList.SelectedItem is not ActionStepItem selected)
        {
            MessageBox.Show("편집할 단계를 선택하세요.", "단계 편집", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int index = _steps.IndexOf(selected);
        StepEditorWindow editor = new(selected) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            ActionStepItem edited = editor.Step;
            edited.Order = selected.Order;
            edited.Detail = edited.BuildDetail();
            ValidateStep(edited);
            _steps[index] = edited;
            _isDirty = true;
            StepsList.SelectedItem = edited;
            SetStatus("선택한 단계를 수정했습니다.");
        }
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int direction)
    {
        if (StepsList.SelectedItem is not ActionStepItem selected) return;
        int current = _steps.IndexOf(selected);
        int target = current + direction;
        if (target < 0 || target >= _steps.Count) return;
        _steps.Move(current, target);
        RenumberSteps();
        _isDirty = true;
        StepsList.SelectedItem = selected;
        SetStatus("단계 순서를 변경했습니다.");
    }

    private void DeleteStep_Click(object sender, RoutedEventArgs e)
    {
        if (StepsList.SelectedItem is not ActionStepItem selected) return;
        _steps.Remove(selected);
        RenumberSteps();
        UpdateStepCount();
        _isDirty = true;
        SetStatus("선택한 단계를 삭제했습니다.");
    }

    private void SaveAction_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dialog = new()
        {
            Filter = "날라액션 (*.nlaction)|*.nlaction|JSON (*.json)|*.json",
            FileName = SanitizeFileName(ActionNameTextBox.Text) + ".nlaction",
            AddExtension = true,
            DefaultExt = ".nlaction"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            ActionDocument document = new()
            {
                SchemaVersion = "2.0",
                Name = string.IsNullOrWhiteSpace(ActionNameTextBox.Text) ? "새 액션" : ActionNameTextBox.Text.Trim(),
                UpdatedAt = DateTimeOffset.UtcNow,
                Steps = _steps.ToList()
            };
            ValidateDocument(document);
            AtomicWriteJson(dialog.FileName, document);
            _isDirty = false;
            SetStatus($"저장 완료: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("액션 저장에 실패했습니다.");
        }
    }

    private void LoadAction_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges()) return;

        OpenFileDialog dialog = new()
        {
            Filter = "날라액션 (*.nlaction)|*.nlaction|JSON (*.json)|*.json",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            FileInfo info = new(dialog.FileName);
            if (!info.Exists) throw new FileNotFoundException("액션 파일을 찾을 수 없습니다.");
            if (info.Length > 25 * 1024 * 1024) throw new InvalidDataException("액션 파일이 허용 크기(25MB)를 초과했습니다.");

            ActionDocument? document = JsonSerializer.Deserialize<ActionDocument>(File.ReadAllText(dialog.FileName));
            if (document is null) throw new InvalidDataException("액션 파일을 읽을 수 없습니다.");
            ValidateDocument(document);

            StopAll("준비");
            SetActionName(document.Name);
            _steps.Clear();
            foreach (ActionStepItem step in document.Steps)
            {
                step.Detail = step.BuildDetail();
                _steps.Add(step);
            }
            RenumberSteps();
            UpdateStepCount();
            _isDirty = false;
            SetStatus($"불러오기 완료: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "액션 파일 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("액션 파일 불러오기에 실패했습니다.");
        }
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_isDirty) return true;
        return MessageBox.Show(
            "저장하지 않은 변경사항이 있습니다. 변경사항을 버리시겠습니까?",
            "날라액션",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private static void AtomicWriteJson(string path, ActionDocument document)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("저장 경로를 확인할 수 없습니다.");
        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            string json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static void ValidateDocument(ActionDocument document)
    {
        if (document.SchemaVersion is not ("1.0" or "2.0"))
            throw new InvalidDataException($"지원하지 않는 액션 파일 버전입니다: {document.SchemaVersion}");
        if (string.IsNullOrWhiteSpace(document.Name) || document.Name.Length > 200)
            throw new InvalidDataException("액션 이름이 비어 있거나 너무 깁니다.");
        if (document.Steps is null || document.Steps.Count > MaxSteps)
            throw new InvalidDataException($"액션 단계 수가 허용 범위(최대 {MaxSteps:N0})를 벗어났습니다.");
        ValidateSteps(document.Steps);
    }

    private static void ValidateSteps(IEnumerable<ActionStepItem> steps)
    {
        foreach (ActionStepItem step in steps) ValidateStep(step);
    }

    private static void ValidateStep(ActionStepItem step)
    {
        if (!SupportedStepTypes.Contains(step.Type))
            throw new InvalidDataException($"지원하지 않는 단계 형식입니다: {step.Type}");
        if (step.DelayBeforeMs is < 0 or > 60000)
            throw new InvalidDataException($"{step.Order:00} 단계의 실행 전 대기시간이 허용 범위를 벗어났습니다.");
        if (step.Text?.Length > 100000)
            throw new InvalidDataException($"{step.Order:00} 단계의 입력 문자열이 너무 깁니다.");
        if (step.Type == "Wait" && step.Value is < 0 or > 600000)
            throw new InvalidDataException($"{step.Order:00} 단계의 대기시간은 0~600000ms 범위여야 합니다.");
        if (step.Type == "KeyPress" && step.Value is <= 0 or > ushort.MaxValue)
            throw new InvalidDataException($"{step.Order:00} 단계의 키 코드가 유효하지 않습니다.");
        if (step.Type == "MouseButton" && step.Text is not ("Left:Down" or "Left:Up" or "Right:Down" or "Right:Up" or "Middle:Down" or "Middle:Up"))
            throw new InvalidDataException($"{step.Order:00} 단계의 마우스 버튼 동작이 유효하지 않습니다.");
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            e.Cancel = true;
            return;
        }

        if (_hotKeyRegistered && _windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, EmergencyHotKeyId);
            _hotKeyRegistered = false;
        }
        _windowSource?.RemoveHook(WindowMessageHook);
        _recorder.Dispose();
        _playbackCancellation?.Cancel();
        _playbackCancellation?.Dispose();
    }

    private void SetActionName(string value)
    {
        _suppressDirtyTracking = true;
        ActionNameTextBox.Text = value;
        _suppressDirtyTracking = false;
    }

    private void SetStatus(string message) => StatusText.Text = message;
    private void UpdateStepCount() => StepCountText.Text = $"{_steps.Count}개";

    private void RenumberSteps()
    {
        for (int index = 0; index < _steps.Count; index++) _steps[index].Order = index + 1;
        StepsList.Items.Refresh();
    }

    private static string SanitizeFileName(string value)
    {
        string safe = string.IsNullOrWhiteSpace(value) ? "new-action" : value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars()) safe = safe.Replace(invalid, '-');
        return safe;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

public sealed class ActionDocument
{
    public string SchemaVersion { get; set; } = "2.0";
    public string Name { get; set; } = "새 액션";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ActionStepItem> Steps { get; set; } = [];
}

public sealed class ActionStepItem
{
    public int Order { get; set; }
    public bool Enabled { get; set; } = true;
    public string Type { get; set; } = "Unknown";
    public string Name { get; set; } = "단계";
    public string Detail { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Value { get; set; }
    public int DelayBeforeMs { get; set; }

    public ActionStepItem Clone() => new()
    {
        Order = Order,
        Enabled = Enabled,
        Type = Type,
        Name = Name,
        Detail = Detail,
        Text = Text,
        X = X,
        Y = Y,
        Value = Value,
        DelayBeforeMs = DelayBeforeMs
    };

    public string BuildDetail() => Type switch
    {
        "MouseMove" => $"X={X}, Y={Y}",
        "MouseButton" => $"{Text}, X={X}, Y={Y}",
        "MouseWheel" => $"Delta={Value}, X={X}, Y={Y}",
        "KeyPress" => string.IsNullOrWhiteSpace(Text) ? $"VK={Value}" : Text,
        "TextInput" => Text,
        "Wait" => $"{Value}ms",
        _ => Detail
    };

    public override string ToString()
    {
        string disabled = Enabled ? string.Empty : "[꺼짐] ";
        return $"{Order:00}  {disabled}{Name}  ·  {BuildDetail()}";
    }
}
