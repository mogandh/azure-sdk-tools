﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using Microsoft.WindowsAzure.Commands.Utilities.Common;

namespace Microsoft.WindowsAzure.Commands.ServiceManagement.Model
{
    public class GatewayConnectionContext : ManagementOperationContext
    {
        public string ConnectivityState { get; set; }

        public ulong EgressBytesTransferred { get; set; }

        public ulong IngressBytesTransferred { get; set; }

        public string LastConnectionEstablished { get; set; }

        public string LastEventID { get; set; } 

        public string LastEventMessage { get; set; }

        public string LastEventTimeStamp { get; set; } 

        public string LocalNetworkSiteName { get; set; }
    }
}