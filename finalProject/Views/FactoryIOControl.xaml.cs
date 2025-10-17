using finalProject.Models;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.ComponentModel;

namespace finalProject.Views
{
    public partial class FactoryIOControl : Window
    {
        private TcpListener modbusServer;
        private TcpClient connectedClient;
        private NetworkStream stream;
        private DispatcherTimer plcTimer;
        private Thread serverThread;
        private bool isRunning = false;
        private bool isServerRunning = false;

        // 로직 클래스 인스턴스
        private BasesLidsLogic basesLidsLogic;
        private PickAndPlaceLogic pickAndPlaceLogic;
        private StackerLogic stackerLogic;

        // Factory IO가 보내는 신호 (센서들) - Inputs
        private bool[] factoryInputs = new bool[100];

        // Factory IO가 읽는 신호 (액추에이터들) - Coils
        private bool[] factoryCoils = new bool[100];

        private ushort[] holdingRegisters = new ushort[100];

        // YOLO 및 분류 로직 변수
        private bool prodsAtEntryPrev = false;
        private bool convWithSensorStop = false;
        private bool prodAtPusherPrev = false;
        private bool productsPusher = false;
        private bool errorPusher = false;
        private bool prodCounterPrev = false;
        private bool errorCounterPrev = false;
        private bool prodsBoxNeeded = false;
        private bool errorsBoxNeeded = false;
        private bool prodsRollerActive = false;
        private bool errorsRollerActive = false;
        private bool prodCounterWasHigh = false;
        private bool errorCounterWasHigh = false;
        private bool normalSortStop = false;
        private bool deletePCBStop = false;
        private bool errorEnterPrev = false;
        private bool errorSortConvStop = false;
        private bool normalSensorPrev = false;
        private bool errorCateSensorPrev = false;

        private int convWithSensorStopTimer = 0;
        private const int CONV_RESTART_DELAY = 40;
        private int pusherTimer = 0;
        private const int PUSHER_ACTIVE_TIME = 20;
        private int productCount = 0;
        private int errorCount = 0;
        private int prodRollerTimer = 0;
        private int errorRollerTimer = 0;
        private const int ROLLER_ACTIVE_TIME = 170;
        private const int ERROR_ROLLER_ACTIVE_TIME = 170;
        private int errorSortConvStopTimer = 0;
        private const int ERROR_SORT_CONV_RESTART_DELAY = 40;
        private int errorPusherTimer = 0;
        private const int ERROR_PUSHER_ACTIVE_TIME = 20;
        private int prodRollerDelayTimer = 0;
        private const int PROD_ROLLER_DELAY_TIME = 40; // 2초 (50ms * 40 = 2000ms)
        private bool prodRollerDelayActive = false;

        // 비전 시스템 관련 변수 추가
        private readonly CameraManager _cameraManager;
        private readonly ImageProcessor _imageProcessor;
        private readonly VisionProcessor _visionProcessor;
        private readonly string _savePath = @"C:\Users\user\Desktop\PCB";
        private Bitmap _currentFrame;
        private readonly object _frameLock = new object();
        private bool isInspecting = false;
        private bool lastInspectionResult = true;

        public FactoryIOControl()
        {
            InitializeComponent();

            // 로직 클래스 초기화
            basesLidsLogic = new BasesLidsLogic();
            pickAndPlaceLogic = new PickAndPlaceLogic();
            stackerLogic = new StackerLogic();

            InitializeModbusServer();
            InitializePlcTimer();

            // 비전 시스템 초기화 로직
            _cameraManager = new CameraManager();
            _imageProcessor = new ImageProcessor();

            // ONNX 모델 파일 경로 지정
            _visionProcessor = new VisionProcessor("pcb_best.onnx");

            // 저장 폴더 생성
            Directory.CreateDirectory(_savePath);

            // 카메라 시작 및 이벤트 핸들러 연결
            _cameraManager.NewFrame += CameraManager_NewFrame;
            if (!_cameraManager.StartCamera())
            {
                LogMessage("사용 가능한 카메라를 찾을 수 없습니다.");
                MessageBox.Show("카메라를 찾을 수 없습니다!", "카메라 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                LogMessage("카메라 시작됨.");
            }
        }

        // 새 메서드 및 이벤트 핸들러 추가
        /// <summary>
        /// 카메라에서 새 프레임이 들어올 때마다 호출됩니다.
        /// </summary>
        private void CameraManager_NewFrame(Bitmap frame)
        {
            lock (_frameLock)
            {
                _currentFrame?.Dispose();
                _currentFrame = (Bitmap)frame.Clone();
            }

            // UI 스레드에서 이미지를 업데이트합니다.
            Dispatcher.Invoke(() =>
            {
                CameraFeed.Source = BitmapToBitmapSource(frame);
            });
        }

        /// <summary>
        /// 키보드 키를 누를 때 호출됩니다.
        /// </summary>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                LogMessage("스페이스바 감지. ROI 캡처 및 분석을 시작합니다.");
                // CaptureAndProcessROI();
            }
        }

        /// <summary>
        /// ROI 영역을 캡처하고 전체 이미지 처리 파이프라인을 실행합니다.
        /// </summary>
        private void CaptureAndProcessROI()
        {
            Bitmap roiBitmap;
            lock (_frameLock)
            {
                if (_currentFrame == null)
                {
                    LogMessage("캡처할 카메라 프레임이 없습니다.");
                    return;
                }

                // ROI 영역 계산 (카메라 중앙 320x320)
                int roiSize = 320;
                int x = (_currentFrame.Width - roiSize) / 2;
                int y = (_currentFrame.Height - roiSize) / 2;
                var roiRect = new System.Drawing.Rectangle(x, y, roiSize, roiSize);

                // ROI 영역만 잘라내기
                roiBitmap = _currentFrame.Clone(roiRect, _currentFrame.PixelFormat);
            }

            // 원본 캡처 이미지 저장
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            string originalFileName = $"{timestamp}.png";
            string originalFilePath = Path.Combine(_savePath, originalFileName);
            roiBitmap.Save(originalFilePath, ImageFormat.Png);
            LogMessage($"원본 이미지 저장: {originalFilePath}");

            // 이미지 전처리 (ImageProcessor 사용)
            Bitmap binarizedBitmap = _imageProcessor.ProcessForPrediction(roiBitmap);

            // 이진화된 이미지 저장
            string binaryFileName = $"{timestamp}_binary.png";
            string binaryFilePath = Path.Combine(_savePath, binaryFileName);
            binarizedBitmap.Save(binaryFilePath, ImageFormat.Png);
            LogMessage($"이진화 이미지 저장: {binaryFilePath}");

            // 이진화된 이미지 UI에 표시
            Dispatcher.Invoke(() =>
            {
                BinarizedImageFeed.Source = BitmapToBitmapSource(binarizedBitmap);
            });

            // 불량 탐지 (VisionProcessor 사용)
            (var detections, _) = _visionProcessor.Predict(roiBitmap);  // 원본 ROI 이미지를 전달

            // 결과 로그 출력
            if (detections == null || detections.Count == 0)
            {
                lastInspectionResult = true; // 정상
                LogMessage("👉 결과: 정상 제품입니다.");
            }
            else
            {
                lastInspectionResult = false;
                LogMessage($"👉 결과: 불량 탐지! (총 {detections.Count}개)");

                var defectCounts = detections.GroupBy(d => d.Label)
                                             .ToDictionary(g => g.Key, g => g.Count());

                foreach (var defect in defectCounts)
                {
                    LogMessage($"   - {defect.Key}: {defect.Value}개");
                }
            }

            // 사용된 비트맵 객체 메모리 해제
            roiBitmap.Dispose();
            binarizedBitmap.Dispose();
        }

        /// <summary>
        /// System.Drawing.Bitmap을 WPF에서 사용할 수 있는 BitmapSource로 변환합니다.
        /// </summary>
        private BitmapSource BitmapToBitmapSource(Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();   // UI 스레드 간 충돌 방지
                return bitmapImage;
            }
        }

        private void InitializeModbusServer()
        {
            try
            {
                modbusServer = new TcpListener(IPAddress.Any, 502);
                modbusServer.Start();
                isServerRunning = true;

                serverThread = new Thread(ListenForClients);
                serverThread.IsBackground = true;
                serverThread.Start();

                LogMessage("✓ Modbus 서버 시작 (Port 502)");
                LogMessage("  Factory IO에서 연결을 기다리는 중...");
            }
            catch (Exception ex)
            {
                LogMessage($"✗ Modbus 서버 시작 실패: {ex.Message}");
                MessageBox.Show($"Modbus 서버를 시작할 수 없습니다!\n\n오류: {ex.Message}",
                    "서버 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ListenForClients()
        {
            while (isServerRunning)
            {
                try
                {
                    if (modbusServer.Pending())
                    {
                        connectedClient = modbusServer.AcceptTcpClient();
                        stream = connectedClient.GetStream();

                        Dispatcher.Invoke(() => LogMessage("✓ Factory IO 연결됨!"));

                        HandleClientRequests();
                    }
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => LogMessage($"연결 오류: {ex.Message}"));
                }
            }
        }

        private void HandleClientRequests()
        {
            byte[] buffer = new byte[256];

            while (connectedClient != null && connectedClient.Connected && isServerRunning)
            {
                try
                {
                    if (stream.DataAvailable)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            byte[] response = ProcessModbusRequest(buffer, bytesRead);
                            if (response != null)
                            {
                                stream.Write(response, 0, response.Length);
                            }
                        }
                    }
                    Thread.Sleep(1);
                }
                catch (Exception)
                {
                    break;
                }
            }

            Dispatcher.Invoke(() => LogMessage("✗ Factory IO 연결 끊김"));
            connectedClient?.Close();
            connectedClient = null;
        }

        private byte[] ProcessModbusRequest(byte[] request, int length)
        {
            if (length < 8) return null;

            ushort transactionId = (ushort)((request[0] << 8) | request[1]);
            byte unitId = request[6];
            byte functionCode = request[7];

            switch (functionCode)
            {
                case 0x01: // Read Coils
                    return ReadCoils(request, transactionId, unitId);

                case 0x02: // Read Discrete Inputs
                    return ReadDiscreteInputs(request, transactionId, unitId);

                case 0x03: // Read Holding Registers
                    return ReadHoldingRegisters(request, transactionId, unitId);

                case 0x05: // Write Single Coil
                    return WriteSingleCoil(request, transactionId, unitId);

                case 0x0F: // Write Multiple Coils
                    return WriteMultipleCoils(request, transactionId, unitId);

                default:
                    LogMessage($"⚠ 지원하지 않는 Function Code: {functionCode:X2}");
                    return null;
            }
        }

        private byte[] ReadCoils(byte[] request, ushort transactionId, byte unitId)
        {
            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            int byteCount = (quantity + 7) / 8;
            byte[] response = new byte[9 + byteCount];

            response[0] = (byte)(transactionId >> 8);
            response[1] = (byte)(transactionId & 0xFF);
            response[2] = 0x00;
            response[3] = 0x00;
            response[4] = (byte)((3 + byteCount) >> 8);
            response[5] = (byte)((3 + byteCount) & 0xFF);
            response[6] = unitId;
            response[7] = 0x01;
            response[8] = (byte)byteCount;

            for (int i = 0; i < quantity; i++)
            {
                if (startAddress + i < factoryCoils.Length && factoryCoils[startAddress + i])
                {
                    response[9 + i / 8] |= (byte)(1 << (i % 8));
                }
            }

            return response;
        }

        private byte[] ReadDiscreteInputs(byte[] request, ushort transactionId, byte unitId)
        {
            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            int byteCount = (quantity + 7) / 8;
            byte[] response = new byte[9 + byteCount];

            response[0] = (byte)(transactionId >> 8);
            response[1] = (byte)(transactionId & 0xFF);
            response[2] = 0x00;
            response[3] = 0x00;
            response[4] = (byte)((3 + byteCount) >> 8);
            response[5] = (byte)((3 + byteCount) & 0xFF);
            response[6] = unitId;
            response[7] = 0x02;
            response[8] = (byte)byteCount;

            for (int i = 0; i < quantity; i++)
            {
                if (startAddress + i < factoryCoils.Length && factoryCoils[startAddress + i])
                {
                    response[9 + i / 8] |= (byte)(1 << (i % 8));
                }
            }

            return response;
        }

        private byte[] ReadHoldingRegisters(byte[] request, ushort transactionId, byte unitId)
        {
            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            byte[] response = new byte[9 + (quantity * 2)];

            response[0] = (byte)(transactionId >> 8);
            response[1] = (byte)(transactionId & 0xFF);
            response[2] = 0x00;
            response[3] = 0x00;
            response[4] = (byte)((3 + (quantity * 2)) >> 8);
            response[5] = (byte)((3 + (quantity * 2)) & 0xFF);
            response[6] = unitId;
            response[7] = 0x03;
            response[8] = (byte)(quantity * 2);

            for (int i = 0; i < quantity; i++)
            {
                int address = startAddress + i;
                if (address < holdingRegisters.Length)
                {
                    response[9 + (i * 2)] = (byte)(holdingRegisters[address] >> 8);
                    response[10 + (i * 2)] = (byte)(holdingRegisters[address] & 0xFF);
                }
            }

            return response;
        }

        private byte[] WriteSingleCoil(byte[] request, ushort transactionId, byte unitId)
        {
            ushort address = (ushort)((request[8] << 8) | request[9]);
            bool value = request[10] == 0xFF;

            if (address < factoryInputs.Length)
            {
                factoryInputs[address] = value;
            }

            byte[] response = new byte[12];
            Array.Copy(request, response, 12);
            return response;
        }

        private byte[] WriteMultipleCoils(byte[] request, ushort transactionId, byte unitId)
        {
            ushort startAddress = (ushort)((request[8] << 8) | request[9]);
            ushort quantity = (ushort)((request[10] << 8) | request[11]);

            for (int i = 0; i < quantity; i++)
            {
                int address = startAddress + i;
                if (address < factoryInputs.Length)
                {
                    bool value = (request[13 + i / 8] & (1 << (i % 8))) != 0;
                    factoryInputs[address] = value;
                }
            }

            byte[] response = new byte[12];
            response[0] = (byte)(transactionId >> 8);
            response[1] = (byte)(transactionId & 0xFF);
            response[2] = 0x00;
            response[3] = 0x00;
            response[4] = 0x00;
            response[5] = 0x06;
            response[6] = unitId;
            response[7] = 0x0F;
            response[8] = request[8];
            response[9] = request[9];
            response[10] = request[10];
            response[11] = request[11];

            return response;
        }

        private void InitializePlcTimer()
        {
            plcTimer = new DispatcherTimer();
            plcTimer.Interval = TimeSpan.FromMilliseconds(50);
            plcTimer.Tick += PlcScanCycle;
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!isRunning && connectedClient != null && connectedClient.Connected)
            {
                isRunning = true;
                plcTimer.Start();
                LogMessage("▶ 시스템 시작 - PLC 스캔 시작");
                BtnStart.IsEnabled = false;
                BtnStop.IsEnabled = true;
            }
            else if (connectedClient == null || !connectedClient.Connected)
            {
                MessageBox.Show("Factory IO가 연결되어 있지 않습니다!",
                    "연결 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning)
            {
                isRunning = false;
                plcTimer.Stop();
                ResetAllOutputs();
                LogMessage("⏸ 시스템 정지");
                BtnStart.IsEnabled = true;
                BtnStop.IsEnabled = false;
            }
        }

        private void PlcScanCycle(object sender, EventArgs e)
        {
            try
            {
                ExecutePlcLogic();
            }
            catch (Exception ex)
            {
                LogMessage($"⚠ 스캔 오류: {ex.Message}");
            }
        }

        private void ExecutePlcLogic()
        {
            // 모든 입력이 꺼졌을 때 리셋
            bool allInputsOff = !factoryInputs[FactoryAddresses.INPUT_BASES_AT_ENTRY] &&
                                !factoryInputs[FactoryAddresses.INPUT_BASES_AT_EXIT] &&
                                !factoryInputs[FactoryAddresses.INPUT_LIDS_AT_ENTRY] &&
                                !factoryInputs[FactoryAddresses.INPUT_LIDS_AT_EXIT];

            if (allInputsOff)
            {
                // 필요시 리셋 로직
            }

            // 1. Bases 로직 실행
            basesLidsLogic.ExecuteBasesLogic(factoryInputs, factoryCoils);

            // 2. Lids 로직 실행
            basesLidsLogic.ExecuteLidsLogic(factoryInputs, factoryCoils);

            // 3. 픽앤플레이스 조립 로직 실행
            pickAndPlaceLogic.ExecuteAssembly(
                basesLidsLogic.BasesReadyForAssembly,
                basesLidsLogic.LidsReadyForAssembly,
                factoryCoils,
                basesLidsLogic
            );

            // 4. YOLO 모델 연동 - 불량 검출
            ExecuteYoloDetectionLogic();

            // 5. 정상 제품 분류 로직
            ExecuteNormalSortingLogic();

            // 6. 불량 제품 분류 로직
            ExecuteErrorSortingLogic();

            // 7. Stacker 로직 실행
            stackerLogic.ExecuteNormalStacker(factoryInputs, factoryCoils, holdingRegisters);
            stackerLogic.ExecuteErrorStacker(factoryInputs, factoryCoils, holdingRegisters);

            // 8. 공통 컨베이어 제어
            factoryCoils[FactoryAddresses.COIL_CURVED_CONVC] = true;
            factoryCoils[FactoryAddresses.COIL_SORT_CONVC] = !errorSortConvStop;
        }

        // 불량 유무 검출
        private void ExecuteYoloDetectionLogic()
        {
            bool prodsEnter = factoryInputs[FactoryAddresses.INPUT_ERROR_DERECTED] && !prodsAtEntryPrev;
            prodsAtEntryPrev = factoryInputs[FactoryAddresses.INPUT_ERROR_DERECTED];
            if (prodsEnter)
            {
                convWithSensorStop = true;
                convWithSensorStopTimer = 0;
                isInspecting = true;

                if (!prodsRollerActive)
                {
                    prodsBoxNeeded = true;
                }

                Task.Run(() =>
                {
                    CaptureAndProcessROI();
                    convWithSensorStop = false;
                    isInspecting = false;
                });

            }

            if (factoryInputs[FactoryAddresses.INPUT_ERROR_DERECTED])
            {
                // 센서에 제품이 있을 때만 조명 제어
                bool yellowLight = isInspecting;
                bool greenLight = !isInspecting && lastInspectionResult;
                bool redLight = !isInspecting && !lastInspectionResult;

                factoryCoils[FactoryAddresses.COIL_DEFECTED_LIGHT] = yellowLight;
                factoryCoils[FactoryAddresses.COIL_NORMAL_LIGHT] = greenLight;
                factoryCoils[FactoryAddresses.COIL_ERROR_LIGHT] = redLight;
            }
            else
            {
                // 센서에 제품이 없으면 모든 조명 OFF
                factoryCoils[FactoryAddresses.COIL_DEFECTED_LIGHT] = false;
                factoryCoils[FactoryAddresses.COIL_NORMAL_LIGHT] = false;
                factoryCoils[FactoryAddresses.COIL_ERROR_LIGHT] = false;
            }

            factoryCoils[FactoryAddresses.COIL_CONV_WITH_SENSOR] = !convWithSensorStop;
            factoryCoils[FactoryAddresses.COIL_BOX_EMITTER] = prodsBoxNeeded && !prodsRollerActive;

        }

        private void ExecuteNormalSortingLogic()
        {
            // 1. 정상 제품 센서 감지
            bool normalSensor = factoryInputs[FactoryAddresses.INPUT_NORMAL_SENSOR];
            bool productPassedSensor = !normalSensor && normalSensorPrev;

            if (productPassedSensor)
            {
                normalSortStop = true;
                productsPusher = true;
                pusherTimer = 0;
            }
            normalSensorPrev = normalSensor;

            // 2. Pusher 타이머 처리
            if (productsPusher)
            {
                pusherTimer++;
                if (pusherTimer >= PUSHER_ACTIVE_TIME)
                {
                    normalSortStop = false;
                    productsPusher = false;
                    pusherTimer = 0;
                }
            }

            // 3. 정상 제품 카운트
            bool prodCounterCurrent = factoryInputs[FactoryAddresses.INPUT_PROD_COUNTER];

            if (prodCounterCurrent && !prodCounterPrev)
            {
                prodCounterWasHigh = true;
            }

            if (!prodCounterCurrent && prodCounterPrev && prodCounterWasHigh)
            {
                productCount++;
                LogMessage($"📦 상품 개수 측정 : {productCount}/3");
                prodCounterWasHigh = false;
            }
            prodCounterPrev = prodCounterCurrent;

            // 4. 박스에 3개 담기면 롤러 가동
            if (productCount >= 3 && !prodsRollerActive && !prodRollerDelayActive)
            {
                prodRollerDelayActive = true;
                prodRollerDelayTimer = 0;
                prodRollerTimer = 0;
                productCount = 0;
                prodsBoxNeeded = false;
            }

            if (prodRollerDelayActive)
            {
                prodRollerDelayTimer++;
                if (prodRollerDelayTimer >= PROD_ROLLER_DELAY_TIME)
                {
                    prodsRollerActive = true;
                    prodRollerTimer = 0;
                    prodRollerDelayActive = false;
                    prodRollerDelayTimer = 0;
                }
            }

            if (prodsRollerActive)
            {
                prodRollerTimer++;
                if (prodRollerTimer >= ROLLER_ACTIVE_TIME)
                {
                    prodsRollerActive = false;
                    prodRollerTimer = 0;
                }
            }

            // Outputs
            factoryCoils[FactoryAddresses.COIL_NORMAL_PUSHER] = productsPusher;
            factoryCoils[FactoryAddresses.COIL_NORMAL_SORT] = !normalSortStop;
            factoryCoils[FactoryAddresses.COIL_NORMAL_ROLLER] = prodsRollerActive;
        }

        private void ExecuteErrorSortingLogic()
        {
            // 1. 불량 종류 분석
            bool errorEnter = factoryInputs[FactoryAddresses.INPUT_ERROR_SORT_SENSOR] && !errorEnterPrev;
            errorEnterPrev = factoryInputs[FactoryAddresses.INPUT_ERROR_SORT_SENSOR];
            if (errorEnter)
            {
                errorSortConvStop = true;
                errorSortConvStopTimer = 0;

                if (!errorsRollerActive)
                {
                    errorsBoxNeeded = true;
                }
            }

            // Conveyor 재시작 타이머
            if (errorSortConvStop)
            {
                errorSortConvStopTimer++;
                if (errorSortConvStopTimer >= ERROR_SORT_CONV_RESTART_DELAY)
                {
                    errorSortConvStop = false;
                    errorSortConvStopTimer = 0;
                }
            }

            // 2. 불량 컨베이어 Pusher
            bool errorCateSensor = factoryInputs[FactoryAddresses.INPUT_ERROR_CATE_SENSOR];
            bool errorPassedSensor = !errorCateSensor && errorCateSensorPrev;

            if (errorPassedSensor)
            {
                deletePCBStop = true;
                errorPusher = true;
                errorPusherTimer = 0;
            }
            errorCateSensorPrev = errorCateSensor;

            if (errorPusher)
            {
                errorPusherTimer++;
                if (errorPusherTimer >= PUSHER_ACTIVE_TIME)
                {
                    deletePCBStop = false;
                    errorPusher = false;
                    errorPusherTimer = 0;
                }
            }

            // 3. 불량 제품 카운트
            bool errorCounterCurrent = factoryInputs[FactoryAddresses.INPUT_ERROR_COUNTER];

            if (errorCounterCurrent && !errorCounterPrev)
            {
                errorCounterWasHigh = true;
            }

            if (!errorCounterCurrent && errorCounterPrev && errorCounterWasHigh)
            {
                errorCount++;
                LogMessage($"📦 불량품 개수 측정 : {errorCount}/3");
                errorCounterWasHigh = false;
            }
            errorCounterPrev = errorCounterCurrent;

            // 4. 박스에 3개 담기면 롤러 가동
            if (errorCount >= 3 && !errorsRollerActive)
            {
                errorsRollerActive = true;
                errorRollerTimer = 0;
                errorCount = 0;
                errorsBoxNeeded = false;
            }

            if (errorsRollerActive)
            {
                errorRollerTimer++;
                if (errorRollerTimer >= ERROR_ROLLER_ACTIVE_TIME)
                {
                    errorsRollerActive = false;
                    errorRollerTimer = 0;
                }
            }

            // Outputs
            factoryCoils[FactoryAddresses.COIL_ERROR_BOX_EMITTER] = errorsBoxNeeded && !errorsRollerActive;
            factoryCoils[FactoryAddresses.COIL_ERROR_PUSHER] = errorPusher;
            factoryCoils[FactoryAddresses.COIL_DEL_PCB] = !deletePCBStop;
            factoryCoils[FactoryAddresses.COIL_ERROR_ROLLER] = errorsRollerActive;
        }

        private void ResetAllOutputs()
        {
            Array.Clear(factoryCoils, 0, factoryCoils.Length);

            // 로직 클래스 리셋
            basesLidsLogic.Reset();
            pickAndPlaceLogic.Reset();
            stackerLogic.Reset();

            // YOLO 및 분류 변수 리셋
            prodsAtEntryPrev = false;
            convWithSensorStop = false;
            productsPusher = false;
            errorPusher = false;
            prodCounterPrev = false;
            errorCounterPrev = false;
            prodsBoxNeeded = false;
            errorsBoxNeeded = false;
            prodsRollerActive = false;
            errorsRollerActive = false;
            prodCounterWasHigh = false;
            errorCounterWasHigh = false;
            normalSortStop = false;
            deletePCBStop = false;
            errorEnterPrev = false;
            errorSortConvStop = false;
            normalSensorPrev = false;
            errorCateSensorPrev = false;

            convWithSensorStopTimer = 0;
            pusherTimer = 0;
            productCount = 0;
            errorCount = 0;
            prodRollerTimer = 0;
            errorRollerTimer = 0;
            errorSortConvStopTimer = 0;
            errorPusherTimer = 0;
            isInspecting = false;
            lastInspectionResult = true;
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
                TxtLog.ScrollToEnd();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            isServerRunning = false;
            isRunning = false;
            plcTimer?.Stop();
            ResetAllOutputs();
            stream?.Close();
            connectedClient?.Close();
            modbusServer?.Stop();
            _cameraManager?.StopCamera();
            _currentFrame?.Dispose();
            base.OnClosed(e);
        }

        //protected override void OnClosing(CancelEventArgs e)
        //{
        //    base.OnClosing(e);

        //    // WPF 창이 닫히기 전에 카메라 정지
        //    if (_cameraManager.IsRunning)
        //    {
        //        _cameraManager.StopCamera();
        //    }
        //}
    }
}