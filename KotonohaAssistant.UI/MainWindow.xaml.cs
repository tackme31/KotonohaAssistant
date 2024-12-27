using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace KotonohaAssistant.UI
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

        private DispatcherTimer _timer;

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

            // タイマーの初期化
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000 / 30) // 30fps
            };
            _timer.Tick += UpdateCapture;
            _timer.Start();
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

        private void UpdateCapture(object sender, EventArgs e)
        {
            var aiEditor = FindWindowTitle("A.I.VOICE Editor");
            if (aiEditor == null)
            {
                Console.WriteLine("ウィンドウが見つかりません。");
                return;
            }

            // ウィンドウハンドルを取得
            IntPtr hWnd = FindWindow(null, aiEditor);
            if (hWnd == IntPtr.Zero)
            {
                Console.WriteLine("ウィンドウが見つかりません。");
                return;
            }

            // ウィンドウのRECTを取得
            if (!GetWindowRect(hWnd, out RECT rect))
            {
                Console.WriteLine("ウィンドウ情報の取得に失敗しました。");
                return;
            }

            // ウィンドウDCを取得
            IntPtr hdcWindow = GetWindowDC(hWnd);
            if (hdcWindow == IntPtr.Zero)
            {
                Console.WriteLine("ウィンドウDCの取得に失敗しました。");
                return;
            }

            int widthOffset = 10;
            int heightOffset = 70;
            int width = 190;
            int height = 300;

            using (var bitmap = new Bitmap(width, height))
            {
                bool success = false;

                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    IntPtr hdc = g.GetHdc();

                    success = BitBlt(hdc, 0, 0, width, height, hdcWindow, rect.Width - width - widthOffset, heightOffset, (uint)TernaryRasterOperations.SRCCOPY);
                }

                // WPF Imageコントロールに画像を表示
                if (success)
                {
                    // (0, 0)の色でどちらが喋ってるか判断
                    Color pixelColor = bitmap.GetPixel(0, 0);
                    if (pixelColor == AkaneColor)
                    {
                        Akane.Source = ConvertBitmapToImageSource(bitmap);
                    }
                    if (pixelColor == AoiColor)
                    {
                        Aoi.Source = ConvertBitmapToImageSource(bitmap);
                    }
                }
            }

            // ウィンドウDCを解放
            ReleaseDC(hWnd, hdcWindow);
        }

        private BitmapSource ConvertBitmapToImageSource(Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }
    }
}
