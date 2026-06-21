using System.Drawing;
using System.Windows.Forms;

namespace DigitalSecretary.Features.Calculator;

/// <summary>Quick calculator: use the keypad or just type an expression and press Enter.</summary>
public sealed class CalculatorControl : UserControl
{
    private readonly TextBox _display;

    public CalculatorControl()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        var title = new Label
        {
            Text = "Calculator",
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 43)
        };

        _display = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 52,
            Font = new Font("Consolas", 20f),
            TextAlign = HorizontalAlignment.Right,
            BorderStyle = BorderStyle.FixedSingle
        };
        _display.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                Evaluate();
                e.SuppressKeyPress = true;
            }
        };

        var hint = new Label
        {
            Text = "Tip: type something like (12 + 5) * 3 / 2 and press Enter.",
            Dock = DockStyle.Top,
            Height = 26,
            ForeColor = Color.Gray
        };

        var keypad = BuildKeypad();

        Controls.Add(keypad);
        Controls.Add(hint);
        Controls.Add(_display);
        Controls.Add(title);
    }

    private TableLayoutPanel BuildKeypad()
    {
        string[,] keys =
        {
            { "C", "(", ")", "÷" },
            { "7", "8", "9", "×" },
            { "4", "5", "6", "−" },
            { "1", "2", "3", "+" },
            { "0", ".", "⌫", "=" }
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 5,
            Padding = new Padding(0, 8, 0, 0)
        };
        for (var c = 0; c < 4; c++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        for (var r = 0; r < 5; r++)
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

        for (var r = 0; r < 5; r++)
        {
            for (var c = 0; c < 4; c++)
            {
                var key = keys[r, c];
                var btn = new Button
                {
                    Text = key,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(4),
                    Font = new Font("Segoe UI", 14f),
                    FlatStyle = FlatStyle.System,
                    Cursor = Cursors.Hand
                };
                btn.Click += (_, _) => OnKey(key);
                grid.Controls.Add(btn, c, r);
            }
        }
        return grid;
    }

    private void OnKey(string key)
    {
        switch (key)
        {
            case "C":
                _display.Clear();
                break;
            case "⌫":
                if (_display.Text.Length > 0)
                    _display.Text = _display.Text[..^1];
                break;
            case "=":
                Evaluate();
                break;
            default:
                _display.Text += key;
                break;
        }
        _display.SelectionStart = _display.Text.Length;
        _display.Focus();
    }

    private void Evaluate()
    {
        if (string.IsNullOrWhiteSpace(_display.Text))
            return;

        _display.Text = CalculatorEngine.Evaluate(_display.Text);
        _display.SelectionStart = _display.Text.Length;
    }
}
