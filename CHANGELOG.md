# Changelog

## v1.1.0
- Fixed mixed Arabic and English sentences so valid words are not broken or reordered.
- Fixed URLs, emails, domains, file paths, IPs, and `@handles` so they stay untouched in all text.
- Fixed PgDn and other navigation keys so scrolling works normally and keys are never blocked.
- Added a focus check before pasting so text is never pasted into the wrong window.
- Added a safer wait before restoring the clipboard so slow apps paste the right text and new copies are never overwritten.
- Fixed small cleanup and safety bugs (safer settings saving, tray menu/icon cleanup, and error handling).

## v1.0.0
- First release of KeyFixer.
