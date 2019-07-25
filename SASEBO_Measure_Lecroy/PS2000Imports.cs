using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SASEBO_Measure_Lecroy
{
    class Imports
    {
        #region constants
        private const string _DRIVER_FILENAME = "ps2000a.dll";

        public const int MaxValue = 32512;
        public const int MaxLogicLevel = 32767;
        #endregion

        #region Driver enums

        public enum Channel : int
        {
            ChannelA,
            ChannelB,
            ChannelC,
            ChannelD,
            External,
            Aux,
            None,
            PS2000A_DIGITAL_PORT0 = 0x80,    // digital channel 0 - 7
            PS2000A_DIGITAL_PORT1,			 // digital channel 8 - 15
            PS2000A_DIGITAL_PORT2,			 // digital channel 16 - 23
            PS2000A_DIGITAL_PORT3			 // digital channel 24 - 31
        }

        public enum Range : int
        {
            Range_10MV,
            Range_20MV,
            Range_50MV,
            Range_100MV,
            Range_200MV,
            Range_500MV,
            Range_1V,
            Range_2V,
            Range_5V,
            Range_10V,
            Range_20V,
            Range_50V,
        }

        public enum ReportedTimeUnits : int
        {
            FemtoSeconds,
            PicoSeconds,
            NanoSeconds,
            MicroSeconds,
            MilliSeconds,
            Seconds,
        }

        public enum ThresholdMode : int
        {
            Level,
            Window
        }

        public enum ThresholdDirection : int
        {
            // Values for level threshold mode
            //
            Above,
            Below,
            Rising,
            Falling,
            RisingOrFalling,

            // Values for window threshold mode
            //
            Inside = Above,
            Outside = Below,
            Enter = Rising,
            Exit = Falling,
            EnterOrExit = RisingOrFalling,
            PositiveRunt = 9,
            NegativeRunt,

            None = Rising,
        }

        public enum DownSamplingMode : int
        {
            None,
            Aggregate
        }

        public enum PulseWidthType : int
        {
            None,
            LessThan,
            GreaterThan,
            InRange,
            OutOfRange
        }

        public enum TriggerState : int
        {
            DontCare,
            True,
            False,
        }

        public enum RatioMode : int
        {
            None,
            Aggregate,
            Average,
            Decimate
        }

        public enum Model : int
        {
            NONE = 0,
            PS2206 = 2206,
            PS2207 = 2207,
            PS2208 = 2208,
            PS2205MSO = 2205
        }


        public enum DigitalDirection : int
        {
            PS2000A_DIGITAL_DONT_CARE,
            PS2000A_DIGITAL_DIRECTION_LOW,
            PS2000A_DIGITAL_DIRECTION_HIGH,
            PS2000A_DIGITAL_DIRECTION_RISING,
            PS2000A_DIGITAL_DIRECTION_FALLING,
            PS2000A_DIGITAL_DIRECTION_RISING_OR_FALLING,
            PS2000A_DIGITAL_MAX_DIRECTION
        }

        public enum Mode : int
        {
            ANALOGUE,
            DIGITAL,
            AGGREGATED,
            MIXED
        }

        public enum DigitalChannel : int
        {
            PS2000A_DIGITAL_CHANNEL_0,
            PS2000A_DIGITAL_CHANNEL_1,
            PS2000A_DIGITAL_CHANNEL_2,
            PS2000A_DIGITAL_CHANNEL_3,
            PS2000A_DIGITAL_CHANNEL_4,
            PS2000A_DIGITAL_CHANNEL_5,
            PS2000A_DIGITAL_CHANNEL_6,
            PS2000A_DIGITAL_CHANNEL_7,
            PS2000A_DIGITAL_CHANNEL_8,
            PS2000A_DIGITAL_CHANNEL_9,
            PS2000A_DIGITAL_CHANNEL_10,
            PS2000A_DIGITAL_CHANNEL_11,
            PS2000A_DIGITAL_CHANNEL_12,
            PS2000A_DIGITAL_CHANNEL_13,
            PS2000A_DIGITAL_CHANNEL_14,
            PS2000A_DIGITAL_CHANNEL_15,
            PS2000A_DIGITAL_CHANNEL_16,
            PS2000A_DIGITAL_CHANNEL_17,
            PS2000A_DIGITAL_CHANNEL_18,
            PS2000A_DIGITAL_CHANNEL_19,
            PS2000A_DIGITAL_CHANNEL_20,
            PS2000A_DIGITAL_CHANNEL_21,
            PS2000A_DIGITAL_CHANNEL_22,
            PS2000A_DIGITAL_CHANNEL_23,
            PS2000A_DIGITAL_CHANNEL_24,
            PS2000A_DIGITAL_CHANNEL_25,
            PS2000A_DIGITAL_CHANNEL_26,
            PS2000A_DIGITAL_CHANNEL_27,
            PS2000A_DIGITAL_CHANNEL_28,
            PS2000A_DIGITAL_CHANNEL_29,
            PS2000A_DIGITAL_CHANNEL_30,
            PS2000A_DIGITAL_CHANNEL_31
        }

        public enum IndexMode : int
        {
            PS2000A_SINGLE,
            PS2000A_DUAL,
            PS2000A_QUAD,
            PS2000A_MAX_INDEX_MODES
        }

        public enum WaveType : int
        {
            PS2000A_SINE,
            PS2000A_SQUARE,
            PS2000A_TRIANGLE,
            PS2000A_RAMP_UP,
            PS2000A_RAMP_DOWN,
            PS2000A_SINC,
            PS2000A_GAUSSIAN,
            PS2000A_HALF_SINE,
            PS2000A_DC_VOLTAGE,
            PS2000A_WHITE_NOISE,
            PS2000A_MAX_WAVE_TYPES
        }

        public enum SweepType : int
        {
            PS2000A_UP,
            PS2000A_DOWN,
            PS2000A_UPDOWN,
            PS2000A_DOWNUP,
            PS2000A_MAX_SWEEP_TYPES
        }

        public enum ExtraOperations : int
        {
            PS2000A_ES_OFF,
            PS2000A_WHITENOISE,
            PS2000A_PRBS // Pseudo-Random Bit Stream 
        }

        public enum SigGenTrigType : int
        {
            PS2000A_SIGGEN_RISING,
            PS2000A_SIGGEN_FALLING,
            PS2000A_SIGGEN_GATE_HIGH,
            PS2000A_SIGGEN_GATE_LOW
        }

        public enum SigGenTrigSource : int
        {
            PS2000A_SIGGEN_NONE,
            PS2000A_SIGGEN_SCOPE_TRIG,
            PS2000A_SIGGEN_AUX_IN,
            PS2000A_SIGGEN_EXT_IN,
            PS2000A_SIGGEN_SOFT_TRIG
        }

        #endregion

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TriggerChannelProperties
        {
            public short ThresholdMajor;
            public ushort HysteresisMajor;
            public short ThresholdMinor;
            public ushort HysteresisMinor;
            public Channel Channel;
            public ThresholdMode ThresholdMode;


            public TriggerChannelProperties(
                short thresholdMajor,
                ushort hysteresisMajor,
                short thresholdMinor,
                ushort hysteresisMinor,
                Channel channel,
                ThresholdMode thresholdMode)
            {
                this.ThresholdMajor = thresholdMajor;
                this.HysteresisMajor = hysteresisMajor;
                this.ThresholdMinor = thresholdMinor;
                this.HysteresisMinor = hysteresisMinor;
                this.Channel = channel;
                this.ThresholdMode = thresholdMode;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TriggerConditions
        {
            public TriggerState ChannelA;
            public TriggerState ChannelB;
            public TriggerState ChannelC;
            public TriggerState ChannelD;
            public TriggerState External;
            public TriggerState Aux;
            public TriggerState Pwq;
            public TriggerState Digital;

            public TriggerConditions(
                TriggerState channelA,
                TriggerState channelB,
                TriggerState channelC,
                TriggerState channelD,
                TriggerState external,
                TriggerState aux,
                TriggerState pwq,
                TriggerState digital)
            {
                this.ChannelA = channelA;
                this.ChannelB = channelB;
                this.ChannelC = channelC;
                this.ChannelD = channelD;
                this.External = external;
                this.Aux = aux;
                this.Pwq = pwq;
                this.Digital = digital;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PwqConditions
        {
            public TriggerState ChannelA;
            public TriggerState ChannelB;
            public TriggerState ChannelC;
            public TriggerState ChannelD;
            public TriggerState External;
            public TriggerState Aux;

            public PwqConditions(
                TriggerState channelA,
                TriggerState channelB,
                TriggerState channelC,
                TriggerState channelD,
                TriggerState external,
                TriggerState aux)
            {
                this.ChannelA = channelA;
                this.ChannelB = channelB;
                this.ChannelC = channelC;
                this.ChannelD = channelD;
                this.External = external;
                this.Aux = aux;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct DigitalChannelDirections
        {
            public DigitalChannel DigiPort;
            public DigitalDirection DigiDirection;

            public DigitalChannelDirections(
                DigitalChannel digiPort,
                DigitalDirection digiDirection)
            {
                this.DigiPort = digiPort;
                this.DigiDirection = digiDirection;
            }
        }

        #region Driver Imports
        #region Callback delegates
        public delegate void ps2000aBlockReady(short handle, short status, IntPtr pVoid);

        public delegate void ps2000aStreamingReady(
                                                short handle,
                                                int noOfSamples,
                                                uint startIndex,
                                                short ov,
                                                uint triggerAt,
                                                short triggered,
                                                short autoStop,
                                                IntPtr pVoid);

        public delegate void ps3000DataReady(
                                                short handle,
                                                short status,
                                                int noOfSamples,
                                                short overflow,
                                                IntPtr pVoid);
        #endregion

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aOpenUnit")]
        public static extern short OpenUnit(out short handle, StringBuilder serial);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aCloseUnit")]
        public static extern short CloseUnit(short handle);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aRunBlock")]
        public static extern short RunBlock(
                                                short handle,
                                                int noOfPreTriggerSamples,
                                                int noOfPostTriggerSamples,
                                                uint timebase,
                                                short oversample,
                                                out int timeIndisposedMs,
                                                ushort segmentIndex,
                                                ps2000aBlockReady lpps2000aBlockReady,
                                                IntPtr pVoid);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aStop")]
        public static extern short Stop(short handle);
        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aIsReady")]
        public static extern short IsReady(short handle, out short ready);
        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSetChannel")]
        public static extern short SetChannel(
                                                short handle,
                                                Channel channel,
                                                short enabled,
                                                short dc,
                                                Range range,
                                                float analogueOffset);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSetSimpleTrigger")]
        public static extern short SetTrigger(
                                                short handle,
                                                short enabled,
                                                Channel source,
                                                short threshold,
                                                ThresholdDirection direction,
                                                uint delay,
                                                short autoTrigger_ms);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSetDataBuffer")]
        public static extern short SetDataBuffer(
                                                short handle,
                                                Channel channel,
                                                short[] buffer,
                                                int bufferLth,
                                                ushort segmentIndex,
                                                RatioMode ratioMode);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSetDataBuffers")]
        public static extern short SetDataBuffers(
                                                short handle,
                                                Channel channel,
                                                short[] bufferMax,
                                                short[] bufferMin,
                                                int bufferLth,
                                                ushort segmentIndex,
                                                RatioMode ratioMode);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSetTriggerChannelDirections")]
        public static extern short SetTriggerChannelDirections(
                                                short handle,
                                                ThresholdDirection channelA,
                                                ThresholdDirection channelB,
                                                ThresholdDirection channelC,
                                                ThresholdDirection channelD,
                                                ThresholdDirection ext,
                                                ThresholdDirection aux);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aGetTimebase")]
        public static extern short GetTimebase(
                                             short handle,
                                             uint timebase,
                                             int noSamples,
                                             out int timeIntervalNanoseconds,
                                             short oversample,
                                             out int maxSamples,
                                             ushort segmentIndex);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aGetValues")]
        public static extern short GetValues(
                short handle,
                uint startIndex,
                ref uint noOfSamples,
                uint downSampleRatio,
                DownSamplingMode downSampleRatioMode,
                ushort segmentIndex,
                out short overflow);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSetPulseWidthQualifier")]
        public static extern short SetPulseWidthQualifier(
            short handle,
            PwqConditions[] conditions,
            short nConditions,
            ThresholdDirection direction,
            uint lower,
            uint upper,
            PulseWidthType type);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSetTriggerChannelProperties")]
        public static extern short SetTriggerChannelProperties(
            short handle,
            TriggerChannelProperties[] channelProperties,
            short nChannelProperties,
            short auxOutputEnable,
            int autoTriggerMilliseconds);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSetTriggerChannelConditions")]
        public static extern short SetTriggerChannelConditions(
            short handle,
            TriggerConditions[] conditions,
            short nConditions);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSetTriggerDelay")]
        public static extern short SetTriggerDelay(short handle, uint delay);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aGetUnitInfo")]
        public static extern short GetUnitInfo(
            short handle,
            StringBuilder infoString,
            short stringLength,
            out short requiredSize,
            int info);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aRunStreaming")]
        public static extern short RunStreaming(
            short handle,
            ref uint sampleInterval,
            ReportedTimeUnits sampleIntervalTimeUnits,
            uint maxPreTriggerSamples,
            uint maxPostPreTriggerSamples,
            bool autoStop,
            uint downSamplingRatio,
            RatioMode downSampleRatioMode,
            uint overviewBufferSize);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aGetStreamingLatestValues")]
        public static extern short GetStreamingLatestValues(
            short handle,
            ps2000aStreamingReady lpps2000aStreamingReady,
            IntPtr pVoid);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSetNoOfCaptures")]
        public static extern short SetNoOfRapidCaptures(
            short handle,
            ushort nCaptures);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aMemorySegments")]
        public static extern short MemorySegments(
            short handle,
            ushort nSegments,
            out int nMaxSamples);


        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aGetValuesBulk")]
        public static extern short GetValuesRapid(
            short handle,
            ref uint noOfSamples,
            ushort fromSegmentIndex,
            ushort toSegmentIndex,
            uint downSampleRatio,
            DownSamplingMode downSampleRatioMode,
            short[] overflow);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSetDigitalPort")]
        public static extern short SetDigitalPort(
            short handle,
            Channel digiPort,
            short enabled,
            short logicLevel);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSetTriggerDigitalPortProperties")]
        public static extern short SetTriggerDigitalPort(
            short handle,
            DigitalChannelDirections[] DigiChannelDirections,
            short nDigiChannelDirections);


        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSetSigGenBuiltIn")]
        public static extern Int16 SetSigGenBuiltIn(
            short handle,
            int offsetVoltage,
            uint pkToPk,
            WaveType waveType,
            float startFrequency,
            float stopFrequency,
            uint increment, //not double
            uint dwellTime, //not double...
            SweepType sweepType,
            ExtraOperations operation,
            uint shots,
            uint sweeps,
            SigGenTrigType triggerType,
            SigGenTrigSource triggerSource,
            short extInThreshold);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSetSigGenArbitrary")]
        public static extern Int16 SetSigGenArbitray(
            short handle,
             int offsetVoltage,
            uint pkTopk,
            uint StartDeltaPhase,
            uint StopDeltaPhase,
            uint deltaPhaseIncrement,
            uint dwellCount,
            short[] arbitaryWaveform,
            int arbitaryWaveformSize,
            SweepType sweepType,
            ExtraOperations operation,
            IndexMode indexMode,
            uint shots,
            uint sweeps,
            SigGenTrigType triggerType,
            SigGenTrigSource triggerSource,
            short extInThreshold);

        [DllImport(_DRIVER_FILENAME, EntryPoint = "ps2000aSigGenFrequencyToPhase")]
        public static extern Int16 SigGenFrequencyToPhase(
            short handle,
            double frequency,
            IndexMode indexMode,
            uint bufferlength,
            out uint Phase);



        #endregion
    }
}
