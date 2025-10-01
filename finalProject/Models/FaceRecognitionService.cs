using Newtonsoft.Json.Linq;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace finalProject.Models
{
    internal class FaceRecognitionService
    {
        private static readonly HttpClient client = new HttpClient();

        // 작업자 데이터베이스 (실제로는 DB나 파일에서 로드)
        public static List<Worker> WorkerDatabase = new List<Worker>
        {
            new Worker { WorkerId = "PCB_QC_01", Name = "전동흔", Department = "품질 검사원", AssignedLine = "A", ProfileImagePath = "PCB_QC_01.jpg" },
            new Worker { WorkerId = "PCB_QC_02", Name = "노현신", Department = "품질 검사원", AssignedLine = "B", ProfileImagePath = "PCB_QC_02.jpg" },
            new Worker { WorkerId = "PCB_QC_03", Name = "방한민", Department = "품질 검사원", AssignedLine = "C", ProfileImagePath = "PCB_QC_03.jpg" }
        };

        // 캡처된 이미지로 안면 인식
        public static async Task<Worker> RecognizeFaceAsync(string capturedImagePath)
        {
            if (!File.Exists(capturedImagePath))
            {
                Console.WriteLine($"파일을 찾을 수 없습니다: {capturedImagePath}");
                return null;
            }

            try
            {
                byte[] imageData = File.ReadAllBytes(capturedImagePath);

                var content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(imageData), "image", "capture.jpg");

                // DeepFace API 호출 (Flask 서버가 실행 중이어야 함)
                HttpResponseMessage response = await client.PostAsync("http://127.0.0.1:5000/verify", content);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                var jsonResult = JObject.Parse(responseBody);

                if (jsonResult["status"]?.ToString() == "success")
                {
                    string workerId = jsonResult["worker_id"]?.ToString();
                    return WorkerDatabase.FirstOrDefault(w => w.WorkerId == workerId);
                }
                else
                {
                    Console.WriteLine($"인식 실패: {jsonResult["message"]?.ToString()}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"얼굴 인식 오류: {ex.Message}");
                return null;
            }
        }

        // 이미지 직접 인식 > 임시 파일로 저장 후 인식
        public static async Task<Worker> RecognizeFaceAsync(Mat frame)
        {
            if (frame == null || frame.Empty())
            {
                Console.WriteLine("유효하지 않은 프레임입니다.");
                return null;
            }

            try
            {
                // 임시 파일로 저장
                string tempPath = Path.Combine(Path.GetTempPath(), $"face_temp_{DateTime.Now:yyyyMMddHHmmss}.jpg");
                Cv2.ImWrite(tempPath, frame);

                Worker result = await RecognizeFaceAsync(tempPath);

                // 임시 파일 삭제
                try { File.Delete(tempPath); } catch { }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"얼굴 인식 오류: {ex.Message}");
                return null;
            }
        }

        // 캡처된 이미지들과 비교하여 얼굴 인식
        // DeepFace API가 없을 경우 대체
        public static Worker RecognizeFaceLocal(string capturedImagePath)
        {
            // 실제 구현 시에는 OpenCV의 얼굴 인식 알고리즘 사용
            // 여기서는 파일명 기반 매칭으로 시뮬레이션

            string fileName = Path.GetFileName(capturedImagePath);

            // "이름_2025-01-01_12-00-00.jpg" 형식에서 이름 추출
            if (fileName.StartsWith("이름_"))
            {
                // 임시로 첫 번째 작업자 반환 (실제로는 얼굴 비교 알고리즘 필요)
                return WorkerDatabase.FirstOrDefault();
            }

            return null;
        }
    }
}
