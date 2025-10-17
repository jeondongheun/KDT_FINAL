namespace finalProject.Models
{
    public class PickAndPlaceLogic
    {
        private bool moveXActive = false;
        private bool moveZActive = false;
        private bool grabLidsActive = false;
        private bool basesRightPositionerActive = false;
        private int assemblyState = 0;
        private int stateTimer = 0;

        public void ExecuteAssembly(
            bool basesReady,
            bool lidsReady,
            bool[] coils,
            BasesLidsLogic basesLidsLogic)
        {
            // 1. 픽앤플레이스 시작 조건: 둘 다 준비되었을 때만
            if (basesReady && lidsReady && assemblyState == 0)
            {
                assemblyState = 1;
                stateTimer = 0;
                basesLidsLogic.ResetBasesFlags();
                basesLidsLogic.ResetLidsFlags();
            }

            // 2. 픽앤플레이스 동작
            switch (assemblyState)
            {
                case 0:  // 대기 상태
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
                    stateTimer++;
                    if (stateTimer >= 7)
                    {
                        basesLidsLogic.DeactivateLidsClamp();
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
                    if (stateTimer >= 12)
                    {
                        assemblyState = 5;
                        stateTimer = 0;
                    }
                    break;

                case 5:  // Move Z Down (Bases 위로 하강)
                    moveZActive = true;
                    stateTimer++;
                    if (stateTimer >= 7)
                    {
                        assemblyState = 6;
                        stateTimer = 0;
                    }
                    break;

                case 6:  // Grab 해제 (Lids를 Bases 위에 놓기)
                    grabLidsActive = false;
                    stateTimer++;
                    if (stateTimer >= 5)
                    {
                        basesLidsLogic.DeactivateBasesClamp();
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

                case 8:  // Move X 복귀 (원위치)
                    moveXActive = false;
                    stateTimer++;
                    if (stateTimer >= 10)
                    {
                        assemblyState = 9;
                        stateTimer = 0;
                    }
                    break;

                case 9:  // Right Positioner 활성화 및 컨베이어 재가동
                    basesRightPositionerActive = true;
                    basesLidsLogic.StopLidsExitConv(false);
                    basesLidsLogic.StopBasesExitConv(false);
                    stateTimer++;
                    if (stateTimer >= 10)
                    {
                        assemblyState = 10;
                        stateTimer = 0;
                    }
                    break;

                case 10:  // 완료 및 초기화
                    basesRightPositionerActive = false;
                    assemblyState = 0;
                    stateTimer = 0;
                    break;

                default:
                    assemblyState = 0;
                    break;
            }

            // Outputs 설정
            coils[FactoryAddresses.COIL_GRAB] = grabLidsActive;
            coils[FactoryAddresses.COIL_MOVE_Z] = moveZActive;
            coils[FactoryAddresses.COIL_MOVE_X] = moveXActive;
            coils[FactoryAddresses.COIL_BASES_RIGHT_POSITIONER] = basesRightPositionerActive;
        }

        public void Reset()
        {
            moveXActive = false;
            moveZActive = false;
            grabLidsActive = false;
            basesRightPositionerActive = false;
            assemblyState = 0;
            stateTimer = 0;
        }
    }
}
