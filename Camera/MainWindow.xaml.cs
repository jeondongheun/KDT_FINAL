using ModernWpf.Controls;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Threading;
// using static Camera.SafetyCheck;
using Path = System.IO.Path;
using Window = System.Windows.Window;

namespace Camera
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
            CheckSafety.MainWin = this;
            CheckSafety.InitializeModel();

            // SafetyCheck 모델 초기화
            // SafetyCheck.Safety();

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
                                processedFrame = CheckSafety.ProcessFrame(_frame);
                            }

                            /*
                            // 안전 검사 실행
                            SafetyResult safetyResult = null;
                            if (_safetyCheckEnabled && !_isClosing)
                            {
                                safetyResult = SafetyCheck.CheckSafety(_frame);
                            }
                            */

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

                                            /*
                                            // 안전 검사 결과 처리
                                            if (safetyResult != null)
                                            {
                                                // UI 상태 업데이트
                                                UpdateSafetyUI(safetyResult);

                                                // 쿨다운 체크 (너무 자주 경고하지 않기 위해)
                                                if (safetyResult.PersonDetected && DateTime.Now - _lastWarningTime > _warningCooldown)
                                                {
                                                    ErrorCapture(_frame, DateTime.Now, safetyResult.PersonDetected, safetyResult.HasHelmet,
                                                               safetyResult.HasSafetyVest, safetyResult.HasGloves);
                                                    _lastWarningTime = DateTime.Now;
                                                }
                                            }

                                            // 안전 상태 로그 출력 (선택사항)
                                            if (safetyResult != null)
                                            {
                                                Console.WriteLine($"안전 상태: {safetyResult.Message}");
                                            }
                                            */
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

        private void StopCamera()
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

            // 모든 MessageBox 닫기 (가능하다면)
            try
            {
                // MessageBox가 열려있을 수 있으니 강제로 ESC 키 보내기 (선택사항)
                // SendKeys.SendWait("{ESC}");
            }
            catch { }

            StopCamera();

            try
            {
                // SafetyCheck.Dispose(); // 모델 리소스 해제
                CheckSafety.Cleanup();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SafetyCheck 해제 오류: {ex.Message}");
            }

            Console.WriteLine("앱 종료 완료");
            base.OnClosing(e);
        }

        /*
        // 안전한 에러 캡처
        private void ErrorCapture(Mat video, DateTime now, bool personDetected, bool hasHelmet, bool hasVest, bool hasGloves)
        {
            // 종료 중이면 스킵
            if (_isClosing || !personDetected || video == null || video.IsDisposed || video.Empty())
            {
                return;
            }

            string folder = @"C:\Users\user\Desktop\Safety_Equipment_Violations";

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string timeStamp = now.ToString("yyyy-MM-dd_HH-mm-ss");

            // Mat 복사본 생성 (원본 보호)
            using (Mat safeCopy = video.Clone())
            {
                if (safeCopy == null || safeCopy.Empty() || _isClosing) return;

                if (!hasHelmet)
                {
                    string helmetFile = Path.Combine(folder, $"SafetyHelmet_{timeStamp}.jpg");
                    Cv2.ImWrite(helmetFile, safeCopy);

                    ShowWarning("안전모를 착용해 주세요.");
                }

                if (!hasVest)
                {
                    string vestFile = Path.Combine(folder, $"SafetyVest_{timeStamp}.jpg");
                    Cv2.ImWrite(vestFile, safeCopy);

                    ShowWarning("안전 조끼를 착용해 주세요.");
                }

                if (!hasGloves)
                {
                    string gloveFile = Path.Combine(folder, $"SafetyGloves_{timeStamp}.jpg");
                    Cv2.ImWrite(gloveFile, safeCopy);

                    ShowWarning("안전 장갑을 착용해 주세요.");
                }
            }
           
        }

        private void ShowWarning(string message)
        {
            if (!_isClosing && Application.Current != null)
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (!_isClosing)
                    {
                        try
                        {
                            MessageBox.Show(message, "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        catch { }
                    }
                });
            }
        }
        */

        // 착용 여부 판별 후 결과 변경 (UI 반영)
        private void UpdateSafetyUI(bool hasHelmet, bool hasVest, bool hasGloves, bool hasBoots = false)
        {
            if (_isClosing) return;

            try
            {
                // 헬멧
                if (hasHelmet)
                {
                    progHelmet.Visibility = Visibility.Collapsed;
                    checkH.Visibility = Visibility.Visible;
                    warnH.Visibility = Visibility.Collapsed;

                    borderH.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
                    helmets.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    helmets.Text = "안전모 착용 확인";
                }
                else
                {
                    progHelmet.Visibility = Visibility.Collapsed;
                    checkH.Visibility = Visibility.Collapsed;
                    warnH.Visibility = Visibility.Visible;

                    borderH.Background = new SolidColorBrush(Color.FromRgb(254, 252, 232));
                    helmets.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    helmets.Text = "안전모 미착용";
                }

                // 조끼
                if (hasVest)
                {
                    progVest.Visibility = Visibility.Collapsed;
                    checkV.Visibility = Visibility.Visible;
                    warnV.Visibility = Visibility.Collapsed;

                    borderV.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
                    vest.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    vest.Text = "안전 조끼 착용 확인";
                }
                else
                {
                    progVest.Visibility = Visibility.Collapsed;
                    checkV.Visibility = Visibility.Collapsed;
                    warnV.Visibility = Visibility.Visible;

                    borderV.Background = new SolidColorBrush(Color.FromRgb(254, 252, 232));
                    vest.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    vest.Text = "안전 조끼 미착용";
                }

                // 장갑
                if (hasGloves)
                {
                    progGloves.Visibility = Visibility.Collapsed;
                    checkG.Visibility = Visibility.Visible;
                    warnG.Visibility = Visibility.Collapsed;

                    borderG.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
                    gloves.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    gloves.Text = "안전 장갑 착용 확인";
                }
                else
                {
                    progGloves.Visibility = Visibility.Collapsed;
                    checkG.Visibility = Visibility.Collapsed;
                    warnG.Visibility = Visibility.Visible;

                    borderG.Background = new SolidColorBrush(Color.FromRgb(254, 252, 232));
                    gloves.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    gloves.Text = "안전 장갑 미착용";
                }
            }
            catch (Exception ex)
            {
                if (!_isClosing)
                {
                    Console.WriteLine($"UI 업데이트 오류: {ex.Message}");
                }
            }
        }
    }
}