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

namespace Microsoft.WindowsAzure.Commands.CloudGame.BackCompat
{
    using System;
    using System.Management.Automation;
    using Utilities.CloudGame.BackCompat;

    /// <summary>
    /// Remove the cloud game mode.
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "AzureGameServicesXblGameMode"), OutputType(typeof(bool))]
    [Obsolete("This cmdlet is obsolete. Please use Remove-AzureGameServicesGameMode instead.")]
    public class RemoveAzureGameServicesXblGameModeCommand : AzureGameServicesHttpClientCommandBase
    {
        [Parameter(Position = 0, Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "The Xbox Live compute instance name.")]
        [ValidateNotNullOrEmpty]
        public string XblComputeName { get; set; }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "The GameMode Id.")]
        [ValidateNotNullOrEmpty]
        public System.Guid GameModeId { get; set; }

        [Parameter(HelpMessage = "Do not confirm deletion of game mode.")]
        public SwitchParameter Force { get; set; }

        public IXblComputeClient Client { get; set; }

        protected override void Execute()
        {
            ConfirmAction(Force.IsPresent,
                          string.Format("GameMode ID:{0} will be deleted by this action.", GameModeId),
                          string.Empty,
                          string.Empty,
                          () =>
                          {
                              Client = Client ?? new XblComputeClient(CurrentSubscription, WriteDebugLog);
                              var result = Client.RemoveXblGameMode(XblComputeName, GameModeId).Result;
                              WriteObject(result);
                          });
        }
    }
}