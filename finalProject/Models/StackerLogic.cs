using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace finalProject.Models
{
    public class StackerLogic
    {
        // Normal Stacker
        private int stackerState = 0;
        private int stackerTimer = 0;
        private bool stackerBusy = false;
        private ushort stackerTargetPosition = 1;
        private bool stackerMovingXPrev = false;
        private bool stackerMovingZPrev = false;
        private bool normalBoxEnterPrev = false;
        private bool normalBoxLoadingConv = false;

        // Error Stacker
        private int errorStackerState = 0;
        private int errorStackerTimer = 0;
        private bool errorStackerBusy = false;
        private ushort errorStackerTargetPosition = 1;
        private bool errorStackerMovingXPrev = false;
        private bool errorStackerMovingZPrev = false;
        private bool errorBoxEnterPrev = false;
        private bool errorBoxLoadingConv = false;

        public void ExecuteNormalStacker(bool[] inputs, bool[] coils, ushort[] registers)
        {
            // 박스 진입 감지
            bool normalBoxEnter = inputs[FactoryAddresses.INPUT_NORAML_BOX] && !normalBoxEnterPrev;
            normalBoxEnterPrev = inputs[FactoryAddresses.INPUT_NORAML_BOX];
            if (normalBoxEnter)
            {
                normalBoxLoadingConv = true;
            }

            // Stacker 작업 시작
            if (normalBoxLoadingConv && !stackerBusy && stackerState == 0)
            {
                stackerState = 1;
                stackerTimer = 0;
                stackerBusy = true;
            }

            // Stacker 상태 머신
            switch (stackerState)
            {
                case 0:  // 대기 상태
                    coils[FactoryAddresses.COIL_STACKER_RIGHT] = false;
                    coils[FactoryAddresses.COIL_STACKER_LIFT] = false;
                    coils[FactoryAddresses.COIL_STACKER_LEFT] = false;
                    break;

                case 1:  // 오른쪽으로 이동 (박스 들기 위함)
                    coils[FactoryAddresses.COIL_STACKER_RIGHT] = true;
                    stackerTimer++;
                    if (stackerTimer >= 60)
                    {
                        stackerState = 2;
                        stackerTimer = 0;
                    }
                    break;

                case 2:  // 박스 들어올리기 (Lift)
                    normalBoxLoadingConv = false;
                    coils[FactoryAddresses.COIL_STACKER_RIGHT] = false;
                    coils[FactoryAddresses.COIL_STACKER_LIFT] = true;
                    stackerTimer++;
                    if (stackerTimer >= 60)
                    {
                        stackerState = 3;
                        stackerTimer = 0;
                    }
                    break;

                case 3:  // X축 Target Position 설정
                    registers[FactoryAddresses.REGISTER_STACKER_TARGET_POS] = stackerTargetPosition;
                    stackerState = 4;
                    stackerTimer = 0;
                    break;

                case 4:  // X축 이동 완료 대기
                    {
                        bool stackerMovingX = inputs[FactoryAddresses.INPUT_STACKER_MOVING_X];
                        bool stackerMovingXComplete = !stackerMovingX && stackerMovingXPrev;
                        stackerMovingXPrev = stackerMovingX;

                        if (stackerMovingXComplete)
                        {
                            stackerState = 5;
                            stackerTimer = 0;
                        }

                        stackerTimer++;
                        if (stackerTimer >= 20)
                        {
                            stackerState = 5;
                            stackerTimer = 0;
                        }
                    }
                    break;

                case 5:  // Z축 이동 완료 대기
                    {
                        bool stackerMovingZ = inputs[FactoryAddresses.INPUT_STACKER_MOVING_Z];
                        bool stackerMovingZComplete = !stackerMovingZ && stackerMovingZPrev;
                        stackerMovingZPrev = stackerMovingZ;

                        if (stackerMovingZComplete)
                        {
                            stackerState = 6;
                            stackerTimer = 0;
                        }

                        stackerTimer++;
                        if (stackerTimer >= 20)
                        {
                            stackerState = 6;
                            stackerTimer = 0;
                        }
                    }
                    break;

                case 6:  // 물건 적재
                    coils[FactoryAddresses.COIL_STACKER_LEFT] = true;
                    stackerTimer++;
                    if (stackerTimer >= 40)
                    {
                        stackerState = 7;
                        stackerTimer = 0;
                    }
                    break;

                case 7:  // 박스 내려두기
                    coils[FactoryAddresses.COIL_STACKER_LEFT] = false;
                    coils[FactoryAddresses.COIL_STACKER_LIFT] = false;
                    stackerTimer++;
                    if (stackerTimer >= 40)
                    {
                        stackerState = 8;
                        stackerTimer = 0;
                    }
                    break;

                case 8:  // 좌표값 원위치 설정
                    registers[FactoryAddresses.REGISTER_STACKER_TARGET_POS] = 99;
                    stackerTimer++;
                    if (stackerTimer >= 5)
                    {
                        stackerState = 9;
                        stackerTimer = 0;
                    }
                    break;

                case 9:  // 원위치 이동 완료 대기
                    {
                        bool stackerMovingX = inputs[FactoryAddresses.INPUT_STACKER_MOVING_X];
                        bool stackerMovingXComplete = !stackerMovingX && stackerMovingXPrev;
                        stackerMovingXPrev = stackerMovingX;

                        if (stackerMovingXComplete)
                        {
                            stackerTargetPosition++;
                            if (stackerTargetPosition > 10)
                            {
                                stackerTargetPosition = 1;
                            }

                            stackerState = 0;
                            stackerTimer = 0;
                            stackerBusy = false;
                        }

                        stackerTimer++;
                        if (stackerTimer >= 60)
                        {
                            stackerTargetPosition++;
                            if (stackerTargetPosition > 10)
                            {
                                stackerTargetPosition = 1;
                            }
                            stackerState = 0;
                            stackerTimer = 0;
                            stackerBusy = false;
                        }
                    }
                    break;

                default:
                    stackerState = 0;
                    break;
            }

            // Loading Conveyor 제어
            coils[FactoryAddresses.COIL_LOADING_NORAML] = !normalBoxLoadingConv;
        }

        public void ExecuteErrorStacker(bool[] inputs, bool[] coils, ushort[] registers)
        {
            // 박스 진입 감지
            bool errorBoxEnter = inputs[FactoryAddresses.INPUT_ERROR_BOX] && !errorBoxEnterPrev;
            errorBoxEnterPrev = inputs[FactoryAddresses.INPUT_ERROR_BOX];
            if (errorBoxEnter)
            {
                errorBoxLoadingConv = true;
            }

            // Stacker 작업 시작
            if (errorBoxLoadingConv && !errorStackerBusy && errorStackerState == 0)
            {
                errorStackerState = 1;
                errorStackerTimer = 0;
                errorStackerBusy = true;
            }

            // Stacker 상태 머신
            switch (errorStackerState)
            {
                case 0:  // 대기 상태
                    coils[FactoryAddresses.COIL_ERROR_STACKER_RIGHT] = false;
                    coils[FactoryAddresses.COIL_ERROR_STACKER_LIFT] = false;
                    coils[FactoryAddresses.COIL_ERROR_STACKER_LEFT] = false;
                    break;

                case 1:  // 오른쪽으로 이동 (박스 들기 위함)
                    coils[FactoryAddresses.COIL_ERROR_STACKER_RIGHT] = true;
                    errorStackerTimer++;
                    if (errorStackerTimer >= 40)
                    {
                        errorStackerState = 2;
                        errorStackerTimer = 0;
                    }
                    break;

                case 2:  // 박스 들어올리기 (Lift)
                    errorBoxLoadingConv = false;
                    coils[FactoryAddresses.COIL_ERROR_STACKER_RIGHT] = false;
                    coils[FactoryAddresses.COIL_ERROR_STACKER_LIFT] = true;
                    errorStackerTimer++;
                    if (errorStackerTimer >= 40)
                    {
                        errorStackerState = 3;
                        errorStackerTimer = 0;
                    }
                    break;

                case 3:  // X축 Target Position 설정
                    registers[FactoryAddresses.REGISTER_ERROR_STACKER_TARGET_POS] = errorStackerTargetPosition;
                    errorStackerState = 4;
                    errorStackerTimer = 0;
                    break;

                case 4:  // X축 이동 완료 대기
                    {
                        bool stackerMovingX = inputs[FactoryAddresses.INPUT_ERROR_STACKER_MOVING_X];
                        bool stackerMovingXComplete = !stackerMovingX && errorStackerMovingXPrev;
                        errorStackerMovingXPrev = stackerMovingX;

                        if (stackerMovingXComplete)
                        {
                            errorStackerState = 5;
                            errorStackerTimer = 0;
                        }

                        errorStackerTimer++;
                        if (errorStackerTimer >= 20)
                        {
                            errorStackerState = 5;
                            errorStackerTimer = 0;
                        }
                    }
                    break;

                case 5:  // Z축 이동 완료 대기
                    {
                        bool stackerMovingZ = inputs[FactoryAddresses.INPUT_ERROR_STACKER_MOVING_Z];
                        bool stackerMovingZComplete = !stackerMovingZ && errorStackerMovingZPrev;
                        errorStackerMovingZPrev = stackerMovingZ;

                        if (stackerMovingZComplete)
                        {
                            errorStackerState = 6;
                            errorStackerTimer = 0;
                        }

                        errorStackerTimer++;
                        if (errorStackerTimer >= 20)
                        {
                            errorStackerState = 6;
                            errorStackerTimer = 0;
                        }
                    }
                    break;

                case 6:  // 물건 적재
                    coils[FactoryAddresses.COIL_ERROR_STACKER_LEFT] = true;
                    errorStackerTimer++;
                    if (errorStackerTimer >= 40)
                    {
                        errorStackerState = 7;
                        errorStackerTimer = 0;
                    }
                    break;

                case 7:  // 박스 내려두기
                    coils[FactoryAddresses.COIL_ERROR_STACKER_LEFT] = false;
                    coils[FactoryAddresses.COIL_ERROR_STACKER_LIFT] = false;
                    errorStackerTimer++;
                    if (errorStackerTimer >= 40)
                    {
                        errorStackerState = 8;
                        errorStackerTimer = 0;
                    }
                    break;

                case 8:  // 좌표값 원위치 설정
                    registers[FactoryAddresses.REGISTER_ERROR_STACKER_TARGET_POS] = 99;
                    errorStackerTimer++;
                    if (errorStackerTimer >= 5)
                    {
                        errorStackerState = 9;
                        errorStackerTimer = 0;
                    }
                    break;

                case 9:  // 원위치 이동 완료 대기
                    {
                        bool stackerMovingX = inputs[FactoryAddresses.INPUT_ERROR_STACKER_MOVING_X];
                        bool stackerMovingXComplete = !stackerMovingX && errorStackerMovingXPrev;
                        errorStackerMovingXPrev = stackerMovingX;

                        if (stackerMovingXComplete)
                        {
                            errorStackerTargetPosition++;
                            if (errorStackerTargetPosition > 10)
                            {
                                errorStackerTargetPosition = 1;
                            }

                            errorStackerState = 0;
                            errorStackerTimer = 0;
                            errorStackerBusy = false;
                        }

                        errorStackerTimer++;
                        if (errorStackerTimer >= 60)
                        {
                            errorStackerTargetPosition++;
                            if (errorStackerTargetPosition > 10)
                            {
                                errorStackerTargetPosition = 1;
                            }
                            errorStackerState = 0;
                            errorStackerTimer = 0;
                            errorStackerBusy = false;
                        }
                    }
                    break;

                default:
                    errorStackerState = 0;
                    break;
            }

            // Loading Conveyor 제어
            coils[FactoryAddresses.COIL_LOADING_ERROR] = !errorBoxLoadingConv;
        }

        public void Reset()
        {
            stackerState = 0;
            stackerTimer = 0;
            stackerBusy = false;
            stackerMovingXPrev = false;
            stackerMovingZPrev = false;
            normalBoxEnterPrev = false;
            normalBoxLoadingConv = false;

            errorStackerState = 0;
            errorStackerTimer = 0;
            errorStackerBusy = false;
            errorStackerMovingXPrev = false;
            errorStackerMovingZPrev = false;
            errorBoxEnterPrev = false;
            errorBoxLoadingConv = false;
        }
    }
}
