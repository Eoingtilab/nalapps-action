using System.Windows;
using System.Windows.Controls;

namespace NalaApps.Action.App;

public partial class StepEditorWindow : Window
{
    public StepEditorWindow(ActionStepItem step)
    {
        InitializeComponent();
        Step = step.Clone();
        SelectType(Step.Type);
        NameTextBox.Text = Step.Name;
        ValueTextBox.Text = BuildValueText(Step);
        DelayTextBox.Text = Step.DelayBeforeMs.ToString();
        EnabledCheckBox.IsChecked = Step.Enabled;
        UpdateValueLabel();
    }

    public ActionStepItem Step { get; private set; }

    private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateValueLabel();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string type = SelectedType();
        if (!int.TryParse(DelayTextBox.Text, out int delay) || delay < 0)
        {
            MessageBox.Show("실행 전 대기시간은 0 이상의 숫자로 입력하세요.", "단계 편집", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ActionStepItem edited = Step.Clone();
        edited.Type = type;
        edited.Name = string.IsNullOrWhiteSpace(NameTextBox.Text) ? DefaultName(type) : NameTextBox.Text.Trim();
        edited.DelayBeforeMs = Math.Clamp(delay, 0, 60000);
        edited.Enabled = EnabledCheckBox.IsChecked == true;

        try
        {
            ApplyValue(edited, ValueTextBox.Text.Trim());
        }
        catch (FormatException ex)
        {
            MessageBox.Show(ex.Message, "단계 편집", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        edited.Detail = edited.BuildDetail();
        Step = edited;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ApplyValue(ActionStepItem step, string value)
    {
        switch (step.Type)
        {
            case "MouseMove":
                (step.X, step.Y) = ParseCoordinates(value);
                break;
            case "MouseButton":
                string normalized = value.Replace(" ", string.Empty);
                if (!new[] { "Left:Down", "Left:Up", "Right:Down", "Right:Up", "Middle:Down", "Middle:Up" }.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    throw new FormatException("마우스 버튼 값은 Left:Down, Left:Up, Right:Down 같은 형식으로 입력하세요.");
                step.Text = normalized;
                break;
            case "MouseWheel":
            case "Wait":
                if (!int.TryParse(value, out int number)) throw new FormatException("숫자 값을 입력하세요.");
                step.Value = number;
                break;
            case "KeyPress":
                if (!int.TryParse(value, out int virtualKey)) throw new FormatException("키보드 Virtual Key 숫자를 입력하세요.");
                step.Value = virtualKey;
                step.Text = virtualKey.ToString();
                break;
            case "TextInput":
                step.Text = value;
                break;
        }
    }

    private static (int X, int Y) ParseCoordinates(string value)
    {
        string[] parts = value.Replace("X=", "", StringComparison.OrdinalIgnoreCase)
                              .Replace("Y=", "", StringComparison.OrdinalIgnoreCase)
                              .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y))
            throw new FormatException("좌표는 X,Y 형식으로 입력하세요. 예: 500,300");
        return (x, y);
    }

    private void SelectType(string type)
    {
        foreach (ComboBoxItem item in TypeCombo.Items)
        {
            if (string.Equals(item.Tag?.ToString(), type, StringComparison.OrdinalIgnoreCase))
            {
                TypeCombo.SelectedItem = item;
                return;
            }
        }
        TypeCombo.SelectedIndex = 0;
    }

    private string SelectedType() => (TypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Wait";

    private void UpdateValueLabel()
    {
        if (ValueLabel is null) return;
        ValueLabel.Text = SelectedType() switch
        {
            "MouseMove" => "좌표 (X,Y)",
            "MouseButton" => "버튼과 상태 (예: Left:Down)",
            "MouseWheel" => "휠 값 (위 120 / 아래 -120)",
            "KeyPress" => "Virtual Key 숫자",
            "TextInput" => "입력할 텍스트",
            "Wait" => "대기 시간(ms)",
            _ => "실행 값"
        };
    }

    private static string BuildValueText(ActionStepItem step) => step.Type switch
    {
        "MouseMove" => $"{step.X},{step.Y}",
        "MouseButton" or "TextInput" => step.Text,
        _ => step.Value.ToString()
    };

    private static string DefaultName(string type) => type switch
    {
        "MouseMove" => "마우스 이동",
        "MouseButton" => "마우스 버튼",
        "MouseWheel" => "마우스 휠",
        "KeyPress" => "키보드 입력",
        "TextInput" => "텍스트 입력",
        "Wait" => "대기 시간",
        _ => "단계"
    };
}
