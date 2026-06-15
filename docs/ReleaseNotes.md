# Thinksnap Release Notes

This page contains the public change history for Thinksnap releases.

## 0.1.5

- Fixed a native Authenticode verification layout bug that could close Thinksnap after the download reached 100%.
- Fully downloaded update files are now verified directly instead of being downloaded again.
- The progress bar now switches to a verification state instead of presenting download completion as installation completion.

## 0.1.4

- Simplified the in-app updater to show status and progress without embedded patch notes.
- Fixed repeated update prompts after a successful installation.
- Added automatic application restart after silent updates.
- Added a verified 100% completion indicator after the updated application starts.

## 0.1.3

- Fixed update dialogs displaying the internal semantic-version object instead of a readable version number.
- Moved the update percentage and transferred-size indicator inside the progress bar.

## 0.1.2

- Added secure automatic update checks with Stable and Beta channels.
- Added resumable downloads, cancellation, retry, and persistent ready-to-install state.
- Added mandatory GitHub digest, checksum-file, and local SHA-256 verification.
- Added Authenticode validation and explicit warnings for unsigned installers.
- Added background update checks and tray notifications.

## 0.1.1

- Improved multi-monitor capture and draggable screenshot-window behavior.
- Improved the installation commands and GitHub download presentation.
- Added language selection and updater foundations.

## 0.1.0

- Initial Windows MVP release.
- Added region capture, pixelation, annotations, clipboard copy, and PNG saving.
