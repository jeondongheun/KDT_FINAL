using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace finalProject.Models
{
    /// <summary>
    /// 개별 검사 결과 데이터
    /// </summary>
    public class InspectionResult
    {
        public DateTime Time { get; set; }
        public string Type { get; set; }
        public string Result { get; set; }
        public int DefectCount { get; set; }
    }

    /// <summary>
    /// PCB 검사 통계 데이터를 관리하는 클래스
    /// </summary>
    public class InspectionStatistics
    {
        // 총 검사 횟수
        public int TotalInspected { get; set; }

        // 정상 제품 수
        public int NormalCount { get; set; }

        // 총 불량 제품 수
        public int DefectCount { get; set; }

        // 불량 유형별 카운트
        public Dictionary<string, int> DefectTypeCount { get; set; }

        // 최근 검사 결과 리스트
        public ObservableCollection<InspectionResult> RecentResults { get; set; }

        // 정상률 (%)
        public double NormalRate => TotalInspected > 0
            ? Math.Round((double)NormalCount / TotalInspected * 100, 2)
            : 0;

        // 불량률 (%)
        public double DefectRate => TotalInspected > 0
            ? Math.Round((double)DefectCount / TotalInspected * 100, 2)
            : 0;

        public InspectionStatistics()
        {
            TotalInspected = 0;
            NormalCount = 0;
            DefectCount = 0;

            DefectTypeCount = new Dictionary<string, int>
            {
                { "short", 0 },
                { "mousebite", 0 },
                { "pin-hole", 0 },
                { "spur", 0 },
                { "open", 0 },
                { "copper", 0 }
            };

            RecentResults = new ObservableCollection<InspectionResult>();
        }

        /// <summary>
        /// 검사 결과를 기록합니다
        /// </summary>
        public void RecordInspection(bool isNormal, List<string> detectedDefects = null)
        {
            TotalInspected++;

            if (isNormal)
            {
                NormalCount++;

                // 최근 결과에 추가
                AddRecentResult("Normal", "OK", 0);
            }
            else
            {
                DefectCount++;

                // 불량 유형별 카운트 증가
                if (detectedDefects != null && detectedDefects.Count > 0)
                {
                    foreach (var defect in detectedDefects)
                    {
                        if (DefectTypeCount.ContainsKey(defect))
                        {
                            DefectTypeCount[defect]++;
                        }
                    }

                    // 최근 결과에 추가 (첫 번째 불량 유형을 대표로 표시)
                    AddRecentResult(detectedDefects[0], "NG", detectedDefects.Count);
                }
                else
                {
                    AddRecentResult("Unknown", "NG", 1);
                }
            }
        }

        /// <summary>
        /// 최근 결과 리스트에 추가
        /// </summary>
        private void AddRecentResult(string type, string result, int defectCount)
        {
            var newResult = new InspectionResult
            {
                Time = DateTime.Now,
                Type = type,
                Result = result,
                DefectCount = defectCount
            };

            // 리스트 맨 앞에 추가
            RecentResults.Insert(0, newResult);

            // 최대 50개만 유지
            while (RecentResults.Count > 50)
            {
                RecentResults.RemoveAt(RecentResults.Count - 1);
            }
        }

        /// <summary>
        /// 통계 초기화
        /// </summary>
        public void Reset()
        {
            TotalInspected = 0;
            NormalCount = 0;
            DefectCount = 0;

            foreach (var key in DefectTypeCount.Keys.ToList())
            {
                DefectTypeCount[key] = 0;
            }

            RecentResults.Clear();
        }

        /// <summary>
        /// 특정 불량 유형의 발생률 (%)
        /// </summary>
        public double GetDefectTypeRate(string defectType)
        {
            if (TotalInspected == 0) return 0;

            if (DefectTypeCount.ContainsKey(defectType))
            {
                return Math.Round((double)DefectTypeCount[defectType] / TotalInspected * 100, 2);
            }

            return 0;
        }
    }
}