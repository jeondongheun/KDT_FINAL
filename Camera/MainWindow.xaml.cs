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
using static Camera.SafetyCheck;
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

        // 안전 검사 관련 변수
        private bool _safetyCheckEnabled = true;
        private DateTime _lastWarningTime = DateTime.MinValue;
        private readonly TimeSpan _warningCooldown = TimeSpan.FromSeconds(5);   // 쿨다운

        public MainWindow()
        {
            InitializeComponent();
            _frame = new Mat();

            // SafetyCheck 모델 초기화
            SafetyCheck.Safety();

            // 창 로드 시 자동 실행
            this.Loaded += MainWindow_Loaded;
        }

        // 창 로드 시 자동 실행 이벤트 핸들러
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await StartCameraAsync();
        }

        private async Task StartCameraAsync()
        {
            if (_isRunning) return;

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
                await Task.Delay(2000);
                await RunCameraLoopAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MessageBox.Show($"카메라 동작 오류: {ex.Message}", "Error");
                StopCamera();
            }
        }

        private async Task RunCameraLoopAsync(CancellationToken token)
        {
            await Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (_capture == null || !_capture.IsOpened()) break;

                    if (_capture.Read(_frame) && !_frame.Empty())
                    {
                        // 안전 검사 실행
                        SafetyResult safetyResult = null;
                        if (_safetyCheckEnabled)
                        {
                            safetyResult = SafetyCheck.CheckSafety(_frame);
                        }

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            BitmapSource bmp = _frame.ToWriteableBitmap();
                            imgCamera.Source = bmp;

                            // 카메라 실행 시 라이브 문구 송출
                            liveDot.Visibility = Visibility.Visible;
                            txtLive.Visibility = Visibility.Visible;
                            checkH.Visibility = Visibility.Collapsed;
                            warnH.Visibility = Visibility.Collapsed;

                            // 카메라 실행 시 문구 및 로딩바 숨김
                            progressRing.Visibility = Visibility.Collapsed;
                            camera.Visibility = Visibility.Collapsed;
                            txtCam.Visibility = Visibility.Collapsed;
                            //checkH.Visibility = Visibility.Collapsed;
                            //warnH.Visibility = Visibility.Collapsed;

                            // 안전 검사 결과 처리
                            if (safetyResult != null)
                            {
                                // UI 상태 업데이트
                                UpdateSafetyUI(safetyResult);

                                // 쿨다운 체크 (너무 자주 경고하지 않기 위해)
                                if (DateTime.Now - _lastWarningTime > _warningCooldown)
                                {
                                    ErrorCapture(_frame, DateTime.Now, safetyResult.HasHelmet,
                                               safetyResult.HasSafetyVest, safetyResult.HasGloves);
                                    _lastWarningTime = DateTime.Now;
                                }
                            }

                            // 안전 상태 로그 출력 (선택사항)
                            if (safetyResult != null)
                            {
                                Console.WriteLine($"안전 상태: {safetyResult.Message}");
                            }
                        });
                    }
                    else
                    {
                        await Task.Delay(10, token);
                    }

                    await Task.Delay(15, token);
                }

                _frame?.Release();
                _frame = null;
            }, token);
        }

        private void StopCamera()
        {
            if (!_isRunning) return;

            try
            {
                _cts?.Cancel();
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

            _isRunning = false;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            StopCamera();
            SafetyCheck.Dispose();      // 모델 리소스 해제
            base.OnClosing(e);
        }

        // 에러 발생 시 캡처
        private static void ErrorCapture(Mat video, DateTime now, bool hasHelmet, bool hasVest, bool hasGloves)
        {
            // 저장 폴더 경로
            string folder = @"C:\Users\user\Desktop\안전 장비 미착용 작업자";
            if (!Directory.Exists(folder))
            {
                // 폴더 없으면 생성
                Directory.CreateDirectory(folder);
            }


            try
            {
                // 시간 포맷에 시분초 추가 (같은 날 여러 번 촬영 대비)
                string timeStamp = now.ToString("yyyy-MM-dd_HH-mm-ss");

                if (!hasHelmet)
                {
                    string helmetFile = Path.Combine(folder, $"SafetyHelmet_{timeStamp}.jpg");
                    Cv2.ImWrite(helmetFile, video);

                    // UI 스레드에서 MessageBox 실행
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("안전모를 착용해 주세요.", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                }

                if (!hasVest)
                {
                    string vestFile = Path.Combine(folder, $"SafetyVest_{timeStamp}.jpg");
                    Cv2.ImWrite(vestFile, video);

                    // UI 스레드에서 MessageBox 실행
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("안전 조끼를 착용해 주세요.", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                }

                if (!hasGloves)
                {
                    string glovesFile = Path.Combine(folder, $"SafetyGloves_{timeStamp}.jpg");
                    Cv2.ImWrite(glovesFile, video);

                    // UI 스레드에서 MessageBox 실행
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("안전 장갑을 착용해 주세요.", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"예외 발생 : {e.Message}");
            }
        }

        // 착용 여부 판별 후 결과 변경 (UI 반영)
        private void UpdateSafetyUI(SafetyCheck.SafetyResult result)
        {
            if(result.HasHelmet)
            {
                progHelmet.Visibility = Visibility.Collapsed;
                checkH.Visibility = Visibility.Visible;
                
                borderH.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
                helmets.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); 
                helmets.Text = "안전모 착용 확인";
            } else
            {
                progHelmet.Visibility = Visibility.Collapsed;
                warnH.Visibility = Visibility.Visible;

                borderH.Background = new SolidColorBrush(Color.FromRgb(254, 252, 232));
                helmets.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)); 
                helmets.Text = "안전모 미착용";
            }

            if (result.HasSafetyVest)
            {
                progVest.Visibility = Visibility.Collapsed;
                checkV.Visibility = Visibility.Visible;

                borderV.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
                vest.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                vest.Text = "안전모 조끼 착용 확인";
            } else
            {
                progVest.Visibility = Visibility.Collapsed;
                warnV.Visibility = Visibility.Visible;

                borderV.Background = new SolidColorBrush(Color.FromRgb(254, 252, 232));
                vest.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                vest.Text = "안전 조끼 미착용";
            }

            if (result.HasGloves)
            {
                progGloves.Visibility = Visibility.Collapsed;
                checkG.Visibility = Visibility.Visible;

                borderG.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
                gloves.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                gloves.Text = "안전 장갑 착용 확인";
            } else
            {
                progGloves.Visibility = Visibility.Collapsed;
                warnG.Visibility = Visibility.Visible;

                borderG.Background = new SolidColorBrush(Color.FromRgb(254, 252, 232));
                gloves.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                gloves.Text = "안전 장갑 미착용";
            }
        } 
    }
}