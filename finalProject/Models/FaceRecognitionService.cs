using Newtonsoft.Json.Linq;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// 안면 인식용 cs 파일
namespace finalProject.Models
{
    internal class FaceRecognitionService
    {
        private static readonly HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10) // 타임아웃 설정
        };

        // 동시 요청 방지를 위한 세마포어
        private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        // 마지막 요청 시간 추적
        private static DateTime lastRequestTime = DateTime.MinValue;
        private static readonly TimeSpan requestCooldown = TimeSpan.FromMilliseconds(500);

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
                Debug.WriteLine($"파일을 찾을 수 없습니다: {capturedImagePath}");
                return null;
            }

            // 너무 빠른 연속 요청 방지
            var timeSinceLastRequest = DateTime.Now - lastRequestTime;
            if (timeSinceLastRequest < requestCooldown)
            {
                await Task.Delay(requestCooldown - timeSinceLastRequest);
            }

            // 동시 요청 방지
            await semaphore.WaitAsync();

            try
            {
                lastRequestTime = DateTime.Now;

                byte[] imageData = File.ReadAllBytes(capturedImagePath);

                using (var content = new MultipartFormDataContent())
                {
                    content.Add(new ByteArrayContent(imageData), "image", "capture.jpg");

                    Debug.WriteLine("Flask 서버로 안면 인식 요청 전송...");

                    // DeepFace API 호출 (Flask 서버가 실행 중이어야 함)
                    using (HttpResponseMessage response = await client.PostAsync("http://127.0.0.1:5000/verify", content))
                    {
                        response.EnsureSuccessStatusCode();

                        string responseBody = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"서버 응답: {responseBody}");

                        var jsonResult = JObject.Parse(responseBody);

                        if (jsonResult["status"]?.ToString() == "success")
                        {
                            string workerId = jsonResult["worker_id"]?.ToString();
                            var worker = WorkerDatabase.FirstOrDefault(w => w.WorkerId == workerId);

                            if (worker != null)
                            {
                                Debug.WriteLine($"안면 인식 성공: {worker.Name} ({workerId})");
                            }
                            else
                            {
                                Debug.WriteLine($"작업자 DB에서 찾을 수 없음: {workerId}");
                            }

                            return worker;
                        }
                        else
                        {
                            Debug.WriteLine($"인식 실패: {jsonResult["message"]?.ToString()}");
                            return null;
                        }
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                Debug.WriteLine($"안면 인식 타임아웃: {ex.Message}");
                return null;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"Flask 서버 연결 실패: {ex.Message}");
                Debug.WriteLine("Flask 서버가 실행 중인지 확인하세요: http://127.0.0.1:5000");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"얼굴 인식 오류: {ex.Message}");
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        }

        // 이미지 직접 인식 > 임시 파일로 저장 후 인식
        public static async Task<Worker> RecognizeFaceAsync(Mat frame)
        {
            if (frame == null || frame.Empty())
            {
                Debug.WriteLine("유효하지 않은 프레임입니다.");
                return null;
            }

            string tempPath = null;

            try
            {
                // 임시 파일로 저장
                tempPath = Path.Combine(Path.GetTempPath(), $"face_temp_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid()}.jpg");
                Cv2.ImWrite(tempPath, frame);

                Worker result = await RecognizeFaceAsync(tempPath);

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"얼굴 인식 오류: {ex.Message}");
                return null;
            }
            finally
            {
                // 임시 파일 삭제 (finally로 확실히 삭제)
                if (!string.IsNullOrEmpty(tempPath))
                {
                    try
                    {
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                            Debug.WriteLine($"임시 파일 삭제: {tempPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"임시 파일 삭제 실패: {ex.Message}");
                    }
                }
            }
        }

        // Flask 서버 상태 확인
        public static async Task<bool> CheckServerStatusAsync()
        {
            try
            {
                using (var response = await client.GetAsync("http://127.0.0.1:5000/health"))
                {
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
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