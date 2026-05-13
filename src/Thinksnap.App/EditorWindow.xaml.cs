using System.Windows;
using System.Windows.Media.Imaging;
using Thinksnap.Core.Annotations;

namespace Thinksnap.App;

public partial class EditorWindow : Window
{
    public EditorWindow(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        InitializeComponent();
        CanvasHost.SetImage(source);
    }

    private void PixelateButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.ActiveTool = AnnotationTool.Pixelate;
    }

    private void ArrowButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.ActiveTool = AnnotationTool.Arrow;
    }

    private void LineButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.ActiveTool = AnnotationTool.Line;
    }

    private void RectangleButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.ActiveTool = AnnotationTool.Rectangle;
    }

    private void PenButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.ActiveTool = AnnotationTool.Pen;
    }

    private void TextButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.ActiveTool = AnnotationTool.Text;
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        CanvasHost.Undo();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this, "Copy export is implemented in the next task.", "Thinksnap");
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this, "Save export is implemented in the next task.", "Thinksnap");
    }
}
