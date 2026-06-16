using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Channels;

#pragma warning disable CA1416 // System.Drawing is enabled for the ComfileTech.WinForms Linux target.

namespace ComfileTech.ComfilePi.QrCodeDemo
{
    public partial class Form1 : Form
    {
        private const int PreviewWidth = 480;
        private const int PreviewHeight = 320;
        private const int FrameSize = PreviewWidth * PreviewHeight * 3;
        private const string ZBarLib = "libzbar.so.0";
        private const string RepositoryUrl = "https://github.com/COMFILE-Technology/ComfileTech.ComfilePi.QrCodeDemo";

        private static readonly uint FourCC = 'Y' | ((uint)'8' << 8) | ((uint)'0' << 16) | ((uint)'0' << 24);

        private IntPtr _scanner;
        private Process? _cameraProcess;
        private CancellationTokenSource? _streamCts;
        private Channel<byte[]> _previewChannel = Channel.CreateUnbounded<byte[]>();
        private Channel<byte[]> _scanChannel = Channel.CreateUnbounded<byte[]>();
        private Task? _parseTask;
        private Task? _previewTask;
        private Task? _scanTask;
        private bool _started;

        public Form1()
        {
            InitializeComponent();
            Shown += Form1_Shown;
            FormClosing += Form1_FormClosing;
        }

        private void Form1_Shown(object? sender, EventArgs e)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            StartStreaming();
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            StopStreaming();
        }

        private static void RepositoryLinkLabel_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = RepositoryUrl,
                UseShellExecute = true
            });
        }

        private void StartStreaming()
        {
            try
            {
                _scanner = zbar_image_scanner_create();
            }
            catch (DllNotFoundException)
            {
                SetStatus($"ZBar library not found: {ZBarLib}");
                return;
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to initialize ZBar: {ex.Message}");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "gst-launch-1.0",
                Arguments =
                    "libcamerasrc ! " +
                    $"video/x-raw,width={PreviewWidth},height={PreviewHeight},framerate=30/1,format=NV12 ! " +
                    "videoconvert ! " +
                    "video/x-raw,format=RGB ! " +
                    "multipartmux boundary=frame ! " +
                    "fdsink fd=1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                CreateNoWindow = true
            };

            try
            {
                _cameraProcess = Process.Start(psi);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to start GStreamer: {ex.Message}");
                DestroyScanner();
                return;
            }

            if (_cameraProcess is null)
            {
                SetStatus("Failed to start GStreamer.");
                DestroyScanner();
                return;
            }

            _streamCts = new CancellationTokenSource();
            _previewChannel = Channel.CreateUnbounded<byte[]>();
            _scanChannel = Channel.CreateUnbounded<byte[]>();

            _parseTask = ParseFramesAsync(_cameraProcess.StandardOutput.BaseStream, _streamCts);
            _previewTask = GeneratePreviewAsync(_streamCts);
            _scanTask = ScanForQrCodeAsync(_streamCts);
        }

        private Task ParseFramesAsync(Stream stream, CancellationTokenSource cts)
        {
            return Task.Run(async () =>
            {
                try
                {
                    var previewWriter = _previewChannel.Writer;
                    var scanWriter = _scanChannel.Writer;

                    while (!cts.IsCancellationRequested)
                    {
                        if (!SkipToFramePayload(stream, cts.Token))
                        {
                            break;
                        }

                        var frame = new byte[FrameSize];
                        await stream.ReadExactlyAsync(frame, cts.Token);

                        await previewWriter.WriteAsync(frame, cts.Token);
                        await scanWriter.WriteAsync(frame, cts.Token);
                    }

                    if (!cts.IsCancellationRequested)
                    {
                        SetStatus("Camera stream ended.");
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (EndOfStreamException)
                {
                    if (!cts.IsCancellationRequested)
                    {
                        SetStatus("Camera stream ended.");
                    }
                }
                catch (Exception ex)
                {
                    if (!cts.IsCancellationRequested)
                    {
                        SetStatus($"Camera stream error: {ex.Message}");
                    }
                }
                finally
                {
                    _previewChannel.Writer.TryComplete();
                    _scanChannel.Writer.TryComplete();
                    cts.Cancel();
                }
            });
        }

        private Task GeneratePreviewAsync(CancellationTokenSource cts)
        {
            return Task.Run(async () =>
            {
                try
                {
                    await foreach (var frame in _previewChannel.Reader.ReadAllAsync(cts.Token))
                    {
                        var image = CreateBitmapFromRgb(frame);
                        if (!PostToUi(() =>
                            {
                                var oldImage = previewPictureBox.Image;
                                previewPictureBox.Image = image;
                                oldImage?.Dispose();
                            }))
                        {
                            image.Dispose();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    if (!cts.IsCancellationRequested)
                    {
                        SetStatus($"Preview error: {ex.Message}");
                    }
                }
            });
        }

        private Task ScanForQrCodeAsync(CancellationTokenSource cts)
        {
            return Task.Run(async () =>
            {
                try
                {
                    await foreach (var frame in _scanChannel.Reader.ReadAllAsync(cts.Token))
                    {
                        var text = ScanQrFromRgb(frame);
                        if (!string.IsNullOrEmpty(text))
                        {
                            SetStatus($"QR Code: {text}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (DllNotFoundException)
                {
                    if (!cts.IsCancellationRequested)
                    {
                        SetStatus($"ZBar library not found: {ZBarLib}");
                    }
                }
                catch (Exception ex)
                {
                    if (!cts.IsCancellationRequested)
                    {
                        SetStatus($"QR scanner error: {ex.Message}");
                    }
                }
            });
        }

        private static bool SkipToFramePayload(Stream stream, CancellationToken token)
        {
            var matched = 0;
            ReadOnlySpan<byte> marker = stackalloc byte[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };

            while (!token.IsCancellationRequested)
            {
                var value = stream.ReadByte();
                if (value < 0)
                {
                    return false;
                }

                if (value == marker[matched])
                {
                    matched++;
                    if (matched == marker.Length)
                    {
                        return true;
                    }
                }
                else
                {
                    matched = value == marker[0] ? 1 : 0;
                }
            }

            return false;
        }

        private static Bitmap CreateBitmapFromRgb(byte[] rgb)
        {
            var bgr = new byte[FrameSize];
            for (var i = 0; i < rgb.Length; i += 3)
            {
                bgr[i] = rgb[i + 2];
                bgr[i + 1] = rgb[i + 1];
                bgr[i + 2] = rgb[i];
            }

            var bitmap = new Bitmap(PreviewWidth, PreviewHeight, PixelFormat.Format24bppRgb);
            var data = bitmap.LockBits(
                new Rectangle(0, 0, PreviewWidth, PreviewHeight),
                ImageLockMode.WriteOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                var sourceStride = PreviewWidth * 3;
                if (data.Stride == sourceStride)
                {
                    Marshal.Copy(bgr, 0, data.Scan0, bgr.Length);
                }
                else
                {
                    for (var y = 0; y < PreviewHeight; y++)
                    {
                        Marshal.Copy(bgr, y * sourceStride, IntPtr.Add(data.Scan0, y * data.Stride), sourceStride);
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }

        private string? ScanQrFromRgb(ReadOnlySpan<byte> rgb)
        {
            if (_scanner == IntPtr.Zero)
            {
                return null;
            }

            var gray = new byte[PreviewWidth * PreviewHeight];
            for (var i = 0; i < rgb.Length; i += 3)
            {
                var y = (int)((rgb[i] * 0.299) + (rgb[i + 1] * 0.587) + (rgb[i + 2] * 0.114));
                gray[i / 3] = (byte)Math.Clamp(y, 0, 255);
            }

            var image = zbar_image_create();
            if (image == IntPtr.Zero)
            {
                return null;
            }

            var dataPtr = Marshal.AllocHGlobal(gray.Length);
            try
            {
                zbar_image_set_format(image, FourCC);
                zbar_image_set_size(image, PreviewWidth, PreviewHeight);

                Marshal.Copy(gray, 0, dataPtr, gray.Length);
                zbar_image_set_data(image, dataPtr, (UIntPtr)gray.Length, IntPtr.Zero);

                if (zbar_scan_image(_scanner, image) <= 0)
                {
                    return null;
                }

                var symbol = zbar_image_first_symbol(image);
                return symbol != IntPtr.Zero ? Marshal.PtrToStringAnsi(zbar_symbol_get_data(symbol)) : null;
            }
            finally
            {
                zbar_image_destroy(image);
                Marshal.FreeHGlobal(dataPtr);
            }
        }

        private void StopStreaming()
        {
            var cts = _streamCts;
            _streamCts = null;

            if (cts is not null && !cts.IsCancellationRequested)
            {
                cts.Cancel();
            }

            _previewChannel.Writer.TryComplete();
            _scanChannel.Writer.TryComplete();

            StopCameraProcess();
            WaitForWorkers();
            DestroyScanner();

            cts?.Dispose();
            _parseTask = null;
            _previewTask = null;
            _scanTask = null;

            var oldImage = previewPictureBox.Image;
            previewPictureBox.Image = null;
            oldImage?.Dispose();
        }

        private void StopCameraProcess()
        {
            var process = _cameraProcess;
            _cameraProcess = null;

            if (process is null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        private void WaitForWorkers()
        {
            var tasks = new[] { _parseTask, _previewTask, _scanTask }.Where(task => task is not null).Cast<Task>().ToArray();
            if (tasks.Length == 0)
            {
                return;
            }

            try
            {
                Task.WaitAll(tasks, TimeSpan.FromSeconds(2));
            }
            catch
            {
            }
        }

        private void DestroyScanner()
        {
            if (_scanner == IntPtr.Zero)
            {
                return;
            }

            zbar_image_scanner_destroy(_scanner);
            _scanner = IntPtr.Zero;
        }

        private void SetStatus(string text)
        {
            if (IsDisposed)
            {
                return;
            }

            if (!IsHandleCreated || !InvokeRequired)
            {
                statusLabel.Text = text;
                return;
            }

            PostToUi(() => statusLabel.Text = text);
        }

        private bool PostToUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return false;
            }

            try
            {
                BeginInvoke((MethodInvoker)(() =>
                {
                    if (!IsDisposed)
                    {
                        action();
                    }
                }));
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        [DllImport(ZBarLib)]
        private static extern IntPtr zbar_image_scanner_create();

        [DllImport(ZBarLib)]
        private static extern void zbar_image_scanner_destroy(IntPtr scanner);

        [DllImport(ZBarLib)]
        private static extern IntPtr zbar_image_create();

        [DllImport(ZBarLib)]
        private static extern void zbar_image_destroy(IntPtr image);

        [DllImport(ZBarLib)]
        private static extern void zbar_image_set_format(IntPtr image, uint fourcc);

        [DllImport(ZBarLib)]
        private static extern void zbar_image_set_size(IntPtr image, uint width, uint height);

        [DllImport(ZBarLib)]
        private static extern void zbar_image_set_data(IntPtr image, IntPtr data, UIntPtr length, IntPtr cleanup);

        [DllImport(ZBarLib)]
        private static extern int zbar_scan_image(IntPtr scanner, IntPtr image);

        [DllImport(ZBarLib)]
        private static extern IntPtr zbar_image_first_symbol(IntPtr image);

        [DllImport(ZBarLib)]
        private static extern IntPtr zbar_symbol_get_data(IntPtr symbol);
    }
}
