using finalProject.Models;
using ModernWpf.Controls;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Window = System.Windows.Window;

namespace finalProject.Views
{
    /// <summary>
    /// WorkersInfo.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WorkersInfo : Window
    {
        private DispatcherTimer sirenTimer;
        private bool sirenVisible = true;
        private FactoryIOControl factoryIOControl;

        public WorkersInfo()
        {
            InitializeComponent();

            // CameraDetection 초기화 및 MainWindow 참조 설정
            SafetyCheck.WorkersWin = this;
            SafetyCheck.InitializeModel();

            // 사이렌 깜빡임 설정
            InitializeSirenBlink();

            // factoryIO 서버 시작
            InitializeFactoryIO();

            // 창 닫기 이벤트 처리 추가
            this.Closing += WorkersInfo_Closing;
        }

        private void InitializeSirenBlink()
        {
            sirenTimer = new DispatcherTimer();
            sirenTimer.Interval = TimeSpan.FromMilliseconds(500); // 0.5초마다
            sirenTimer.Tick += SirenTimer_Tick;
            sirenTimer.Start();
        }

        private void SirenTimer_Tick(object sender, EventArgs e)
        {
            sirenVisible = !sirenVisible;
            sirenF.Opacity = sirenVisible ? 1.0 : 0.2;
            sirenB.Opacity = sirenVisible ? 1.0 : 0.2;
        }

        private void InitializeFactoryIO()
        {
            try
            {
                // FactoryIOControl 인스턴스 생성 (서버 자동 시작됨)
                factoryIOControl = new FactoryIOControl();

                // 창을 숨김 상태로 유지 (백그라운드 실행)
                factoryIOControl.Hide();

                Debug.WriteLine("Factory IO 서버가 백그라운드에서 시작되었습니다.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Factory IO 초기화 실패: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnStartWork_Click(object sender, RoutedEventArgs e)
        {
            // Factory IO 연결 확인
            if (factoryIOControl != null && factoryIOControl.IsConnected())
            {
                // Factory IO 시스템 시작
                factoryIOControl.StartFactoryIOSystem();
            }
            else
            {
                MessageBox.Show("Factory IO가 연결되지 않았습니다.\n먼저 Factory IO를 실행하고 연결해주세요.",
                    "연결 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ResultDashboard 창 열기
            ResultDashboard dashboard = new ResultDashboard(factoryIOControl);
            dashboard.Show();
            
            // 현재 WorkersInfo 창 닫기 (선택사항)
            this.Close();
        }

        private void WorkersInfo_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            sirenTimer?.Stop();

            try
            {
                // SafetyCheck 리소스 정리는 유지합니다.
                SafetyCheck.Cleanup();

                // 아래 줄을 주석 처리하거나 삭제하세요.
                // Application.Current.Shutdown(); 

                Debug.WriteLine("WorkersInfo 창이 닫혔습니다.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkersInfo 종료 중 오류: {ex.Message}");
            }
        }
    }
}
