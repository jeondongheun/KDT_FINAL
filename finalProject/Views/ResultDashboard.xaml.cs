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

namespace finalProject.Views
{
    /// <summary>
    /// ResultDashboard.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ResultDashboard : Window
    {
        private FactoryIOControl _factoryIOControl;

        public ResultDashboard(FactoryIOControl factoryIOControl)
        {
            InitializeComponent();
            _factoryIOControl = factoryIOControl;

            // 기존 초기화 코드...
            this.Closing += ResultDashboard_Closing;
        }
        
        // Vision Camera

        private void BtnCamera_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_factoryIOControl != null)
                {
                    // 카메라는 이미 실행 중이므로, 창만 표시
                    _factoryIOControl.Show();
                    _factoryIOControl.Activate();

                    Debug.WriteLine("Factory IO Control 창이 열렸습니다.");
                }
                else
                {
                    MessageBox.Show("Factory IO Control을 사용할 수 없습니다.",
                        "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Vision 창 열기 실패: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Export CSV
        /*
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Export Recent Results",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = "results.csv"
            };
            if (dlg.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Time,Result,Confidence,Product,Class");
                foreach (var r in Recent)
                    sb.AppendLine($"{r.Time},{r.Result},{r.Confidence.ToString("P1", CultureInfo.InvariantCulture)},{r.Product},{r.Class}");
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("CSV 저장 완료 ✅", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        */

        private void ResultDashboard_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // ResultDashboard가 닫힐 때 FactoryIOControl도 함께 정리
                if (_factoryIOControl != null)
                {
                    _factoryIOControl.Close();
                    Debug.WriteLine("Factory IO Control이 종료되었습니다.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ResultDashboard 종료 중 오류: {ex.Message}");
            }
        }
    }
}
