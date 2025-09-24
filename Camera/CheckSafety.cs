using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Point = OpenCvSharp.Point;
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;

namespace Camera
{
    internal class CheckSafety
    {
        public static MainWindow MainWin { get; set; }

        // 메세지 박스 자주 뜨는 것 방지 (쿨다운)
        private static DateTime lastWarningTime = DateTime.MinValue;
        private static readonly TimeSpan warningCooldown = TimeSpan.FromSeconds(3);

        // ONNX 모델 세션
        private static InferenceSession session;

        public static void InitializeModel()
        {
            string modelPath = "best.onnx";     // 모델 경로
            session = new InferenceSession(modelPath);
        }

        public static Mat ProcessFrame(Mat frame)
        {
            if (session == null || frame.Empty()) return frame;

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
                    // all, helmet, gloves, vest, boots, none, Person, no_helmet, no_gloves 순서
                    switch (label)
                    {
                        case 0: // all - 무시
                            break;
                        case 1: // helmet
                            helmets.Add((x1, y1, x2, y2));
                            break;
                        case 2: // gloves
                            gloves.Add((x1, y1, x2, y2));
                            break;
                        case 3: // vest
                            vests.Add((x1, y1, x2, y2));
                            break;
                        case 4: // boots
                            break;
                        case 5: // none - 무시
                            break;
                        case 6: // Person
                            persons.Add((x1, y1, x2, y2, score));
                            break;
                        case 7: // no_helmet
                                // 헬멧 미착용으로 처리
                            break;
                        case 8: // no_gloves
                                // 장갑 미착용으로 처리
                            break;
                    }
                }

                // 사람이 감지된 경우만 PPE 분석
                if (persons.Count > 0)
                {
                    Console.WriteLine($"Person detected: {persons.Count}명");
                    foreach (var person in persons)
                    {
                        AnalyzePPEStatus(frame, person, helmets, vests, gloves, boots);
                    }
                }
                else
                {
                    Console.WriteLine("No person detected");
                    // 사람이 감지되지 않으면 UI를 로딩 상태로 리셋
                    ResetPPEUI();
                }

                // 현재 시간 표시
                string txtTime = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                Cv2.PutText(frame, txtTime, new Point(440, 25),
                           HersheyFonts.HersheyComplex, 0.5, Scalar.Tomato);
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
            List<(int x1, int y1, int x2, int y2)> gloves,
            List<(int x1, int y1, int x2, int y2)> boots)
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

            // PPE 미착용 체크 및 경고
            bool hasViolation = !hasHelmet || !hasVest || !hasGloves;
            if (hasViolation && DateTime.Now - lastWarningTime > warningCooldown)
            {
                lastWarningTime = DateTime.Now;
                ShowPPEWarning(hasHelmet, hasVest, hasGloves);
            }

            // 화면에 상태 표시
            DrawPPEStatus(frame, person, hasHelmet, hasVest, hasGloves);
        }

        private static void DrawPPEStatus(Mat frame,
            (int x1, int y1, int x2, int y2, float score) person,
            bool hasHelmet, bool hasVest, bool hasGloves)
        {
            // 전체 PPE 준수 여부에 따른 색상 결정
            bool allCompliant = hasHelmet && hasVest && hasGloves;
            Scalar boxColor = allCompliant ? Scalar.Green : Scalar.Red;
            string statusText = allCompliant ? "안전 준수" : "PPE 점검 필요";

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

        // PPE 경고 메시지 표시
        private static void ShowPPEWarning(bool hasHelmet, bool hasVest, bool hasGloves)
        {
            var missing = new List<string>();
            if (!hasHelmet) missing.Add("- 안전모");
            if (!hasVest) missing.Add("- 안전조끼");
            if (!hasGloves) missing.Add("- 안전장갑");

            string message = "다음 개인보호구를 착용해 주세요:\n" + string.Join("\n", missing);

            MainWin?.Dispatcher.BeginInvoke(() =>
            {
                MessageBox.Show(message, "안전 경고", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        // PPE UI 상태 업데이트
        private static void UpdatePPEUI(bool hasHelmet, bool hasVest, bool hasGloves)
        {
            MainWin?.Dispatcher.BeginInvoke(() =>
            {
                // 헬멧 상태
                MainWin.progHelmet?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.checkH?.SetCurrentValue(UIElement.VisibilityProperty, hasHelmet ? Visibility.Visible : Visibility.Collapsed);
                MainWin.warnH?.SetCurrentValue(UIElement.VisibilityProperty, hasHelmet ? Visibility.Collapsed : Visibility.Visible);

                // 조끼 상태
                MainWin.progVest?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.checkV?.SetCurrentValue(UIElement.VisibilityProperty, hasVest ? Visibility.Visible : Visibility.Collapsed);
                MainWin.warnV?.SetCurrentValue(UIElement.VisibilityProperty, hasVest ? Visibility.Collapsed : Visibility.Visible);

                // 장갑 상태
                MainWin.progGloves?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
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
                MainWin.checkH?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.warnH?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);

                MainWin.progVest?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
                MainWin.checkV?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.warnV?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);

                MainWin.progGloves?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
                MainWin.checkG?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.warnG?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
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