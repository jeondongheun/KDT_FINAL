using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace finalProject.Models
{
    internal class Worker
    {
        public string WorkerId { get; set; }
        public string Name { get; set; }
        public string Department { get; set; }
        public string AssignedLine { get; set; }
        public string ProfileImagePath { get; set; }
    }

    internal static class WorkerManager
    {
        private static List<Worker> workers = new List<Worker>();

        // 초기 데이터 로드 (프로그램 시작 시 호출)
        public static void Initialize()
        {
            // 실제 작업자 데이터 - DB나 파일에서 로드하거나 직접 입력
            workers = new List<Worker>
            {
                new Worker
                {
                    WorkerId = "PCB_QC_01",
                    Name = "전동흔",
                    Department = "품질관리원",
                    AssignedLine = "A",
                    ProfileImagePath = "PCB_QC_01.jpg"
                },
                new Worker
                {
                    WorkerId = "PCB_QC_02",
                    Name = "노현신",
                    Department = "품질관리원",
                    AssignedLine = "B",
                    ProfileImagePath = "PCB_QC_02.jpg"
                },
                new Worker
                {
                    WorkerId = "PCB_QC_03",
                    Name = "방한민",
                    Department = "품질관리원",
                    AssignedLine = "C",
                    ProfileImagePath = "PCB_QC_03.jpg"
                }
            };
        }

        // WorkerId로 Worker 조회
        public static Worker GetWorkerById(string workerId)
        {
            return workers.FirstOrDefault(w => w.WorkerId == workerId);
        }

        // 모든 Worker 조회
        public static List<Worker> GetAllWorkers()
        {
            return workers;
        }
    }
}
