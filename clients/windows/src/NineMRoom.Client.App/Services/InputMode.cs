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

    private static bool IsModifier(Key key) => key is
        Key.LeftAlt or Key.RightAlt or
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin;

    private void OnPreProcessInput(object? sender, PreProcessInputEventArgs e)
    {
        switch (e.StagingItem.Input)
        {
            case MouseButtonEventArgs:
                IsKeyboard = false;
                break;
            case KeyEventArgs keyArgs:
                // Alt / Ctrl / Shift 等纯修饰键不算键盘操作，避免按 Alt 时弹出焦点框。
                var key = keyArgs.Key == Key.System ? keyArgs.SystemKey : keyArgs.Key;
                if (!IsModifier(key))
                {
                    IsKeyboard = true;
                }

                break;
        }
    }
}
