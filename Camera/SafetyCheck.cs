using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Size = OpenCvSharp.Size;

namespace Camera
{
    internal class SafetyCheck
    {
        private static InferenceSession session;

        // 카메라로 들어오는 이미지 분석용
        private static readonly int imgSize = 640;
        private static readonly float threshold = 0.5f;

        // 안전 장비 클래스 (YOLO 모델에 맞게 조정 필요)
        private static readonly string[] classNames = {
            "helmet",           // 0: 안전모
            "safety_vest",      // 1: 안전 조끼
            "gloves",           // 2: 장갑
            "person"            // 3: 사람
        };

        public class SafetyResult
        {
            public bool PersonDetected { get; set; }
            public bool HasHelmet { get; set; }
            public bool HasSafetyVest { get; set; }
            public bool HasGloves { get; set; }
            public bool IsSafe { get; set; }
            public string Message { get; set; }
        }

        // onnx 모델 연동
        public static void Safety()
        {
            string modelPath = "helmet.onnx";       // 파일명 변경해야 함
            session = new InferenceSession(modelPath);

            if (session == null) MessageBox.Show("모델 연동 실패");
        }

        public static SafetyResult CheckSafety(Mat frame)
        {
            if (session == null) Safety();

            try
            {
                // 이미지 전처리
                var inputTensor = PreprocessImage(frame);

                // 모델 실행
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };
                var results = session.Run(inputs);
                var output = results.FirstOrDefault()?.AsTensor<float>();

                // 결과 분석
                return AnalyzeDetections(output);
            }
            catch (Exception ex)
            {
                return new SafetyResult { Message = $"오류: {ex.Message}" };
            }
        }

        // 이미지 전처리
        private static Tensor<float> PreprocessImage(Mat frame)
        {
            var resized = new Mat();
            Cv2.Resize(frame, resized, new Size(imgSize, imgSize));

            var tensor = new DenseTensor<float>(new[] { 1, 3, imgSize, imgSize });
            var data = new byte[resized.Total() * resized.ElemSize()];
            resized.GetArray(out data);

            for (int y = 0; y < imgSize; y++)
            {
                for (int x = 0; x < imgSize; x++)
                {
                    int i = (y * imgSize + x) * 3;
                    tensor[0, 0, y, x] = data[i + 2] / 255.0f;     // R
                    tensor[0, 1, y, x] = data[i + 1] / 255.0f;     // G
                    tensor[0, 2, y, x] = data[i] / 255.0f;         // B
                }
            }
            return tensor;
        }

        private static SafetyResult AnalyzeDetections(Tensor<float> output)
        {
            var result = new SafetyResult();
            if (output == null) return result;

            bool hasHelmet = false, hasVest = false, hasGloves = false, hasPerson = false;
            int detections = output.Dimensions[1];

            for (int i = 0; i < detections; i++)
            {
                float confidence = output[0, i, 4];
                if (confidence < threshold) continue;

                // 가장 높은 확률의 클래스 찾기
                int classId = 0;
                float maxScore = output[0, i, 5];
                for (int c = 1; c < classNames.Length; c++)
                {
                    float score = output[0, i, 5 + c];
                    if (score > maxScore)
                    {
                        maxScore = score;
                        classId = c;
                    }
                }

                if (confidence * maxScore < threshold) continue;

                // 감지된 객체 확인
                switch (classNames[classId])
                {
                    case "helmet": hasHelmet = true; break;
                    case "safety_vest": hasVest = true; break;
                    case "gloves": hasGloves = true; break;
                    case "person": hasPerson = true; break;
                }
            }

            // 결과 설정
            result.HasHelmet = hasHelmet;
            result.HasSafetyVest = hasVest;
            result.HasGloves = hasGloves;
            result.PersonDetected = hasPerson;

            if (hasPerson)
            {
                var missing = new List<string>();
                if (!hasHelmet) missing.Add("안전모");
                if (!hasVest) missing.Add("안전조끼");
                if (!hasGloves) missing.Add("안전장갑");

                result.IsSafe = missing.Count == 0;
                result.Message = missing.Count == 0 ? "안전장비 착용 완료" : $"미착용: {string.Join(", ", missing)}";
            }
            else
            {
                result.IsSafe = true;
                result.Message = "작업자 없음";
            }

            return result;
        }

        public static void Dispose()
        {
            session?.Dispose();
        }
    }
}
