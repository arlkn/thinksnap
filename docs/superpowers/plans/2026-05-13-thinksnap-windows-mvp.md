# Thinksnap Windows MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows WPF MVP that captures a selected screen region, lets the user pixelate sensitive areas, annotate with basic tools, undo edits, and export to clipboard or PNG.

**Architecture:** Use one WPF application plus a small testable core library. Bitmap pixelation, annotation models, undo state, capture, overlay selection, editor rendering, and export are kept in focused classes so the main windows do not become the whole app.

**Tech Stack:** .NET 8 SDK, C# 12, WPF, xUnit for unit tests, Windows clipboard and save dialog APIs.

---

## Prerequisites

The current machine has .NET 8 runtimes but no .NET SDK. Install the .NET 8 SDK before executing implementation tasks.

- [ ] **Step 1: Verify SDK availability**

Run:

```powershell
dotnet --list-sdks
```

Expected before implementation: at least one `8.0.x` SDK line is printed.

- [ ] **Step 2: If no SDK is installed, install .NET 8 SDK**

Install the current .NET 8 SDK from Microsoft, then rerun:

```powershell
dotnet --info
```

Expected: `SDKs installed` includes an `8.0.x` SDK, and `Microsoft.WindowsDesktop.App` runtime is present.

---

## File Structure

Create this structure:

```text
Thinksnap.sln
src/Thinksnap.App/Thinksnap.App.csproj
src/Thinksnap.App/App.xaml
src/Thinksnap.App/App.xaml.cs
src/Thinksnap.App/MainWindow.xaml
src/Thinksnap.App/MainWindow.xaml.cs
src/Thinksnap.App/SelectionOverlayWindow.xaml
src/Thinksnap.App/SelectionOverlayWindow.xaml.cs
src/Thinksnap.App/EditorWindow.xaml
src/Thinksnap.App/EditorWindow.xaml.cs
src/Thinksnap.App/Controls/AnnotationCanvas.cs
src/Thinksnap.App/Interop/GlobalHotkey.cs
src/Thinksnap.App/Services/CaptureService.cs
src/Thinksnap.App/Services/ExportService.cs
src/Thinksnap.Core/Thinksnap.Core.csproj
src/Thinksnap.Core/Annotations/AnnotationOperation.cs
src/Thinksnap.Core/Annotations/AnnotationTool.cs
src/Thinksnap.Core/Annotations/PointD.cs
src/Thinksnap.Core/Annotations/RectD.cs
src/Thinksnap.Core/Editing/EditHistory.cs
src/Thinksnap.Core/Imaging/PixelateProcessor.cs
tests/Thinksnap.Core.Tests/Thinksnap.Core.Tests.csproj
tests/Thinksnap.Core.Tests/EditHistoryTests.cs
tests/Thinksnap.Core.Tests/PixelateProcessorTests.cs
```

Responsibilities:

- `Thinksnap.Core`: framework-light logic for annotations, undo history, and pixelation.
- `Thinksnap.App`: WPF windows, Windows interop, capture, rendering, clipboard, and file save.
- `Thinksnap.Core.Tests`: fast automated tests for the core behavior required by the spec.

---

### Task 1: Solution And Projects

**Files:**
- Create: `Thinksnap.sln`
- Create: `src/Thinksnap.App/Thinksnap.App.csproj`
- Create: `src/Thinksnap.Core/Thinksnap.Core.csproj`
- Create: `tests/Thinksnap.Core.Tests/Thinksnap.Core.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

Run:

```powershell
dotnet new sln -n Thinksnap
dotnet new classlib -n Thinksnap.Core -o src/Thinksnap.Core -f net8.0
dotnet new wpf -n Thinksnap.App -o src/Thinksnap.App -f net8.0-windows
dotnet new xunit -n Thinksnap.Core.Tests -o tests/Thinksnap.Core.Tests -f net8.0
dotnet sln Thinksnap.sln add src/Thinksnap.Core/Thinksnap.Core.csproj
dotnet sln Thinksnap.sln add src/Thinksnap.App/Thinksnap.App.csproj
dotnet sln Thinksnap.sln add tests/Thinksnap.Core.Tests/Thinksnap.Core.Tests.csproj
dotnet add src/Thinksnap.App/Thinksnap.App.csproj reference src/Thinksnap.Core/Thinksnap.Core.csproj
dotnet add tests/Thinksnap.Core.Tests/Thinksnap.Core.Tests.csproj reference src/Thinksnap.Core/Thinksnap.Core.csproj
```

Expected: each command succeeds and `Thinksnap.sln` contains three projects.

- [ ] **Step 2: Remove template placeholder classes**

Delete:

```text
src/Thinksnap.Core/Class1.cs
tests/Thinksnap.Core.Tests/UnitTest1.cs
```

- [ ] **Step 3: Build the empty solution**

Run:

```powershell
dotnet build Thinksnap.sln
```

Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

Run:

```powershell
git add Thinksnap.sln src tests
git commit -m "chore: scaffold Thinksnap solution"
```

---

### Task 2: Core Geometry And Annotation Model

**Files:**
- Create: `src/Thinksnap.Core/Annotations/AnnotationTool.cs`
- Create: `src/Thinksnap.Core/Annotations/PointD.cs`
- Create: `src/Thinksnap.Core/Annotations/RectD.cs`
- Create: `src/Thinksnap.Core/Annotations/AnnotationOperation.cs`
- Create: `tests/Thinksnap.Core.Tests/EditHistoryTests.cs`

- [ ] **Step 1: Write annotation model tests**

Create `tests/Thinksnap.Core.Tests/EditHistoryTests.cs`:

```csharp
using Thinksnap.Core.Annotations;
using Thinksnap.Core.Editing;

namespace Thinksnap.Core.Tests;

public sealed class EditHistoryTests
{
    [Fact]
    public void Undo_RemovesMostRecentAnnotation()
    {
        var history = new EditHistory();
        var first = AnnotationOperation.Line(new PointD(0, 0), new PointD(10, 10), "#ff0000", 3);
        var second = AnnotationOperation.Rectangle(new RectD(2, 2, 20, 10), "#ff0000", 3);

        history.Add(first);
        history.Add(second);

        var undone = history.Undo();

        Assert.Equal(second, undone);
        Assert.Equal([first], history.Operations);
    }

    [Fact]
    public void Undo_OnEmptyHistory_ReturnsNull()
    {
        var history = new EditHistory();

        Assert.Null(history.Undo());
        Assert.Empty(history.Operations);
    }

    [Fact]
    public void PixelateOperation_StoresSelectedBounds()
    {
        var operation = AnnotationOperation.Pixelate(new RectD(4, 5, 30, 18));

        Assert.Equal(AnnotationTool.Pixelate, operation.Tool);
        Assert.Equal(new RectD(4, 5, 30, 18), operation.Bounds);
    }
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
dotnet test tests/Thinksnap.Core.Tests/Thinksnap.Core.Tests.csproj --filter EditHistoryTests
```

Expected: FAIL because `AnnotationOperation`, `PointD`, `RectD`, and `EditHistory` do not exist.

- [ ] **Step 3: Implement annotation primitives**

Create `src/Thinksnap.Core/Annotations/AnnotationTool.cs`:

```csharp
namespace Thinksnap.Core.Annotations;

public enum AnnotationTool
{
    Pixelate,
    Arrow,
    Line,
    Rectangle,
    Pen,
    Text
}
```

Create `src/Thinksnap.Core/Annotations/PointD.cs`:

```csharp
namespace Thinksnap.Core.Annotations;

public readonly record struct PointD(double X, double Y);
```

Create `src/Thinksnap.Core/Annotations/RectD.cs`:

```csharp
namespace Thinksnap.Core.Annotations;

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public bool IsTooSmall(double minimumSize) => Width < minimumSize || Height < minimumSize;
}
```

Create `src/Thinksnap.Core/Annotations/AnnotationOperation.cs`:

```csharp
namespace Thinksnap.Core.Annotations;

public sealed record AnnotationOperation
{
    private AnnotationOperation(
        AnnotationTool tool,
        RectD? bounds,
        PointD? start,
        PointD? end,
        IReadOnlyList<PointD> points,
        string? text,
        string color,
        double strokeThickness)
    {
        Tool = tool;
        Bounds = bounds;
        Start = start;
        End = end;
        Points = points;
        Text = text;
        Color = color;
        StrokeThickness = strokeThickness;
    }

    public AnnotationTool Tool { get; }
    public RectD? Bounds { get; }
    public PointD? Start { get; }
    public PointD? End { get; }
    public IReadOnlyList<PointD> Points { get; }
    public string? Text { get; }
    public string Color { get; }
    public double StrokeThickness { get; }

    public static AnnotationOperation Pixelate(RectD bounds) =>
        new(AnnotationTool.Pixelate, bounds, null, null, [], null, "#ff0000", 0);

    public static AnnotationOperation Arrow(PointD start, PointD end, string color, double strokeThickness) =>
        new(AnnotationTool.Arrow, null, start, end, [], null, color, strokeThickness);

    public static AnnotationOperation Line(PointD start, PointD end, string color, double strokeThickness) =>
        new(AnnotationTool.Line, null, start, end, [], null, color, strokeThickness);

    public static AnnotationOperation Rectangle(RectD bounds, string color, double strokeThickness) =>
        new(AnnotationTool.Rectangle, bounds, null, null, [], null, color, strokeThickness);

    public static AnnotationOperation Pen(IReadOnlyList<PointD> points, string color, double strokeThickness) =>
        new(AnnotationTool.Pen, null, null, null, points, null, color, strokeThickness);

    public static AnnotationOperation TextLabel(PointD position, string text, string color) =>
        new(AnnotationTool.Text, null, position, null, [], text, color, 0);
}
```

- [ ] **Step 4: Run tests and verify current failure**

Run:

```powershell
dotnet test tests/Thinksnap.Core.Tests/Thinksnap.Core.Tests.csproj --filter EditHistoryTests
```

Expected: FAIL because `EditHistory` does not exist.

- [ ] **Step 5: Implement edit history**

Create `src/Thinksnap.Core/Editing/EditHistory.cs`:

```csharp
using Thinksnap.Core.Annotations;

namespace Thinksnap.Core.Editing;

public sealed class EditHistory
{
    private readonly List<AnnotationOperation> _operations = [];

    public IReadOnlyList<AnnotationOperation> Operations => _operations;

    public void Add(AnnotationOperation operation) => _operations.Add(operation);

    public AnnotationOperation? Undo()
    {
        if (_operations.Count == 0)
        {
            return null;
        }

        var lastIndex = _operations.Count - 1;
        var operation = _operations[lastIndex];
        _operations.RemoveAt(lastIndex);
        return operation;
    }
}
```

- [ ] **Step 6: Run tests and verify they pass**

Run:

```powershell
dotnet test tests/Thinksnap.Core.Tests/Thinksnap.Core.Tests.csproj --filter EditHistoryTests
```

Expected: PASS.

- [ ] **Step 7: Commit**

Run:

```powershell
git add src/Thinksnap.Core tests/Thinksnap.Core.Tests
git commit -m "feat: add annotation edit model"
```

---

### Task 3: Pixelate Processor

**Files:**
- Create: `src/Thinksnap.Core/Imaging/PixelateProcessor.cs`
- Create: `tests/Thinksnap.Core.Tests/PixelateProcessorTests.cs`

- [ ] **Step 1: Write pixelation tests**

Create `tests/Thinksnap.Core.Tests/PixelateProcessorTests.cs`:

```csharp
using Thinksnap.Core.Imaging;

namespace Thinksnap.Core.Tests;

public sealed class PixelateProcessorTests
{
    [Fact]
    public void Pixelate_ReplacesEachBlockWithAverageColor()
    {
        var pixels = new[]
        {
            new Rgba32(10, 20, 30, 255), new Rgba32(30, 40, 50, 255),
            new Rgba32(50, 60, 70, 255), new Rgba32(70, 80, 90, 255)
        };

        var result = PixelateProcessor.Pixelate(pixels, width: 2, height: 2, x: 0, y: 0, regionWidth: 2, regionHeight: 2, blockSize: 2);

        Assert.All(result, pixel => Assert.Equal(new Rgba32(40, 50, 60, 255), pixel));
    }

    [Fact]
    public void Pixelate_OnlyChangesRequestedRegion()
    {
        var pixels = Enumerable.Repeat(new Rgba32(1, 1, 1, 255), 9).ToArray();
        pixels[4] = new Rgba32(90, 90, 90, 255);

        var result = PixelateProcessor.Pixelate(pixels, width: 3, height: 3, x: 1, y: 1, regionWidth: 1, regionHeight: 1, blockSize: 4);

        Assert.Equal(new Rgba32(90, 90, 90, 255), result[4]);
        Assert.Equal(new Rgba32(1, 1, 1, 255), result[0]);
    }

    [Fact]
    public void Pixelate_ClampsRegionToImageBounds()
    {
        var pixels = Enumerable.Range(0, 4).Select(value => new Rgba32((byte)value, 0, 0, 255)).ToArray();

        var result = PixelateProcessor.Pixelate(pixels, width: 2, height: 2, x: 1, y: 1, regionWidth: 5, regionHeight: 5, blockSize: 2);

        Assert.Equal(pixels[0], result[0]);
        Assert.Equal(pixels[1], result[1]);
        Assert.Equal(pixels[2], result[2]);
        Assert.Equal(pixels[3], result[3]);
    }
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run:

```powershell
dotnet test tests/Thinksnap.Core.Tests/Thinksnap.Core.Tests.csproj --filter PixelateProcessorTests
```

Expected: FAIL because `PixelateProcessor` and `Rgba32` do not exist.

- [ ] **Step 3: Implement pixelation**

Create `src/Thinksnap.Core/Imaging/PixelateProcessor.cs`:

```csharp
namespace Thinksnap.Core.Imaging;

public readonly record struct Rgba32(byte R, byte G, byte B, byte A);

public static class PixelateProcessor
{
    public static Rgba32[] Pixelate(
        IReadOnlyList<Rgba32> pixels,
        int width,
        int height,
        int x,
        int y,
        int regionWidth,
        int regionHeight,
        int blockSize)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive.");
        }

        if (pixels.Count != width * height)
        {
            throw new ArgumentException("Pixel count must match width multiplied by height.", nameof(pixels));
        }

        if (blockSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be positive.");
        }

        var output = pixels.ToArray();
        var left = Math.Clamp(x, 0, width);
        var top = Math.Clamp(y, 0, height);
        var right = Math.Clamp(x + regionWidth, 0, width);
        var bottom = Math.Clamp(y + regionHeight, 0, height);

        for (var blockY = top; blockY < bottom; blockY += blockSize)
        {
            for (var blockX = left; blockX < right; blockX += blockSize)
            {
                var blockRight = Math.Min(blockX + blockSize, right);
                var blockBottom = Math.Min(blockY + blockSize, bottom);
                var average = Average(output, width, blockX, blockY, blockRight, blockBottom);

                for (var py = blockY; py < blockBottom; py++)
                {
                    for (var px = blockX; px < blockRight; px++)
                    {
                        output[py * width + px] = average;
                    }
                }
            }
        }

        return output;
    }

    private static Rgba32 Average(IReadOnlyList<Rgba32> pixels, int width, int left, int top, int right, int bottom)
    {
        var count = 0;
        var r = 0;
        var g = 0;
        var b = 0;
        var a = 0;

        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var pixel = pixels[y * width + x];
                r += pixel.R;
                g += pixel.G;
                b += pixel.B;
                a += pixel.A;
                count++;
            }
        }

        return new Rgba32((byte)(r / count), (byte)(g / count), (byte)(b / count), (byte)(a / count));
    }
}
```

- [ ] **Step 4: Run tests and verify they pass**

Run:

```powershell
dotnet test tests/Thinksnap.Core.Tests/Thinksnap.Core.Tests.csproj --filter PixelateProcessorTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/Thinksnap.Core/Imaging tests/Thinksnap.Core.Tests/PixelateProcessorTests.cs
git commit -m "feat: add pixelate processor"
```

---

### Task 4: WPF App Shell And Capture Entry

**Files:**
- Modify: `src/Thinksnap.App/App.xaml`
- Modify: `src/Thinksnap.App/MainWindow.xaml`
- Modify: `src/Thinksnap.App/MainWindow.xaml.cs`
- Create: `src/Thinksnap.App/Interop/GlobalHotkey.cs`

- [ ] **Step 1: Configure app startup**

Update `src/Thinksnap.App/App.xaml`:

```xml
<Application x:Class="Thinksnap.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources />
</Application>
```

- [ ] **Step 2: Create main window UI**

Update `src/Thinksnap.App/MainWindow.xaml`:

```xml
<Window x:Class="Thinksnap.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Thinksnap"
        Width="420"
        Height="220"
        WindowStartupLocation="CenterScreen">
    <Grid Margin="24">
        <StackPanel VerticalAlignment="Center">
            <TextBlock Text="Thinksnap" FontSize="28" FontWeight="SemiBold" />
            <TextBlock Margin="0,8,0,20"
                       Text="Press PrintScreen or click Capture Region."
                       Foreground="#555" />
            <Button x:Name="CaptureButton"
                    Width="160"
                    Height="36"
                    HorizontalAlignment="Left"
                    Click="CaptureButton_Click"
                    Content="Capture Region" />
            <TextBlock x:Name="StatusText"
                       Margin="0,16,0,0"
                       Foreground="#666"
                       Text="Ready" />
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 3: Implement global hotkey wrapper**

Create `src/Thinksnap.App/Interop/GlobalHotkey.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Thinksnap.App.Interop;

public sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private readonly int _id;
    private readonly HwndSource _source;
    private readonly Action _onPressed;
    private bool _disposed;

    public GlobalHotkey(HwndSource source, int id, uint modifiers, uint virtualKey, Action onPressed)
    {
        _source = source;
        _id = id;
        _onPressed = onPressed;
        _source.AddHook(WndProc);

        if (!RegisterHotKey(_source.Handle, _id, modifiers, virtualKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register global hotkey.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        UnregisterHotKey(_source.Handle, _id);
        _source.RemoveHook(WndProc);
        _disposed = true;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == _id)
        {
            handled = true;
            _onPressed();
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
```

- [ ] **Step 4: Wire capture entry in MainWindow**

Update `src/Thinksnap.App/MainWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Interop;
using Thinksnap.App.Interop;

namespace Thinksnap.App;

public partial class MainWindow : Window
{
    private const uint VkPrintScreen = 0x2C;
    private GlobalHotkey? _printScreenHotkey;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += (_, _) => _printScreenHotkey?.Dispose();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var source = (HwndSource)PresentationSource.FromVisual(this);

        try
        {
            _printScreenHotkey = new GlobalHotkey(source, id: 1, modifiers: 0, virtualKey: VkPrintScreen, StartCapture);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"PrintScreen unavailable: {ex.Message}";
        }
    }

    private void CaptureButton_Click(object sender, RoutedEventArgs e) => StartCapture();

    private void StartCapture()
    {
        StatusText.Text = "Capture flow will open in the next task.";
    }
}
```

- [ ] **Step 5: Build**

Run:

```powershell
dotnet build Thinksnap.sln
```

Expected: `Build succeeded`.

- [ ] **Step 6: Commit**

Run:

```powershell
git add src/Thinksnap.App
git commit -m "feat: add WPF capture entry"
```

---

### Task 5: Capture Service And Region Overlay

**Files:**
- Create: `src/Thinksnap.App/Services/CaptureService.cs`
- Create: `src/Thinksnap.App/SelectionOverlayWindow.xaml`
- Create: `src/Thinksnap.App/SelectionOverlayWindow.xaml.cs`
- Modify: `src/Thinksnap.App/MainWindow.xaml.cs`

- [ ] **Step 1: Implement screen capture service**

Create `src/Thinksnap.App/Services/CaptureService.cs`:

```csharp
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Media.Imaging;
using Point = System.Drawing.Point;

namespace Thinksnap.App.Services;

public sealed class CaptureService
{
    public Bitmap CaptureVirtualScreen()
    {
        var left = (int)SystemParameters.VirtualScreenLeft;
        var top = (int)SystemParameters.VirtualScreenTop;
        var width = (int)SystemParameters.VirtualScreenWidth;
        var height = (int)SystemParameters.VirtualScreenHeight;

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(new Point(left, top), Point.Empty, bitmap.Size);
        return bitmap;
    }

    public BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Png);
        memory.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = memory;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public Bitmap Crop(Bitmap source, Int32Rect region)
    {
        var cropped = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(cropped);
        graphics.DrawImage(source, new Rectangle(0, 0, region.Width, region.Height), new Rectangle(region.X, region.Y, region.Width, region.Height), GraphicsUnit.Pixel);
        return cropped;
    }
}
```

- [ ] **Step 2: Add System.Drawing reference support**

Update `src/Thinksnap.App/Thinksnap.App.csproj` inside `<PropertyGroup>`:

```xml
<UseWPF>true</UseWPF>
<UseWindowsForms>true</UseWindowsForms>
```

- [ ] **Step 3: Implement overlay XAML**

Create `src/Thinksnap.App/SelectionOverlayWindow.xaml`:

```xml
<Window x:Class="Thinksnap.App.SelectionOverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="#66000000"
        Topmost="True"
        ShowInTaskbar="False"
        KeyDown="Window_KeyDown"
        MouseLeftButtonDown="Window_MouseLeftButtonDown"
        MouseMove="Window_MouseMove"
        MouseLeftButtonUp="Window_MouseLeftButtonUp">
    <Canvas x:Name="OverlayCanvas">
        <Rectangle x:Name="SelectionRectangle"
                   Stroke="#ff2d2d"
                   StrokeThickness="2"
                   Fill="#22ff2d2d"
                   Visibility="Collapsed" />
    </Canvas>
</Window>
```

- [ ] **Step 4: Implement overlay selection logic**

Create `src/Thinksnap.App/SelectionOverlayWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Thinksnap.App;

public partial class SelectionOverlayWindow : Window
{
    private Point? _start;

    public SelectionOverlayWindow()
    {
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Loaded += (_, _) => Focus();
    }

    public Int32Rect? SelectedRegion { get; private set; }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(OverlayCanvas);
        SelectionRectangle.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_start is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DrawSelection(_start.Value, e.GetPosition(OverlayCanvas));
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_start is null)
        {
            return;
        }

        ReleaseMouseCapture();
        var end = e.GetPosition(OverlayCanvas);
        var rect = Normalize(_start.Value, end);

        if (rect.Width < 5 || rect.Height < 5)
        {
            DialogResult = false;
            Close();
            return;
        }

        SelectedRegion = new Int32Rect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        DialogResult = true;
        Close();
    }

    private void DrawSelection(Point start, Point end)
    {
        var rect = Normalize(start, end);
        Canvas.SetLeft(SelectionRectangle, rect.X);
        Canvas.SetTop(SelectionRectangle, rect.Y);
        SelectionRectangle.Width = rect.Width;
        SelectionRectangle.Height = rect.Height;
    }

    private static Rect Normalize(Point start, Point end)
    {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var width = Math.Abs(start.X - end.X);
        var height = Math.Abs(start.Y - end.Y);
        return new Rect(x, y, width, height);
    }
}
```

- [ ] **Step 5: Open editor with cropped capture**

Replace `StartCapture` in `src/Thinksnap.App/MainWindow.xaml.cs` with:

```csharp
private void StartCapture()
{
    var captureService = new Services.CaptureService();
    using var fullScreen = captureService.CaptureVirtualScreen();

    Hide();
    var overlay = new SelectionOverlayWindow();
    var accepted = overlay.ShowDialog() == true;
    Show();

    if (!accepted || overlay.SelectedRegion is null)
    {
        StatusText.Text = "Capture canceled.";
        return;
    }

    using var cropped = captureService.Crop(fullScreen, overlay.SelectedRegion.Value);
    var editor = new EditorWindow(captureService.ToBitmapSource(cropped));
    editor.Show();
    StatusText.Text = "Capture opened in editor.";
}
```

- [ ] **Step 6: Build and manually verify overlay**

Run:

```powershell
dotnet build Thinksnap.sln
dotnet run --project src/Thinksnap.App/Thinksnap.App.csproj
```

Expected: clicking `Capture Region` opens overlay; dragging a region attempts to open `EditorWindow` after Task 6 creates it; pressing `Esc` cancels.

- [ ] **Step 7: Commit**

Run:

```powershell
git add src/Thinksnap.App
git commit -m "feat: add region capture overlay"
```

---

### Task 6: Editor Window And Annotation Canvas

**Files:**
- Create: `src/Thinksnap.App/EditorWindow.xaml`
- Create: `src/Thinksnap.App/EditorWindow.xaml.cs`
- Create: `src/Thinksnap.App/Controls/AnnotationCanvas.cs`

- [ ] **Step 1: Create editor UI**

Create `src/Thinksnap.App/EditorWindow.xaml`:

```xml
<Window x:Class="Thinksnap.App.EditorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:Thinksnap.App.Controls"
        Title="Thinksnap Editor"
        Width="1000"
        Height="720"
        WindowStartupLocation="CenterScreen">
    <DockPanel>
        <ToolBar DockPanel.Dock="Top">
            <Button Content="Pixelate" Click="Pixelate_Click" />
            <Button Content="Arrow" Click="Arrow_Click" />
            <Button Content="Line" Click="Line_Click" />
            <Button Content="Rectangle" Click="Rectangle_Click" />
            <Button Content="Pen" Click="Pen_Click" />
            <Button Content="Text" Click="Text_Click" />
            <Separator />
            <Button Content="Undo" Click="Undo_Click" />
            <Button Content="Copy" Click="Copy_Click" />
            <Button Content="Save" Click="Save_Click" />
        </ToolBar>
        <ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Auto">
            <controls:AnnotationCanvas x:Name="CanvasHost" />
        </ScrollViewer>
    </DockPanel>
</Window>
```

- [ ] **Step 2: Implement annotation canvas**

Create `src/Thinksnap.App/Controls/AnnotationCanvas.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Thinksnap.Core.Annotations;
using Thinksnap.Core.Editing;

namespace Thinksnap.App.Controls;

public sealed class AnnotationCanvas : Canvas
{
    private readonly Image _image = new();
    private readonly EditHistory _history = new();
    private Point? _start;
    private Polyline? _activePen;

    public AnnotationCanvas()
    {
        Background = Brushes.Transparent;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    public BitmapSource? Source { get; private set; }
    public AnnotationTool ActiveTool { get; set; } = AnnotationTool.Pixelate;
    public string StrokeColor { get; set; } = "#ff0000";
    public double StrokeThicknessValue { get; set; } = 3;

    public void SetImage(BitmapSource source)
    {
        Source = source;
        Width = source.PixelWidth;
        Height = source.PixelHeight;
        _image.Source = source;
        Children.Clear();
        Children.Add(_image);
    }

    public void Undo()
    {
        var operation = _history.Undo();
        if (operation is null)
        {
            return;
        }

        if (Children.Count > 1)
        {
            Children.RemoveAt(Children.Count - 1);
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        _start = e.GetPosition(this);
        CaptureMouse();

        if (ActiveTool == AnnotationTool.Pen)
        {
            _activePen = new Polyline { Stroke = Brush(), StrokeThickness = StrokeThicknessValue };
            _activePen.Points.Add(_start.Value);
            Children.Add(_activePen);
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_activePen is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            _activePen.Points.Add(e.GetPosition(this));
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_start is null)
        {
            return;
        }

        ReleaseMouseCapture();
        var end = e.GetPosition(this);
        var start = _start.Value;
        _start = null;

        if (_activePen is not null)
        {
            var points = _activePen.Points.Select(p => new PointD(p.X, p.Y)).ToArray();
            _history.Add(AnnotationOperation.Pen(points, StrokeColor, StrokeThicknessValue));
            _activePen = null;
            return;
        }

        switch (ActiveTool)
        {
            case AnnotationTool.Arrow:
                AddLine(start, end, true);
                _history.Add(AnnotationOperation.Arrow(ToPointD(start), ToPointD(end), StrokeColor, StrokeThicknessValue));
                break;
            case AnnotationTool.Line:
                AddLine(start, end, false);
                _history.Add(AnnotationOperation.Line(ToPointD(start), ToPointD(end), StrokeColor, StrokeThicknessValue));
                break;
            case AnnotationTool.Rectangle:
            case AnnotationTool.Pixelate:
                AddRectangle(start, end, ActiveTool == AnnotationTool.Pixelate);
                _history.Add(ActiveTool == AnnotationTool.Pixelate
                    ? AnnotationOperation.Pixelate(ToRectD(start, end))
                    : AnnotationOperation.Rectangle(ToRectD(start, end), StrokeColor, StrokeThicknessValue));
                break;
            case AnnotationTool.Text:
                AddText(start);
                _history.Add(AnnotationOperation.TextLabel(ToPointD(start), "Text", StrokeColor));
                break;
        }
    }

    private void AddLine(Point start, Point end, bool arrow)
    {
        Children.Add(new Line
        {
            X1 = start.X,
            Y1 = start.Y,
            X2 = end.X,
            Y2 = end.Y,
            Stroke = Brush(),
            StrokeThickness = StrokeThicknessValue
        });

        if (arrow)
        {
            AddArrowHead(start, end);
        }
    }

    private void AddArrowHead(Point start, Point end)
    {
        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        const double length = 12;
        var left = new Point(end.X - length * Math.Cos(angle - Math.PI / 6), end.Y - length * Math.Sin(angle - Math.PI / 6));
        var right = new Point(end.X - length * Math.Cos(angle + Math.PI / 6), end.Y - length * Math.Sin(angle + Math.PI / 6));

        Children.Add(new Polygon
        {
            Points = [end, left, right],
            Fill = Brush()
        });
    }

    private void AddRectangle(Point start, Point end, bool pixelatePlaceholder)
    {
        var rect = Normalize(start, end);
        var shape = new Rectangle
        {
            Width = rect.Width,
            Height = rect.Height,
            Stroke = pixelatePlaceholder ? Brushes.Transparent : Brush(),
            StrokeThickness = pixelatePlaceholder ? 0 : StrokeThicknessValue,
            Fill = pixelatePlaceholder ? new SolidColorBrush(Color.FromArgb(90, 120, 120, 120)) : Brushes.Transparent
        };

        SetLeft(shape, rect.X);
        SetTop(shape, rect.Y);
        Children.Add(shape);
    }

    private void AddText(Point point)
    {
        var textBox = new TextBox
        {
            Text = "Text",
            Foreground = Brush(),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 18,
            MinWidth = 60
        };

        SetLeft(textBox, point.X);
        SetTop(textBox, point.Y);
        Children.Add(textBox);
        textBox.Focus();
        textBox.SelectAll();
    }

    private SolidColorBrush Brush() => (SolidColorBrush)new BrushConverter().ConvertFromString(StrokeColor)!;

    private static Rect Normalize(Point start, Point end) =>
        new(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(start.X - end.X), Math.Abs(start.Y - end.Y));

    private static PointD ToPointD(Point point) => new(point.X, point.Y);

    private static RectD ToRectD(Point start, Point end)
    {
        var rect = Normalize(start, end);
        return new RectD(rect.X, rect.Y, rect.Width, rect.Height);
    }
}
```

- [ ] **Step 3: Wire editor commands**

Create `src/Thinksnap.App/EditorWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Media.Imaging;
using Thinksnap.Core.Annotations;

namespace Thinksnap.App;

public partial class EditorWindow : Window
{
    public EditorWindow(BitmapSource source)
    {
        InitializeComponent();
        CanvasHost.SetImage(source);
    }

    private void Pixelate_Click(object sender, RoutedEventArgs e) => CanvasHost.ActiveTool = AnnotationTool.Pixelate;
    private void Arrow_Click(object sender, RoutedEventArgs e) => CanvasHost.ActiveTool = AnnotationTool.Arrow;
    private void Line_Click(object sender, RoutedEventArgs e) => CanvasHost.ActiveTool = AnnotationTool.Line;
    private void Rectangle_Click(object sender, RoutedEventArgs e) => CanvasHost.ActiveTool = AnnotationTool.Rectangle;
    private void Pen_Click(object sender, RoutedEventArgs e) => CanvasHost.ActiveTool = AnnotationTool.Pen;
    private void Text_Click(object sender, RoutedEventArgs e) => CanvasHost.ActiveTool = AnnotationTool.Text;
    private void Undo_Click(object sender, RoutedEventArgs e) => CanvasHost.Undo();
    private void Copy_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Copy will be implemented in the export task.", "Thinksnap");
    private void Save_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Save will be implemented in the export task.", "Thinksnap");
}
```

- [ ] **Step 4: Build and manually verify editor opens**

Run:

```powershell
dotnet build Thinksnap.sln
dotnet run --project src/Thinksnap.App/Thinksnap.App.csproj
```

Expected: region capture opens editor; tools draw visible annotations; undo removes the most recent visual child for simple operations.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src/Thinksnap.App
git commit -m "feat: add editor annotation canvas"
```

---

### Task 7: Real Pixelate Rendering And Export

**Files:**
- Modify: `src/Thinksnap.App/Controls/AnnotationCanvas.cs`
- Create: `src/Thinksnap.App/Services/ExportService.cs`
- Modify: `src/Thinksnap.App/EditorWindow.xaml.cs`

- [ ] **Step 1: Update canvas imports for real bitmap editing**

Add these usings to `src/Thinksnap.App/Controls/AnnotationCanvas.cs`:

```csharp
using System.Windows.Media.Imaging;
using Thinksnap.Core.Imaging;
```

- [ ] **Step 2: Add bitmap state fields**

Add these fields to `AnnotationCanvas`:

```csharp
private readonly Stack<BitmapSource> _pixelateUndo = new();
private WriteableBitmap? _editableBitmap;
```

- [ ] **Step 3: Store an editable bitmap when an image is loaded**

Replace `SetImage` in `AnnotationCanvas`:

```csharp
public void SetImage(BitmapSource source)
{
    _editableBitmap = new WriteableBitmap(source);
    Source = _editableBitmap;
    Width = _editableBitmap.PixelWidth;
    Height = _editableBitmap.PixelHeight;
    _image.Source = _editableBitmap;
    Children.Clear();
    Children.Add(_image);
}
```

- [ ] **Step 4: Apply real pixelate when the pixelate tool is used**

Add these methods to `AnnotationCanvas`:

```csharp
private void ApplyPixelate(RectD bounds)
{
    if (_editableBitmap is null)
    {
        return;
    }

    var x = Math.Max(0, (int)Math.Floor(bounds.X));
    var y = Math.Max(0, (int)Math.Floor(bounds.Y));
    var width = Math.Min(_editableBitmap.PixelWidth - x, (int)Math.Ceiling(bounds.Width));
    var height = Math.Min(_editableBitmap.PixelHeight - y, (int)Math.Ceiling(bounds.Height));

    if (width < 5 || height < 5)
    {
        return;
    }

    _pixelateUndo.Push(_editableBitmap.Clone());

    var stride = _editableBitmap.PixelWidth * 4;
    var raw = new byte[stride * _editableBitmap.PixelHeight];
    _editableBitmap.CopyPixels(raw, stride, 0);

    var rgba = new Rgba32[_editableBitmap.PixelWidth * _editableBitmap.PixelHeight];
    for (var index = 0; index < rgba.Length; index++)
    {
        var byteIndex = index * 4;
        rgba[index] = new Rgba32(raw[byteIndex + 2], raw[byteIndex + 1], raw[byteIndex], raw[byteIndex + 3]);
    }

    var pixelated = PixelateProcessor.Pixelate(rgba, _editableBitmap.PixelWidth, _editableBitmap.PixelHeight, x, y, width, height, blockSize: 12);

    for (var index = 0; index < pixelated.Length; index++)
    {
        var byteIndex = index * 4;
        raw[byteIndex] = pixelated[index].B;
        raw[byteIndex + 1] = pixelated[index].G;
        raw[byteIndex + 2] = pixelated[index].R;
        raw[byteIndex + 3] = pixelated[index].A;
    }

    _editableBitmap.WritePixels(new Int32Rect(0, 0, _editableBitmap.PixelWidth, _editableBitmap.PixelHeight), raw, stride, 0);
}

private void RestoreLastPixelate()
{
    if (_pixelateUndo.Count == 0)
    {
        return;
    }

    _editableBitmap = new WriteableBitmap(_pixelateUndo.Pop());
    Source = _editableBitmap;
    _image.Source = _editableBitmap;
}
```

- [ ] **Step 5: Change pixelate mouse-up behavior to modify the bitmap**

In `OnMouseLeftButtonUp`, replace the shared `Rectangle`/`Pixelate` case:

```csharp
case AnnotationTool.Rectangle:
case AnnotationTool.Pixelate:
    AddRectangle(start, end, ActiveTool == AnnotationTool.Pixelate);
    _history.Add(ActiveTool == AnnotationTool.Pixelate
        ? AnnotationOperation.Pixelate(ToRectD(start, end))
        : AnnotationOperation.Rectangle(ToRectD(start, end), StrokeColor, StrokeThicknessValue));
    break;
```

with:

```csharp
case AnnotationTool.Rectangle:
    AddRectangle(start, end);
    _history.Add(AnnotationOperation.Rectangle(ToRectD(start, end), StrokeColor, StrokeThicknessValue));
    break;
case AnnotationTool.Pixelate:
    var pixelateBounds = ToRectD(start, end);
    ApplyPixelate(pixelateBounds);
    _history.Add(AnnotationOperation.Pixelate(pixelateBounds));
    break;
```

- [ ] **Step 6: Replace rectangle drawing helper**

Replace `AddRectangle`:

```csharp
private void AddRectangle(Point start, Point end)
{
    var rect = Normalize(start, end);
    var shape = new Rectangle
    {
        Width = rect.Width,
        Height = rect.Height,
        Stroke = Brush(),
        StrokeThickness = StrokeThicknessValue,
        Fill = Brushes.Transparent
    };

    SetLeft(shape, rect.X);
    SetTop(shape, rect.Y);
    Children.Add(shape);
}
```

- [ ] **Step 7: Make undo restore pixelated bitmap state**

Replace `Undo` in `AnnotationCanvas`:

```csharp
public void Undo()
{
    var operation = _history.Undo();
    if (operation is null)
    {
        return;
    }

    if (operation.Tool == AnnotationTool.Pixelate)
    {
        RestoreLastPixelate();
        return;
    }

    if (Children.Count > 1)
    {
        Children.RemoveAt(Children.Count - 1);
    }
}
```

- [ ] **Step 8: Add render method to annotation canvas**

Add this method to `AnnotationCanvas`:

```csharp
public RenderTargetBitmap RenderOutput()
{
    Measure(new Size(Width, Height));
    Arrange(new Rect(0, 0, Width, Height));

    var target = new RenderTargetBitmap((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32);
    target.Render(this);
    target.Freeze();
    return target;
}
```

- [ ] **Step 9: Create export service**

Create `src/Thinksnap.App/Services/ExportService.cs`:

```csharp
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Thinksnap.App.Services;

public sealed class ExportService
{
    public void CopyToClipboard(BitmapSource bitmap)
    {
        Clipboard.SetImage(bitmap);
    }

    public bool SavePng(BitmapSource bitmap)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Thinksnap capture",
            Filter = "PNG Image (*.png)|*.png",
            DefaultExt = ".png",
            FileName = $"thinksnap-{DateTime.Now:yyyyMMdd-HHmmss}.png"
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(dialog.FileName);
        encoder.Save(stream);
        return true;
    }
}
```

- [ ] **Step 10: Wire export commands**

Replace `Copy_Click` and `Save_Click` in `EditorWindow.xaml.cs`:

```csharp
private readonly Services.ExportService _exportService = new();

private void Copy_Click(object sender, RoutedEventArgs e)
{
    try
    {
        _exportService.CopyToClipboard(CanvasHost.RenderOutput());
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Could not copy image: {ex.Message}", "Thinksnap", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

private void Save_Click(object sender, RoutedEventArgs e)
{
    try
    {
        _exportService.SavePng(CanvasHost.RenderOutput());
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Could not save image: {ex.Message}", "Thinksnap", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

- [ ] **Step 11: Build and manually verify export**

Run:

```powershell
dotnet build Thinksnap.sln
dotnet run --project src/Thinksnap.App/Thinksnap.App.csproj
```

Expected: copy puts edited image on clipboard; save writes a PNG; canceling save returns to editor without error. Pixelate must visibly alter the underlying image before export, and undo must restore the previous bitmap.

- [ ] **Step 12: Commit**

Run:

```powershell
git add src/Thinksnap.App
git commit -m "feat: add clipboard and PNG export"
```

---

### Task 8: Final Verification And MVP Notes

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Create README**

Create `README.md`:

```markdown
# Thinksnap

Thinksnap is a lightweight Windows screenshot MVP inspired by Flameshot. It focuses on region capture, pixelate censorship, basic annotations, undo, clipboard copy, and PNG save.

## Requirements

- Windows
- .NET 8 SDK

## Run

```powershell
dotnet run --project src/Thinksnap.App/Thinksnap.App.csproj
```

## Test

```powershell
dotnet test Thinksnap.sln
```

## MVP Scope

- Region capture
- Pixelate censorship
- Arrow, line, rectangle, pen, and text annotations
- Undo
- Clipboard copy
- PNG save
```

- [ ] **Step 2: Run full tests**

Run:

```powershell
dotnet test Thinksnap.sln
```

Expected: all tests pass.

- [ ] **Step 3: Build release**

Run:

```powershell
dotnet build Thinksnap.sln -c Release
```

Expected: `Build succeeded`.

- [ ] **Step 4: Manual MVP verification**

Run:

```powershell
dotnet run --project src/Thinksnap.App/Thinksnap.App.csproj
```

Verify:

- `Capture Region` opens overlay.
- `Esc` cancels overlay.
- Dragging a region opens editor.
- Pixelate marks a region.
- Arrow, line, rectangle, pen, and text tools add visible annotations.
- Undo removes the most recent edit.
- Copy places the rendered output on clipboard.
- Save writes a PNG.

- [ ] **Step 5: Commit**

Run:

```powershell
git add README.md
git commit -m "docs: add Thinksnap usage notes"
```

---

## Self-Review

Spec coverage:

- Windows C#/WPF target: covered by Task 1 and Task 4.
- Region capture flow: covered by Task 5.
- Pixelate censorship: covered by Task 3, Task 6, and Task 7.
- Arrow, line, rectangle, pen, and text tools: covered by Task 2 and Task 6.
- Undo: covered by Task 2 and Task 6.
- Clipboard and PNG export: covered by Task 7.
- Error handling for overlay cancel, small selection, clipboard, save cancel, and save failure: covered by Task 5 and Task 7.
- Automated tests for pixelate, annotation model, and undo: covered by Task 2 and Task 3.
- Manual verification: covered by Task 8.

No placeholder or deferred MVP requirements remain in the plan.
