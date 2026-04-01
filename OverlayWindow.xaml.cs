using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfRect = System.Windows.Shapes.Rectangle;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ScreenGrab.Services;

namespace ScreenGrab;

public partial class OverlayWindow : Window
{
    private readonly List<WordElement> _wordElements = new();
    private readonly List<WordElement> _selectedWords = new();
    private bool _isDragging;
    private System.Windows.Point _dragStart;

    private record WordElement(OcrService.OcrWord Word, WpfRect Rect);

    public OverlayWindow()
    {
        InitializeComponent();
    }

    public void Setup(CaptureService.CaptureResult capture, OcrService.OcrResult ocrResult)
    {
        Left = capture.ScreenLeft / capture.DpiScale;
        Top = capture.ScreenTop / capture.DpiScale;
        Width = capture.ScreenWidth / capture.DpiScale;
        Height = capture.ScreenHeight / capture.DpiScale;

        BackgroundImage.Source = capture.BitmapSource;

        double scaleX = Width / capture.ScreenWidth;
        double scaleY = Height / capture.ScreenHeight;

        foreach (var word in ocrResult.Words)
        {
            var rect = new WpfRect
            {
                Width = word.Width * scaleX,
                Height = word.Height * scaleY,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(rect, word.X * scaleX);
            Canvas.SetTop(rect, word.Y * scaleY);
            WordCanvas.Children.Add(rect);

            _wordElements.Add(new WordElement(word, rect));
        }
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _dragStart = e.GetPosition(WordCanvas);
        ClearSelection();
        WordCanvas.CaptureMouse();

        SelectionRect.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRect, _dragStart.X);
        Canvas.SetTop(SelectionRect, _dragStart.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
    }

    private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging) return;

        var pos = e.GetPosition(WordCanvas);
        var selectionRect = GetSelectionBounds(_dragStart, pos);

        Canvas.SetLeft(SelectionRect, selectionRect.X);
        Canvas.SetTop(SelectionRect, selectionRect.Y);
        SelectionRect.Width = selectionRect.Width;
        SelectionRect.Height = selectionRect.Height;

        UpdateSelection(selectionRect);
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        WordCanvas.ReleaseMouseCapture();

        var pos = e.GetPosition(WordCanvas);
        var selectionRect = GetSelectionBounds(_dragStart, pos);
        UpdateSelection(selectionRect);

        if (_selectedWords.Count > 0)
        {
            StatusText.Text = $"{_selectedWords.Count} word(s) selected. Ctrl+C to copy. Escape to cancel.";
        }
    }

    private static Rect GetSelectionBounds(System.Windows.Point start, System.Windows.Point end)
    {
        return new Rect(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X),
            Math.Abs(end.Y - start.Y));
    }

    private void UpdateSelection(Rect selectionRect)
    {
        ClearSelection();

        foreach (var wordEl in _wordElements)
        {
            double wordLeft = Canvas.GetLeft(wordEl.Rect);
            double wordTop = Canvas.GetTop(wordEl.Rect);
            var wordBounds = new Rect(wordLeft, wordTop, wordEl.Rect.Width, wordEl.Rect.Height);

            if (selectionRect.IntersectsWith(wordBounds))
            {
                wordEl.Rect.Fill = new SolidColorBrush(Color.FromArgb(80, 0, 120, 215));
                _selectedWords.Add(wordEl);
            }
        }
    }

    private void ClearSelection()
    {
        foreach (var wordEl in _selectedWords)
        {
            wordEl.Rect.Fill = Brushes.Transparent;
        }
        _selectedWords.Clear();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            return;
        }

        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CopyAndClose();
            return;
        }
    }

    private void CopyAndClose()
    {
        if (_selectedWords.Count == 0)
        {
            Close();
            return;
        }

        var lineGroups = _selectedWords
            .GroupBy(w => w.Word.LineIndex)
            .OrderBy(g => g.Key);

        var lines = new List<string>();
        foreach (var group in lineGroups)
        {
            var lineText = string.Join(" ", group
                .OrderBy(w => w.Word.X)
                .Select(w => w.Word.Text));
            lines.Add(lineText);
        }

        var text = string.Join("\n", lines);
        System.Windows.Clipboard.SetText(text);

        Close();
    }
}
