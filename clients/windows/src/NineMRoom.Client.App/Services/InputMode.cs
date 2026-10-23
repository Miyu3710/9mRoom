using System.ComponentModel;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace NineMRoom.Client.App;

/// <summary>
/// 全局输入模态：最近一次输入是键盘还是鼠标。
/// 焦点环只在键盘操作时显示，避免鼠标点击按钮后出现一圈加粗边框。
/// </summary>
public sealed class InputMode : INotifyPropertyChanged
{
    public static InputMode Instance { get; } = new();

    private bool _isKeyboard = true;

    private InputMode()
    {
        InputManager.Current.PreProcessInput += OnPreProcessInput;
    }

    public bool IsKeyboard
    {
        get => _isKeyboard;
        private set
        {
            if (_isKeyboard == value)
            {
                return;
            }

            _isKeyboard = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsKeyboard)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Cue)));
        }
    }

    /// <summary>用于 XAML 触发器的字符串形式（布尔条件对普通 CLR 源不做类型转换）。</summary>
    public string Cue => _isKeyboard ? "True" : "False";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPreProcessInput(object? sender, PreProcessInputEventArgs e)
    {
        switch (e.StagingItem.Input)
        {
            case MouseButtonEventArgs:
                IsKeyboard = false;
                break;
            case KeyEventArgs:
                IsKeyboard = true;
                break;
        }
    }
}
