﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using GalaSoft.MvvmLight.Messaging;
using ZXing;
using ZXing.QrCode;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace WindowsGoodbye
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class PairingPage : Page
    {
        
        private readonly BarcodeWriter _barcodeWriter;

        public PairingPage()
        {
            this.InitializeComponent();
            _barcodeWriter = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    DisableECI = true,
                    CharacterSet = "UTF-8",
                    Width = (int) QRCode.Width,
                    Height = (int) QRCode.Height
                }
            };
        }
        
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (DevicePairingContext.ActiveDevicePairingContext != null)
                DevicePairingContext.ActiveDevicePairingContext.TerminatePairing();

            var context = new DevicePairingContext();
            DevicePairingContext.ActiveDevicePairingContext = context;

            var qrData = context.GeneratePairData();
            QRCode.Source = GenerateQRCode(qrData);

            Messenger.Default.Register<PairDeviceDetectedMessage>(this, true, async msg =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                 {
                     var resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
                     WaitingText.Text = string.Format(resourceLoader.GetString("WaitingText/Text"),
                         msg.DeviceFriendlyName, msg.DeviceModelName);
                     QRCodePanel.Visibility = Visibility.Collapsed;
                     ProgressRing.IsActive = true;
                     WaitingPanel.Visibility = Visibility.Visible;
                     Messenger.Default.Unregister(this);
                 });
            });

            context.StartListening();
            QRCodePanel.Visibility = Visibility.Visible;

            ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.Sleep(5000);
                Messenger.Default.Send(new PairDeviceDetectedMessage("假的", "真的是假的"));
            });
        }

        private ImageSource GenerateQRCode(string qrData)
        {
            return _barcodeWriter.Write(qrData);
        }
    }
}
