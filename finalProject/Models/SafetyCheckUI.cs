using finalProject.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace finalProject.Models
{
    internal class SafetyCheckUI
    {
        public static MainWindow MainWin { get; set; }
        public static WorkersInfo WorkersWin { get; set; }

        // PPE UI 상태 업데이트
        public static void UpdatePPEUI(bool hasHelmet, bool hasVest, bool hasGloves, bool hasGoggles)
        {
            MainWin?.Dispatcher.BeginInvoke(() =>
            {
                // 헬멧 상태
                MainWin.progHelmet?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.borderH?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(hasHelmet ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 252, 232)));
                MainWin.helmets?.SetCurrentValue(TextBlock.TextProperty, hasHelmet ? "안전모 착용" : "안전모 미착용");
                MainWin.checkH?.SetCurrentValue(UIElement.VisibilityProperty, hasHelmet ? Visibility.Visible : Visibility.Collapsed);
                MainWin.warnH?.SetCurrentValue(UIElement.VisibilityProperty, hasHelmet ? Visibility.Collapsed : Visibility.Visible);

                // 조끼 상태
                MainWin.progVest?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.borderV?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(hasVest ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 252, 232)));
                MainWin.vest?.SetCurrentValue(TextBlock.TextProperty, hasVest ? "안전 조끼 착용" : "안전 조끼 미착용");
                MainWin.checkV?.SetCurrentValue(UIElement.VisibilityProperty, hasVest ? Visibility.Visible : Visibility.Collapsed);
                MainWin.warnV?.SetCurrentValue(UIElement.VisibilityProperty, hasVest ? Visibility.Collapsed : Visibility.Visible);

                // 장갑 상태
                MainWin.progGloves?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.borderG?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(hasGloves ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 252, 232)));
                MainWin.gloves?.SetCurrentValue(TextBlock.TextProperty, hasGloves ? "안전 장갑 착용" : "안전 장갑 미착용");
                MainWin.checkG?.SetCurrentValue(UIElement.VisibilityProperty, hasGloves ? Visibility.Visible : Visibility.Collapsed);
                MainWin.warnG?.SetCurrentValue(UIElement.VisibilityProperty, hasGloves ? Visibility.Collapsed : Visibility.Visible);

                // 보안경 상태
                MainWin.progGoggles?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.borderGo?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(hasGoggles ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 252, 232)));
                MainWin.goggles?.SetCurrentValue(TextBlock.TextProperty, hasGoggles ? "보안경 착용" : "보안경 미착용");
                MainWin.checkGo?.SetCurrentValue(UIElement.VisibilityProperty, hasGoggles ? Visibility.Visible : Visibility.Collapsed);
                MainWin.warnGo?.SetCurrentValue(UIElement.VisibilityProperty, hasGoggles ? Visibility.Collapsed : Visibility.Visible);
            });
        }

        // PPE UI를 로딩 상태로 리셋
        public static void ResetPPEUI()
        {
            MainWin?.Dispatcher.BeginInvoke(() =>
            {
                // 모든 PPE를 로딩 상태로 리셋
                MainWin.progHelmet?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
                MainWin.borderH?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(240, 245, 255)));
                MainWin.helmets?.SetCurrentValue(TextBlock.TextProperty, "안전모");
                MainWin.checkH?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.warnH?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);

                MainWin.progVest?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
                MainWin.borderV?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(240, 245, 255)));
                MainWin.vest?.SetCurrentValue(TextBlock.TextProperty, "안전 조끼");
                MainWin.checkV?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.warnV?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);

                MainWin.progGloves?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
                MainWin.borderG?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(240, 245, 255)));
                MainWin.gloves?.SetCurrentValue(TextBlock.TextProperty, "안전 장갑");
                MainWin.checkG?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.warnG?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);

                MainWin.progGoggles?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
                MainWin.borderGo?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(240, 245, 255)));
                MainWin.goggles?.SetCurrentValue(TextBlock.TextProperty, "보안경");
                MainWin.checkGo?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                MainWin.warnGo?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            });
        }

        public static void UpdateWorkersInfo(bool hasHelmet, bool hasVest, bool hasGloves, bool hasGoggles, Worker worker)
        {
            Debug.WriteLine($"Worker: {worker?.Name ?? "null"}");
            Debug.WriteLine($"WorkersWin: {WorkersWin != null}");

            WorkersWin?.Dispatcher.BeginInvoke(() =>
            {
                var currentTime = DateTime.Now;
                WorkersWin.startWork.Text = $"{currentTime:T}";

                // 작업자 정보 표시
                if (worker != null)
                {
                    // WorkersInfo.xaml에 작업자 정보를 표시할 TextBlock들이 있다고 가정
                    // 실제 컨트롤 이름에 맞게 수정하세요
                    if (WorkersWin.txtWorkerName != null)
                        WorkersWin.txtWorkerName.Text = worker.Name;

                    if (WorkersWin.txtWorkerId != null)
                        WorkersWin.txtWorkerId.Text = worker.WorkerId;

                    if (WorkersWin.txtDepartment != null)
                        WorkersWin.txtDepartment.Text = worker.Department;

                    if (WorkersWin.txtAssignedLine != null)
                        WorkersWin.txtAssignedLine.Text = worker.AssignedLine + " 라인";

                    // 프로필 이미지
                    if (!string.IsNullOrEmpty(worker.ProfileImagePath))
                    {
                        try
                        {
                            string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, worker.ProfileImagePath);

                            Debug.WriteLine($"이미지 경로: {imagePath}");
                            Debug.WriteLine($"파일 존재: {File.Exists(imagePath)}");

                            if (File.Exists(imagePath))
                            {
                                BitmapImage bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.EndInit();

                                WorkersWin.imgProfile.Source = bitmap;
                                Debug.WriteLine("프로필 이미지 로드 성공");
                            }
                            else
                            {
                                Debug.WriteLine($"프로필 이미지를 찾을 수 없습니다: {imagePath}");
                                Debug.WriteLine($"현재 디렉토리: {AppDomain.CurrentDomain.BaseDirectory}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"프로필 이미지 로드 오류: {ex.Message}");
                        }
                    }
                    else
                    {
                        // 인식 실패 시
                        if (WorkersWin.txtWorkerName != null)
                            WorkersWin.txtWorkerName.Text = "인식 실패";
                    }
                }
                else
                {
                    // 미등록 작업자 처리 (새로 추가)
                    if (WorkersWin.txtWorkerName != null)
                        WorkersWin.txtWorkerName.Text = "미등록 작업자";

                    if (WorkersWin.txtWorkerId != null)
                        WorkersWin.txtWorkerId.Text = "UNKNOWN";

                    if (WorkersWin.txtDepartment != null)
                        WorkersWin.txtDepartment.Text = "미확인";

                    if (WorkersWin.txtAssignedLine != null)
                        WorkersWin.txtAssignedLine.Text = "미배정";

                    // 프로필 이미지를 기본 이미지로 설정 (선택사항)
                    WorkersWin.imgProfile.Source = null;

                    Debug.WriteLine("미등록 작업자 정보 표시");
                }

                // 헬멧 상태
                WorkersWin.progHelmet?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                WorkersWin.borderH?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(hasHelmet ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 252, 232)));
                WorkersWin.helmets?.SetCurrentValue(TextBlock.TextProperty, hasHelmet ? "안전모 착용" : "안전모 미착용");
                WorkersWin.checkH?.SetCurrentValue(UIElement.VisibilityProperty, hasHelmet ? Visibility.Visible : Visibility.Collapsed);
                WorkersWin.warnH?.SetCurrentValue(UIElement.VisibilityProperty, hasHelmet ? Visibility.Collapsed : Visibility.Visible);

                // 조끼 상태
                WorkersWin.progVest?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                WorkersWin.borderV?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(hasVest ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 252, 232)));
                WorkersWin.vest?.SetCurrentValue(TextBlock.TextProperty, hasVest ? "안전 조끼 착용" : "안전 조끼 미착용");
                WorkersWin.checkV?.SetCurrentValue(UIElement.VisibilityProperty, hasVest ? Visibility.Visible : Visibility.Collapsed);
                WorkersWin.warnV?.SetCurrentValue(UIElement.VisibilityProperty, hasVest ? Visibility.Collapsed : Visibility.Visible);

                // 장갑 상태
                WorkersWin.progGloves?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                WorkersWin.borderG?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(hasGloves ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 252, 232)));
                WorkersWin.gloves?.SetCurrentValue(TextBlock.TextProperty, hasGloves ? "안전 장갑 착용" : "안전 장갑 미착용");
                WorkersWin.checkG?.SetCurrentValue(UIElement.VisibilityProperty, hasGloves ? Visibility.Visible : Visibility.Collapsed);
                WorkersWin.warnG?.SetCurrentValue(UIElement.VisibilityProperty, hasGloves ? Visibility.Collapsed : Visibility.Visible);

                // 보안경 상태
                WorkersWin.progGoggles?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
                WorkersWin.borderGo?.SetCurrentValue(Border.BackgroundProperty, new SolidColorBrush(hasGloves ? Color.FromRgb(240, 253, 244) : Color.FromRgb(254, 252, 232)));
                WorkersWin.goggles?.SetCurrentValue(TextBlock.TextProperty, hasGloves ? "보안경 착용" : "보안경 미착용");
                WorkersWin.checkGo?.SetCurrentValue(UIElement.VisibilityProperty, hasGloves ? Visibility.Visible : Visibility.Collapsed);
                WorkersWin.warnGo?.SetCurrentValue(UIElement.VisibilityProperty, hasGloves ? Visibility.Collapsed : Visibility.Visible);

                // txtAlert에 현재 PPE 착용 상태 표시
                if (WorkersWin.txtAlert != null)
                {
                    var missingItems = new List<string>();
                    if (!hasHelmet) missingItems.Add("안전모");
                    if (!hasVest) missingItems.Add("안전 조끼");
                    if (!hasGloves) missingItems.Add("안전 장갑");
                    if (!hasGoggles) missingItems.Add("보안경");

                    if (missingItems.Count == 0)
                    {
                        WorkersWin.txtAlert.Text = "모든 안전 장비 착용 완료";
                        WorkersWin.txtAlert.Foreground = new SolidColorBrush(Colors.Green);
                    }
                    else
                    {
                        WorkersWin.txtAlert.Text = $"안전 장비 착용 후 입장해 주세요.";
                        WorkersWin.txtAlert.Foreground = new SolidColorBrush(Colors.Red);
                    }

                    Debug.WriteLine($"txtAlert 업데이트: {WorkersWin.txtAlert.Text}");
                }
                else
                {
                    Debug.WriteLine("txtAlert가 null입니다!");
                }

                // 근무 시간 판별
                int hour = currentTime.Hour;
                if (hour >= 6 && hour < 14)
                    WorkersWin.workTime.Text = "오전";
                else if (hour >= 14 && hour < 22)
                    WorkersWin.workTime.Text = "오후";
                else
                    WorkersWin.workTime.Text = "야간";
            });
        }
    }
}
