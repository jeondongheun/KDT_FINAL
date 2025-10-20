using System;
using System.Threading;
using XGCommLib; // XGCommLib.dll의 기본 클래스를 직접 사용합니다.

namespace finalProject.Models
{
    /// <summary>
    /// LS ELECTRIC PLC와의 통신을 관리하는 클래스 (WpfApp1 예제와 동일한 직접 제어 방식)
    /// </summary>
    public class PLCManager : IDisposable
    {
        // === PLC 통신 상태 ===
        private readonly CommObjectFactory20 _factory = new CommObjectFactory20();
        private readonly string _endpoint;
        private readonly object _lock = new object();

        // PLC 메모리 비트 인덱스
        public const int BIT_CONVEYOR = 0;     // %MX0 - 컨베이어 가동
        public const int BIT_NORMAL = 1;       // %MX1 - 정상 제품
        public const int BIT_ERROR = 2;        // %MX2 - 불량 제품
        private const char DEVICE_TYPE = 'M';  // M 디바이스

        // 이벤트
        public event Action<string> OnLogMessage;
        public event Action OnConnected;
        public event Action OnDisconnected;

        public bool IsConnected { get; private set; }

        public PLCManager(string ip = "192.168.0.200", long port = 2004)
        {
            _endpoint = $"{ip}:{port}";
            IsConnected = false;
        }

        /// <summary>
        /// PLC에 연결을 시도하고 확인합니다.
        /// </summary>
        public bool Connect()
        {
            try
            {
                // WpfApp1 예제와 같이, 통신 전에 매번 새 드라이버로 연결을 확인하는 방식
                using (var tempDriver = new FreshDriver(_factory, _endpoint))
                {
                    if (tempDriver.IsConnected)
                    {
                        IsConnected = true;
                        Log($"✓ PLC 연결 확인 성공 ({_endpoint})");
                        OnConnected?.Invoke();
                        // 연결 성공 후 모든 비트 초기화
                        ResetAllSignals();
                        return true;
                    }
                    else
                    {
                        IsConnected = false;
                        Log($"✗ PLC 연결 실패. PLC 전원 및 네트워크 연결을 확인하세요.");
                        OnDisconnected?.Invoke();
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                IsConnected = false;
                // "클래스가 등록되지 않았습니다" 오류는 보통 여기서 발생합니다.
                Log($"✗ PLC 연결 오류: {ex.Message}");
                Log("   (오류 원인: 1. 프로젝트 플랫폼 타겟이 x86이 아닌 경우. 2. XGCommLib.dll이 등록되지 않은 경우)");
                OnDisconnected?.Invoke();
                return false;
            }
        }

        /// <summary>
        /// PLC와의 연결을 끊습니다. (실제 연결은 매번 새로 하므로 상태만 변경)
        /// </summary>
        public void Disconnect()
        {
            if (IsConnected)
            {
                ResetAllSignals();
            }
            IsConnected = false;
            Log("PLC 연결 상태 해제됨");
            OnDisconnected?.Invoke();
        }

        /// <summary>
        /// PLC에 단일 Bit를 씁니다. (WpfApp1의 Byte Shadowing 방식)
        /// </summary>
        private bool WriteBitWithByteShadowing(int bitIndex, bool value)
        {
            if (!IsConnected)
            {
                Log($"⚠ PLC 미연결 - 쓰기 실패 (MX{bitIndex})");
                return false;
            }

            try
            {
                using (var driver = new FreshDriver(_factory, _endpoint))
                {
                    if (!driver.IsConnected)
                    {
                        Log("⚠ 쓰기 작업 중 PLC 연결 실패");
                        IsConnected = false;
                        OnDisconnected?.Invoke();
                        return false;
                    }

                    // 1. 현재 %MB0 (MX0~MX7) 바이트 값을 읽어옵니다.
                    byte[] currentByteValue = new byte[1];
                    if (!driver.ReadByte(DEVICE_TYPE, 0, currentByteValue))
                    {
                        Log("⚠ PLC 현재 상태 읽기 실패");
                        return false;
                    }

                    // 2. 읽어온 값에서 원하는 비트만 수정합니다.
                    byte newByteValue;
                    if (value)
                    {
                        newByteValue = (byte)(currentByteValue[0] | (1 << bitIndex));  // 비트 켜기
                    }
                    else
                    {
                        newByteValue = (byte)(currentByteValue[0] & ~(1 << bitIndex)); // 비트 끄기
                    }

                    // 3. 수정된 바이트 값을 PLC에 씁니다.
                    if (!driver.WriteByte(DEVICE_TYPE, 0, newByteValue))
                    {
                        Log($"⚠ PLC 쓰기 실패 (%MX{bitIndex})");
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"⚠ PLC 쓰기 오류 (MX{bitIndex}): {ex.Message}");
                return false;
            }
        }

        // Keep-Alive는 매번 새로 연결하므로 필요 없습니다.
        public void UpdateKeepAlive() { }

        // ========== 공개 메서드 (Factory IO 로직에서 호출) ==========

        public bool SetConveyorRunning(bool isRunning)
        {
            bool success = WriteBitWithByteShadowing(BIT_CONVEYOR, isRunning);
            if (success)
            {
                Log($"🔧 컨베이어: {(isRunning ? "가동" : "정지")}");
            }
            return success;
        }

        public bool SendInspectionResult(bool isNormal)
        {
            // 먼저 모든 검사 비트를 끈 상태로 만듭니다.
            if (!WriteBitWithByteShadowing(BIT_NORMAL, false) || !WriteBitWithByteShadowing(BIT_ERROR, false))
            {
                Log("⚠ 검사 신호 초기화 실패");
                return false;
            }

            Thread.Sleep(50); // PLC 스캔 시간 확보

            // 결과에 맞는 비트를 켭니다.
            int targetBit = isNormal ? BIT_NORMAL : BIT_ERROR;
            if (!WriteBitWithByteShadowing(targetBit, true))
            {
                Log("⚠ 검사 결과 신호 전송 실패");
                return false;
            }

            string logMsg = isNormal ? "✅ 정상 제품 신호 전송 (%MX1 ON)" : "❌ 불량 제품 신호 전송 (%MX2 ON)";
            Log(logMsg);

            // 1초 후 자동으로 끄는 작업을 백그라운드에서 수행
            System.Threading.Tasks.Task.Run(() =>
            {
                Thread.Sleep(1000);
                WriteBitWithByteShadowing(targetBit, false);
                Log("🔄 검사 신호 자동 OFF");
            });

            return true;
        }

        public bool ResetAllSignals()
        {
            Log("🔄 PLC 모든 신호 초기화 시도...");
            // 모든 비트를 0으로 설정한 바이트(0x00)를 한번에 씁니다.
            try
            {
                using (var driver = new FreshDriver(_factory, _endpoint))
                {
                    if (!driver.IsConnected) return false;
                    if (driver.WriteByte(DEVICE_TYPE, 0, 0x00))
                    {
                        Log("✅ PLC 모든 신호 초기화 완료.");
                        return true;
                    }
                    else
                    {
                        Log("⚠ PLC 신호 초기화 실패.");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"⚠ 신호 초기화 오류: {ex.Message}");
                return false;
            }
        }

        private void Log(string message)
        {
            OnLogMessage?.Invoke(message);
        }

        public void Dispose()
        {
            if (IsConnected)
            {
                ResetAllSignals();
            }
        }

        // WpfApp1의 WithFreshDriver와 유사한 역할을 하는 내부 클래스
        private class FreshDriver : IDisposable
        {
            private CommObject20 _driver;
            private CommObjectFactory20 _factory;
            public bool IsConnected { get; private set; }

            public FreshDriver(CommObjectFactory20 factory, string endpoint)
            {
                _factory = factory;
                _driver = _factory.GetMLDPCommObject20(endpoint);
                if (_driver.Connect("") == 1)
                {
                    IsConnected = true;
                }
            }

            public bool ReadByte(char deviceType, int offset, byte[] buffer)
            {
                var device = _factory.CreateDevice();
                device.ucDataType = (byte)'B';
                device.ucDeviceType = (byte)deviceType;
                device.lOffset = offset;
                device.lSize = 1;

                _driver.RemoveAll();
                _driver.AddDeviceInfo(device);

                return _driver.ReadRandomDevice(buffer) == 1;
            }

            public bool WriteByte(char deviceType, int offset, byte value)
            {
                var device = _factory.CreateDevice();
                device.ucDataType = (byte)'B';
                device.ucDeviceType = (byte)deviceType;
                device.lOffset = offset;
                device.lSize = 1;

                _driver.RemoveAll();
                _driver.AddDeviceInfo(device);

                return _driver.WriteRandomDevice(new byte[] { value }) == 1;
            }

            public void Dispose()
            {
                if (_driver != null)
                {
                    try { _driver.Disconnect(); } catch { }
                    _driver = null;
                }
            }
        }
    }
}

