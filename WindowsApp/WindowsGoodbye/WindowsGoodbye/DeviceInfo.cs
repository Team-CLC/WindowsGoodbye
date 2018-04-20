﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WindowsGoodbye
{
    class DeviceInfo
    {
        public Guid DeviceId;
        public byte[] DeviceKey, AuthKey;
        public string DeviceFriendlyName, DeviceModelName;
        public string DeviceMacAddress;
    }
}