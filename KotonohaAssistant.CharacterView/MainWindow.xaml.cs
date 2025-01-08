using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;

namespace KotonohaAssistant.CharacterView
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, uint dwRop);

        // EnumWindowsのコールバックデリゲート
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private Bitmap _bitmap = new Bitmap(CaptureWidth, CaptureHeight);

        private const int FPS = 1000 / 30; // 30vps

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        private enum TernaryRasterOperations : uint
        {
            SRCCOPY = 0x00CC0020
        }

        /// <summary>
        /// 茜の背景色
        /// </summary>
        private static readonly Color AkaneColor = Color.FromArgb(255, 255, 240, 240);

        /// <summary>
        /// 葵の背景色
        /// </summary>
        private static readonly Color AoiColor = Color.FromArgb(255, 240, 255, 255);

        public MainWindow()
        {
            InitializeComponent();

            _bitmap = new Bitmap(CaptureWidth, CaptureHeight);
            _ = StartCaptureLoop().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    // ログやエラーメッセージの出力
                    Console.WriteLine($"Capture loop error: {t.Exception}");
                }
            });
        }

        private async Task StartCaptureLoop()
        {
            while (true)
            {
                UpdateCapture();
                await Task.Delay(FPS);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _bitmap?.Dispose();
            base.OnClosed(e);
        }

        public static string FindWindowTitle(string targetTitle)
        {
            string foundTitle = null;

            EnumWindows((hWnd, lParam) =>
            {
                var length = GetWindowTextLength(hWnd);
                if (length > 0)
                {
                    var windowTitle = new StringBuilder(length + 1);
                    GetWindowText(hWnd, windowTitle, windowTitle.Capacity);

                    var title = windowTitle.ToString();
                    if (title.Contains(targetTitle))
                    {
                        foundTitle = title;
                        return false; // 列挙を中断
                    }
                }

                return true;
            }, IntPtr.Zero);

            return foundTitle;
        }

        private const int CaptureWidthOffset = 10;
        private const int CaptureHeightOffset = 70;
        private const int CaptureWidth = 190;
        private const int CaptureHeight = 300;

        private void UpdateCapture()
        {
            if(!TryGetWindowData("A.I.VOICE Editor", out var windowData))
            {
                return;
            }

            var success = false;
            using (var g = Graphics.FromImage(_bitmap))
            {
                var hdc = g.GetHdc();
                // エディタ上のキャラクターが写ってる部分をキャプチャ
                success = BitBlt(
                    hdc,
                    xDest: 0,
                    yDest: 0,
                    width: CaptureWidth,
                    height: CaptureHeight,
                    windowData.hdcWindow,
                    xSrc: windowData.rect.Width - CaptureWidth - CaptureWidthOffset,
                    ySrc: CaptureHeightOffset,
                    (uint)TernaryRasterOperations.SRCCOPY);
            }

            if (success)
            {
                // 背景色でどちらが喋ってるか判断
                var pixelColor = GetPixelFast(_bitmap, 0, 0);
                if (pixelColor == AkaneColor)
                {
                    Akane.Source = ConvertBitmapToImageSource(_bitmap);
                }
                else if (pixelColor == AoiColor)
                {
                    Aoi.Source = ConvertBitmapToImageSource(_bitmap);
                }
            }

            ReleaseDC(windowData.hWnd, windowData.hdcWindow);
        }

        private bool TryGetWindowData(string targetWindow, out (IntPtr hWnd, IntPtr hdcWindow, RECT rect) windowData)
        {
            windowData = default;

            var aiEditor = FindWindowTitle(targetWindow);
            if (aiEditor == null)
            {
                Console.WriteLine("ウィンドウが見つかりません。");
                return false;
            }

            var hWnd = FindWindow(null, aiEditor);
            if (hWnd == IntPtr.Zero)
            {
                Console.WriteLine("ウィンドウが見つかりません。");
                return false;
            }

            if (!GetWindowRect(hWnd, out var rect))
            {
                Console.WriteLine("ウィンドウ情報の取得に失敗しました。");
                return false;
            }

            var hdcWindow = GetWindowDC(hWnd);
            if (hdcWindow == IntPtr.Zero)
            {
                Console.WriteLine("ウィンドウDCの取得に失敗しました。");
                return false;
            }

            windowData = (hWnd, hdcWindow, rect);
            return true;
        }

        private BitmapSource ConvertBitmapToImageSource(Bitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var writeableBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            writeableBitmap.WritePixels(
                new Int32Rect(0, 0, width, height),
                bitmapData.Scan0,
                bitmapData.Stride * height,
                bitmapData.Stride);
            bitmap.UnlockBits(bitmapData);
            return writeableBitmap;
        }

        public Color GetPixelFast(Bitmap bitmap, int x, int y)
        {
            // 画像のビットマップデータをロック
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            try
            {
                // ピクセルの位置に基づいて、データを直接読み取る
                var stride = bitmapData.Stride;  // 1行あたりのバイト数
                var ptr = bitmapData.Scan0;  // ピクセルデータの開始位置

                // ピクセルの位置を計算
                var offset = (y * stride) + (x * 4); // 4バイト（ARGB）ごとにピクセル

                // メモリをバイト配列にコピー
                var pixelData = new byte[4]; // ARGB 各1バイト（アルファ、赤、緑、青）
                Marshal.Copy(IntPtr.Add(ptr, offset), pixelData, 0, 4);

                // 色を生成
                return Color.FromArgb(pixelData[3], pixelData[2], pixelData[1], pixelData[0]); // ARGB順
            }
            finally
            {
                // ビットマップデータのロックを解除
                bitmap.UnlockBits(bitmapData);
            }
        }
    }
}
