using System.Drawing;
using System.Windows.Forms;

namespace DigitalSecretary.Features.Launcher;

/// <summary>A small modal text-input dialog (WinForms has no built-in one).</summary>
internal static class Prompt
{
    public static string? Show(string title, string label, string defaultValue = "")
    {
        using var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(430, 132),
            Font = new Font("Segoe UI", 9.5f)
        };

        var lbl = new Label { Text = label, Left = 14, Top = 16, Width = 402, Height = 22, AutoSize = false };
        var txt = new TextBox { Left = 14, Top = 44, Width = 402, Text = defaultValue };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 244, Top = 88, Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 332, Top = 88, Width = 84 };

        form.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        if (form.ShowDialog() != DialogResult.OK)
            return null;

        var value = txt.Text.Trim();
        return value.Length == 0 ? null : value;
    }
}
