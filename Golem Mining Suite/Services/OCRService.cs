using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Tesseract;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// Service to capture screen regions and perform OCR text extraction
    /// </summary>
    public class OCRService : IDisposable
    {
        private TesseractEngine? _engine;
        private readonly string _tessDataPath;
        private bool _isInitialized = false;

        // Windows API for screen capture
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
            IntPtr hdcSource, int xSrc, int ySrc, CopyPixelOperation rop);

        public OCRService(string tessDataPath = "tessdata")
        {
            _tessDataPath = tessDataPath;
        }

        /// <summary>
        /// Initialize the Tesseract OCR engine
        /// </summary>
        public bool Initialize()
        {
            try
            {
                // Check if tessdata folder exists
                if (!Directory.Exists(_tessDataPath))
                {
                    // TODO: Download tessdata automatically or provide instructions
                    return false;
                }

                _engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
                _isInitialized = true;
                return true;
            }
            catch (Exception)
            {
                _isInitialized = false;
                return false;
            }
        }

        /// <summary>
        /// Capture a region of the screen
        /// </summary>
        public Bitmap? CaptureScreenRegion(int x, int y, int width, int height)
        {
            try
            {
                Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    IntPtr hdcDest = graphics.GetHdc();
                    IntPtr hdcSrc = GetDC(IntPtr.Zero);

                    BitBlt(hdcDest, 0, 0, width, height, hdcSrc, x, y, CopyPixelOperation.SourceCopy);

                    graphics.ReleaseHdc(hdcDest);
                    ReleaseDC(IntPtr.Zero, hdcSrc);
                }

                return bitmap;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Perform OCR on a bitmap image
        /// </summary>
        public string? ExtractText(Bitmap bitmap)
        {
            if (!_isInitialized || _engine == null)
            {
                return null;
            }

            try
            {
                // Convert Bitmap to Pix using Tesseract's built-in method
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    
                    using (var pix = Pix.LoadFromMemory(ms.ToArray()))
                    using (var page = _engine.Process(pix))
                    {
                        return page.GetText();
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Capture and extract text from a screen region
        /// </summary>
        public string? CaptureAndExtractText(int x, int y, int width, int height)
        {
            using (var bitmap = CaptureScreenRegion(x, y, width, height))
            {
                if (bitmap == null) return null;
                return ExtractText(bitmap);
            }
        }

        /// <summary>
        /// Estimate terminal region based on game window bounds
        /// This is a heuristic - terminal UI is typically in center-bottom of screen
        /// </summary>
        public (int X, int Y, int Width, int Height)? EstimateTerminalRegion(int windowX, int windowY, int windowWidth, int windowHeight)
        {
            // Terminal UI in Star Citizen is typically:
            // - Centered horizontally
            // - In bottom 60% of screen
            // - Takes up about 70% of screen width
            
            int terminalWidth = (int)(windowWidth * 0.7);
            int terminalHeight = (int)(windowHeight * 0.6);
            int terminalX = windowX + (windowWidth - terminalWidth) / 2;
            int terminalY = windowY + (int)(windowHeight * 0.3);

            return (terminalX, terminalY, terminalWidth, terminalHeight);
        }

        public void Dispose()
        {
            _engine?.Dispose();
            _isInitialized = false;
        }
    }
}
