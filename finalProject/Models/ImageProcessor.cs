using AForge.Imaging;
using AForge.Imaging.Filters;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Shapes;

namespace finalProject.Models
{
    /// <summary>
    /// Handles advanced image processing tasks before prediction.
    /// </summary>
    public class ImageProcessor
    {
        /// <summary>
        /// Processes a captured image through a pipeline to enhance features for prediction.
        /// Pipeline: Grayscale -> Contrast Stretch -> Sharpen -> Bradley Thresholding (Adaptive Binarization)
        /// </summary>
        /// <param name="originalImage">The raw bitmap captured from the camera.</param>
        /// <returns>A binarized bitmap ready for the ONNX model.</returns>
        public Bitmap ProcessForPrediction(Bitmap originalImage)
        {
            // 1. 그레이스케일로 변환
            Bitmap grayscaleImage = Grayscale.CommonAlgorithms.BT709.Apply(originalImage);

            // 2. 대비(Contrast) 향상 필터 적용
            ContrastStretch contrastFilter = new ContrastStretch();
            contrastFilter.ApplyInPlace(grayscaleImage);

            // 3. 샤프닝(Sharpening) 필터 적용
            Sharpen sharpenFilter = new Sharpen();
            sharpenFilter.ApplyInPlace(grayscaleImage);

            // 4. 적응형 이진화 (Bradley's Local Thresholding) 적용
            BradleyLocalThresholding thresholdFilter = new BradleyLocalThresholding();
            Bitmap binarizedImage = thresholdFilter.Apply(grayscaleImage);

            // 중간 단계에서 사용된 그레이스케일 이미지는 메모리에서 해제
            grayscaleImage.Dispose();

            return binarizedImage;
        }
    }
}
