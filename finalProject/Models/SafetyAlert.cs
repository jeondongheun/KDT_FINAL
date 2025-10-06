using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace finalProject.Models
{
    public class SafetyAlert
    {
        // 이메일 설정
        private static readonly string SmtpServer = "smtp.gmail.com";
        private static readonly int SmtpPort = 587;
        private static readonly string SenderEmail = "gustls0709@gmail.com";        // 발신자 이메일
        private static readonly string SenderPassword = "qdvp kicv jfis zsll";
        private static readonly string RecipientEmail = "gustls0709@naver.com";     // 담당자 이메일

        // 6시간 간격으로 미착용자 보고서 전송
        private static DateTime lastPeriodicReport = DateTime.MinValue;
        private static readonly TimeSpan ReportInterval = TimeSpan.FromHours(6);
        private static List<ViolationRecord> accumulatedViolations = new List<ViolationRecord>();

        // 저장 경로 추가
        private static readonly string DataFolder = @"C:\Users\user\Desktop\SafetyAlerts\Data";
        private static readonly string ViolationsFile = Path.Combine(DataFolder, "violations.json");
        private static readonly string LastReportFile = Path.Combine(DataFolder, "last_report.txt");

        // 프로그램 시작 시 호출 (생성자나 초기화 메서드에서)
        public static void Initialize()
        {
            if (!Directory.Exists(DataFolder))
                Directory.CreateDirectory(DataFolder);

            LoadViolations();
            LoadLastReportTime();
        }

        // 위반 기록 저장
        private static void SaveViolations()
        {
            try
            {
                var json = JsonConvert.SerializeObject(accumulatedViolations, Formatting.Indented);
                File.WriteAllText(ViolationsFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"위반 기록 저장 실패: {ex.Message}");
            }
        }

        // 위반 기록 로드
        private static void LoadViolations()
        {
            try
            {
                if (File.Exists(ViolationsFile))
                {
                    var json = File.ReadAllText(ViolationsFile);
                    accumulatedViolations = JsonConvert.DeserializeObject<List<ViolationRecord>>(json)
                                            ?? new List<ViolationRecord>();
                    Console.WriteLine($"이전 위반 기록 {accumulatedViolations.Count}건 로드됨");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"위반 기록 로드 실패: {ex.Message}");
            }
        }

        // 마지막 보고 시간 저장
        private static void SaveLastReportTime()
        {
            try
            {
                File.WriteAllText(LastReportFile, lastPeriodicReport.ToString("o"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"보고 시간 저장 실패: {ex.Message}");
            }
        }

        // 마지막 보고 시간 로드
        private static void LoadLastReportTime()
        {
            try
            {
                if (File.Exists(LastReportFile))
                {
                    var timeStr = File.ReadAllText(LastReportFile);
                    lastPeriodicReport = DateTime.Parse(timeStr);
                    Console.WriteLine($"마지막 보고 시간: {lastPeriodicReport}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"보고 시간 로드 실패: {ex.Message}");
                lastPeriodicReport = DateTime.MinValue;
            }
        }

        // 위반 기록 클래스
        public class ViolationRecord
        {
            public string WorkerId { get; set; }
            public DateTime Time { get; set; }
            public List<string> MissingEquipment { get; set; } = new List<string>();
            public string ImagePath { get; set; }
        }

        // 위반 감지 시 호출되는 메인 메서드
        public static async Task ProcessViolation(string workerId, bool missingHelmet, bool missingVest, bool missingGloves)
        {
            var violations = new List<string>();
            if (missingHelmet) violations.Add("안전모");
            if (missingVest) violations.Add("안전 조끼");
            if (missingGloves) violations.Add("안전 장갑");

            if (violations.Count == 0) return;

            // 위반 기록 저장
            var record = new ViolationRecord
            {
                WorkerId = workerId,
                Time = DateTime.Now,
                MissingEquipment = violations
            };
            accumulatedViolations.Add(record);

            // 데이터 파일에 저장
            SaveViolations();

            // 6시간 간격으로 보고서 전송 체크
            await CheckAndSendPeriodicReport();
        }

        // 정기 보고서 전송 체크
        private static async Task CheckAndSendPeriodicReport()
        {
            if (DateTime.Now - lastPeriodicReport >= ReportInterval && accumulatedViolations.Count > 0)
            {
                await SendCombinedAlert();
                lastPeriodicReport = DateTime.Now;
                accumulatedViolations.Clear(); // 전송 후 리스트 초기화
            }
        }

        // 안전 장비 미착용 시 전송할 메일
        public static async Task SendCombinedAlert()
        {
            var reportTime = DateTime.Now;
            string timeRange = GetTimeRange(reportTime);

            string subject = $"[안내] 안전 장비 미착용 감지 ({accumulatedViolations.Count}건)";

            // 미착용자 목록 생성
            var violationDetails = new StringBuilder();

            foreach (var violation in accumulatedViolations)
            {
                var worker = WorkerManager.GetWorkerById(violation.WorkerId);

                if (worker != null)
                {
                    string violations = string.Join(", ", violation.MissingEquipment);
                    violationDetails.Append($" - {worker.Name} ({worker.WorkerId}) : {violations} <br>");
                }
                else
                {
                    // Worker 정보가 없는 경우
                    string violations = string.Join(", ", violation.MissingEquipment);
                    violationDetails.Append($" - 미등록 작업자 ({violation.WorkerId}) : {violations} <br>");
                }
            }

            string body = $@"
            <html>
            <body style='font-family: 맑은 고딕, Arial; line-height: 1.6;'>
                <h3>[안전 장비 미착용 보고서]</h3>

                <p><strong>일자</strong> : {DateTime.Now:yyyy-MM-dd} {timeRange}</p>
                <p><strong>위치</strong> : 메인 작업장 출입 카메라</p> <br>

                <h4>[안전 장비 미착용자 정보]</h4>
                <div style='margin-left: 20px;'>
                    {violationDetails}
                </div> <br>

                <p><strong>총 미착용 인원</strong> : {accumulatedViolations.Count}명</p><br>

                <p>현장 확인 후 필요한 조치를 취해 주시기 바랍니다.<br>
                안전 모니터링 시스템</p>
            </body>
            </html>
            ";

            await SendEmailAsync(subject, body);

            // 전송 후 저장
            lastPeriodicReport = DateTime.Now;
            SaveLastReportTime();

            accumulatedViolations.Clear();
            SaveViolations();
        }

        // 현재 시간대에 따른 시간 범위 반환
        // 여섯 시간에 한 번 메일 전송을 위함
        private static string GetTimeRange(DateTime currentTime)
        {
            int hour = currentTime.Hour;

            if (hour >= 0 && hour < 6) return "00:00~06:00";
            else if (hour >= 6 && hour < 12) return "06:00~12:00";
            else if (hour >= 12 && hour < 18) return "12:00~18:00";
            else return "18:00~24:00";
        }

        // 누적된 위반 사항 요약
        private static string GetViolationSummary()
        {
            if (!accumulatedViolations.Any()) return "";

            var allViolations = accumulatedViolations
                                .SelectMany(v => v.MissingEquipment)
                                .Distinct()
                                .ToList();

            return string.Join(", ", allViolations);
        }

        // 메일 전송
        private static async Task SendEmailAsync(string subject, string body)
        {
            try
            {
                using (var client = new SmtpClient(SmtpServer, SmtpPort))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(SenderEmail, SenderPassword);

                    using (var message = new MailMessage())
                    {
                        message.From = new MailAddress(SenderEmail, "안전 모니터링 시스템");
                        message.To.Add(RecipientEmail);
                        message.Subject = subject;
                        message.Body = body;
                        message.IsBodyHtml = true;

                        /*
                        // 이미지 첨부 (옵션)
                        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                        {
                            var attachment = new Attachment(imagePath);
                            message.Attachments.Add(attachment);
                        }
                        */

                        await client.SendMailAsync(message);
                        Console.WriteLine($"안전 알림 이메일 전송 완료: {subject}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"이메일 전송 실패: {ex.Message}");
                // 로그 파일에 기록하는 것도 좋은 방법입니다
                LogAlert(subject, body, ex.Message);
            }
        }

        // 메일 전송 실패 시 로그 기록 저장
        private static void LogAlert(string subject, string body, string error)
        {
            try
            {
                string logFolder = @"C:\Users\user\Desktop\SafetyAlerts";
                if (!Directory.Exists(logFolder))
                    Directory.CreateDirectory(logFolder);

                string logFile = Path.Combine(logFolder, $"AlertLog_{DateTime.Now:yyyy-MM-dd}.txt");
                string logEntry = $@"
                === {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===
                Subject: {subject}
                Body: {body}
                Error: {error}
                ==========================================
                ";
                File.AppendAllText(logFile, logEntry);
            }
            catch (Exception logEx)
            {
                Console.WriteLine($"로그 기록 실패: {logEx.Message}");
            }
        }

        // 이메일 설정 검증
        public static bool ValidateEmailSettings()
        {
            return !string.IsNullOrEmpty(SenderEmail) &&
                   !string.IsNullOrEmpty(SenderPassword) &&
                   !string.IsNullOrEmpty(RecipientEmail);
        }
    }
}
