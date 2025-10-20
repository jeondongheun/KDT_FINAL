namespace finalProject.Models
{
    public static class FactoryAddresses
    {
        // INPUT ADDRESSES
        // Factory IO가 보내는 신호 (센서)
        // Lids Center
        public const int INPUT_LIDS_CENTER_BUSY = 0;
        public const int INPUT_LIDS_CENTER_ERROR = 1;
        public const int INPUT_LIDS_AT_ENTRY = 2;
        public const int INPUT_LIDS_AT_EXIT = 6;
        public const int INPUT_LIDS_CLAMPED = 20;
        public const int INPUT_LIDS_ENTER = 28;

        // Bases Center
        public const int INPUT_BASES_CENTER_BUSY = 3;
        public const int INPUT_BASES_CENTER_ERROR = 4;
        public const int INPUT_BASES_AT_ENTRY = 5;
        public const int INPUT_BASES_AT_EXIT = 7;
        public const int INPUT_BASES_CLAMPED = 18;
        public const int INPUT_BASES_ENTER = 27;

        // Product Detection
        public const int INPUT_ITEM_DETECTED = 14;
        public const int INPUT_ERROR_DERECTED = 29;
        public const int INPUT_PROD_COUNTER = 34;
        public const int INPUT_ERROR_COUNTER = 44;

        // Sensors
        public const int INPUT_NORMAL_SENSOR = 40;
        public const int INPUT_ERROR_SORT_SENSOR = 42;
        public const int INPUT_ERROR_CATE_SENSOR = 43;

        // Box Detection
        public const int INPUT_NORAML_BOX = 47;
        public const int INPUT_ERROR_BOX = 48;

        // Stacker Status
        public const int INPUT_STACKER_MOVING_X = 56;
        public const int INPUT_STACKER_MOVING_Z = 57;
        public const int INPUT_ERROR_STACKER_MOVING_X = 54;
        public const int INPUT_ERROR_STACKER_MOVING_Z = 55;

        // COIL ADDRESSES
        // Factory IO가 읽는 신호 (액추에이터)
        // Center Control
        public const int COIL_LIDS_CENTER_START = 8;
        public const int COIL_BASES_CENTER_START = 9;

        // Emitters
        public const int COIL_LIDS_EMITTER = 4;
        public const int COIL_BASES_EMITTER = 11;
        public const int COIL_BOX_EMITTER = 10;
        public const int COIL_ERROR_BOX_EMITTER = 18;

        // Lids Conveyors
        public const int COIL_LIDS_EXIT_CONV1 = 5;
        public const int COIL_LIDS_EXIT_CONV2 = 21;
        public const int COIL_LIDS_EXIT_CONV3 = 13;
        public const int COIL_LIDS_RAW_CONV = 16;

        // Bases Conveyors
        public const int COIL_BASES_EXIT_CONV1 = 12;
        public const int COIL_BASES_EXIT_CONV2 = 15;
        public const int COIL_BASES_EXIT_CONV3 = 22;
        public const int COIL_BASES_RAW_CONV = 17;

        // Curved Conveyors
        public const int COIL_CURVED_EXIT_L = 14;
        public const int COIL_CURVED_EXIT_L2 = 23;
        public const int COIL_CURVED_EXIT_B = 20;
        public const int COIL_CURVED_EXIT_B2 = 19;
        public const int COIL_CURVED_CONVC = 36;

        // Pick & Place
        public const int COIL_MOVE_Z = 24;
        public const int COIL_MOVE_X = 25;
        public const int COIL_GRAB = 26;
        public const int COIL_CLAMP_LIDS = 32;
        public const int COIL_CLAMP_BASES = 33;
        public const int COIL_BASES_RIGHT_POSITIONER = 6;

        // Sorting System
        public const int COIL_CONV_WITH_SENSOR = 35;
        public const int COIL_SORT_CONVC = 39;
        public const int COIL_DEL_PCB = 30;
        public const int COIL_NORMAL_SORT = 41;
        public const int COIL_ERROR_LIGHT = 55;
        public const int COIL_NORMAL_LIGHT = 56;
        public const int COIL_DEFECTED_LIGHT = 57;
        public const int COIL_DISPOSED_LIGTH = 58;
        public const int COIL_REPROCESSING = 59;

        // Pushers
        public const int COIL_NORMAL_PUSHER = 31;
        public const int COIL_ERROR_PUSHER = 44;

        // Rollers
        public const int COIL_NORMAL_ROLLER = 37;
        public const int COIL_ERROR_ROLLER = 45;

        // Loading Conveyors
        public const int COIL_LOADING_NORAML = 38;
        public const int COIL_LOADING_ERROR = 46;

        // Normal Stacker
        public const int COIL_STACKER_RIGHT = 53;
        public const int COIL_STACKER_LIFT = 52;
        public const int COIL_STACKER_LEFT = 54;

        // Error Stacker
        public const int COIL_ERROR_STACKER_RIGHT = 51;
        public const int COIL_ERROR_STACKER_LIFT = 50;
        public const int COIL_ERROR_STACKER_LEFT = 49;

        // REGISTER ADDRESSES 
        public const int REGISTER_ERROR_STACKER_TARGET_POS = 0;
        public const int REGISTER_STACKER_TARGET_POS = 1;
    }
}
