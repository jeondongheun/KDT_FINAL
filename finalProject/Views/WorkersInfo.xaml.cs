using finalProject.Models;
using ModernWpf.Controls;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
using Window = System.Windows.Window;

namespace finalProject.Views
{
    /// <summary>
    /// WorkersInfo.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WorkersInfo : Window
    {
        public WorkersInfo()
        {
            InitializeComponent();

            // CameraDetection 초기화 및 MainWindow 참조 설정
            SafetyCheck.WorkersWin = this;
            SafetyCheck.InitializeModel();

            // 창 닫기 이벤트 처리 추가
            this.Closing += WorkersInfo_Closing;
        }

        private void WorkersInfo_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // SafetyCheck 리소스 정리
                SafetyCheck.Cleanup();

                // 애플리케이션 완전 종료
                Application.Current.Shutdown();
                Debug.WriteLine("프로그램 종료");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WorkersInfo 종료 중 오류: {ex.Message}");
                // 오류가 있어도 강제 종료
                Application.Current.Shutdown();
            }
        }
    }
}
