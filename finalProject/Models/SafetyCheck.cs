using finalProject.Views;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;

namespace finalProject.Models
{
    class SafetyCheck
    {
        public static MainWindow MainWin { get; set; }
        public static WorkersInfo WorkersWin { get; set; }

        // 메세지 박스 자주 뜨는 것 방지
        private static DateTime lastWarningTime = DateTime.MinValue;
        private static readonly TimeSpan warningCooldown = TimeSpan.FromSeconds(10);

        // 이메일 알림 관련
        private static DateTime lastEmailAlert = DateTime.MinValue;
        private static readonly TimeSpan emailCooldown = TimeSpan.FromMinutes(2);

        // ONNX 모델 세션
        private static InferenceSession session;

        // WorkersInfo로 화면 전환 시 MainWindow 동작 정지
        private static bool isProcessingActive = true;

        public static void InitializeModel()
        {
            string modelPath = "best.onnx";     // 모델 경로
            session = new InferenceSession(modelPath);
        }

        public static Mat ProcessFrame(Mat frame)
        {
            if (!isProcessingActive || session == null || frame.Empty()) return frame;

            try
            {
                // 이미지 전처리
                Mat resized = new Mat();
                Cv2.CvtColor(frame, resized, ColorConversionCodes.BGR2RGB);
                Cv2.Resize(resized, resized, new Size(640, 640));

                var inputTensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });
                var data = new float[3 * 640 * 640];
                int idx = 0;
                for (int c = 0; c < 3; c++)
                {
                    for (int y = 0; y < 640; y++)
                    {
                        for (int x = 0; x < 640; x++)
                        {
                            data[idx++] = resized.At<Vec3b>(y, x)[c] / 255.0f;
                        }
                    }
                }
                data.CopyTo(inputTensor.Buffer.Span);

                // 추론 실행
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("images", inputTensor)
                };

                using var results = session.Run(inputs);

                var output = results.First().AsEnumerable<float>().ToArray();
                int numBoxes = output.Length / 6;

                // 각 객체 리스트
                var persons = new List<(int x1, int y1, int x2, int y2, float score)>();
                var helmets = new List<(int x1, int y1, int x2, int y2)>();
                var vests = new List<(int x1, int y1, int x2, int y2)>();
                var gloves = new List<(int x1, int y1, int x2, int y2)>();
                var boots = new List<(int x1, int y1, int x2, int y2)>();

                for (int i = 0; i < numBoxes; i++)
                {
                    float score = output[i * 6 + 4];
                    if (score < 0.3f) continue;

                    int x1 = (int)output[i * 6];
                    int y1 = (int)output[i * 6 + 1];
                    int x2 = (int)output[i * 6 + 2];
                    int y2 = (int)output[i * 6 + 3];
                    int label = (int)output[i * 6 + 5];

                    // 디버깅용 로그
                    Debug.WriteLine($"Detection - Label: {label}, Score: {score:F3}, Box: ({x1},{y1})-({x2},{y2})");

                    // 좌표를 원본 프레임 크기에 맞게 스케일링
                    float scaleX = (float)frame.Width / 640f;
                    float scaleY = (float)frame.Height / 640f;

                    x1 = (int)(x1 * scaleX);
                    y1 = (int)(y1 * scaleY);
                    x2 = (int)(x2 * scaleX);
                    y2 = (int)(y2 * scaleY);

                    // 모델 학습 결과에 따른 라벨 분류
                    // helmet, gloves, vest, boots, goggles, none, Person, no_helmet, no_goggle, no_gloves, no_boots
                    switch (label)
                    {
                        case 0: // helmet
                            helmets.Add((x1, y1, x2, y2));
                            break;
                        case 1: // gloves
                            gloves.Add((x1, y1, x2, y2));
                            break;
                        case 2: // vest
                            vests.Add((x1, y1, x2, y2));
                            break;
                        case 3: // boots
                            boots.Add((x1, y1, x2, y2));
                            break;
                        case 4: // goggles (필요하면 처리)
                            break;
                        case 5: // none - 무시
                            break;
                        case 6: // Person
                            persons.Add((x1, y1, x2, y2, score));
                            break;
                        case 7: // no_helmet
                            // 헬멧 미착용 처리 필요
                            break;
                        case 8: // no_goggle
                            break;
                        case 9: // no_gloves
                            break;
                        case 10: // no_boots
                            break;
                    }
                }

                // 사람이 감지된 경우만 PPE 분석
                if (persons.Count > 0)
                {
                    Console.WriteLine($"Person detected: {persons.Count}명");
                    foreach (var person in persons)
                    {
                        AnalyzePPEStatus(frame, person, helmets, vests, gloves);
                    }
                }
                else
                {
                    Console.WriteLine("No person detected");
                    // 사람이 감지되지 않으면 UI를 로딩 상태로 리셋
                    ResetPPEUI();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"프레임 처리 오류: {ex.Message}");
            }

            return frame;
        }

        private static void AnalyzePPEStatus(Mat frame,
            (int x1, int y1, int x2, int y2, float score) person,
            List<(int x1, int y1, int x2, int y2)> helmets,
            List<(int x1, int y1, int x2, int y2)> vests,
            List<(int x1, int y1, int x2, int y2)> gloves)
        {
            // PPE 착용 상태 확인
            bool hasHelmet = helmets.Any(h => RectOverlap(person.x1, person.y1, person.x2, person.y2,
                                                          h.x1, h.y1, h.x2, h.y2));
            bool hasVest = vests.Any(v => RectOverlap(person.x1, person.y1, person.x2, person.y2,
                                                      v.x1, v.y1, v.x2, v.y2));
            bool hasGloves = gloves.Any(g => RectOverlap(person.x1, person.y1, person.x2, person.y2,
                                                         g.x1, g.y1, g.x2, g.y2));

            // 디버깅용 로그
            Console.WriteLine($"PPE Status - Helmet: {hasHelmet}, Vest: {hasVest}, Gloves: {hasGloves}");

            // UI 업데이트
            UpdatePPEUI(hasHelmet, hasVest, hasGloves);

            // PPE 착용 여부 - 안전모 필착
            bool entryViolation = !hasHelmet;
            if (entryViolation && DateTime.Now - lastWarningTime > warningCooldown)
            {
                lastWarningTime = DateTime.Now;
            }

            // 헬멧 착용 시 MainWindow → WorkersInfo로 페이지 전환
            if (entryViolation)
            {
                // MainWindow 카메라 정지
                isProcessingActive = false;

                // 메인 스레드에서 화면 전환 실행
                MainWin?.Dispatcher.BeginInvoke(async () =>
                {
                    try
                    {
                        await Task.Delay(3000);
                        
                        // 카메라 완전 중단
                        MainWin.StopCamera();

                        // WorkersWin이 null인 경우 초기화
                        if (WorkersWin == null) WorkersWin = new WorkersInfo();

                        // MainWindow 숨기기
                        MainWin.Hide();

                        // WorkersInfo 출력
                        WorkersWin.Show();
                        UpdateWorkersInfo(hasHelmet, hasVest, hasGloves);

                        Debug.WriteLine("안전 장비 착용 확인 - 작업자 정보 확인 요망");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"화면 전환 오류: {ex.Message}");
                    }
                });

                // 화면 전환 후에는 더 이상 PPE 체크를 하지 않고 리턴
                return;
            }

            // PPE 착용 여부 판별 - 미착용 시 담당자에게 이메일 전송
            bool hasViolation = !hasHelmet || !hasVest || !hasGloves;

            // 이메일 알림 체크 및 전송 (더 긴 쿨다운으로 스팸 방지)
            if (hasViolation && DateTime.Now - lastEmailAlert > emailCooldown)
            {
                lastEmailAlert = DateTime.Now;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        // PPE 미착용 시 화면 추가 캡처
                        await CaptureViolationImage(frame);
                        
                        // 통합 알림으로 여섯 시간에 한 번 메일 전송
                        // 개별 알림은 관리자 메일함이 터질 수 있음......
                        await SafetyAlert.ProcessViolation(!hasHelmet, !hasVest, !hasGloves);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"이메일 알림 처리 오류: {ex.Message}");
                    }
                });
            }

            // 화면에 상태 표시
            DrawPPEStatus(frame, person, hasHelmet, hasVest, hasGloves);
        }

        // 안전 장비 위반 시 이미지 캡처
        // 출입 카메라에서 착용 여부와 무관하게 촬영하는 것에 +a
        // 안전 장비 위반 시 이미지 캡처
        private static async Task<string> CaptureViolationImage(Mat frame)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string folder = @"C:\Users\user\Desktop\Workers\Safety_Equipment_Violations";
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    string filename = $"Safety_Violation_{timestamp}.jpg";
                    string fullPath = Path.Combine(folder, filename);

                    // 안전한 복사본으로 이미지 저장
                    using (Mat safeCopy = frame.Clone())
                    {
                        if (!safeCopy.Empty())
                        {
                            Cv2.ImWrite(fullPath, safeCopy);
                            Console.WriteLine($"안전 장비 위반 이미지 저장: {fullPath}");
                            return fullPath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"이미지 캡처 오류: {ex.Message}");
                }
                return null;
            });
        }

        private static void DrawPPEStatus(Mat frame,
            (int x1, int y1, int x2, int y2, float score) person,
            bool hasHelmet, bool hasVest, bool hasGloves)
        {
            // 전체 PPE 준수 여부에 따른 색상 결정
            bool allCompliant = hasHelmet && hasVest && hasGloves;
            Scalar boxColor = allCompliant ? Scalar.Green : Scalar.Red;
            string statusText = allCompliant ? "Safety Compliance" : "PPE inspection required";

            // 바운딩 박스 그리기
            int boxWidth = person.x2 - person.x1;
            int boxHeight = person.y2 - person.y1;

            float scale = 0.6f;
            int newWidth = (int)(boxWidth * scale);
            int newHeight = (int)(boxHeight * scale);
            int newX = person.x1 + (boxWidth - newWidth) / 2;
            int newY = person.y1;

            // 메인 상태 박스
            Cv2.Rectangle(frame, new Rect(newX, newY + 30, statusText.Length * 8, 20), boxColor, -1);
            Cv2.Rectangle(frame, new Rect(newX, newY + 50, newWidth, newHeight), boxColor, 2);
            Cv2.PutText(frame, statusText, new OpenCvSharp.Point(newX + 3, newY + 45),
                        HersheyFonts.HersheySimplex, 0.4, Scalar.White, 1);

            // PPE 상세 상태
            var ppeStatus = new List<string>
            {
                $"헬멧: {(hasHelmet ? "✓" : "✗")}",
                $"조끼: {(hasVest ? "✓" : "✗")}",
                $"장갑: {(hasGloves ? "✓" : "✗")}"
            };

            int yOffset = newY + newHeight + 70;
            for (int i = 0; i < ppeStatus.Count; i++)
            {
                Scalar textColor = ppeStatus[i].Contains("✓") ? Scalar.Green : Scalar.Red;
                Cv2.PutText(frame, ppeStatus[i],
                           new OpenCvSharp.Point(newX, yOffset + i * 15),
                           HersheyFonts.HersheyComplex, 0.3, textColor, 1);
            }
        }

        // PPE UI 상태 업데이트
        private static void UpdatePPEUI(bool hasHelmet, bool hasVest, bool hasGloves)
        {
            MainWin?.Dispatcher.BeginInvoke(() =>
            {
                // 헬멧 상태
                MainWin.progHelmet?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.borderH?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(hasHelmet ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 252, 232)));
                MainWin.helmets?.SetCurrentValue(TextBlock.TextProperty, hasHelmet ? "안전모 착용" : "안전모 미착용");
                MainWin.checkH?.SetCurrentValue(UIElement.VisibilityProperty, hasHelmet ? Visibility.Visible : Visibility.Collapsed);
                MainWin.warnH?.SetCurrentValue(UIElement.VisibilityProperty, hasHelmet ? Visibility.Collapsed : Visibility.Visible);

                // 조끼 상태
                MainWin.progVest?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.borderV?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(hasVest ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 252, 232)));
                MainWin.vest?.SetCurrentValue(TextBlock.TextProperty, hasVest ? "안전 조끼 착용" : "안전 조끼 미착용");
                MainWin.checkV?.SetCurrentValue(UIElement.VisibilityProperty, hasVest ? Visibility.Visible : Visibility.Collapsed);
                MainWin.warnV?.SetCurrentValue(UIElement.VisibilityProperty, hasVest ? Visibility.Collapsed : Visibility.Visible);

                // 장갑 상태
                MainWin.progGloves?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.borderG?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(hasGloves ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 252, 232)));
                MainWin.gloves?.SetCurrentValue(TextBlock.TextProperty, hasGloves ? "안전 장갑 착용" : "안전 장갑 미착용");
                MainWin.checkG?.SetCurrentValue(UIElement.VisibilityProperty, hasGloves ? Visibility.Visible : Visibility.Collapsed);
                MainWin.warnG?.SetCurrentValue(UIElement.VisibilityProperty, hasGloves ? Visibility.Collapsed : Visibility.Visible);
            });
        }

        // PPE UI를 로딩 상태로 리셋
        private static void ResetPPEUI()
        {
            MainWin?.Dispatcher.BeginInvoke(() =>
            {
                // 모든 PPE를 로딩 상태로 리셋
                MainWin.progHelmet?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
                MainWin.borderH?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(240, 245, 255)));
                MainWin.helmets?.SetCurrentValue(TextBlock.TextProperty, "안전모");
                MainWin.checkH?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.warnH?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);

                MainWin.progVest?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
                MainWin.borderV?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(240, 245, 255)));
                MainWin.vest?.SetCurrentValue(TextBlock.TextProperty, "안전 조끼");
                MainWin.checkV?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.warnV?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);

                MainWin.progGloves?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
                MainWin.borderG?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(240, 245, 255)));
                MainWin.gloves?.SetCurrentValue(TextBlock.TextProperty, "안전 장갑");
                MainWin.checkG?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.warnG?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            });
        }

        private static void UpdateWorkersInfo(bool hasHelmet, bool hasVest, bool hasGloves)
        {
            WorkersWin?.Dispatcher.BeginInvoke(() =>
            {
                var currentTime = DateTime.Now;
                WorkersWin.startWork.Text = $"{currentTime:T}";

                // 헬멧 상태
                WorkersWin.progHelmet?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                WorkersWin.borderH?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(hasHelmet ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 252, 232)));
                WorkersWin.checkH?.SetCurrentValue(UIElement.VisibilityProperty, hasHelmet ? Visibility.Visible : Visibility.Collapsed);
                WorkersWin.warnH?.SetCurrentValue(UIElement.VisibilityProperty, hasHelmet ? Visibility.Collapsed : Visibility.Visible);

                // 조끼 상태
                WorkersWin.progVest?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                WorkersWin.borderV?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(hasVest ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 252, 232)));
                WorkersWin.checkV?.SetCurrentValue(UIElement.VisibilityProperty, hasVest ? Visibility.Visible : Visibility.Collapsed);
                WorkersWin.warnV?.SetCurrentValue(UIElement.VisibilityProperty, hasVest ? Visibility.Collapsed : Visibility.Visible);

                // 장갑 상태
                WorkersWin.progGloves?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                WorkersWin.borderG?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(hasGloves ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 252, 232)));
                WorkersWin.checkG?.SetCurrentValue(UIElement.VisibilityProperty, hasGloves ? Visibility.Visible : Visibility.Collapsed);
                WorkersWin.warnG?.SetCurrentValue(UIElement.VisibilityProperty, hasGloves ? Visibility.Collapsed : Visibility.Visible);

                // txtAlert에 현재 PPE 착용 상태 표시
                if (WorkersWin.txtAlert != null)
                {
                    var missingItems = new List<string>();
                    if (!hasHelmet) missingItems.Add("안전모");
                    if (!hasVest) missingItems.Add("안전 조끼");
                    if (!hasGloves) missingItems.Add("안전 장갑");

                    if (missingItems.Count == 0)
                    {
                        WorkersWin.txtAlert.Text = "모든 안전 장비 착용 완료";
                    }
                    else
                    {
                        WorkersWin.txtAlert.Text = $"안전 장비 착용 후 입장해 주세요.";
                    }
                }

                // 근무 시간 판별
                int hour = currentTime.Hour;

                if (hour >= 6 && hour < 14)
                    WorkersWin.workTime.Text = "오전";
                else if (hour >= 14 && hour < 22)
                    WorkersWin.workTime.Text = "오후";
                else
                    WorkersWin.workTime.Text = "야간";
            });
        }

        // 사각형 겹침 여부 판단 (좀 더 관대하게)
        private static bool RectOverlap(int x1a, int y1a, int x2a, int y2a,
                                        int x1b, int y1b, int x2b, int y2b)
        {
            int x_overlap = Math.Max(0, Math.Min(x2a, x2b) - Math.Max(x1a, x1b));
            int y_overlap = Math.Max(0, Math.Min(y2a, y2b) - Math.Max(y1a, y1b));

            // 겹치는 영역의 최소 크기 조건을 완화
            return x_overlap > 10 && y_overlap > 10;
        }

        public static void Cleanup()
        {
            session?.Dispose();
        }
    }
}