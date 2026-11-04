using System;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;

namespace Lilbar
{
    // Mobile hotspot (WinRT tethering manager) and airplane mode (the shell's radio manager, the same switch
    // as Quick Settings). Polled on the background thread; -1 means this PC can't do it.
    static class Quick
    {
        [ComImport, Guid("db3afbfb-08e6-46c6-aa70-bf9a34c30ab7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IRadioManager
        {
            [PreserveSig] int IsRMSupported(out uint state);
            [PreserveSig] int GetUIRadioInstances([MarshalAs(UnmanagedType.IUnknown)] out object instances);
            [PreserveSig] int GetSystemRadioState(out int enabled, out int policyEnabled, out int changeReason);
            [PreserveSig] int SetSystemRadioState(int enabled);
            [PreserveSig] int Refresh();
            [PreserveSig] int OnHardwareSliderChange(int a, int b);
        }
        [ComImport, Guid("581333f6-28db-41be-bc7a-ff201f12f3f6")] class RadioManagerClass { }

        public static volatile int Hotspot = -1, Airplane = -1; // 0 off, 1 on, 2 switching
        static IRadioManager radios;

        public static void Poll()
        {
            try
            {
                ConnectionProfile p = NetworkInformation.GetInternetConnectionProfile();
                if (p == null || NetworkOperatorTetheringManager.GetTetheringCapabilityFromConnectionProfile(p) != TetheringCapability.Enabled) Hotspot = -1;
                else
                {
                    TetheringOperationalState s = NetworkOperatorTetheringManager.CreateFromConnectionProfile(p).TetheringOperationalState;
                    Hotspot = s == TetheringOperationalState.On ? 1 : s == TetheringOperationalState.Off ? 0 : 2;
                }
            }
            catch (Exception) { Hotspot = -1; }
            try
            {
                if (radios == null) radios = (IRadioManager)new RadioManagerClass();
                int enabled, policy, reason;
                Airplane = radios.GetSystemRadioState(out enabled, out policy, out reason) == 0 ? (enabled == 0 ? 1 : 0) : -1; // radios disabled = airplane mode
            }
            catch (Exception) { Airplane = -1; }
        }

        public static void ToggleHotspot()
        {
            int was = Hotspot;
            Hotspot = 2;
            Manager.PostRefresh();
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var m = NetworkOperatorTetheringManager.CreateFromConnectionProfile(NetworkInformation.GetInternetConnectionProfile());
                    NetworkOperatorTetheringOperationResult r = WinRt.Wait(was == 1 ? m.StopTetheringAsync() : m.StartTetheringAsync());
                    if (r.Status != TetheringOperationStatus.Success) Taskbar.Log(new Exception("hotspot: " + r.Status + " " + r.AdditionalErrorMessage));
                }
                catch (Exception e) { Taskbar.Log(e); }
                Poll();
                Manager.PostRefresh();
            });
        }

        public static void ToggleAirplane()
        {
            int was = Airplane;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { if (radios != null) radios.SetSystemRadioState(was == 1 ? 1 : 0); }
                catch (Exception e) { Taskbar.Log(e); }
                Thread.Sleep(600);
                Poll();
                Radios.Poll();
                Manager.PostRefresh();
            });
        }
    }
}
