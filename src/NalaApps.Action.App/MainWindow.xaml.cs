using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace NalaApps.Action.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ActionStepItem> _steps = [];
    private bool _isRecording;
    private CancellationTokenSource? _playbackCancellation;

    public MainWindow()
    {
        InitializeComponent();
        StepsList.ItemsSource = _steps;
    }

    private void NewAction_Click(object sender, RoutedEventArgs e)
    {
        _steps.Clear();
        ActionNameTextBox.Text = "새 액션";
        SetStatus("새 액션을 만들었습니다.");
        UpdateStepCount();
    }

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        _isRecording = !_isRecording;
        RecordButton.Content = _isRecording ? "녹화 중지" : "녹화 시작";

        if (_isRecording)
        {
            _steps.Add(new ActionStepItem
            {
                Order = _steps.Count + 1,
                Type = "RecordingMarker",
                Name = "녹화 시작"
            });
            SetStatus("녹화 중입니다. 현재 공개 빌드는 UI 및 저장 흐름 검증용 프리뷰입니다.");
        }
        else
        {
            _steps.Add(new ActionStepItem
            {
                Order = _steps.Count + 1,
                Type = "RecordingMarker",
                Name = "녹화 중지"
            });
            SetStatus("녹화를 중지했습니다.");
        }

        UpdateStepCount();
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_steps.Count == 0)
        {
            MessageBox.Show("실행할 단계가 없습니다.", "NalaApps Action", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _playbackCancellation?.Cancel();
        _playbackCancellation = new CancellationTokenSource();
        SetStatus("3초 후 실행합니다.");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), _playbackCancellation.Token);

            foreach (ActionStepItem step in _steps)
            {
                _playbackCancellation.Token.ThrowIfCancellationRequested();
                SetStatus($"실행 중: {step.Order}. {step.Name}");
                await Task.Delay(250, _playbackCancellation.Token);
            }

            SetStatus("실행이 완료되었습니다.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("실행을 중지했습니다.");
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _isRecording = false;
        RecordButton.Content = "녹화 시작";
        _playbackCancellation?.Cancel();
        SetStatus("긴급 중지했습니다.");
    }

    private void DeleteStep_Click(object sender, RoutedEventArgs e)
    {
        if (StepsList.SelectedItem is not ActionStepItem selected)
        {
            return;
        }

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

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ActionDocument document = new()
        {
            SchemaVersion = "1.0",
            Name = string.IsNullOrWhiteSpace(ActionNameTextBox.Text) ? "새 액션" : ActionNameTextBox.Text.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow,
            Steps = _steps.ToList()
        };

        string json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dialog.FileName, json);
        SetStatus($"저장 완료: {dialog.FileName}");
    }

    private void LoadAction_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "NalaApps Action (*.nlaction)|*.nlaction|JSON (*.json)|*.json",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(dialog.FileName);
            ActionDocument? document = JsonSerializer.Deserialize<ActionDocument>(json);
            if (document is null)
            {
                throw new InvalidDataException("액션 파일을 읽을 수 없습니다.");
            }

            ActionNameTextBox.Text = document.Name;
            _steps.Clear();
            foreach (ActionStepItem step in document.Steps)
            {
                _steps.Add(step);
            }

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

    private void SetStatus(string message) => StatusText.Text = message;

    private void UpdateStepCount() => StepCountText.Text = $"{_steps.Count}개";

    private void RenumberSteps()
    {
        for (int index = 0; index < _steps.Count; index++)
        {
            _steps[index].Order = index + 1;
        }

        StepsList.Items.Refresh();
    }

    private static string SanitizeFileName(string value)
    {
        string safe = string.IsNullOrWhiteSpace(value) ? "new-action" : value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(invalid, '-');
        }

        return safe;
    }
}

public sealed class ActionDocument
{
    public string SchemaVersion { get; set; } = "1.0";
    public string Name { get; set; } = "새 액션";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ActionStepItem> Steps { get; set; } = [];
}

public sealed class ActionStepItem
{
    public int Order { get; set; }
    public string Type { get; set; } = "Unknown";
    public string Name { get; set; } = "단계";

    public override string ToString() => $"{Order:00}  {Name}  [{Type}]";
}
