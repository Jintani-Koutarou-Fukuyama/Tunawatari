using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed class JoyConInputProvider : MonoBehaviour
{
    private const string RuntimeObjectName = "JoyConInputProvider";
    private const int MaxDeviceCount = 16;

    private static JoyConInputProvider instance;

    [SerializeField] private bool logSensorValues = true;
    [SerializeField] private float logInterval = 0.5f;
    [SerializeField] private bool reconnectOnRefresh = true;
    [SerializeField] private float reconnectInterval = 2f;

    private readonly List<JoyConState> joyCons = new List<JoyConState>();
    private readonly int[] deviceHandles = new int[MaxDeviceCount];
    private float nextLogTime;
    private float nextRefreshTime;
    private bool joyShockLibraryAvailable = true;

    public static JoyConInputProvider Instance => instance;
    public IReadOnlyList<JoyConState> JoyCons => joyCons;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(runtimeObject);
        instance = runtimeObject.AddComponent<JoyConInputProvider>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        RefreshJoyConDevices();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            JslSafeDisconnectAll();
            instance = null;
        }
    }

    private void Update()
    {
        if (!joyShockLibraryAvailable)
        {
            return;
        }

        if (reconnectOnRefresh && joyCons.Count == 0 && Time.unscaledTime >= nextRefreshTime)
        {
            RefreshJoyConDevices();
        }

        if (!logSensorValues || Time.unscaledTime < nextLogTime)
        {
            return;
        }

        nextLogTime = Time.unscaledTime + Mathf.Max(0.05f, logInterval);
        LogJoyConSensors();
    }

    public void RefreshJoyConDevices()
    {
        if (!joyShockLibraryAvailable)
        {
            return;
        }

        try
        {
            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, reconnectInterval);

            int connectedCount = JoyShockLibraryNative.JslConnectDevices();
            int handleCount = JoyShockLibraryNative.JslGetConnectedDeviceHandles(deviceHandles, deviceHandles.Length);

            joyCons.Clear();
            Debug.Log($"[JoyConInputProvider] JoyShockLibrary connected devices: connectedCount={connectedCount}, handleCount={handleCount}");

            int count = Mathf.Min(handleCount, deviceHandles.Length);
            for (int i = 0; i < count; i++)
            {
                int handle = deviceHandles[i];
                JoyConControllerType controllerType = (JoyConControllerType)JoyShockLibraryNative.JslGetControllerType(handle);
                JoyConSide side = ToJoyConSide(controllerType);

                Debug.Log($"[JoyConInputProvider] JoyShockLibrary device: handle={handle}, controllerType={controllerType}");

                if (side == JoyConSide.Unknown)
                {
                    continue;
                }

                joyCons.Add(new JoyConState(handle, side, controllerType));
                Debug.Log($"[JoyConInputProvider] Joy-Con detected: handle={handle}, side={side}, controllerType={controllerType}");
            }
        }
        catch (DllNotFoundException exception)
        {
            joyShockLibraryAvailable = false;
            Debug.LogError($"[JoyConInputProvider] JoyShockLibrary.dll was not found. {exception.Message}");
        }
        catch (EntryPointNotFoundException exception)
        {
            joyShockLibraryAvailable = false;
            Debug.LogError($"[JoyConInputProvider] JoyShockLibrary function was not found. {exception.Message}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[JoyConInputProvider] Failed to refresh Joy-Con devices. {exception.Message}");
        }
    }

    private void LogJoyConSensors()
    {
        if (joyCons.Count == 0)
        {
            return;
        }

        for (int i = joyCons.Count - 1; i >= 0; i--)
        {
            JoyConState state = joyCons[i];

            if (!JoyShockLibraryNative.JslStillConnected(state.Handle))
            {
                Debug.Log($"[JoyConInputProvider] Joy-Con disconnected: handle={state.Handle}, side={state.Side}");
                joyCons.RemoveAt(i);
                continue;
            }

            JoyShockLibraryNative.ImuState imuState = JoyShockLibraryNative.JslGetIMUState(state.Handle);
            Debug.Log(
                $"[JoyConInputProvider] Joy-Con sensor: handle={state.Handle}, side={state.Side}, " +
                $"accel=({imuState.accelX:F3}, {imuState.accelY:F3}, {imuState.accelZ:F3}), " +
                $"gyro=({imuState.gyroX:F3}, {imuState.gyroY:F3}, {imuState.gyroZ:F3})");
        }
    }

    private static JoyConSide ToJoyConSide(JoyConControllerType controllerType)
    {
        switch (controllerType)
        {
            case JoyConControllerType.LeftJoyCon:
                return JoyConSide.Left;
            case JoyConControllerType.RightJoyCon:
                return JoyConSide.Right;
            default:
                return JoyConSide.Unknown;
        }
    }

    private static void JslSafeDisconnectAll()
    {
        try
        {
            JoyShockLibraryNative.JslDisconnectAndDisposeAll();
        }
        catch (Exception)
        {
        }
    }
}

public enum JoyConSide
{
    Unknown,
    Left,
    Right
}

public enum JoyConControllerType
{
    Unknown = 0,
    LeftJoyCon = 1,
    RightJoyCon = 2,
    SwitchProController = 3,
    DualShock4 = 4,
    DualSense = 5
}

public readonly struct JoyConState
{
    public JoyConState(int handle, JoyConSide side, JoyConControllerType controllerType)
    {
        Handle = handle;
        Side = side;
        ControllerType = controllerType;
    }

    public int Handle { get; }
    public JoyConSide Side { get; }
    public JoyConControllerType ControllerType { get; }
}

internal static class JoyShockLibraryNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ImuState
    {
        public float accelX;
        public float accelY;
        public float accelZ;
        public float gyroX;
        public float gyroY;
        public float gyroZ;
    }

    [DllImport("JoyShockLibrary")]
    public static extern int JslConnectDevices();

    [DllImport("JoyShockLibrary")]
    public static extern int JslGetConnectedDeviceHandles(int[] deviceHandleArray, int size);

    [DllImport("JoyShockLibrary")]
    public static extern void JslDisconnectAndDisposeAll();

    [DllImport("JoyShockLibrary")]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool JslStillConnected(int deviceId);

    [DllImport("JoyShockLibrary", CallingConvention = CallingConvention.Cdecl)]
    public static extern ImuState JslGetIMUState(int deviceId);

    [DllImport("JoyShockLibrary")]
    public static extern int JslGetControllerType(int deviceId);
}
