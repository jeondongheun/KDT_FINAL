using System;
using System.Drawing;
using System.Linq;
using AForge.Video;
using AForge.Video.DirectShow;

namespace finalProject.Models
{
    public class CameraManager
    {
        public event Action<Bitmap> NewFrame;
        private VideoCaptureDevice videoSource;
        public bool IsRunning => videoSource != null && videoSource.IsRunning;

        public bool StartCamera(int cameraIndex = 0)
        {
            var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices.Count == 0)
            {
                // 사용 가능한 카메라 없음
                return false;
            }

            // 가장 첫 번째 카메라로 시작
            videoSource = new VideoCaptureDevice(videoDevices[cameraIndex].MonikerString);
            videoSource.NewFrame += VideoSource_NewFrame;
            videoSource.Start();
            return true;
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            // 새 프레임이 도착하면 이벤트를 통해 전달
            NewFrame?.Invoke((Bitmap)eventArgs.Frame.Clone());
        }

        public void StopCamera()
        {
            if (videoSource != null)
            {
                videoSource.NewFrame -= VideoSource_NewFrame;
                
                if (videoSource.IsRunning)
                {
                    videoSource.SignalToStop();
                    videoSource.WaitForStop();
                }
                videoSource = null;
            }
        }
    }
}

