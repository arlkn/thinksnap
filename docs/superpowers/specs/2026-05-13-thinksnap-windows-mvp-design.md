# Thinksnap Windows MVP Design

## Goal

Thinksnap is a lightweight Windows screenshot tool inspired by Flameshot. The MVP focuses on a reliable region capture flow and a real pixelate censorship tool. The app should feel fast, native, and practical before adding secondary features such as tray settings, profiles, or advanced export destinations.

## Target Platform

- Windows desktop.
- C# and WPF.
- Single application process for the MVP.

## Primary User Flow

1. The user opens Thinksnap.
2. The user starts capture with `PrintScreen` or a configured shortcut.
3. A transparent full-screen selection overlay appears.
4. The user drags to select a screen region.
5. The selected region opens in the editor.
6. The user annotates or censors the image.
7. The user copies the result to the clipboard or saves it as a PNG file.

## MVP Features

- Region selection screenshot capture.
- Pixelate censorship by dragging a rectangular area.
- Arrow tool.
- Line tool.
- Rectangle tool.
- Freehand pen tool.
- Text tool.
- Undo for the most recent edit operations.
- Copy edited output to clipboard.
- Save edited output as PNG.

## Out Of Scope For MVP

- Full-screen capture command.
- Active-window capture.
- System tray settings UI.
- Cloud upload.
- OCR.
- Multi-profile configuration.
- Automatic UI tests for overlay/global hotkey behavior.

## Architecture

The MVP will use a single WPF application with focused internal services and windows:

- `CaptureService`: captures the screen bitmap used by the selection flow.
- `SelectionOverlayWindow`: displays the full-screen overlay and returns the selected rectangle.
- `EditorWindow`: hosts the editor UI and export commands.
- `AnnotationCanvas`: manages visual annotation operations and pixelate selections.
- `ExportService`: copies rendered output to the clipboard and saves PNG files.

The initial implementation should keep these boundaries simple and testable. Capture, annotation modeling, pixelation, undo state, and export rendering should not be tangled inside one large window class.

## Pixelate Behavior

Pixelate is the priority censorship feature. The user selects a rectangular region in the editor, and Thinksnap applies a mosaic effect to that part of the actual bitmap. Exported files and clipboard output must contain the censored pixels directly, not just a temporary visual overlay.

Pixelate operations are tracked as undoable edits. Undo should restore the previous bitmap state for that operation.

## Editor Behavior

The editor uses a compact toolbar and a central image canvas. The default annotation color is red, with a medium stroke thickness. The MVP may include small controls for color and thickness if they do not delay the core tools, but the required behavior is that each listed tool works reliably.

Each edit is stored as a discrete operation:

- Pixelate region.
- Arrow from start point to end point.
- Line from start point to end point.
- Rectangle bounds.
- Freehand stroke points.
- Text content and position.

Undo removes the most recent operation. For pixelate, undo restores the previous bitmap state. For shape/text annotations, undo removes the most recent annotation.

## Error Handling

- Pressing `Esc` in the overlay cancels capture and closes the overlay.
- If the selected area is too small, the editor does not open.
- If clipboard copy fails, the editor shows a short error message.
- If save is canceled, the editor remains open without an error.
- If save fails, the editor shows a short error message.

## Testing Strategy

Automated tests should cover:

- Pixelate algorithm behavior.
- Annotation operation model.
- Undo behavior.

Manual verification should cover:

- Starting region capture.
- Canceling the overlay with `Esc`.
- Selecting a region and opening the editor.
- Pixelate output in saved PNG and clipboard copy.
- Arrow, line, rectangle, pen, and text tools.
- Undo for annotations and pixelate operations.

## Success Criteria

The MVP is successful when a user can capture a region, pixelate sensitive information, add basic annotations, and export the edited image to clipboard or PNG without crashes or confusing intermediate steps.
