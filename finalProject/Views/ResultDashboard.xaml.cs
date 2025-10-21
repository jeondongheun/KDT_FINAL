using finalProject.Models;
using Microsoft.Win32;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Window = System.Windows.Window;
using IoPath = System.IO.Path;
using ShapePath = System.Windows.Shapes.Path;
using Size = System.Windows.Size;
using Point = System.Windows.Point;

namespace finalProject.Views
{
    public partial class ResultDashboard : Window
    {
        private DispatcherTimer updateTimer;
        private FactoryIOControl factoryControl;

        public ResultDashboard(FactoryIOControl factory)
        {
            InitializeComponent();

            factoryControl = factory;

            // FactoryIOControl의 통계 업데이트 이벤트 구독
            factoryControl.OnStatisticsUpdated += UpdateStatisticsUI;

            // 주기적 UI 업데이트 타이머 (1초마다)
            updateTimer = new DispatcherTimer();
            updateTimer.Interval = TimeSpan.FromSeconds(1);
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();

            // 초기 데이터 로드
            LoadInitialData();

            // Export 버튼 이벤트 연결
            BtnExport.Click += BtnExport_Click;
        }

        private void LoadInitialData()
        {
            // DataGrid 열 너비 설정
    ConfigureDataGridColumns();
    
    if (factoryControl != null)
    {
        var stats = factoryControl.GetCurrentStatistics();
        UpdateStatisticsUI(stats);
    }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            // 주기적으로 통계 갱신
            if (factoryControl != null)
            {
                var stats = factoryControl.GetCurrentStatistics();
                UpdateStatisticsUI(stats);
            }
        }

        /// <summary>
        /// 통계 데이터를 받아서 UI 업데이트
        /// </summary>
        public void UpdateStatisticsUI(InspectionStatistics stats)
        {
            if (stats == null) return;

            // KPI 업데이트
            TxtTotal.Text = stats.TotalInspected.ToString();
            TxtNg.Text = stats.DefectCount.ToString();
            TxtConf.Text = $"{stats.NormalRate}%";
            TxtRate.Text = $"{stats.DefectRate}%";

            // Pie Chart 데이터 업데이트
            UpdatePieChart(stats);

            // 최근 결과 테이블 업데이트
            UpdateRecentResults(stats);
        }

        /// <summary>
        /// 최근 검사 결과 테이블 업데이트
        /// </summary>
        private void UpdateRecentResults(InspectionStatistics stats)
        {
            if (stats?.RecentResults == null) return;

            // 최근 10개만 표시
            var recentData = stats.RecentResults.Take(10).Select(r => new
            {
                Time = r.Time.ToString("HH:mm:ss"),
                Type = r.Type,
                Result = r.Result,
                Count = r.DefectCount > 0 ? r.DefectCount.ToString() : "-"
            }).ToList();

            GridRecent.ItemsSource = recentData;
        }

        /// <summary>
        /// DataGrid 열 너비를 동일하게 설정
        /// </summary>
        private void ConfigureDataGridColumns()
        {
            // DataGrid가 로드된 후에 열 설정
            GridRecent.Loaded += (s, e) =>
            {
                if (GridRecent.Columns.Count > 0)
                {
                    // 모든 열을 균등하게 분배
                    //foreach (var column in GridRecent.Columns)
                    //{
                    //    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    //}

                    // 또는 특정 비율로 설정
                    GridRecent.Columns[0].Width = new DataGridLength(2, DataGridLengthUnitType.Star); // Time 열을 2배
                    GridRecent.Columns[1].Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    GridRecent.Columns[2].Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                    GridRecent.Columns[3].Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                }
            };
        }

        /// <summary>
        /// 불량 유형별 파이 차트 업데이트
        /// </summary>
        private void UpdatePieChart(InspectionStatistics stats)
        {
            // PieCanvas를 클리어
            PieCanvas.Children.Clear();

            if (stats.TotalInspected == 0) return;

            double centerX = 100; // Canvas Width = 200
            double centerY = 100; // Canvas Height = 200
            double radius = 80;

            // 불량 유형별 데이터
            var defectTypes = new[]
            {
                new { Name = "short", Count = stats.DefectTypeCount["short"], Color = "#3DA5FF" },
                new { Name = "mousebite", Count = stats.DefectTypeCount["mousebite"], Color = "#FF6B6B" },
                new { Name = "pin-hole", Count = stats.DefectTypeCount["pin-hole"], Color = "#5EE493" },
                new { Name = "spur", Count = stats.DefectTypeCount["spur"], Color = "#F0E06E" },
                new { Name = "open", Count = stats.DefectTypeCount["open"], Color = "#D498AD" },
                new { Name = "copper", Count = stats.DefectTypeCount["copper"], Color = "#A6A0D8" }
            };

            int totalDefects = stats.DefectCount;
            if (totalDefects == 0) return;

            double startAngle = 0;

            foreach (var defect in defectTypes)
            {
                if (defect.Count == 0) continue;

                double sweepAngle = (double)defect.Count / totalDefects * 360;

                // PathGeometry를 사용해 파이 조각 그리기
                var pie = CreatePieSlice(centerX, centerY, radius, startAngle, sweepAngle, defect.Color);
                PieCanvas.Children.Add(pie);

                startAngle += sweepAngle;
            }

            // 중앙 원 (도넛 차트 스타일)
            var centerCircle = new System.Windows.Shapes.Ellipse
            {
                Width = radius * 0.6,
                Height = radius * 0.6,
                Fill = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#181B22"))
            };

            System.Windows.Controls.Canvas.SetLeft(centerCircle, centerX - radius * 0.3);
            System.Windows.Controls.Canvas.SetTop(centerCircle, centerY - radius * 0.3);
            PieCanvas.Children.Add(centerCircle);
        }

        /// <summary>
        /// 파이 차트 조각 생성
        /// </summary>
        private System.Windows.Shapes.Path CreatePieSlice(double centerX, double centerY,
            double radius, double startAngle, double sweepAngle, string colorHex)
        {
            double startRad = startAngle * Math.PI / 180;
            double endRad = (startAngle + sweepAngle) * Math.PI / 180;

            double x1 = centerX + radius * Math.Cos(startRad);
            double y1 = centerY + radius * Math.Sin(startRad);
            double x2 = centerX + radius * Math.Cos(endRad);
            double y2 = centerY + radius * Math.Sin(endRad);

            bool largeArc = sweepAngle > 180;

            var pathFigure = new System.Windows.Media.PathFigure
            {
                StartPoint = new Point(centerX, centerY),
                IsClosed = true
            };

            pathFigure.Segments.Add(new System.Windows.Media.LineSegment(new Point(x1, y1), true));
            pathFigure.Segments.Add(new System.Windows.Media.ArcSegment(
                new Point(x2, y2),
                new Size(radius, radius),
                0,
                largeArc,
                System.Windows.Media.SweepDirection.Clockwise,
                true));

            var pathGeometry = new System.Windows.Media.PathGeometry();
            pathGeometry.Figures.Add(pathFigure);

            var path = new System.Windows.Shapes.Path
            {
                Fill = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex)),
                Data = pathGeometry
            };

            return path;
        }

        /// <summary>
        /// Export CSV 버튼 클릭 이벤트
        /// </summary>
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (factoryControl == null) return;

            var stats = factoryControl.GetCurrentStatistics();

            // CSV 파일 저장 다이얼로그
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV 파일 (*.csv)|*.csv",
                FileName = $"PCB_Inspection_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                ExportToCSV(stats, saveDialog.FileName);
                MessageBox.Show("CSV 파일이 저장되었습니다.", "저장 완료",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// CSV 파일로 내보내기
        /// </summary>
        private void ExportToCSV(InspectionStatistics stats, string filePath)
        {
            var csv = new System.Text.StringBuilder();

            csv.AppendLine("PCB Defect Inspection Report");
            csv.AppendLine($"생성일시,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine();

            csv.AppendLine("=== 전체 통계 ===");
            csv.AppendLine("항목,값");
            csv.AppendLine($"총 검사 수,{stats.TotalInspected}");
            csv.AppendLine($"정상 제품,{stats.NormalCount}");
            csv.AppendLine($"불량 제품,{stats.DefectCount}");
            csv.AppendLine($"정상률,{stats.NormalRate}%");
            csv.AppendLine($"불량률,{stats.DefectRate}%");
            csv.AppendLine();

            csv.AppendLine("=== 불량 유형별 통계 ===");
            csv.AppendLine("불량 유형,발생 횟수,비율");

            foreach (var kvp in stats.DefectTypeCount)
            {
                double rate = stats.GetDefectTypeRate(kvp.Key);
                csv.AppendLine($"{kvp.Key},{kvp.Value},{rate}%");
            }

            csv.AppendLine();
            csv.AppendLine("=== 최근 검사 결과 ===");
            csv.AppendLine("시간,유형,결과,불량개수");

            foreach (var result in stats.RecentResults.Take(50))
            {
                csv.AppendLine($"{result.Time:yyyy-MM-dd HH:mm:ss},{result.Type},{result.Result},{result.DefectCount}");
            }

            System.IO.File.WriteAllText(filePath, csv.ToString(), System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Vision 버튼 클릭 이벤트
        /// </summary>
        private void BtnCamera_Click(object sender, RoutedEventArgs e)
        {
            // FactoryIOControl 창 표시
            factoryControl?.ShowWindow();
        }

        /// <summary>
        /// 창이 닫힐 때
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            updateTimer?.Stop();

            // 이벤트 구독 해제
            if (factoryControl != null)
            {
                factoryControl.OnStatisticsUpdated -= UpdateStatisticsUI;
            }

            base.OnClosed(e);
        }
    }
}