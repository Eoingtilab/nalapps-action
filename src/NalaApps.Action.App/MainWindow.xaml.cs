using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace NalaApps.Action.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ActionStepItem> _steps = [];
    private readonly WindowsInputRecorder _recorder;
    private bool _isRecording;
    private bool _isPlaying;
    private CancellationTokenSource? _playbackCancellation;

    public MainWindow()
    {
        InitializeComponent();
        StepsList.ItemsSource = _steps;
        _recorder = new WindowsInputRecorder(OnRecordedStep);
    }

    private void NewAction_Click(object sender, RoutedEventArgs e)
    {
        StopAll("준비");
        _steps.Clear();
        ActionNameTextBox.Text = "새 액션";
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

        if (_isRecording)
        {
            StopRecording();
        }

        if (_steps.Count == 0)
        {
            MessageBox.Show("실행할 단계가 없습니다.", "NalaApps Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _playbackCancellation?.Cancel();
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
                    SetStatus($"실행 중: {step.Order:00} {step.Name} · {step.Detail}");
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
            step.Order = _steps.Count + 1;
            _steps.Add(step);
            UpdateStepCount();
            StepsList.SelectedItem = step;
            StepsList.ScrollIntoView(step);
            SetStatus($"기록 중: {step.Name} · {step.Detail}");
        });
    }

    private void AddStep_Click(object sender, RoutedEventArgs e)
    {
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
            _steps.Add(step);
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
            _steps[index] = edited;
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
        StepsList.SelectedItem = selected;
        SetStatus("단계 순서를 변경했습니다.");
    }

    private void DeleteStep_Click(object sender, RoutedEventArgs e)
    {
        if (StepsList.SelectedItem is not ActionStepItem selected) return;
        _steps.Remove(selected);
        RenumberSteps();
        UpdateStepCount();
        SetStatus("선택한 단계를 삭제했습니다.");
    }

    private void SaveAction_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog dialog = new()
        {
            Filter = "NalaApps Action (*.nlaction)|*.nlaction|JSON (*.json)|*.json",
            FileName = SanitizeFileName(ActionNameTextBox.Text) + ".nlaction",
            AddExtension = true,
            DefaultExt = ".nlaction"
        };
        if (dialog.ShowDialog(this) != true) return;

        ActionDocument document = new()
        {
            SchemaVersion = "2.0",
            Name = string.IsNullOrWhiteSpace(ActionNameTextBox.Text) ? "새 액션" : ActionNameTextBox.Text.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow,
            Steps = _steps.ToList()
        };
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
        SetStatus($"저장 완료: {dialog.FileName}");
    }

    private void LoadAction_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "NalaApps Action (*.nlaction)|*.nlaction|JSON (*.json)|*.json",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            ActionDocument? document = JsonSerializer.Deserialize<ActionDocument>(File.ReadAllText(dialog.FileName));
            if (document is null) throw new InvalidDataException("액션 파일을 읽을 수 없습니다.");
            StopAll("준비");
            ActionNameTextBox.Text = document.Name;
            _steps.Clear();
            foreach (ActionStepItem step in document.Steps) _steps.Add(step);
            RenumberSteps();
            UpdateStepCount();
            SetStatus($"불러오기 완료: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "액션 파일 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("액션 파일 불러오기에 실패했습니다.");
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _recorder.Dispose();
        _playbackCancellation?.Cancel();
        _playbackCancellation?.Dispose();
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
        "MouseWheel" => $"Delta={Value}",
        "KeyPress" => string.IsNullOrWhiteSpace(Text) ? $"VK={Value}" : Text,
        "TextInput" => Text,
        "Wait" => $"{Value}ms",
        _ => Detail
    };

    public override string ToString()
    {
        string disabled = Enabled ? string.Empty : "[꺼짐] ";
        string detail = string.IsNullOrWhiteSpace(Detail) ? BuildDetail() : Detail;
        return $"{Order:00}  {disabled}{Name}  ·  {detail}";
    }
}
