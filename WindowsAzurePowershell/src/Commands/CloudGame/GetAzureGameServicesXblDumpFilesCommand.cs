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

namespace Microsoft.WindowsAzure.Management.XblCompute
{
    using Microsoft.WindowsAzure.Commands.Utilities.XblCompute;
    using Microsoft.WindowsAzure.Commands.Utilities.XblCompute.Contract;
    using Microsoft.WindowsAzure.Commands.XblCompute;
    using System.Management.Automation;

    /// <summary>
    /// Get dump file from an instances.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "AzureGameServicesXblDumpFiles"), OutputType(typeof(XblEnumerateDiagnosticFilesResponse))]
    public class GetAzureGameServicesXblDumpFilesCommand : AzureGameServicesHttpClientCommandBase
    {
        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "The Xbox Live compute instance name.")]
        [ValidateNotNullOrEmpty]
        public string XblComputeName { get; set; }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "The instance Id to get dumps from.")]
        [ValidateNotNullOrEmpty]
        public string InstanceId { get; set; }

        public IXblComputeClient Client { get; set; }

        public override void ExecuteCmdlet()
        {
            Client = Client ?? new XblComputeClient(CurrentSubscription, WriteDebug);
            XblEnumerateDiagnosticFilesResponse result = null;

            CatchAggregatedExceptionFlattenAndRethrow(() => { result = Client.GetDumpFiles(XblComputeName, InstanceId).Result; });
            WriteObject(result);
        }
    }
}