using System;
using System.IO;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Platform;
using UnityEngine;

namespace LetMeSleep.Online
{
    [Serializable]
    public sealed class EosConfiguration
    {
        public string productId, sandboxId, deploymentId, clientId, clientSecret;
        public bool IsValid => !string.IsNullOrWhiteSpace(productId)
            && !string.IsNullOrWhiteSpace(sandboxId) && !string.IsNullOrWhiteSpace(deploymentId)
            && !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);

        public static EosConfiguration Load(string path)
        {
            var data = JsonUtility.FromJson<EosConfiguration>(File.ReadAllText(path));
            if (data == null || !data.IsValid) throw new InvalidDataException("Online configuration is incomplete.");
            return data;
        }
    }

    public enum ConnectionState { Offline, SigningIn, Ready, Failed, Disposed }

    /// <summary>One SDK/platform owner per game process. Call Tick on the Unity main thread.</summary>
    public sealed class EosConnection : IDisposable
    {
        private PlatformInterface platform;
        private bool ownsSdk;
#if UNITY_EDITOR || EOS_DYNAMIC_BINDINGS
        private PlayEveryWare.EpicOnlineServices.DLLHandle library;
#endif
        private double deadline;
        private string displayName;
        private ulong expiryNotification;
        private double clock;
        private int authGeneration;
        public ConnectionState State { get; private set; }
        public string FailureCode { get; private set; } = "";
        public ProductUserId LocalUserId { get; private set; }
        public PlatformInterface Platform => platform;
        public event Action<ConnectionState> StateChanged;

        public void Initialize(EosConfiguration config, string name, string cacheDirectory)
        {
            if (State != ConnectionState.Offline) throw new InvalidOperationException("Connection has already started.");
            if (config == null || !config.IsValid) throw new ArgumentException("Invalid online configuration.");
            displayName = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
            if (displayName.Length > 24) displayName = displayName.Substring(0, 24);
            Directory.CreateDirectory(cacheDirectory);
            try
            {
#if UNITY_EDITOR || EOS_DYNAMIC_BINDINGS
                library = PlayEveryWare.EpicOnlineServices.DLLHandle.LoadDynamicLibrary(Common.LIBRARY_NAME);
                if (library == null) { Fail("NativeSdkMissing"); return; }
                Bindings.Hook(library, (handle, symbol) => handle.LoadFunctionAsIntPtr(symbol));
#endif
                var init = new InitializeOptions { ProductName = "Let me sleep", ProductVersion = "0.9.4-alfa" };
                var result = PlatformInterface.Initialize(ref init);
                if (result != Result.Success) { Fail("SDK_" + result); return; }
                ownsSdk = true;
                var options = new Options
                {
                    ProductId = config.productId, SandboxId = config.sandboxId, DeploymentId = config.deploymentId,
                    ClientCredentials = new ClientCredentials { ClientId = config.clientId, ClientSecret = config.clientSecret },
                    IsServer = false, Flags = PlatformFlags.DisableOverlay | (Application.isEditor ? PlatformFlags.LoadingInEditor : PlatformFlags.None),
                    CacheDirectory = cacheDirectory, TickBudgetInMilliseconds = 2, TaskNetworkTimeoutSeconds = 20
                };
                platform = PlatformInterface.Create(ref options);
                if (platform == null) { Fail("PlatformCreateFailed"); return; }
                deadline = clock + 30;
                authGeneration++; SetState(ConnectionState.SigningIn);
                var device = new CreateDeviceIdOptions { DeviceModel = "Windows PC" };
                platform.GetConnectInterface().CreateDeviceId(ref device, authGeneration, OnDeviceCreated);
            }
            catch (DllNotFoundException) { Fail("NativeSdkMissing"); }
            catch (EntryPointNotFoundException) { Fail("NativeSdkIncompatible"); }
        }

        public void Tick(double monotonicSeconds)
        {
            // The initial deadline is armed on the first tick, not against an arbitrary editor uptime.
            if (clock == 0 && deadline > 0) deadline += monotonicSeconds;
            clock = monotonicSeconds;
            if (State == ConnectionState.Disposed) return;
            platform?.Tick();
            if (State == ConnectionState.SigningIn && clock >= deadline) Fail("SignInTimedOut");
        }

        private void OnDeviceCreated(ref CreateDeviceIdCallbackInfo info)
        {
            if (State != ConnectionState.SigningIn || !(info.ClientData is int generation) || generation != authGeneration) return;
            if (info.ResultCode == Result.Success || info.ResultCode == Result.DuplicateNotAllowed) Login();
            else Fail("Device_" + info.ResultCode);
        }

        public bool RetryAuthentication()
        {
            if (State != ConnectionState.Failed || platform == null) return false;
            FailureCode = ""; deadline = clock + 30; authGeneration++; SetState(ConnectionState.SigningIn);
            var device = new CreateDeviceIdOptions { DeviceModel = "Windows PC" };
            platform.GetConnectInterface().CreateDeviceId(ref device, authGeneration, OnDeviceCreated);
            return true;
        }

        private void Login()
        {
            var options = new LoginOptions
            {
                Credentials = new Credentials { Type = ExternalCredentialType.DeviceidAccessToken },
                UserLoginInfo = new UserLoginInfo { DisplayName = displayName }
            };
            platform.GetConnectInterface().Login(ref options, authGeneration, OnLogin);
        }

        private void OnLogin(ref LoginCallbackInfo info)
        {
            if (State != ConnectionState.SigningIn || !(info.ClientData is int generation) || generation != authGeneration) return;
            if (info.ResultCode == Result.Success) { LoggedIn(info.LocalUserId); return; }
            if (info.ResultCode != Result.InvalidUser) { Fail("Login_" + info.ResultCode); return; }
            var create = new CreateUserOptions { ContinuanceToken = info.ContinuanceToken };
            platform.GetConnectInterface().CreateUser(ref create, authGeneration, OnUserCreated);
        }

        private void OnUserCreated(ref CreateUserCallbackInfo info)
        {
            if (State != ConnectionState.SigningIn || !(info.ClientData is int generation) || generation != authGeneration) return;
            if (info.ResultCode == Result.Success) LoggedIn(info.LocalUserId);
            else Fail("User_" + info.ResultCode);
        }

        private void LoggedIn(ProductUserId id)
        {
            LocalUserId = id;
            if (expiryNotification == 0)
            {
                var options = new AddNotifyAuthExpirationOptions();
                expiryNotification = platform.GetConnectInterface().AddNotifyAuthExpiration(ref options, null, OnAuthExpiring);
            }
            SetState(ConnectionState.Ready);
        }

        private void OnAuthExpiring(ref AuthExpirationCallbackInfo info)
        {
            if (State != ConnectionState.Ready) return;
            deadline = clock + 30;
            authGeneration++; SetState(ConnectionState.SigningIn);
            Login();
        }

        private void Fail(string code) { FailureCode = code; SetState(ConnectionState.Failed); }
        private void SetState(ConnectionState state) { State = state; StateChanged?.Invoke(state); }

        public void Dispose()
        {
            if (State == ConnectionState.Disposed) return;
            SetState(ConnectionState.Disposed);
            if (platform != null)
            {
                if (expiryNotification != 0) platform.GetConnectInterface().RemoveNotifyAuthExpiration(expiryNotification);
                platform.Release(); platform = null;
            }
            if (ownsSdk) { PlatformInterface.Shutdown(); ownsSdk = false; }
#if UNITY_EDITOR || EOS_DYNAMIC_BINDINGS
            if (library != null) { Bindings.Unhook(); library.Dispose(); library = null; }
#endif
            LocalUserId = null;
            StateChanged = null;
        }
    }
}

