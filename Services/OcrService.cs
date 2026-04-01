using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;

namespace ScreenGrab.Services;

public class OcrService : IDisposable
{
    public record OcrWord(string Text, double X, double Y, double Width, double Height, int LineIndex);
    public record OcrResult(List<OcrWord> Words, List<OcrLine> Lines);
    public record OcrLine(string Text, List<OcrWord> Words);

    private Process? _pythonProcess;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private readonly string _scriptPath;
    private bool _ready;

    public OcrService()
    {
        _scriptPath = Path.Combine(AppContext.BaseDirectory, "Scripts", "ocr_server.py");

        // Fall back to project directory during development
        if (!File.Exists(_scriptPath))
        {
            var devPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Scripts", "ocr_server.py");
            if (File.Exists(devPath))
                _scriptPath = Path.GetFullPath(devPath);
        }

        StartPythonProcess();
    }

    private void StartPythonProcess()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"\"{_scriptPath}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _pythonProcess = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Python OCR process.");

        _stdin = _pythonProcess.StandardInput;
        _stdout = _pythonProcess.StandardOutput;

        // Wait for READY signal (PaddleOCR model loading)
        var readyLine = _stdout.ReadLine();
        _ready = readyLine == "READY";

        if (!_ready)
            throw new InvalidOperationException($"OCR server failed to initialize. Got: {readyLine}");
    }

    public async Task<OcrResult> RecognizeAsync(Bitmap bitmap)
    {
        // Save bitmap to temp file
        var tempPath = Path.Combine(Path.GetTempPath(), $"screengrab_{Guid.NewGuid():N}.png");
        try
        {
            bitmap.Save(tempPath, ImageFormat.Png);

            // Send path to Python process
            await _stdin!.WriteLineAsync(tempPath);
            await _stdin.FlushAsync();

            // Read JSON response
            var jsonLine = await _stdout!.ReadLineAsync()
                ?? throw new InvalidOperationException("OCR process returned no output.");

            var response = JsonSerializer.Deserialize<OcrResponse>(jsonLine,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (response?.Error != null)
                throw new InvalidOperationException($"OCR error: {response.Error}");

            return BuildResult(response?.Words ?? []);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    private static OcrResult BuildResult(List<OcrWordJson> rawWords)
    {
        var words = new List<OcrWord>();
        var lineGroups = new Dictionary<int, List<OcrWord>>();

        foreach (var rw in rawWords)
        {
            var word = new OcrWord(rw.Text, rw.X, rw.Y, rw.Width, rw.Height, rw.LineIndex);
            words.Add(word);

            if (!lineGroups.ContainsKey(rw.LineIndex))
                lineGroups[rw.LineIndex] = new List<OcrWord>();
            lineGroups[rw.LineIndex].Add(word);
        }

        var lines = lineGroups
            .OrderBy(kv => kv.Key)
            .Select(kv => new OcrLine(
                string.Join(" ", kv.Value.OrderBy(w => w.X).Select(w => w.Text)),
                kv.Value.OrderBy(w => w.X).ToList()))
            .ToList();

        return new OcrResult(words, lines);
    }

    public void Dispose()
    {
        try
        {
            _stdin?.WriteLine("EXIT");
            _stdin?.Flush();
            _pythonProcess?.WaitForExit(3000);
        }
        catch { }

        _stdin?.Dispose();
        _stdout?.Dispose();
        _pythonProcess?.Kill();
        _pythonProcess?.Dispose();
    }

    // JSON deserialization models
    private record OcrResponse(List<OcrWordJson> Words, string? Error);
    private record OcrWordJson(string Text, double X, double Y, double Width, double Height, int LineIndex, double Confidence);
}
