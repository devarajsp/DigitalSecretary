# Feature: Calculator

A quick calculator with both a button keypad and a free-text expression evaluator.

| | |
|---|---|
| **Id** | `calculator` |
| **Category** | Tools |
| **Entry type** | `DigitalSecretary.Features.Calculator.CalculatorModule` |
| **Data** | none (stateless) |

## What it does
- Click the keypad, or type an expression such as `(12 + 5) * 3 / 2` and press **Enter**.
- Supports `+  −  ×  ÷`, parentheses, and decimals; evaluated via `DataTable.Compute`.
- `C` clears, `⌫` backspaces, `=` evaluates.

## Files
| File | Role |
|------|------|
| `CalculatorModule.cs` | `IFeatureModule` entry point. |
| `CalculatorControl.cs` | The UI (display + keypad). |
| `plugin.json` | Manifest. |

## Notes for future changes
- This feature is stateless and ignores `IFeatureContext`. If you add history, persist it under `context.DataDirectory`.
