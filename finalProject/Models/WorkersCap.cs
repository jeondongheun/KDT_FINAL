using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace finalProject.Models
{
    internal class WorkersCap
    {
        // PPE 착용 여부와 무관하게 작업자 캡처
        public static async Task<string> CaptureWorkerImage(Mat frame)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    string folder = @"C:\Users\user\Desktop\Workers";
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    // 안면 인식 실행
                    Worker recognizedWorker = await FaceRecognitionService.RecognizeFaceAsync(frame);

                    string workerFolder;
                    string filename;

                    if (recognizedWorker != null)
                    {
                        // 인식 성공: 작업자별 폴더 생성
                        workerFolder = Path.Combine(folder, recognizedWorker.WorkerId);
                        if (!Directory.Exists(workerFolder)) Directory.CreateDirectory(workerFolder);

                        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                        filename = $"{recognizedWorker.WorkerId}_{timestamp}.jpg";
                    }
                    else
                    {
                        // 인식 실패: Unknown 폴더에 저장
                        workerFolder = Path.Combine(folder, "Unknown");
                        if (!Directory.Exists(workerFolder)) Directory.CreateDirectory(workerFolder);

                        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                        filename = $"Unknown_{timestamp}.jpg";
                    }
                    string fullPath = Path.Combine(workerFolder, filename);

                    using (Mat safeCopy = frame.Clone())
                    {
                        if (!safeCopy.Empty())
                        {
                            Cv2.ImWrite(fullPath, safeCopy);
                            Console.WriteLine($"작업자 이미지 캡처: {fullPath}");
                            return fullPath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"작업자 이미지 캡처 오류: {ex.Message}");
                }
                return null;
            });
        }

        // 안전 장비 위반 시 이미지 캡처
        public static async Task<string> CaptureViolationImage(Mat frame)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    string folder = @"C:\Users\user\Desktop\Workers\Safety_Equipment_Violations";
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    Worker recognizedWorker = await FaceRecognitionService.RecognizeFaceAsync(frame);

                    string workerFolder;
                    string filename;

                    if (recognizedWorker != null)
                    {
                        // 인식 성공: 작업자별 위반 폴더 생성
                        workerFolder = Path.Combine(folder, recognizedWorker.WorkerId);
                        if (!Directory.Exists(workerFolder)) Directory.CreateDirectory(workerFolder);

                        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                        filename = $"{recognizedWorker.WorkerId}_Violation_{timestamp}.jpg";
                    }
                    else
                    {
                        // 인식 실패: Unknown 폴더에 저장
                        workerFolder = Path.Combine(folder, "Unknown");
                        if (!Directory.Exists(workerFolder)) Directory.CreateDirectory(workerFolder);

                        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                        filename = $"Unknown_Violation_{timestamp}.jpg";
                    }

                    string fullPath = Path.Combine(workerFolder, filename);

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

        public static void DrawPPEStatus(Mat frame,
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
    }
}
