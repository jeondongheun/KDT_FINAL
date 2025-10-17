namespace finalProject.Models
{
    public class BasesLidsLogic
    {
        // Bases 상태 변수
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
        private bool basesReadyForAssembly = false;
        private bool basesClampWaiting = false;
        private int basesClampDelayTimer = 0;

        // Lids 상태 변수
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
        private bool lidsReadyForAssembly = false;
        private bool lidsClampWaiting = false;
        private int lidsClampDelayTimer = 0;

        public bool BasesReadyForAssembly => basesReadyForAssembly;
        public bool LidsReadyForAssembly => lidsReadyForAssembly;
        public bool BasesExitConvStop => basesExitConvStop;
        public bool LidsExitConvStop => lidsExitConvStop;

        public void ExecuteBasesLogic(bool[] inputs, bool[] coils)
        {
            // 1. At Entry 감지 (Rising Edge) - Raw Conveyor 정지
            bool basesAtEntryRising = inputs[FactoryAddresses.INPUT_BASES_AT_ENTRY] && !basesAtEntryPrev;
            basesAtEntryPrev = inputs[FactoryAddresses.INPUT_BASES_AT_ENTRY];
            if (basesAtEntryRising)
            {
                basesRawConvStop = true;
                basesWaitingAtEntry = true;
            }

            // 2. Center Start 신호 생성
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
            bool basesAtExitRising = inputs[FactoryAddresses.INPUT_BASES_AT_EXIT] && !basesAtExitPrev;
            basesAtExitPrev = inputs[FactoryAddresses.INPUT_BASES_AT_EXIT];
            if (basesAtExitRising)
            {
                basesCenterBusy = false;
                basesRawConvStop = false;
            }

            // 5. Bases Enter 감지
            bool basesEnterRising = inputs[FactoryAddresses.INPUT_BASES_ENTER] && !basesEnterPrev;
            basesEnterPrev = inputs[FactoryAddresses.INPUT_BASES_ENTER];
            if (basesEnterRising)
            {
                basesClampDelayTimer = 0;
                basesClampWaiting = true;
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
            bool basesClampedRising = inputs[FactoryAddresses.INPUT_BASES_CLAMPED] && !basesClampedPrev;
            basesClampedPrev = inputs[FactoryAddresses.INPUT_BASES_CLAMPED];
            if (basesClampedRising)
            {
                basesExitConvStop = true;
                basesReadyForAssembly = true;
            }

            // Outputs 설정
            coils[FactoryAddresses.COIL_BASES_CENTER_START] = basesCenterStart;
            coils[FactoryAddresses.COIL_BASES_EMITTER] = !basesRawConvStop;
            coils[FactoryAddresses.COIL_BASES_RAW_CONV] = !basesRawConvStop;
            coils[FactoryAddresses.COIL_CLAMP_BASES] = basesClampActive;
            coils[FactoryAddresses.COIL_BASES_EXIT_CONV1] = true;
            coils[FactoryAddresses.COIL_BASES_EXIT_CONV2] = true;
            coils[FactoryAddresses.COIL_BASES_EXIT_CONV3] = !basesExitConvStop;
            coils[FactoryAddresses.COIL_CURVED_EXIT_B] = true;
            coils[FactoryAddresses.COIL_CURVED_EXIT_B2] = true;
        }

        public void ExecuteLidsLogic(bool[] inputs, bool[] coils)
        {
            // 1. At Entry 감지 (Rising Edge) - Raw Conveyor 정지
            bool lidsAtEntryRising = inputs[FactoryAddresses.INPUT_LIDS_AT_ENTRY] && !lidsAtEntryPrev;
            lidsAtEntryPrev = inputs[FactoryAddresses.INPUT_LIDS_AT_ENTRY];
            if (lidsAtEntryRising)
            {
                lidsRawConvStop = true;
                lidsWaitingAtEntry = true;
            }

            // 2. Center Start 신호 생성
            bool lidsCenterStart = lidsCenterBusy || (!lidsCenterBusy && lidsWaitingAtEntry);

            // 3. Center Start Rising Edge → Center Busy SET
            bool lidsCenterStartRising = lidsCenterStart && !lidsCenterStartPrev;
            lidsCenterStartPrev = lidsCenterStart;
            if (lidsCenterStartRising)
            {
                lidsCenterBusy = true;
                lidsWaitingAtEntry = false;
            }

            // 4. At Exit 감지 (Rising Edge) - Center Busy RESET
            bool lidsAtExitRising = inputs[FactoryAddresses.INPUT_LIDS_AT_EXIT] && !lidsAtExitPrev;
            lidsAtExitPrev = inputs[FactoryAddresses.INPUT_LIDS_AT_EXIT];
            if (lidsAtExitRising)
            {
                lidsCenterBusy = false;
                lidsRawConvStop = false;
            }

            // 5. Lids Enter 감지
            bool lidsEnterRising = inputs[FactoryAddresses.INPUT_LIDS_ENTER] && !lidsEnterPrev;
            lidsEnterPrev = inputs[FactoryAddresses.INPUT_LIDS_ENTER];
            if (lidsEnterRising)
            {
                lidsClampDelayTimer = 0;
                lidsClampWaiting = true;
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
            bool lidsClampedRising = inputs[FactoryAddresses.INPUT_LIDS_CLAMPED] && !lidsClampedPrev;
            lidsClampedPrev = inputs[FactoryAddresses.INPUT_LIDS_CLAMPED];
            if (lidsClampedRising)
            {
                lidsExitConvStop = true;
                lidsReadyForAssembly = true;
            }

            // Outputs 설정
            coils[FactoryAddresses.COIL_LIDS_CENTER_START] = lidsCenterStart;
            coils[FactoryAddresses.COIL_LIDS_EMITTER] = !lidsRawConvStop;
            coils[FactoryAddresses.COIL_LIDS_RAW_CONV] = !lidsRawConvStop;
            coils[FactoryAddresses.COIL_CLAMP_LIDS] = lidsClampActive;
            coils[FactoryAddresses.COIL_LIDS_EXIT_CONV1] = true;
            coils[FactoryAddresses.COIL_LIDS_EXIT_CONV2] = true;
            coils[FactoryAddresses.COIL_LIDS_EXIT_CONV3] = !lidsExitConvStop;
            coils[FactoryAddresses.COIL_CURVED_EXIT_L] = true;
            coils[FactoryAddresses.COIL_CURVED_EXIT_L2] = true;
        }

        public void ResetBasesFlags()
        {
            basesReadyForAssembly = false;
        }

        public void ResetLidsFlags()
        {
            lidsReadyForAssembly = false;
        }

        public void StopBasesExitConv(bool stop)
        {
            basesExitConvStop = stop;
        }

        public void StopLidsExitConv(bool stop)
        {
            lidsExitConvStop = stop;
        }

        public void DeactivateBasesClamp()
        {
            basesClampActive = false;
        }

        public void DeactivateLidsClamp()
        {
            lidsClampActive = false;
        }

        public void Reset()
        {
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
            basesReadyForAssembly = false;
            basesClampWaiting = false;
            basesClampDelayTimer = 0;

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
            lidsReadyForAssembly = false;
            lidsClampWaiting = false;
            lidsClampDelayTimer = 0;
        }
    }
}
