using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace FactoryIOControl
{
    public partial class MainWindow : Window
    {
        private TcpListener modbusServer;
        private TcpClient connectedClient;
        private NetworkStream stream;
        private DispatcherTimer plcTimer;
        private Thread serverThread;
        private bool isRunning = false;
        private bool isServerRunning = false;


        // Factory IO가 보내는 신호 (센서들) - Inputs
        private bool[] factoryInputs = new bool[100];

        // Factory IO가 읽는 신호 (액추에이터들) - Coils
        private bool[] factoryCoils = new bool[100];

        private ushort[] holdingRegisters = new ushort[100];

        // Factory IO Input addresses (센서 - Factory IO가 보냄)
        private const int INPUT_LIDS_CENTER_BUSY = 0;
        private const int INPUT_LIDS_CENTER_ERROR = 1;
        private const int INPUT_LIDS_AT_ENTRY = 2;
        private const int INPUT_LIDS_AT_EXIT = 6;
        private const int INPUT_LIDS_CLAMPED = 20;
        private const int INPUT_LIDS_ENTER = 28;
        private const int INPUT_BASES_CENTER_BUSY = 3;
        private const int INPUT_BASES_CENTER_ERROR = 4;
        private const int INPUT_BASES_AT_ENTRY = 5;
        private const int INPUT_BASES_AT_EXIT = 7;
        private const int INPUT_BASES_CLAMPED = 18;
        private const int INPUT_BASES_ENTER = 27;
        private const int INPUT_ITEM_DETECTED = 14;
        private const int INPUT_ERROR_DERECTED = 29;
        private const int INPUT_PROD_COUNTER = 34;
        private const int INPUT_NORMAL_SENSOR = 40;
        private const int INPUT_ERROR_SORT_SENSOR = 42;
        private const int INPUT_ERROR_CATE_SENSOR = 43;

        // Factory IO Coil addresses (액추에이터 - Factory IO가 읽음)
        private const int COIL_LIDS_CENTER_START = 8;
        private const int COIL_BASES_CENTER_START = 9;
        private const int COIL_LIDS_EMITTER = 4;
        private const int COIL_LIDS_EXIT_CONV1 = 5;
        private const int COIL_BASES_EMITTER = 11;
        private const int COIL_BASES_EXIT_CONV1 = 12;
        private const int COIL_LIDS_EXIT_CONV3 = 13;
        private const int COIL_CURVED_EXIT_L = 14;
        private const int COIL_BASES_EXIT_CONV2 = 15;
        private const int COIL_LIDS_RAW_CONV = 16;
        private const int COIL_BASES_RAW_CONV = 17;
        private const int COIL_CURVED_EXIT_B = 20;
        private const int COIL_LIDS_EXIT_CONV2 = 21;
        private const int COIL_BASES_EXIT_CONV3 = 22;
        private const int COIL_MOVE_Z = 24;
        private const int COIL_MOVE_X = 25;
        private const int COIL_GRAB = 26;
        private const int COIL_CLAMP_LIDS = 32;
        private const int COIL_CLAMP_BASES = 33;
        private const int COIL_BASES_RIGHT_POSITIONER = 6;
        private const int COIL_CONV_WITH_SENSOR = 35;
        private const int COIL_CURVED_EXIT_L2 = 23;
        private const int COIL_CURVED_EXIT_B2 = 19;
        private const int COIL_BOX_EMITTER = 10;
        private const int COIL_NORMAL_PUSHER = 31;
        private const int COIL_NORMAL_ROLLER = 37;
        private const int COIL_LOADING_NORAML = 38;
        private const int COIL_CURVED_CONVC = 36;
        private const int COIL_SORT_CONVC = 39;
        private const int COIL_NORMAL_SORT = 41;

        // Internal PLC logic variables
        private bool basesAtEntryPrev = false;
        private bool basesAtExitPrev = false;
        private bool basesCenterBusy = false;
        private bool basesRawConvStop = false;
        private bool basesWaitingAtEntry = false;
        private bool basesCenterStartPrev = false;
        private bool basesEnterPrev = false;
        private bool basesClampActive = false;
        private bool basesClampedPrev = false;
        private bool basesExitConvStop = false;
        private bool moveXActive = false;
        private bool moveZActive = false;
        private bool basesRightPositionerActive = false;
        private bool basesReadyForAssembly = false;
        private bool risingClampActive = false;
        private bool basesClampWaiting = false;

        private bool lidsAtEntryPrev = false;
        private bool lidsAtExitPrev = false;
        private bool lidsCenterBusy = false;
        private bool lidsRawConvStop = false;
        private bool lidsWaitingAtEntry = false;
        private bool lidsCenterStartPrev = false;
        private bool lidsEnterPrev = false;
        private bool lidsClampActive = false;
        private bool lidsClampedPrev = false;
        private bool lidsExitConvStop = false;
        private bool prodsAtEntryPrev = false;
        private bool convWithSensorStop = false;
        private bool lidsReadyForAssembly = false;
        private bool grabLidsActive = false;
        private bool lidsClampWaiting = false;

        private bool prodAtPusherPrev = false;
        private bool productsPusher = false;
        private bool prodCounterPrev = false;
        private bool boxNeeded = false;
        private bool rollerActive = false;
        private bool loadingNormal = false;
        private bool normalroller = false;
        private bool prodCounterWasHigh = false;
        private bool rollerstop = false;
        private bool normalSortStop = false;

        private int lidsClampDelayTimer = 0;
        private int basesClampDelayTimer = 0;
        private int assemblyState = 0;
        private int stateTimer = 0;
        private int convWithSensorStopTimer = 0;  // 타이머 추가
        private const int CONV_RESTART_DELAY = 40; // 2초 = 40 * 50ms
        private int pusherTimer = 0;
        private const int PUSHER_ACTIVE_TIME = 20; // 1초 = 20 * 50ms
        private int productCount = 0;
        private int rollerTimer = 0;
        private const int ROLLER_ACTIVE_TIME = 100;
        private int convStopDelayTimer = 0;
        private const int CONV_STOP_DELAY = 40;
        private bool waitingToStopConv = false;
        private bool normalSensorPrev = false;

        public MainWindow()
        {
            InitializeComponent();
            InitializeModbusServer();
            InitializePlcTimer();
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

                case 0x03: // Read Holding Registers ← 추가!
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
            // Factory IO가 액추에이터 상태를 읽어감 (WPF가 제어하는 신호들)
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
            // Factory IO가 액추에이터 상태를 읽어감 (Discrete Inputs로)
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

            // ⭐ factoryCoils 데이터를 반환 (액추에이터 상태)
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
            bool allInputsOff = !factoryInputs[INPUT_BASES_AT_ENTRY] &&
                                !factoryInputs[INPUT_BASES_AT_EXIT] &&
                                !factoryInputs[INPUT_LIDS_AT_ENTRY] &&
                                !factoryInputs[INPUT_LIDS_AT_EXIT];

            if (allInputsOff && (basesAtEntryPrev || basesAtExitPrev || lidsAtEntryPrev || lidsAtExitPrev))
            {
                ResetAllOutputs();
            }

            // BASES LOGIC
            // 1. At Entry 감지 (Rising Edge) - Raw Conveyor 정지
            bool basesAtEntryRising = factoryInputs[INPUT_BASES_AT_ENTRY] && !basesAtEntryPrev;
            basesAtEntryPrev = factoryInputs[INPUT_BASES_AT_ENTRY];
            if (basesAtEntryRising)
            {
                basesRawConvStop = true;       // Raw Conveyor 정지
                basesWaitingAtEntry = true;    // Entry에서 대기 중
            }

            // 2. Center Start 신호 생성: Center 비어있고 Entry에 plate 있을 때
            bool basesCenterStart = basesCenterBusy || (!basesCenterBusy && basesWaitingAtEntry);

            // 3. Center Start Rising Edge → Center Busy SET
            bool basesCenterStartRising = basesCenterStart && !basesCenterStartPrev;
            basesCenterStartPrev = basesCenterStart;
            if (basesCenterStartRising)
            {
                basesCenterBusy = true;
                basesWaitingAtEntry = false;
            }

            // 4. At Exit 감지 (Rising Edge) - Center Busy RESET
            bool basesAtExitRising = factoryInputs[INPUT_BASES_AT_EXIT] && !basesAtExitPrev;
            basesAtExitPrev = factoryInputs[INPUT_BASES_AT_EXIT];
            if (basesAtExitRising)
            {
                basesCenterBusy = false;        // Center Busy RESET (로봇 동작 완료)
                basesRawConvStop = false;       // Raw Conveyor 재가동
            }

            // 5. Bases Enter 감지
            bool basesEnterRising = factoryInputs[INPUT_BASES_ENTER] && !basesEnterPrev;
            basesEnterPrev = factoryInputs[INPUT_BASES_ENTER];

            if (basesEnterRising)
            {
                basesClampDelayTimer = 0;       // 타이머 시작
                basesClampWaiting = true;       // 대기 상태 활성화
            }

            // 타이머 카운트 및 클램프 작동
            if (basesClampWaiting)
            {
                basesClampDelayTimer++;
                if (basesClampDelayTimer >= 10)
                {
                    basesClampActive = true;
                    basesClampWaiting = false;
                    basesClampDelayTimer = 0;
                }
            }

            // 6. Bases Clamped 감지 → Exit Conveyor 3 정지
            bool basesClampedRising = factoryInputs[INPUT_BASES_CLAMPED] && !basesClampedPrev;
            basesClampedPrev = factoryInputs[INPUT_BASES_CLAMPED];
            if (basesClampedRising)
            {
                basesExitConvStop = true;       // exit conveyor 3 정지
                basesReadyForAssembly = true;
            }

            // Outputs
            factoryCoils[COIL_BASES_CENTER_START] = basesCenterStart;
            factoryCoils[COIL_BASES_EMITTER] = !basesRawConvStop;
            factoryCoils[COIL_BASES_RAW_CONV] = !basesRawConvStop;
            factoryCoils[COIL_CLAMP_BASES] = basesClampActive;

            // Exit Conveyors: 무조건 동작
            factoryCoils[COIL_BASES_EXIT_CONV1] = true;
            factoryCoils[COIL_BASES_EXIT_CONV2] = true;
            factoryCoils[COIL_BASES_EXIT_CONV3] = !basesExitConvStop;
            factoryCoils[COIL_CURVED_EXIT_B] = true;
            factoryCoils[COIL_CURVED_EXIT_B2] = true;

            // LIDS LOGIC
            // 1. At Entry 감지 (Rising Edge) - Raw Conveyor 정지
            bool lidsAtEntryRising = factoryInputs[INPUT_LIDS_AT_ENTRY] && !lidsAtEntryPrev;
            lidsAtEntryPrev = factoryInputs[INPUT_LIDS_AT_ENTRY];
            if (lidsAtEntryRising)
            {
                lidsRawConvStop = true;         // Raw Conveyor 정지
                lidsWaitingAtEntry = true;      // Entry에서 대기 중
            }

            // 2. Center Start 신호 생성: Center 비어있고 Entry에 plate 있을 때
            bool lidsCenterStart = lidsCenterBusy || (!lidsCenterBusy && lidsWaitingAtEntry);

            // 3. Center Start Rising Edge → Center Busy SET
            bool lidsCenterStartRising = lidsCenterStart && !lidsCenterStartPrev;
            lidsCenterStartPrev = lidsCenterStart;
            if (lidsCenterStartRising)
            {
                lidsCenterBusy = true;          // Center Busy SET (로봇 동작 시작)
                lidsWaitingAtEntry = false;     // Entry 대기 해제
            }

            // 4. At Exit 감지 (Rising Edge) - Center Busy RESET
            bool lidsAtExitRising = factoryInputs[INPUT_LIDS_AT_EXIT] && !lidsAtExitPrev;
            lidsAtExitPrev = factoryInputs[INPUT_LIDS_AT_EXIT];
            if (lidsAtExitRising)
            {
                lidsCenterBusy = false;         // Center Busy RESET (로봇 동작 완료)
                lidsRawConvStop = false;        // Raw Conveyor 재가동
            }

            // 5. Lids Enter 감지
            bool lidsEnterRising = factoryInputs[INPUT_LIDS_ENTER] && !lidsEnterPrev;
            lidsEnterPrev = factoryInputs[INPUT_LIDS_ENTER];

            if (lidsEnterRising)
            {
                lidsClampDelayTimer = 0;        // 타이머 시작
                lidsClampWaiting = true;        // 대기 상태 활성화
            }

            // 타이머 카운트 및 클램프 작동
            if (lidsClampWaiting)
            {
                lidsClampDelayTimer++;
                if (lidsClampDelayTimer >= 10)
                {
                    lidsClampActive = true;
                    lidsClampWaiting = false;
                    lidsClampDelayTimer = 0;
                }
            }

            // 6. Lids Clamped 감지 → Exit Conveyor 3 정지
            bool lidsClampedRising = factoryInputs[INPUT_LIDS_CLAMPED] && !lidsClampedPrev;
            lidsClampedPrev = factoryInputs[INPUT_LIDS_CLAMPED];
            if (lidsClampedRising)
            {
                lidsExitConvStop = true;
                lidsReadyForAssembly = true;
            }

            // PICK & PLACE LOGIC
            // 1.픽앤플레이스 시작 조건: 둘 다 준비되었을 때만
            if (basesReadyForAssembly && lidsReadyForAssembly && assemblyState == 0)
            {
                assemblyState = 1;
                stateTimer = 0;
                basesReadyForAssembly = false;  // 플래그 초기화
                lidsReadyForAssembly = false;   // 플래그 초기화
            }

            // 2. 픽앤플레이스 동작
            switch (assemblyState)
            {
                case 0:
                    moveZActive = false;
                    moveXActive = false;
                    grabLidsActive = false;
                    break;

                case 1:  // Move Z Down
                    moveZActive = true;
                    stateTimer++;
                    if (stateTimer >= 5)
                    {
                        assemblyState = 2;
                        stateTimer = 0;
                    }
                    break;

                case 2:  // Grab Lids
                    grabLidsActive = true;
                    lidsClampActive = false;
                    stateTimer++;
                    if (stateTimer >= 5)
                    {
                        assemblyState = 3;
                        stateTimer = 0;
                    }
                    break;

                case 3:  // Move Z Up
                    moveZActive = false;
                    stateTimer++;
                    if (stateTimer >= 5)
                    {
                        assemblyState = 4;
                        stateTimer = 0;
                    }
                    break;

                case 4:  // Move X (Bases 방향으로 이동)
                    moveXActive = true;
                    stateTimer++;
                    if (stateTimer >= 10)
                    {
                        assemblyState = 5;
                        stateTimer = 0;
                    }
                    break;

                case 5:  // Move Z Down (Bases 위로 하강)
                    moveZActive = true;
                    stateTimer++;
                    if (stateTimer >= 5)
                    {
                        assemblyState = 6;
                        stateTimer = 0;
                    }
                    break;

                case 6:  // Grab 해제 (Lids를 Bases 위에 놓기)
                    grabLidsActive = false;
                    basesClampActive = false;
                    stateTimer++;
                    if (stateTimer >= 5)
                    {
                        assemblyState = 7;
                        stateTimer = 0;
                    }
                    break;

                case 7:  // Move Z Up
                    moveZActive = false;
                    stateTimer++;
                    if (stateTimer >= 5)
                    {
                        assemblyState = 8;
                        stateTimer = 0;
                    }
                    break;

                case 8:  // Right Positioner 활성화 및 컨베이어 재가동
                    basesRightPositionerActive = true;
                    lidsExitConvStop = false;
                    basesExitConvStop = false;
                    stateTimer++;
                    if (stateTimer >= 5)
                    {
                        assemblyState = 9;
                        stateTimer = 0;
                    }
                    break;

                case 9:  // Move X 복귀 (원위치)
                    moveXActive = false;
                    stateTimer++;
                    if (stateTimer >= 5)
                    {
                        assemblyState = 10;
                        stateTimer = 0;
                    }
                    break;

                case 10:  // 완료 및 초기화
                    basesRightPositionerActive = false;
                    risingClampActive = false;
                    assemblyState = 0;  // 초기 상태로 복귀
                    stateTimer = 0;
                    break;

                default:
                    assemblyState = 0;
                    break;
            }

            // YOLO MODEL LINKED LOGIC
            // 불량 검출
            bool prodsEnter = factoryInputs[INPUT_ERROR_DERECTED] && !prodsAtEntryPrev;
            prodsAtEntryPrev = factoryInputs[INPUT_ERROR_DERECTED];
            if (prodsEnter)
            {
                convWithSensorStop = true;
                convWithSensorStopTimer = 0;

                // 롤러가 동작 중이 아닐 때만 박스 요청
                if (!rollerActive)
                {
                    boxNeeded = true;  // 박스 필요 플래그
                }
            }

            // Conveyor 재시작 타이머
            if (convWithSensorStop)
            {
                convWithSensorStopTimer++;
                if (convWithSensorStopTimer >= CONV_RESTART_DELAY)
                {
                    convWithSensorStop = false;
                    convWithSensorStopTimer = 0;
                }
            }

            // 정상 제품 분류
            bool normalSensor = factoryInputs[INPUT_NORMAL_SENSOR];
            bool productPassedSensor = !normalSensor && normalSensorPrev;  // 센서 벗어남

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

            // 3. 정상 제품 카운트 (기존 코드 유지)
            bool prodCounterCurrent = factoryInputs[INPUT_PROD_COUNTER];

            if (prodCounterCurrent && !prodCounterPrev)
            {
                prodCounterWasHigh = true;
            }

            if (!prodCounterCurrent && prodCounterPrev && prodCounterWasHigh)
            {
                productCount++;
                LogMessage($"📦 박스에 제품 추가: {productCount}/3");
                prodCounterWasHigh = false;
            }
            prodCounterPrev = prodCounterCurrent;

            // 4. 박스에 3개 담기면 롤러 가동 (기존 코드 유지)
            if (productCount >= 3 && !rollerActive)
            {
                rollerActive = true;
                rollerTimer = 0;
                productCount = 0;
                boxNeeded = false;
            }

            if (rollerActive)
            {
                rollerTimer++;
                if (rollerTimer >= ROLLER_ACTIVE_TIME)
                {
                    rollerActive = false;
                    rollerTimer = 0;
                }
            }

            // Outputs
            factoryCoils[COIL_LIDS_CENTER_START] = lidsCenterStart;
            factoryCoils[COIL_LIDS_EMITTER] = !lidsRawConvStop;
            factoryCoils[COIL_LIDS_RAW_CONV] = !lidsRawConvStop;
            factoryCoils[COIL_CLAMP_LIDS] = lidsClampActive;
            factoryCoils[COIL_GRAB] = grabLidsActive;
            factoryCoils[COIL_MOVE_Z] = moveZActive;
            factoryCoils[COIL_MOVE_X] = moveXActive;
            factoryCoils[COIL_BASES_RIGHT_POSITIONER] = basesRightPositionerActive;
            factoryCoils[COIL_BOX_EMITTER] = boxNeeded && !rollerActive;
            factoryCoils[COIL_NORMAL_PUSHER] = productsPusher;

            // Exit Conveyors: 무조건 동작
            factoryCoils[COIL_LIDS_EXIT_CONV1] = true;
            factoryCoils[COIL_LIDS_EXIT_CONV2] = true;
            factoryCoils[COIL_LIDS_EXIT_CONV3] = !lidsExitConvStop;
            factoryCoils[COIL_CURVED_EXIT_L] = true;
            factoryCoils[COIL_CURVED_EXIT_L2] = true;
            factoryCoils[COIL_CONV_WITH_SENSOR] = !convWithSensorStop;
            factoryCoils[COIL_NORMAL_ROLLER] = rollerActive;
            factoryCoils[COIL_LOADING_NORAML] = true;
            factoryCoils[COIL_CURVED_CONVC] = true;
            factoryCoils[COIL_SORT_CONVC] = true;
            factoryCoils[COIL_NORMAL_SORT] = !normalSortStop;
        }

        private void ResetAllOutputs()
        {
            Array.Clear(factoryCoils, 0, factoryCoils.Length);

            basesAtEntryPrev = false;
            basesAtExitPrev = false;
            basesCenterStartPrev = false;
            basesCenterBusy = false;
            basesWaitingAtEntry = false;
            basesRawConvStop = false;
            basesEnterPrev = false;
            basesClampActive = false;
            basesClampedPrev = false;
            basesExitConvStop = false;
            basesRightPositionerActive = false;

            lidsAtEntryPrev = false;
            lidsAtExitPrev = false;
            lidsCenterStartPrev = false;
            lidsCenterBusy = false;
            lidsWaitingAtEntry = false;
            lidsRawConvStop = false;
            lidsEnterPrev = false;
            lidsClampActive = false;
            lidsClampedPrev = false;
            lidsExitConvStop = false;

            moveXActive = false;
            moveZActive = false;
            grabLidsActive = false;
            assemblyState = 0;
            stateTimer = 0;
            convWithSensorStop = false;
            productsPusher = false;
            rollerstop = false;
            productCount = 0;
            prodCounterPrev = false;
            convStopDelayTimer = 0;
            waitingToStopConv = false;
            pusherTimer = 0;

            normalSensorPrev = false;
            normalSortStop = false;
            productsPusher = false;
            pusherTimer = 0;
            prodCounterPrev = false;
            prodCounterWasHigh = false;
            productCount = 0;
            rollerActive = false;
            rollerTimer = 0;
            boxNeeded = false;
            convWithSensorStop = false;
            convWithSensorStopTimer = 0;
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
            base.OnClosed(e);
        }
    }
}