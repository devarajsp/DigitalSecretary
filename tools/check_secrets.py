"""Secret / PII scanner for DigitalSecretary.

Scans every tracked text artifact (code, docs, config) for personal data and secrets, so they can
never be committed. Wired into build.ps1 (part of the VERDICT) and therefore the pre-commit hook.

Detects: personal email addresses, hardcoded passwords/secrets/tokens/API keys, GitHub/AWS/Google/
Slack tokens, JWTs, private-key blocks, US SSNs, and Luhn-valid credit-card numbers.

Allow a known-safe line with a trailing comment:  ... # pragma: allowlist secret
Run:  python tools/check_secrets.py
"""
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

SKIP_EXT = {".png", ".jpg", ".jpeg", ".gif", ".ico", ".bmp", ".xlsx", ".xls",
            ".dll", ".exe", ".pdf", ".zip", ".gz", ".lock"}
ALLOW_LINE = ("pragma: allowlist secret", "gitleaks:allow", "noqa: secret")

# --- email allow-listing (placeholders / no-reply are fine) ---
ALLOW_EMAILS = {"noreply@github.com", "noreply@anthropic.com"}
ALLOW_DOMAINS = {"example.com", "example.org", "example.net", "localhost",
                 "users.noreply.github.com", "domain.com", "email.com"}
ALLOW_LOCALPARTS = {"yourname", "you", "me", "user", "username", "test", "example",
                    "someone", "sender", "recipient", "to", "from", "foo", "bar",
                    "admin", "noreply", "name", "first.last", "john.doe", "jane.doe"}

EMAIL_RE = re.compile(r"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")
SSN_RE = re.compile(r"\b\d{3}-\d{2}-\d{4}\b")
CC_RE = re.compile(r"\b(?:\d[ \-]?){13,16}\b")

TOKEN_PATTERNS = [
    ("GitHub token", re.compile(r"gh[pousr]_[A-Za-z0-9]{36,}")),
    ("GitHub fine-grained PAT", re.compile(r"github_pat_[A-Za-z0-9_]{60,}")),
    ("AWS access key id", re.compile(r"\bAKIA[0-9A-Z]{16}\b")),
    ("Google API key", re.compile(r"\bAIza[0-9A-Za-z_\-]{35}\b")),
    ("Slack token", re.compile(r"\bxox[baprs]-[A-Za-z0-9\-]{10,}")),
    ("Private key block", re.compile(r"-----BEGIN (?:RSA |EC |DSA |OPENSSH |PGP )?PRIVATE KEY-----")),
    ("JWT", re.compile(r"\beyJ[A-Za-z0-9_\-]{8,}\.[A-Za-z0-9_\-]{8,}\.[A-Za-z0-9_\-]{8,}")),
]

SECRET_ASSIGN = re.compile(
    r"""(?ix)\b(password|passwd|pwd|secret|token|api[_-]?key|access[_-]?key|
        client[_-]?secret|connection[_-]?string|conn[_-]?str)\b\s*[:=]\s*(['"])([^'"]{4,})\2""")
PLACEHOLDER = re.compile(
    r"""(?ix)^(x{3,}|y{3,}|your[_\- ]?.*|<.*>|\{\{.*\}\}|__.*__|\$\{.*\}|changeme|
        change[_\- ]?me|password|p@ssw0rd|secret|example.*|placeholder|none|null|empty|
        true|false|\.\.\.|todo|redacted|\*+|dummy|sample.*|test.*)$""")

DATA_URI = re.compile(r"data:[\w.+\-]+/[\w.+\-]+;base64,[A-Za-z0-9+/=]+")

findings = []


def luhn_ok(s):
    total = 0
    for i, ch in enumerate(s[::-1]):
        d = int(ch)
        if i % 2 == 1:
            d *= 2
            if d > 9:
                d -= 9
        total += d
    return total % 10 == 0


def email_ok(addr):
    a = addr.lower()
    if a in ALLOW_EMAILS:
        return True
    local, _, domain = a.partition("@")
    return domain in ALLOW_DOMAINS or domain.endswith(".example") or local in ALLOW_LOCALPARTS


def scan_line(rel, n, line):
    if any(tag in line for tag in ALLOW_LINE):
        return
    for addr in EMAIL_RE.findall(line):
        if not email_ok(addr):
            findings.append((rel, n, "personal email", addr))
    for label, rx in TOKEN_PATTERNS:
        if rx.search(line):
            findings.append((rel, n, label, rx.search(line).group(0)[:24] + "..."))
    m = SECRET_ASSIGN.search(line)
    if m and not PLACEHOLDER.match(m.group(3).strip()):
        findings.append((rel, n, f"hardcoded {m.group(1).lower()}", m.group(3)[:6] + "..."))
    if SSN_RE.search(line):
        findings.append((rel, n, "US SSN", SSN_RE.search(line).group(0)))
    for cand in CC_RE.findall(line):
        digits = re.sub(r"\D", "", cand)
        if 13 <= len(digits) <= 16 and luhn_ok(digits):
            findings.append((rel, n, "credit-card number", digits[:4] + "********"))


def main():
    out = subprocess.run(["git", "-C", str(ROOT), "ls-files"], capture_output=True, text=True)
    files = [f for f in out.stdout.splitlines() if Path(f).suffix.lower() not in SKIP_EXT]
    for rel in files:
        p = ROOT / rel
        try:
            text = p.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        text = DATA_URI.sub("data:base64,<stripped>", text)  # don't scan embedded image blobs
        for n, line in enumerate(text.splitlines(), 1):
            scan_line(rel, n, line)

    print("=== Secret / PII scan ===")
    for rel, n, kind, snip in findings:
        print(f"  LEAK: {rel}:{n}  [{kind}]  {snip}")
    if not findings:
        print("  No secrets or personal information found.")
    print(f"RESULT findings={len(findings)}")
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main())
