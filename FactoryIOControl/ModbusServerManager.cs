using System;
using EasyModbus;

namespace FactoryIOControl
{
    /// <summary>
    /// Modbus TCP Server 관리 클래스
    /// </summary>
    public class ModbusServerManager
    {
        private ModbusServer? modbusServer;
        private const int MODBUS_PORT = 502;

        public bool IsRunning { get; private set; }

        // Modbus 데이터 배열 (EasyModbus는 내부적으로 관리)
        // Coils: 0-65535
        // Holding Registers: 0-65535

        /// <summary>
        /// Modbus 서버 시작
        /// </summary>
        public bool Start()
        {
            try
            {
                modbusServer = new ModbusServer();
                modbusServer.Port = MODBUS_PORT;
                modbusServer.Listen();
                IsRunning = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Modbus Server Start Failed: {ex.Message}");
                IsRunning = false;
                return false;
            }
        }

        /// <summary>
        /// Modbus 서버 정지
        /// </summary>
        public void Stop()
        {
            if (modbusServer != null)
            {
                modbusServer.StopListening();
                IsRunning = false;
            }
        }

        /// <summary>
        /// Coil 읽기 (Bit)
        /// </summary>
        public bool GetCoil(int address)
        {
            if (modbusServer == null || address < 0 || address >= 65536)
                return false;

            return modbusServer.coils[address + 1];
        }

        /// <summary>
        /// Coil 쓰기 (Bit)
        /// </summary>
        public void SetCoil(int address, bool value)
        {
            if (modbusServer == null || address < 0 || address >= 65536)
                return;

            modbusServer.coils[address + 1] = value;
        }

        /// <summary>
        /// Holding Register 읽기 (16-bit Integer)
        /// </summary>
        public short GetRegister(int address)
        {
            if (modbusServer == null || address < 0 || address >= 65536)
                return 0;

            return modbusServer.holdingRegisters[address + 1];
        }

        /// <summary>
        /// Holding Register 쓰기 (16-bit Integer)
        /// </summary>
        public void SetRegister(int address, short value)
        {
            if (modbusServer == null || address < 0 || address >= 65536)
                return;

            modbusServer.holdingRegisters[address + 1] = value;
        }

        /// <summary>
        /// 모든 Coil 리셋
        /// </summary>
        public void ResetAllCoils()
        {
            if (modbusServer == null) return;

            for (int i = 0; i < 100; i++)
            {
                modbusServer.coils[i] = false;
            }
        }

        /// <summary>
        /// 모든 Register 리셋
        /// </summary>
        public void ResetAllRegisters()
        {
            if (modbusServer == null) return;

            for (int i = 0; i < 100; i++)
            {
                modbusServer.holdingRegisters[i] = 0;
            }
        }
    }
}