﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Payments;
using Windows.ApplicationModel.Resources;
using Windows.UI.Core;

namespace WindowsGoodbye
{
    class DeviceCommunication
    {
        

        
    }

    class DevicePairingContext
    {
        public const int DevicePairingMulticastPort = 26817;
        public const int DevicePairingResultPort = 26818;
        public readonly IPAddress DevicePairingMulticastGroupAddress = IPAddress.Parse("225.67.76.67");
        public const string PairingPrefix = "wingb://pair?";
        public const string PairingRequestPrefix = "wingb://pair_req?";
        public const string PairingFinishPrefix = "wingb://pair_finish?";

        public readonly Guid DeviceId;
        public readonly byte[] DeviceKey;
        public readonly byte[] AuthKey;
        public readonly byte[] PairEncryptKey;

        private readonly UdpClient _udpClient;
        private Task<DevicePairingResult> _task;
        private CancellationTokenSource _cancellation;

        public DevicePairingContext(byte[] deviceKey, byte[] authKey)
        {
            DeviceKey = deviceKey;
            AuthKey = authKey;

            PairEncryptKey = CryptoTools.GenerateAESKey();
            DeviceId = Guid.NewGuid();

            _udpClient = new UdpClient(DevicePairingMulticastPort)
            {
                EnableBroadcast = false
            };
        }

        public string GeneratePairData()
        {
            var stream = new MemoryStream();
            stream.Write(DeviceId.ToByteArray());
            stream.Write(DeviceKey);
            stream.Write(AuthKey);

            var payload = Convert.ToBase64String(stream.ToArray());
            return PairingPrefix + payload;
        }

        public Task<DevicePairingResult> StartPairingListeningAsync()
        {
            _udpClient.JoinMulticastGroup(DevicePairingMulticastGroupAddress);
            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;
            _task = Task.Run(() =>
            {
                while (true)
                {
                    var receiveTask = _udpClient.ReceiveAsync();
                    receiveTask.Wait(token);
                    token.ThrowIfCancellationRequested();
                    if (receiveTask.Exception != null)
                    {
                        Debug.Write(receiveTask.Exception);
                        continue;
                    }
                    var result = receiveTask.Result;

                    var info = Encoding.UTF8.GetString(result.Buffer);
                    if (!info.StartsWith(PairingRequestPrefix) || info.Length <= PairingRequestPrefix.Length)
                        continue;
                    var payload = info.Substring(PairingRequestPrefix.Length + 1);
                    var rawBytes = Convert.FromBase64String(payload);
                    if (rawBytes.Length <= Utils.GuidLength) continue;
                    var deviceIdBytes = new byte[Utils.GuidLength];
                    rawBytes.CopyTo(deviceIdBytes, Utils.GuidLength);
                    var deviceId = new Guid(deviceIdBytes);
                    if (deviceId != DeviceId) continue;

                    // 注意由于已经检测到了设备，下方导致的任何错误都必须直接退出Pair过程并发送通知
                    token.ThrowIfCancellationRequested();
                    var encryptedLen = rawBytes.Length - Utils.GuidLength;
                    var encryptedData = new byte[encryptedLen];

                    rawBytes.CopyTo(encryptedData, encryptedLen);
                    var decryptedData = CryptoTools.DecryptAES(encryptedData, PairEncryptKey);

                    // 通知主界面开始配对过程
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                    MainPage.Instance.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () => MainPage.Instance.PairDeviceDetected());
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

                    if (decryptedData.Length < 2) throw new PairingException(Utils.GetBackgroundI18n("Pairing.Exception.BadResponse"));
                    int friendlyNameLen = encryptedData[0];
                    int modelNameLen = encryptedData[1];
                    if (decryptedData.Length != 2 + friendlyNameLen + modelNameLen) throw new PairingException(Utils.GetBackgroundI18n("Pairing.Exception.BadResponse"));
                    
                    var friendlyName = Encoding.UTF8.GetString(decryptedData, 2, friendlyNameLen);
                    var modelName = Encoding.UTF8.GetString(decryptedData, 2 + friendlyNameLen, modelNameLen);

                    token.ThrowIfCancellationRequested();
                    return new DevicePairingResult
                    {
                        DeviceFriendName = friendlyName,
                        DeviceModelName = modelName,
                        DeviceEndPoint = result.RemoteEndPoint
                    };
                }
            }, _cancellation.Token);
            return _task;
        }

        public void StopPairingListening()
        {
            _cancellation.Cancel();
        }

        public void FinishPairing(DevicePairingResult previousResult, string computerInfo)
        {
            // 配对完成，通知设备
            // TODO: 这里是否需要设备返回，以保证没有发生配对之后手机断开造成电脑注册了手机却没有登记电脑信息的情况？
            var payload = Convert.ToBase64String(CryptoTools.EncryptAES(Encoding.UTF8.GetBytes(computerInfo), PairEncryptKey));
            var bytes = Encoding.UTF8.GetBytes(PairingFinishPrefix + payload);
            new UdpClient(DevicePairingResultPort).SendAsync(bytes, bytes.Length, previousResult.DeviceEndPoint);
        }
    }

    class DeviceAuthContext
    {
        public const int DeviceAuthMulticastPort = 26819;
        public readonly IPAddress DeviceAuthMulticastGroupAddress = IPAddress.Parse("225.67.76.68");
        public readonly IPEndPoint DeviceAuthMulticastEndpoint = new IPEndPoint(DeviceAuthMulticastPort, 26820);
        public const string CheckExistsPrefix = "wingb://auth_exists?";
        public const string CheckExistsResponsePrefix = "wingb://auth_exists_resp?";
        public const string AuthPrefix = "wingb://auth_req?";

        public readonly Guid DeviceId;
        public readonly byte[] DeviceKey;
        public readonly byte[] AuthKey;

        public DeviceAuthContext(Guid deviceId, byte[] deviceKey, byte[] authKey)
        {
            DeviceId = deviceId;
            DeviceKey = deviceKey;
            AuthKey = authKey;
        }

        public async Task WaitForDeviceAsync()
        {
            var udpClient = new UdpClient(DeviceAuthMulticastPort) { EnableBroadcast = false };
            udpClient.JoinMulticastGroup(DeviceAuthMulticastGroupAddress);

            var payload = Convert.ToBase64String(DeviceId.ToByteArray());
            var data = Encoding.UTF8.GetBytes(CheckExistsPrefix + payload);
            await udpClient.SendAsync(data, data.Length, DeviceAuthMulticastEndpoint);
            
            while (true)
            {
                var result = await udpClient.ReceiveAsync();
                var str = Encoding.UTF8.GetString(result.Buffer);
                if (str.Length <= CheckExistsResponsePrefix.Length || !str.StartsWith(CheckExistsResponsePrefix)) continue;

                payload = str.Substring(CheckExistsResponsePrefix.Length + 1);
                var deviceIdBytes = Convert.FromBase64String(payload);
                if (deviceIdBytes.Length != Utils.GuidLength) continue;

                Guid deviceId = new Guid(deviceIdBytes);
                if (deviceId == DeviceId) return;
            }
        }

        public async Task RequestAuthAsync()
        {

        }
    }

    class PairingException: Exception
    {
        public PairingException()
        {

        }

        public PairingException(string message): base(message)
        {
            
        }
    }

    class DevicePairingResult
    {
        public string DeviceFriendName, DeviceModelName;
        public IPEndPoint DeviceEndPoint;
    }
}
