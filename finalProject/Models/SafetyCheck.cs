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
using System.Windows.Media.Imaging;
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
        private static readonly TimeSpan emailCooldown = TimeSpan.FromHours(6);

        // ONNX 모델 세션
        private static InferenceSession session;

        // WorkersInfo로 화면 전환 시 MainWindow 동작 정지
        private static bool isProcessingActive = true;

        private static string currentWorkerId = null;
        private static DateTime lastFaceRecognitionTime = DateTime.MinValue;
        private static readonly TimeSpan faceRecognitionInterval = TimeSpan.FromSeconds(10);

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
                if (DateTime.Now - lastFaceRecognitionTime > faceRecognitionInterval)
                {
                    lastFaceRecognitionTime = DateTime.Now;
                    _ = TryRecognizeFaceAsync(frame);
                }

                if (!string.IsNullOrEmpty(currentWorkerId) &&
                    WorkerSessionManager.IsPPEChecked(currentWorkerId))
                {
                    return frame;
                }

                Debug.WriteLine("YOLO 모델 실행 중...");

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
                var goggles = new List<(int x1, int y1, int x2, int y2)>();

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
                            break;
                        case 4: // goggles (필요하면 처리)
                            goggles.Add((x1, y1, x2, y2));
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
                    Debug.WriteLine($"Person detected: {persons.Count}명");

                    foreach (var person in persons)
                    {
                        AnalyzePPEStatus(frame, person, helmets, vests, gloves, goggles);
                    }

                    if (!string.IsNullOrEmpty(currentWorkerId))
                    {
                        WorkerSessionManager.MarkAsCaptured(currentWorkerId);
                        Debug.WriteLine($"{currentWorkerId} - PPE 검사 완료 표시");
                    }
                }
                else
                {
                    Debug.WriteLine("No person detected");
                    // 사람이 감지되지 않으면 UI를 로딩 상태로 리셋
                    SafetyCheckUI.ResetPPEUI();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"프레임 처리 오류: {ex.Message}");
            }

            return frame;
        }

        // 비동기 안면 인식
        private static async Task TryRecognizeFaceAsync(Mat frame)
        {
            try
            {
                Worker recognizedWorker = await FaceRecognitionService.RecognizeFaceAsync(frame);

                if (recognizedWorker != null)
                {
                    currentWorkerId = recognizedWorker.WorkerId;
                    Debug.WriteLine($"작업자 인식: {recognizedWorker.Name} ({currentWorkerId})");
                }
                else
                {
                    Debug.WriteLine("작업자 인식 실패");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"안면 인식 오류: {ex.Message}");
            }
        }

        // PPE 착용 여부 확인
        private static void AnalyzePPEStatus(Mat frame,
            (int x1, int y1, int x2, int y2, float score) person,
            List<(int x1, int y1, int x2, int y2)> helmets,
            List<(int x1, int y1, int x2, int y2)> vests,
            List<(int x1, int y1, int x2, int y2)> gloves,
            List<(int x1, int y1, int x2, int y2)> goggles)
        {
            // PPE 착용 상태 확인
            bool hasHelmet = helmets.Any(h => RectOverlap(person.x1, person.y1, person.x2, person.y2,
                                                          h.x1, h.y1, h.x2, h.y2));
            bool hasVest = vests.Any(v => RectOverlap(person.x1, person.y1, person.x2, person.y2,
                                                      v.x1, v.y1, v.x2, v.y2));
            bool hasGloves = gloves.Any(g => RectOverlap(person.x1, person.y1, person.x2, person.y2,
                                                         g.x1, g.y1, g.x2, g.y2));
            bool hasGoggles = goggles.Any(g => RectOverlap(person.x1, person.y1, person.x2, person.y2,
                                                         g.x1, g.y1, g.x2, g.y2));

            // 디버깅용 로그
            Debug.WriteLine($"PPE Status - Helmet: {hasHelmet}, Vest: {hasVest}, Gloves: {hasGloves}");

            // UI 업데이트
            SafetyCheckUI.UpdatePPEUI(hasHelmet, hasVest, hasGloves, hasGoggles);

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

                        // 이미지 캡처
                        string capturedImagePath = await WorkersCap.CaptureWorkerImage(frame);

                        // 얼굴 인식 실행
                        Worker recognizedWorker = null;
                        if (!string.IsNullOrEmpty(capturedImagePath))
                        {
                            recognizedWorker = await FaceRecognitionService.RecognizeFaceAsync(capturedImagePath);

                            if (recognizedWorker != null)
                            {
                                Debug.WriteLine($"작업자 인식 성공: {recognizedWorker.Name}");

                                // 중복 출근 체크
                                if (!WorkerSessionManager.TryCheckIn(recognizedWorker.WorkerId))
                                {
                                    // 이미 출근한 작업자
                                    MessageBox.Show($"{recognizedWorker.Name} 님은 이미 출근 처리되었습니다.\n");

                                    // 카메라 재시작하고 메인 화면 유지
                                    isProcessingActive = true;
                                    await MainWin.StartCameraAsync();
                                    return;
                                }
                                WorkerSessionManager.MarkAsCaptured(recognizedWorker.WorkerId);
                            }
                            else
                            {
                                Debug.WriteLine("작업자 인식 실패");
                            }
                        }

                        // 카메라 완전 중단
                        MainWin.StopCamera();

                        // WorkersWin이 null인 경우 초기화
                        if (WorkersWin == null) WorkersWin = new WorkersInfo();

                        SafetyCheckUI.WorkersWin = WorkersWin;
                        Debug.WriteLine($"WorkersWin 초기화 완료: {WorkersWin != null}");
                        Debug.WriteLine($"SafetyCheckUI.WorkersWin 설정 완료: {SafetyCheckUI.WorkersWin != null}");

                        // MainWindow 숨기기
                        MainWin.Hide();

                        // WorkersInfo 출력
                        WorkersWin.Show();
                        SafetyCheckUI.UpdateWorkersInfo(hasHelmet, hasVest, hasGloves, hasGoggles, recognizedWorker);

                        Debug.WriteLine("안전 장비 착용 확인 - 작업자 정보 확인 요망");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"화면 전환 오류: {ex.Message}");
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
                        string tempCapturePath = await WorkersCap.CaptureWorkerImage(frame);

                        Worker recognizedWorker = null;

                        if (!string.IsNullOrEmpty(tempCapturePath))
                        {
                            recognizedWorker = await FaceRecognitionService.RecognizeFaceAsync(tempCapturePath);

                            // 안면 인식용 캡처 삭제
                            try { File.Delete(tempCapturePath); } catch { }
                        }

                        // WorkerId 확보 (인식 실패 시 "UNKNOWN")
                        string workerId = recognizedWorker?.WorkerId ?? "UNKNOWN";

                        // 인식된 작업자가 이미 캡처되었는지 확인
                        bool shouldCapture = true;
                        if (recognizedWorker != null)
                        {
                            if (WorkerSessionManager.WorkersImageCap(workerId))
                            {
                                Debug.WriteLine($"{workerId}는 이미 캡처 완료됨. 추가 캡처 생략.");
                                shouldCapture = false;
                            }
                            else
                            {
                                // 첫 캡처이면 마킹
                                WorkerSessionManager.MarkAsCaptured(workerId);
                            }
                        }

                        // 캡처가 필요한 경우에만 저장
                        if (shouldCapture)
                        {
                            await WorkersCap.CaptureWorkerImage(frame);
                            await WorkersCap.CaptureViolationImage(frame);
                            Debug.WriteLine($"{workerId} PPE 미착용 이미지 캡처 완료");
                        }

                        // 통합 알림으로 여섯 시간에 한 번 메일 전송
                        await SafetyAlert.ProcessViolation(workerId, !hasHelmet, !hasVest, !hasGloves, !hasGoggles);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"이메일 알림 처리 오류: {ex.Message}");
                    }
                });
            }

            // 화면에 상태 표시
            WorkersCap.DrawPPEStatus(frame, person, hasHelmet, hasVest, hasGloves, hasGoggles);
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

        // 24시간에 한 번씩 세션 초기화
        public static void StartDailyResetTimer()
        {
            var timer = new Timer(_ =>
            {
                var now = DateTime.Now;

                // 매일 자정에 세션 초기화
                if (now.Hour == 0 && now.Minute == 0)
                {
                    WorkerSessionManager.ResetDailySessions();
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        public static void Cleanup()
        {
            session?.Dispose();
        }
    }
}