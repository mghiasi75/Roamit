﻿using Newtonsoft.Json;
using QuickShare.Common;
using QuickShare.Common.Rome;
using QuickShare.DevicesListManager;
using QuickShare.Rome;
using QuickShare.UWP.Rome;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;

namespace QuickShare.UWP.Rome
{
    public class AndroidRomePackageManager : IRomePackageManager
    {
        //Singleton class
        static AndroidRomePackageManager _instance = null;
        public static AndroidRomePackageManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new AndroidRomePackageManager();

                return _instance;
            }
        }

        public enum AndroidPackageManagerMode
        {
            MessageCarrier = 1,
            PushNotification = 2,
        }

        public AndroidPackageManagerMode Mode { get; set; } = AndroidPackageManagerMode.PushNotification;

        public bool HasWaitingMessageCarrier { get; private set; }

        readonly int _maxRetryCount = 4;
        readonly double _maxSecondsForCarrier = 5.0;
        readonly int _maxWaitTime = 180;

        List<PackageManagerSendQueueItem> sendQueue = new List<PackageManagerSendQueueItem>();
        static SemaphoreSlim sendQueueSemaphore = new SemaphoreSlim(1, 1);

        NormalizedRemoteSystem nrs = null;
        string userId;
        List<string> whosNotMe;

        private AndroidRomePackageManager() { }

        static Guid latestCarrierCode;

        public async Task MessageCarrierReceivedAsync(AppServiceRequest request)
        {
            Guid guid = Guid.NewGuid();
            latestCarrierCode = guid;
            int counter = 0;

            HasWaitingMessageCarrier = true;

            while (true)
            {
                await sendQueueSemaphore.WaitAsync();

                var queueItem = sendQueue.FirstOrDefault(x => x.RemoteSystemId == (string)request.Message["SenderId"]);

                if (queueItem == null)
                {
                    sendQueueSemaphore.Release();

                    if (latestCarrierCode != guid)
                    {
                        Debug.WriteLine($"A newer carrier is here. I will retire now. I was waiting for {counter} cycles.");
                        break;
                    }
                    else if (counter > _maxWaitTime)
                    {
                        Debug.WriteLine($"Okay I've waited {counter} cycles, that's enough. Bye!");
                        break;
                    }

                    Debug.WriteLine($"Queue is empty. Message Carrier is waiting for some message to arrive... {counter}");
                    await Task.Delay(1000);
                    counter++;
                    continue;
                }

                var vs = queueItem.Data.ToValueSet();
                var result = await request.SendResponseAsync(vs);
                queueItem.SetSendResult((RomeAppServiceResponseStatus)result);
                sendQueue.Remove(queueItem);
                sendQueueSemaphore.Release();

                HasWaitingMessageCarrier = false;

                break;
            }
        }

        public async Task<RomeAppServiceResponse> Send(Dictionary<string, object> data)
        {
            if (Mode == AndroidPackageManagerMode.PushNotification)
                return await SendViaPushNotification(data);
            else
                return await SendViaMessageCarrier(data);
        }

        private async Task<RomeAppServiceResponse> SendViaPushNotification(Dictionary<string, object> data)
        {
            var dataJson = JsonConvert.SerializeObject(data);

            bool result = await Common.Service.Device.SendMessage(userId, nrs.Id, dataJson);

            if (result)
                return new RomeAppServiceResponse
                {
                    Message = null,
                    Status = RomeAppServiceResponseStatus.Success,
                };
            else
                return new RomeAppServiceResponse
                {
                    Message = null,
                    Status = RomeAppServiceResponseStatus.Failure,
                };
        }

        private async Task<RomeAppServiceResponse> SendViaMessageCarrier(Dictionary<string, object> data)
        {
            int tryCount = 0;

            var item = new PackageManagerSendQueueItem
            {
                RemoteSystemId = nrs.Id,
                Data = data,
            };

            await sendQueueSemaphore.WaitAsync();
            sendQueue.Add(item);
            sendQueueSemaphore.Release();

            TaskCompletionSource<RomeAppServiceResponseStatus> tcs = new TaskCompletionSource<RomeAppServiceResponseStatus>();

            item.SendFinished += (e) =>
            {
                tcs.SetResult(e.ResponseStatus);
            };

            RomeAppServiceResponseStatus result = RomeAppServiceResponseStatus.RemoteSystemUnavailable;

            Debug.WriteLine("Waiting for Message Carrier to arrive...");
            while (tryCount < _maxRetryCount)
            {
                result = await tcs.Task.WithTimeout(TimeSpan.FromSeconds(_maxSecondsForCarrier), RomeAppServiceResponseStatus.Unknown);

                //Timeout
                if (result == RomeAppServiceResponseStatus.Unknown)
                {
                    if (tryCount < _maxRetryCount)
                    {
                        Debug.WriteLine("Message Carrier timeout, will retry...");
                        var connectResult = await Connect();

                        if (connectResult != RomeAppServiceConnectionStatus.Success)
                        {
                            Debug.WriteLine("Can't connect.");
                            result = RomeAppServiceResponseStatus.RemoteSystemUnavailable;
                        }
                        else
                        {
                            tryCount++;
                            continue;
                        }
                    }
                    else
                    {
                        result = RomeAppServiceResponseStatus.RemoteSystemUnavailable;
                        Debug.WriteLine("Message Carrier didn't arrive :(");
                    }
                }
                else
                {
                    Debug.WriteLine("Message Carrier arrived.");
                }

                break;
            }

            sendQueue.Remove(item);
            return new RomeAppServiceResponse
            {
                Message = new Dictionary<string, object>(),
                Status = result,
            };
        }

        public async Task<RomeAppServiceConnectionStatus> Connect(NormalizedRemoteSystem remoteSystem, string _userId, IEnumerable<string> _whosNotMe)
        {
            nrs = remoteSystem;
            userId = _userId;
            whosNotMe = new List<string>(_whosNotMe);

            return await Connect();
        }

        public async Task<RomeAppServiceConnectionStatus> Connect()
        {
            if (Mode == AndroidPackageManagerMode.PushNotification)
            {
                return RomeAppServiceConnectionStatus.Success;
            }
            else
            {
                bool result = await Common.Service.Device.RequestMessageCarrier(userId, nrs.Id, whosNotMe);

                if (result)
                    return RomeAppServiceConnectionStatus.Success;
                else
                    return RomeAppServiceConnectionStatus.RemoteSystemUnavailable;
            }
        }

        public static async Task<RomeRemoteLaunchUriStatus> LaunchStoreForApp(NormalizedRemoteSystem remoteSystem, string _userId)
        {
            return await LaunchUri(new Uri(Common.Constants.GooglePlayAppUrl), remoteSystem, _userId);
        }

        public static async Task<RomeRemoteLaunchUriStatus> LaunchUri(Uri _uri, NormalizedRemoteSystem _remoteSystem, string _userId)
        {
            bool result = await Common.Service.Device.LaunchUri(_userId, _remoteSystem.Id, _uri);

            if (result)
                return RomeRemoteLaunchUriStatus.Success;
            else
                return RomeRemoteLaunchUriStatus.RemoteSystemUnavailable;
        }

        public static async Task<bool> QuickClipboard(string _text, NormalizedRemoteSystem _remoteSystem, string _userId, string _senderName)
        {
            if (_text.Length > 1024)
                return false;

            bool result = await Common.Service.Device.SendClipboard(_userId, _remoteSystem.Id, _text, _senderName);

            return result;
        }
    }
}
