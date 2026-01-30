using System.Windows;

namespace Vector.HUD;

public partial class ApprovalWindow : Window
{
    private TaskCompletionSource<bool>? _tcs;

    public ApprovalWindow()
    {
        InitializeComponent();
        ConfirmButton.Click += (s,e) => _tcs?.TrySetResult(true);
        CancelButton.Click += (s,e) => _tcs?.TrySetResult(false);
    }

    public void SetTexts(string oldText, string newText)
    {
        OldText.Text = oldText;
        NewText.Text = newText;
    }

    public Task<bool> ShowDialogAsync()
    {
        _tcs = new TaskCompletionSource<bool>();
        this.Show();
        this.Activate();
        return _tcs.Task;
    }
}
