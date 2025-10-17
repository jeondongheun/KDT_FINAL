using finalProject.Models;
using ModernWpf.Controls;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
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
        public WorkersInfo()
        {
            InitializeComponent();

            // CameraDetection 초기화 및 MainWindow 참조 설정
            SafetyCheck.WorkersWin = this;
            SafetyCheck.InitializeModel();

            // 창 닫기 이벤트 처리 추가
            this.Closing += WorkersInfo_Closing;
        }


        private void btnStartWork_Click(object sender, RoutedEventArgs e)
        {
            // 1. FactoryIOControl 창의 새 인스턴스를 생성합니다.
            FactoryIOControl factoryWindow = new FactoryIOControl();

            // 2. 새로 만든 창을 화면에 보여줍니다.
            factoryWindow.Topmost = true;
            factoryWindow.Show();

            // 3. 현재 창(WorkersInfo)을 닫습니다.
            this.Close();
        }

        private void WorkersInfo_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
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
