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
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;

namespace Camera
{
    internal class SafetyCheck
    {
        private static InferenceSession session;

        // 카메라로 들어오는 이미지 분석용
        private static readonly int imgSize = 416;
        private static readonly float threshold = 0.5f;

        // 안전 장비 클래스 (YOLO 모델에 맞게 조정 필요)
        private static readonly string[] classNames = {
            "helmet",           // 0: 안전모
            "gloves",           // 1: 안전 장갑
            "vest",             // 2: 안전 조끼
            "boots",
            "goggles",
            "none",
            "Person",           // 6: 사람
            "no_helmet",
            "no_goggle",
            "no_gloves",
            "no_boots"
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
            string modelPath = "best.onnx";       // 파일명 변경해야 함

            if (!File.Exists(modelPath))
            {
                Console.WriteLine($"모델 파일이 없습니다: {Path.GetFullPath(modelPath)}");
                MessageBox.Show($"모델 파일을 찾을 수 없습니다: {Path.GetFullPath(modelPath)}");
                return;
            }

            try
            {
                session = new InferenceSession(modelPath);  // 주석 해제!
                Console.WriteLine("모델 로드 성공");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"모델 로드 실패: {ex.Message}");
                MessageBox.Show($"모델 로드 실패: {ex.Message}");
            }
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
                var result = AnalyzeDetections(output);
                Debug.WriteLine($"분석 결과 - Person: {result.PersonDetected}, Helmet: {result.HasHelmet}, Vest: {result.HasSafetyVest}, Gloves: {result.HasGloves}");

                return result;
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

            bool hasPerson = false;
            var persons = new List<Rect>();
            var helmets = new List<Rect>();
            var vests = new List<Rect>();
            var gloves = new List<Rect>();

            int detections = output.Dimensions[1];

            for (int i = 0; i < detections; i++)
            {
                float objConf = output[0, i, 4];
                if (objConf < threshold) continue;

                // 가장 높은 클래스 확률 찾기
                int classId = -1;
                float bestScore = 0f;
                for (int c = 0; c < classNames.Length; c++)
                {
                    float classProb = output[0, i, 5 + c];
                    float score = objConf * classProb;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        classId = c;
                    }
                }
                if (bestScore < threshold || classId < 0) continue;

                // bbox 좌표 (YOLO 출력 구조: cx, cy, w, h)
                float cx = output[0, i, 0];
                float cy = output[0, i, 1];
                float w = output[0, i, 2];
                float h = output[0, i, 3];

                int x = (int)(cx - w / 2);
                int y = (int)(cy - h / 2);
                var rect = new Rect(x, y, (int)w, (int)h);

                // 감지된 클래스별로 분류
                switch (classNames[classId])
                {
                    case "Person": persons.Add(rect); hasPerson = true; break;
                    case "helmet": helmets.Add(rect); break;
                    case "vest": vests.Add(rect); break;
                    case "gloves": gloves.Add(rect); break;
                }
            }

            // 사람 없으면 "작업자 없음" 상태만 반환 (카메라는 계속 돌아감)
            if (persons.Count == 0)
            {
                result.PersonDetected = false;
                result.IsSafe = true;
                result.Message = "작업자 없음";
                return result;
            }

            // 매핑: 사람 bbox 안에 장비가 있는 경우만 착용 인정
            bool hasHelmet = false, hasVest = false, hasGloves = false;
            foreach (var person in persons)
            {
                if (helmets.Any(h => person.IntersectsWith(h))) hasHelmet = true;
                if (vests.Any(v => person.IntersectsWith(v))) hasVest = true;
                if (gloves.Any(g => person.IntersectsWith(g))) hasGloves = true;
            }

            // 결과 세팅
            result.PersonDetected = true;
            result.HasHelmet = hasHelmet;
            result.HasSafetyVest = hasVest;
            result.HasGloves = hasGloves;

            var missing = new List<string>();
            if (!hasHelmet) missing.Add("안전모");
            if (!hasVest) missing.Add("안전조끼");
            if (!hasGloves) missing.Add("안전장갑");

            result.IsSafe = missing.Count == 0;
            result.Message = missing.Count == 0 ? "안전장비 착용 완료" : $"미착용: {string.Join(", ", missing)}";

            return result;
        }

        public static void Dispose()
        {
            session?.Dispose();
        }
    }
}
