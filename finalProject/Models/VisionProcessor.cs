using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace finalProject.Models
{
    public class DetectionResult
    {
        public string Label { get; set; }
        public RectangleF Box { get; set; }
        public float Confidence { get; set; }
    }

    public class VisionProcessor : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly string[] _labels;
        private const int TargetWidth = 640;
        private const int TargetHeight = 640;
        private const byte Threshold = 128;

        public VisionProcessor(string modelPath)
        {
            _labels = new[] { "open", "short", "mousebite", "spur", "copper", "pin-hole" };
            var sessionOptions = new SessionOptions();
            sessionOptions.AppendExecutionProvider_CPU();
            _session = new InferenceSession(modelPath, sessionOptions);
        }

        public (List<DetectionResult> detections, Bitmap binarizedImage) Predict(Bitmap image)
        {
            Bitmap resizedImage = ResizeImage(image, TargetWidth, TargetHeight);
            Bitmap binarized = BinarizeImageFast(resizedImage); // 👈 수정

            var inputTensor = ConvertToOnnxInputFast(binarized); // 👈 수정
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };

            using (var results = _session.Run(inputs))
            {
                var output = results.FirstOrDefault(item => item.Name == "output0")?.AsTensor<float>();
                if (output == null)
                {
                    resizedImage.Dispose();
                    return (new List<DetectionResult>(), binarized);
                }

                var detections = PostProcess(output);
                resizedImage.Dispose();
                return (detections, binarized);
            }
        }

        /// <summary>
        /// 고속 이진화 - LockBits 사용 (100배 이상 빠름)
        /// </summary>
        private Bitmap BinarizeImageFast(Bitmap original)
        {
            Bitmap binarized = new Bitmap(original.Width, original.Height, PixelFormat.Format24bppRgb);

            BitmapData srcData = original.LockBits(
                new Rectangle(0, 0, original.Width, original.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            BitmapData dstData = binarized.LockBits(
                new Rectangle(0, 0, binarized.Width, binarized.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            int bytes = Math.Abs(srcData.Stride) * srcData.Height;
            byte[] srcBuffer = new byte[bytes];
            byte[] dstBuffer = new byte[bytes];

            Marshal.Copy(srcData.Scan0, srcBuffer, 0, bytes);

            for (int i = 0; i < bytes; i += 3)
            {
                // BGR 순서
                byte b = srcBuffer[i];
                byte g = srcBuffer[i + 1];
                byte r = srcBuffer[i + 2];

                int gray = (int)(r * 0.3 + g * 0.59 + b * 0.11);
                byte newValue = (byte)(gray < Threshold ? 0 : 255);

                dstBuffer[i] = newValue;     // B
                dstBuffer[i + 1] = newValue; // G
                dstBuffer[i + 2] = newValue; // R
            }

            Marshal.Copy(dstBuffer, 0, dstData.Scan0, bytes);
            original.UnlockBits(srcData);
            binarized.UnlockBits(dstData);

            return binarized;
        }

        /// <summary>
        /// 고속 텐서 변환 - LockBits 사용
        /// </summary>
        private DenseTensor<float> ConvertToOnnxInputFast(Bitmap image)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 3, TargetHeight, TargetWidth });

            BitmapData bmpData = image.LockBits(
                new Rectangle(0, 0, image.Width, image.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            int bytes = Math.Abs(bmpData.Stride) * bmpData.Height;
            byte[] buffer = new byte[bytes];
            Marshal.Copy(bmpData.Scan0, buffer, 0, bytes);

            int stride = bmpData.Stride;
            for (int y = 0; y < TargetHeight; y++)
            {
                for (int x = 0; x < TargetWidth; x++)
                {
                    int idx = y * stride + x * 3;
                    // BGR 순서를 RGB로 변환
                    tensor[0, 0, y, x] = buffer[idx + 2] / 255.0f; // R
                    tensor[0, 1, y, x] = buffer[idx + 1] / 255.0f; // G
                    tensor[0, 2, y, x] = buffer[idx] / 255.0f;     // B
                }
            }

            image.UnlockBits(bmpData);
            return tensor;
        }

        private List<DetectionResult> PostProcess(Tensor<float> output, float confidenceThreshold = 0.5f, float nmsThreshold = 0.45f)
        {
            var results = new List<DetectionResult>();
            var (batchSize, boxCount, _) = (output.Dimensions[0], output.Dimensions[2], output.Dimensions[1]);

            for (int i = 0; i < boxCount; i++)
            {
                var classScores = new float[_labels.Length];
                for (int j = 0; j < _labels.Length; j++)
                {
                    classScores[j] = output[0, 4 + j, i];
                }

                float maxScore = classScores.Max();
                if (maxScore < confidenceThreshold) continue;

                int classId = Array.IndexOf(classScores, maxScore);

                float x_center = output[0, 0, i];
                float y_center = output[0, 1, i];
                float width = output[0, 2, i];
                float height = output[0, 3, i];

                float x1 = x_center - width / 2;
                float y1 = y_center - height / 2;

                results.Add(new DetectionResult
                {
                    Label = _labels[classId],
                    Box = new RectangleF(x1, y1, width, height),
                    Confidence = maxScore
                });
            }

            return ApplyNms(results, nmsThreshold);
        }

        private List<DetectionResult> ApplyNms(List<DetectionResult> results, float threshold)
        {
            var finalDetections = new List<DetectionResult>();
            results = results.OrderByDescending(r => r.Confidence).ToList();

            while (results.Count > 0)
            {
                var current = results[0];
                finalDetections.Add(current);
                results.RemoveAt(0);

                results = results.Where(r => CalculateIoU(current.Box, r.Box) < threshold).ToList();
            }

            return finalDetections;
        }

        private float CalculateIoU(RectangleF boxA, RectangleF boxB)
        {
            float xA = Math.Max(boxA.Left, boxB.Left);
            float yA = Math.Max(boxA.Top, boxB.Top);
            float xB = Math.Min(boxA.Right, boxB.Right);
            float yB = Math.Min(boxA.Bottom, boxB.Bottom);

            float intersectionArea = Math.Max(0, xB - xA) * Math.Max(0, yB - yA);
            float boxAArea = boxA.Width * boxA.Height;
            float boxBArea = boxB.Width * boxB.Height;

            return intersectionArea / (boxAArea + boxBArea - intersectionArea);
        }

        private static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);
            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);
            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }
            return destImage;
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}