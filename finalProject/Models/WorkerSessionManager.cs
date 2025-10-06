using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

// 출퇴근 확인용 cs 파일
namespace finalProject.Models
{
    internal class WorkerSessionManager
    {
        private static Dictionary<string, WorkerSession> activeSessions = new Dictionary<string, WorkerSession>();
        private static HashSet<string> ppeCheckedWorkers = new HashSet<string>();
        private static HashSet<string> capturedWorkers = new HashSet<string>();
        private static readonly TimeSpan SESSION_DURATION = TimeSpan.FromHours(8);

        public class WorkerSession
        {
            public string WorkerId { get; set; }
            public DateTime CheckInTime { get; set; }
            public DateTime? CheckOutTime { get; set; }
            public bool IsCheckedIn => CheckOutTime == null;
            public bool PPEChecked { get; set; } = false;
        }

        // 출근 체크
        public static bool TryCheckIn(string workerId)
        {
            if (activeSessions.ContainsKey(workerId) && activeSessions[workerId].IsCheckedIn)
            {
                var session = activeSessions[workerId];

                if (DateTime.Now - session.CheckInTime < SESSION_DURATION)
                {
                    Debug.WriteLine($"{workerId} 님은 출근 처리되었습니다. (출근 시각: {session.CheckInTime:HH:mm:ss})");
                    return false;
                }
            }

            activeSessions[workerId] = new WorkerSession
            {
                WorkerId = workerId,
                CheckInTime = DateTime.Now,
                CheckOutTime = null
            };

            Debug.WriteLine($"{workerId} 출근 처리 완료: {DateTime.Now:HH:mm:ss}");
            return true;
        }

        // 퇴근 체크
        public static bool TryCheckOut(string workerId)
        {
            if (!activeSessions.ContainsKey(workerId) || !activeSessions[workerId].IsCheckedIn)
            {
                Debug.WriteLine($"{workerId} 님의 출근 기록이 없습니다.");
                return false;
            }

            activeSessions[workerId].CheckOutTime = DateTime.Now;
            Debug.WriteLine($"{workerId} 퇴근 처리 완료: {DateTime.Now:HH:mm:ss}");
            return true;
        }

        // 출근 상태 확인
        public static bool IsCheckedIn(string workerId)
        {
            if (!activeSessions.ContainsKey(workerId))
                return false;

            var session = activeSessions[workerId];

            if (DateTime.Now - session.CheckInTime >= SESSION_DURATION)
            {
                return false;
            }

            return session.IsCheckedIn;
        }

        // 세션 정보 가져오기
        public static WorkerSession GetSession(string workerId)
        {
            return activeSessions.ContainsKey(workerId) ? activeSessions[workerId] : null;
        }

        // 일일 세션 리셋
        public static void ResetDailySessions()
        {
            activeSessions.Clear();
            capturedWorkers.Clear();
            Debug.WriteLine("일일 세션 초기화 완료");
        }

        // 현재 출근 중인 모든 작업자 조회
        public static List<WorkerSession> GetActiveWorkers()
        {
            return activeSessions.Values
                   .Where(s => s.IsCheckedIn && DateTime.Now - s.CheckInTime < SESSION_DURATION).ToList();
        }

        // 이미지 캡처 여부 확인
        public static bool WorkersImageCap(string workerId)
        {
            return capturedWorkers.Contains(workerId);
        }

        // 캡처 완료
        public static void MarkAsCaptured(string workerId)
        {
            if (string.IsNullOrEmpty(workerId))
                return;

            if (!ppeCheckedWorkers.Contains(workerId))
            {
                ppeCheckedWorkers.Add(workerId);
                Debug.WriteLine($"{workerId} - PPE 검사 완료 기록");
            }

            if (activeSessions.ContainsKey(workerId))
            {
                activeSessions[workerId].PPEChecked = true;
            }
        }

        // PPE 검사 필요 여부 확인 (YOLO 모델 실행 여부 결정)
        public static bool NeedsPPECheck(string workerId)
        {
            if (string.IsNullOrEmpty(workerId))
                return true; // workerId 없으면 일단 검사 (미인식 작업자)

            // 이미 검사했으면 false 반환
            if (ppeCheckedWorkers.Contains(workerId))
            {
                Console.WriteLine($"{workerId} - PPE 검사 이미 완료됨");
                Debug.WriteLine($"{workerId} - PPE 검사 이미 완료됨");
                return false;
            }

            Console.WriteLine($"{workerId} - PPE 검사 필요");
            Debug.WriteLine($"{workerId} - PPE 검사 필요");
            return true;
        }

        public static bool IsPPEChecked(string workerId)
        {
            return ppeCheckedWorkers.Contains(workerId);
        }
    }
}