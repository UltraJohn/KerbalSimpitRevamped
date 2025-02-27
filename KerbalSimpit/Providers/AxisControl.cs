using System;
using System.Runtime.InteropServices;
using UnityEngine;

using KerbalSimpit;
using KerbalSimpit.Utilities;
using KerbalSimpit.KerbalSimpit.Providers;

namespace KerbalSimPit.Providers
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KerbalSimpitAxisController : MonoBehaviour
    {
        [StructLayout(LayoutKind.Sequential, Pack=1)][Serializable]
        public struct RotationalStruct
        {
            public short pitch;
            public short roll;
            public short yaw;
            public byte mask;
        }
        [StructLayout(LayoutKind.Sequential, Pack=1)][Serializable]
        public struct TranslationalStruct
        {
            public short X;
            public short Y;
            public short Z;
            public byte mask;
        }
        [StructLayout(LayoutKind.Sequential, Pack=1)][Serializable]
        public struct WheelStruct
        {
            public short steer;
            public short throttle;
            public byte mask;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)][Serializable]
        public struct CustomAxixStruct
        {
            public short custom1;
            public short custom2;
            public short custom3;
            public short custom4;
            public byte mask;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)][Serializable]
        public struct ThrottleStruct
        {
            public short throttle;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        [Serializable]
        public struct SASModeInfoStruct
        {
            public byte currentSASMode;
            public ushort SASModeAvailability;
        }

        // Inbound messages
        private EventData<byte, object> RotationChannel, TranslationChannel,
            WheelChannel, ThrottleChannel, CustomAxisChannel, SASInfoChannel, AutopilotChannel;

        private RotationalStruct myRotation, newRotation;

        private TranslationalStruct myTranslation, newTranslation;

        private WheelStruct myWheel, newWheel;

        private CustomAxixStruct myCustomAxis, newCustomAxis;

        private SASModeInfoStruct mySASInfo, newSASInfo;

        private short myThrottle;
        private bool lastThrottleSentIsZero = false;

        private VesselAutopilot.AutopilotMode mySASMode;
        private Vessel lastActiveVessel;

        protected FlightCtrlState lastFlightCtrlState = new FlightCtrlState();

        public void Start()
        {
            RotationChannel = GameEvents.FindEvent<EventData<byte, object>>("onSerialReceived" + InboundPackets.VesselRotation);
            if (RotationChannel != null) RotationChannel.Add(vesselRotationCallback);
            TranslationChannel = GameEvents.FindEvent<EventData<byte, object>>("onSerialReceived" + InboundPackets.VesselTranslation);
            if (TranslationChannel != null) TranslationChannel.Add(vesselTranslationCallback);
            WheelChannel = GameEvents.FindEvent<EventData<byte, object>>("onSerialReceived" + InboundPackets.WheelControl);
            if (WheelChannel != null) WheelChannel.Add(wheelCallback);
            ThrottleChannel = GameEvents.FindEvent<EventData<byte, object>>("onSerialReceived" + InboundPackets.VesselThrottle);
            if (ThrottleChannel != null) ThrottleChannel.Add(throttleCallback);
            CustomAxisChannel = GameEvents.FindEvent<EventData<byte, object>>("onSerialReceived" + InboundPackets.VesselCustomAxis);
            if (CustomAxisChannel != null) CustomAxisChannel.Add(customAxisCallback);
            AutopilotChannel = GameEvents.FindEvent<EventData<byte, object>>("onSerialReceived" + InboundPackets.AutopilotMode);
            if (AutopilotChannel != null) AutopilotChannel.Add(autopilotModeCallback);

            SASInfoChannel = GameEvents.FindEvent<EventData<byte, object>>("toSerial" + OutboundPackets.SASInfo);
            KSPit.AddToDeviceHandler(SASInfoProvider);
            mySASInfo.currentSASMode = 255; // value for not enabled
            mySASInfo.SASModeAvailability = 0;

            lastActiveVessel = FlightGlobals.ActiveVessel;
            FlightGlobals.ActiveVessel.OnPostAutopilotUpdate += AutopilotUpdater;
            GameEvents.onVesselChange.Add(OnVesselChange);
        }

        public void OnDestroy()
        {
            if (RotationChannel != null) RotationChannel.Remove(vesselRotationCallback);
            if (TranslationChannel != null) TranslationChannel.Remove(vesselTranslationCallback);
            if (WheelChannel != null) WheelChannel.Remove(wheelCallback);
            if (ThrottleChannel != null) ThrottleChannel.Remove(throttleCallback);
            if (CustomAxisChannel != null) CustomAxisChannel.Remove(customAxisCallback);
            if (AutopilotChannel!= null) AutopilotChannel.Remove(autopilotModeCallback);

            KSPit.RemoveToDeviceHandler(SASInfoProvider);

            lastActiveVessel.OnPostAutopilotUpdate -= AutopilotUpdater;
            GameEvents.onVesselChange.Remove(OnVesselChange);
        }

        public void OnVesselChange(Vessel vessel)
        {
            lastActiveVessel.OnPostAutopilotUpdate -= AutopilotUpdater;
            lastActiveVessel = FlightGlobals.ActiveVessel;
            FlightGlobals.ActiveVessel.OnPostAutopilotUpdate += AutopilotUpdater;
        }

        public void vesselRotationCallback(byte ID, object Data)
        {

            newRotation = KerbalSimpitUtils.ByteArrayToStructure<RotationalStruct>((byte[])Data);
            // Bit fields:
            // pitch = 1
            // roll = 2
            // yaw = 4
            if ((newRotation.mask & (byte)1) > 0)
            {
                myRotation.pitch = newRotation.pitch;
            }
            if ((newRotation.mask & (byte)2) > 0)
            {
                myRotation.roll = newRotation.roll;
            }
            if ((newRotation.mask & (byte)4) > 0)
            {
                myRotation.yaw = newRotation.yaw;
            }
        }

        public void vesselTranslationCallback(byte ID, object Data)
        {
            newTranslation = KerbalSimpitUtils.ByteArrayToStructure<TranslationalStruct>((byte[])Data);
            // Bit fields:
            // X = 1
            // Y = 2
            // Z = 4
            if ((newTranslation.mask & (byte)1) > 0)
            {
                myTranslation.X = newTranslation.X;
            }
            if ((newTranslation.mask & (byte)2) > 0)
            {
                myTranslation.Y = newTranslation.Y;
            }
            if ((newTranslation.mask & (byte)4) > 0)
            {
                myTranslation.Z = newTranslation.Z;
            }
        }

        public void wheelCallback(byte ID, object Data)
        {
            newWheel = KerbalSimpitUtils.ByteArrayToStructure<WheelStruct>((byte[])Data);
            // Bit fields
            // steer = 1
            // throttle = 2
            if ((newWheel.mask & (byte)1) > 0)
            {
                myWheel.steer = newWheel.steer;
            }
            if ((newWheel.mask & (byte)2) > 0)
            {
                myWheel.throttle = newWheel.throttle;
            }
        }

        public void customAxisCallback(byte ID, object Data)
        {
            newCustomAxis = KerbalSimpitUtils.ByteArrayToStructure<CustomAxixStruct>((byte[])Data);

            if ((newCustomAxis.mask & (byte)1) > 0)
            {
                myCustomAxis.custom1 = newCustomAxis.custom1;
            }
            if ((newCustomAxis.mask & (byte)2) > 0)
            {
                myCustomAxis.custom2 = newCustomAxis.custom2;
            }
            if ((newCustomAxis.mask & (byte)4) > 0)
            {
                myCustomAxis.custom3 = newCustomAxis.custom3;
            }
            if ((newCustomAxis.mask & (byte)8) > 0)
            {
                myCustomAxis.custom4 = newCustomAxis.custom4;
            }
        }

        public void throttleCallback(byte ID, object Data)
        {
            myThrottle = BitConverter.ToInt16((byte[])Data, 0);
        }

        public void autopilotModeCallback(byte ID, object Data)
        {
            byte[] payload = (byte[])Data;

            VesselAutopilot autopilot = FlightGlobals.ActiveVessel.Autopilot;

            if(autopilot == null)
            {
                Debug.Log("KerbalSimpit : Ignoring a SAS MODE Message since I could not find the autopilot");
                return;
            }

            mySASMode = (VesselAutopilot.AutopilotMode)(payload[0]);

            if (autopilot.CanSetMode(mySASMode))
            {
                autopilot.SetMode(mySASMode);
                if (KSPit.Config.Verbose)
                {
                    Debug.Log(String.Format("KerbalSimpit: payload is {0}", mySASMode));
                    Debug.Log(String.Format("KerbalSimpit: SAS mode is {0}", FlightGlobals.ActiveVessel.Autopilot.Mode.ToString()));
                }
            }
            else
            {
                Debug.Log(String.Format("KerbalSimpit: Unable to set SAS mode to {0}", mySASMode.ToString()));
            }
        }

        public void AutopilotUpdater(FlightCtrlState fcs)
        {
            // For all axis, we need to control both the usual axis and make sure the matching custom axis are also controlled.
            // This does not seem possible in 1.12.2 without using 2 different methods to set the axis values.
            // If any change is made to the following code, please check both (e.g. add a piston to the roll axis and check that setting roll also control the piston).
            var axisGroupModule = FlightGlobals.ActiveVessel.FindVesselModuleImplementing<AxisGroupsModule>();

            if (myRotation.pitch != 0)
            {
                fcs.pitch = (float)myRotation.pitch / Int16.MaxValue;
                axisGroupModule.UpdateAxisGroup(KSPAxisGroup.Pitch, (float)myRotation.pitch / Int16.MaxValue);
            }
            if (myRotation.roll != 0)
            {
                fcs.roll = (float)myRotation.roll/ Int16.MaxValue;
                axisGroupModule.UpdateAxisGroup(KSPAxisGroup.Roll, (float)myRotation.roll / Int16.MaxValue);
            }
            if (myRotation.yaw != 0)
            {
                fcs.yaw = (float)myRotation.yaw/ Int16.MaxValue;
                axisGroupModule.UpdateAxisGroup(KSPAxisGroup.Yaw, (float)myRotation.yaw / Int16.MaxValue);
            }

            if (myTranslation.X != 0)
            {
                fcs.X = (float)myTranslation.X/ Int16.MaxValue;
                axisGroupModule.UpdateAxisGroup(KSPAxisGroup.TranslateX, (float)myTranslation.X / Int16.MaxValue);
            }
            if (myTranslation.Y != 0)
            {
                fcs.Y = (float)myTranslation.Y/ Int16.MaxValue;
                axisGroupModule.UpdateAxisGroup(KSPAxisGroup.TranslateY, (float)myTranslation.Y / Int16.MaxValue);
            }
            if (myTranslation.Z != 0)
            {
                fcs.Z = (float)myTranslation.Z/ Int16.MaxValue;
                axisGroupModule.UpdateAxisGroup(KSPAxisGroup.TranslateZ, (float)myTranslation.Z / Int16.MaxValue);
            }

            if (myWheel.steer != 0)
            {
                fcs.wheelSteer = (float)myWheel.steer/ Int16.MaxValue;
                axisGroupModule.UpdateAxisGroup(KSPAxisGroup.WheelSteer, (float)myWheel.steer / Int16.MaxValue);
            }
            if (myWheel.throttle != 0)
            {
                fcs.wheelThrottle = (float)myWheel.throttle/ Int16.MaxValue;
                axisGroupModule.UpdateAxisGroup(KSPAxisGroup.WheelThrottle, (float)myWheel.throttle / Int16.MaxValue);
            }

            if (myThrottle != 0 || !lastThrottleSentIsZero)
            {
                // Throttle seems to be handled differently than the other axis since when no value is set, a zero is assumed. For throttle, no value set mean the last one is used.
                // So we need to send a 0 first before stopping to send values.
                FlightInputHandler.state.mainThrottle = (float)myThrottle / Int16.MaxValue;

                lastThrottleSentIsZero = (myThrottle == 0);
            }
            if(myCustomAxis.custom1 != 0)
            {
                axisGroupModule.UpdateAxisGroup(KSPAxisGroup.Custom01, (float)myCustomAxis.custom1 / Int16.MaxValue);
            }
            if (myCustomAxis.custom2 != 0)
            {
                axisGroupModule.UpdateAxisGroup(KSPAxisGroup.Custom02, (float)myCustomAxis.custom2 / Int16.MaxValue);
            }
            if (myCustomAxis.custom3 != 0)
            {
                axisGroupModule.UpdateAxisGroup(KSPAxisGroup.Custom03, (float)myCustomAxis.custom3 / Int16.MaxValue);
            }
            if (myCustomAxis.custom4 != 0)
            {
                axisGroupModule.UpdateAxisGroup(KSPAxisGroup.Custom04, (float)myCustomAxis.custom4 / Int16.MaxValue);
            }

            // Store the last flight command to send them in the dedicated channels
            lastFlightCtrlState.CopyFrom(fcs);
        }

        public void SASInfoProvider()
        {
            if(FlightGlobals.ActiveVessel == null)
            {
                // This can happen when docking/undocking/changing scene, etc.
                return;
            }

            VesselAutopilot autopilot = FlightGlobals.ActiveVessel.Autopilot;

            if(autopilot == null)
            {
                return;
            }

            if (autopilot.Enabled)
            {
                newSASInfo.currentSASMode = (byte)autopilot.Mode;
            } else
            {
                newSASInfo.currentSASMode = 255; //special value to indicate a disabled SAS
            }

            newSASInfo.SASModeAvailability = 0;
            foreach (VesselAutopilot.AutopilotMode i in Enum.GetValues(typeof(VesselAutopilot.AutopilotMode)))
            {
                if (autopilot.CanSetMode(i))
                {
                    newSASInfo.SASModeAvailability = (ushort) (newSASInfo.SASModeAvailability | (1 << (byte)i));
                }
            }

            if(mySASInfo.currentSASMode != newSASInfo.currentSASMode ||
                mySASInfo.SASModeAvailability != newSASInfo.SASModeAvailability)
            {
                if (SASInfoChannel != null)
                {
                    mySASInfo = newSASInfo;
                    SASInfoChannel.Fire(OutboundPackets.SASInfo, mySASInfo);
                }
            }
        }

        class RotationCommandProvider : GenericProvider<RotationalStruct>
        {
            private KerbalSimpitAxisController controller = null;
            RotationCommandProvider() : base(OutboundPackets.RotationCmd){ }

            public override void Start()
            {
                base.Start();
                controller = (KerbalSimpitAxisController)FindObjectOfType(typeof(KerbalSimpitAxisController));
            }

            protected override bool updateMessage(ref RotationalStruct message)
            {
                if (controller != null) {
                    message.pitch = (short)(controller.lastFlightCtrlState.pitch * Int16.MaxValue);
                    message.yaw = (short)(controller.lastFlightCtrlState.yaw * Int16.MaxValue);
                    message.roll = (short)(controller.lastFlightCtrlState.roll * Int16.MaxValue);
                } else {
                    Debug.Log("Simpit : KerbalSimpitAxisController is not found");
                }

                return false;
            }
        }

        class TranslationCommandProvider : GenericProvider<TranslationalStruct>
        {
            private KerbalSimpitAxisController controller = null;
            TranslationCommandProvider() : base(OutboundPackets.TranslationCmd) { }

            public override void Start()
            {
                base.Start();
                controller = (KerbalSimpitAxisController)FindObjectOfType(typeof(KerbalSimpitAxisController));
            }

            protected override bool updateMessage(ref TranslationalStruct message)
            {
                if (controller != null)
                {
                    message.X = (short)(controller.lastFlightCtrlState.X * Int16.MaxValue);
                    message.Y = (short)(controller.lastFlightCtrlState.Y * Int16.MaxValue);
                    message.Z = (short)(controller.lastFlightCtrlState.Z * Int16.MaxValue);
                }
                else
                {
                    Debug.Log("Simpit : KerbalSimpitAxisController is not found");
                }

                return false;
            }
        }

        class WheelCommandProvider : GenericProvider<WheelStruct>
        {
            private KerbalSimpitAxisController controller = null;
            WheelCommandProvider() : base(OutboundPackets.WheelCmd) { }

            public override void Start()
            {
                base.Start();
                controller = (KerbalSimpitAxisController)FindObjectOfType(typeof(KerbalSimpitAxisController));
            }

            protected override bool updateMessage(ref WheelStruct message)
            {
                if (controller != null)
                {
                    message.steer = (short)(controller.lastFlightCtrlState.wheelSteer * Int16.MaxValue);
                    message.throttle = (short)(controller.lastFlightCtrlState.wheelThrottle * Int16.MaxValue);
                }
                else
                {
                    Debug.Log("Simpit : KerbalSimpitAxisController is not found");
                }

                return false;
            }
        }

        class ThrottleCommandProvider : GenericProvider<ThrottleStruct>
        {
            private KerbalSimpitAxisController controller = null;
            ThrottleCommandProvider() : base(OutboundPackets.ThrottleCmd) { }

            public override void Start()
            {
                base.Start();
                controller = (KerbalSimpitAxisController)FindObjectOfType(typeof(KerbalSimpitAxisController));
            }

            protected override bool updateMessage(ref ThrottleStruct message)
            {
                if (controller != null)
                {
                    message.throttle = (short)(controller.lastFlightCtrlState.mainThrottle * Int16.MaxValue);
                }
                else
                {
                    Debug.Log("Simpit : KerbalSimpitAxisController is not found");
                }

                return false;
            }
        }
    }
}
