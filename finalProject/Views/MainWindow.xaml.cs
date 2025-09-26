using finalProject.Models;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;
using Window = System.Windows.Window;

namespace finalProject
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private VideoCapture _capture;
        private Mat _frame;
        private CancellationTokenSource _cts;
        private bool _isRunning = false;
        private bool _isClosing = false;

        // 안전 검사 관련 변수
        private bool _safetyCheckEnabled = true;
        private DateTime _lastWarningTime = DateTime.MinValue;
        private readonly TimeSpan _warningCooldown = TimeSpan.FromSeconds(5);   // 쿨다운

        public MainWindow()
        {
            InitializeComponent();
            _frame = new Mat();

            // CameraDetection 초기화 및 MainWindow 참조 설정
            SafetyCheck.MainWin = this;
            SafetyCheck.InitializeModel();

            // 창 로드 시 자동 실행
            this.Loaded += MainWindow_Loaded;
        }

        // 창 로드 시 자동 실행 이벤트 핸들러
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isClosing)
            {
                await StartCameraAsync();
            }
        }

        private async Task StartCameraAsync()
        {
            if (_isRunning || _isClosing) return;

            try
            {
                _capture = new VideoCapture(0);
                if (!_capture.IsOpened())
                {
                    MessageBox.Show("카메라를 열 수 없습니다.", "Error");
                    _capture?.Release();
                    _capture?.Dispose();
                    _capture = null;
                    return;
                }

                _cts = new CancellationTokenSource();
                _isRunning = true;

                // Live 문구 숨기기
                liveDot.Visibility = Visibility.Collapsed;
                txtLive.Visibility = Visibility.Collapsed;

                // 카메라 시작 전 딜레이 2초 (줄일 수도 있음)
                if (!_isClosing)
                {
                    await Task.Delay(2000, _cts.Token);
                    await RunCameraLoopAsync(_cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // 정상이면 무시
            }
            catch (Exception ex)
            {
                if (!_isClosing)
                {
                    MessageBox.Show($"카메라 동작 오류: {ex.Message}", "Error");
                }
                StopCamera();
            }
        }

        private async Task RunCameraLoopAsync(CancellationToken token)
        {
            await Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && !_isClosing)
                {
                    if (_capture == null || !_capture.IsOpened() && !_isClosing) break;

                    try
                    {

                        if (_capture.Read(_frame) && !_frame.Empty() && !_isClosing)
                        {
                            // CameraDetection으로 프레임 처리
                            Mat processedFrame = null;

                            if (_safetyCheckEnabled && !_isClosing)
                            {
                                processedFrame = SafetyCheck.ProcessFrame(_frame);
                            }

                            if (!_isClosing && Application.Current != null)
                            {
                                try
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (_isClosing) return;

                                        try
                                        {
                                            BitmapSource bmp = _frame.ToWriteableBitmap();
                                            imgCamera.Source = bmp;

                                            // 카메라 실행 시 라이브 문구 송출
                                            liveDot.Visibility = Visibility.Visible;
                                            txtLive.Visibility = Visibility.Visible;

                                            // 카메라 실행 시 문구 및 로딩바 숨김
                                            progressRing.Visibility = Visibility.Collapsed;
                                            camera.Visibility = Visibility.Collapsed;
                                            txtCam.Visibility = Visibility.Collapsed;

                                            WorkersCap(_frame, DateTime.Now);
                                        }
                                        catch (Exception uiEx)
                                        {
                                            Console.WriteLine($"UI 업데이트 오류: {uiEx.Message}");
                                        }

                                    });
                                }
                                catch (TaskCanceledException)
                                {
                                    // Dispatcher 작업이 취소됨, 무시
                                    break;
                                }
                            }
                        }
                        else
                        {
                            await Task.Delay(10, token);
                        }
                        await Task.Delay(15, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break; // 정상적인 취소
                    }
                    catch (ObjectDisposedException)
                    {
                        break; // Mat이 해제됨
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"카메라 루프 오류: {ex.Message}");

                        if (!_isClosing)
                        {
                            await Task.Delay(100, token); // 잠시 대기 후 재시도
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                // 루프 종료 시 안전하게 Mat 해제
                try
                {
                    _frame?.Release();
                    _frame = null;
                }
                catch { }
            }, token);
        }

        public void StopCamera()
        {
            if (!_isRunning) return;

            Console.WriteLine("카메라 중지 시작...");
            _isRunning = false;

            try
            {
                _cts?.Cancel();
            }
            catch { }

            // CancellationToken이 처리될 시간을 줌
            Thread.Sleep(100);

            try
            {
                _cts?.Dispose();
                _cts = null;
            }
            catch { }

            try
            {
                _capture?.Release();
                _capture?.Dispose();
                _capture = null;
            }
            catch { }

            Console.WriteLine("카메라 중지 완료");
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            Console.WriteLine("앱 종료 시작...");
            _isClosing = true;

            StopCamera();

            try
            {
                // SafetyCheck.Dispose(); // 모델 리소스 해제
                SafetyCheck.Cleanup();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SafetyCheck 해제 오류: {ex.Message}");
            }

            Console.WriteLine("앱 종료 완료");
            base.OnClosing(e);
        }

        // 직원 촬영
        // 얼굴 인식 가능해지면 사람마다 폴더 생성, 파일명 사람 이름으로 매칭, 사람당 하루에 한 번 촬영
        private void WorkersCap(Mat video, DateTime now)
        {
            string folder = @"C:\Users\user\Desktop\Workers";

            // 폴더가 없으면 폴더 생성
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
            }

            string timeStamp = now.ToString("yyyy-MM-dd_HH-mm-ss");

            // Mat 복사본 생성 (원본 보호)
            using (Mat safeCopy = video.Clone())
            {
                string workersImg = Path.Combine(folder, $"이름_{timeStamp}.jpg");
                Cv2.ImWrite(workersImg, safeCopy);
            }
        }
    }
}