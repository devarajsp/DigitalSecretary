# Requirement — Calculator

| | |
|---|---|
| Feature id | `calculator` | Category | Tools | Status | Implemented |

## 1. Purpose & value
Give the user a fast arithmetic calculator without leaving the app — usable by clicking a keypad or by
typing a whole expression.

## 2. Users & user stories
- As a user, I want to **click number/operator keys** to compute a result.
- As a user, I want to **type an expression** (with parentheses) and press Enter to evaluate it.
- As a user, I want **clear/backspace** to fix mistakes.

## 3. Functional requirements
| ID | Requirement |
|----|-------------|
| CAL-1 | A display shows the current expression/result. |
| CAL-2 | A keypad provides digits, `.`, `+ − × ÷`, parentheses, `C`, `⌫`, `=`. |
| CAL-3 | The user can type directly and press **Enter** to evaluate. |
| CAL-4 | Supports `+ − × ÷` and parentheses; display operators map to ASCII internally. |
| CAL-5 | Invalid input shows **"Error"** (no crash). Division by zero is treated as an error. |

## 4. Sub-features
### 4.1 Keypad entry
Clicking a key appends/acts on the display. *Accept:* `C` clears, `⌫` deletes last char, `=` evaluates.
### 4.2 Expression evaluation
Evaluate the typed/!built expression. *Accept:* `(12+5)*2 = 34`; `2×3 = 6`; `6÷2 = 3`.
### 4.3 Error handling
*Accept:* `2+` ⇒ `Error`; `abc` ⇒ `Error`; `5/0` ⇒ `Error`; blank ⇒ no change.

## 5. Acceptance criteria (feature)
Keypad and typed input produce identical results; documented examples evaluate correctly.

## 6. Non-functional
- Deterministic, culture-stable results (invariant culture). Stateless (no persistence).

## 7. Out of scope / future
Scientific functions, history, memory keys, percentage, keyboard operator shortcuts beyond Enter.

## 8. Traceability
- Code: `src/Features/Calculator/` (`CalculatorEngine` = logic, `CalculatorControl` = UI).
- Tests: `tests/DigitalSecretary.UnitTests/Features/CalculatorEngineTests.cs` (100% of engine).
