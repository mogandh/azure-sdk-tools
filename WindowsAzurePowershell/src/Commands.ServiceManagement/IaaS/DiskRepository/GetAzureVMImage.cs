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

namespace Microsoft.WindowsAzure.Commands.ServiceManagement.IaaS.DiskRepository
{
    using System;
    using System.Linq;
    using System.Management.Automation;
    using Helpers;
    using Management.Compute.Models;
    using Model;
    using Utilities.Common;

    [Cmdlet(
        VerbsCommon.Get,
        AzureVMImageNoun),
    OutputType(
        typeof(OSImageContext),
        typeof(VMImageContext))]
    public class GetAzureVMImage : ServiceManagementBaseCmdlet
    {
        protected const string AzureVMImageNoun = "AzureVMImage";

        [Parameter(
            Position = 0,
            ValueFromPipelineByPropertyName = true,
            Mandatory = false,
            HelpMessage = "Name of the image in the image library.")]
        [ValidateNotNullOrEmpty]
        public string ImageName { get; set; }

        [Parameter(
            Position = 1,
            ValueFromPipelineByPropertyName = true,
            Mandatory = false,
            HelpMessage = "Name of the image in the image library.")]
        [ValidateNotNullOrEmpty]
        public string Location { get; set; }

        [Parameter(
            Position = 2,
            ValueFromPipelineByPropertyName = true,
            Mandatory = false,
            HelpMessage = "Name of the image in the image library.")]
        [ValidateNotNullOrEmpty]
        public string Publisher { get; set; }

        [Parameter(
            Position = 3,
            ValueFromPipelineByPropertyName = true,
            Mandatory = false,
            HelpMessage = "Name of the image in the image library.")]
        [ValidateNotNullOrEmpty]
        public string Category { get; set; }


        protected void GetAzureVMImageProcess()
        {
            ServiceManagementProfile.Initialize(this);

            if (string.IsNullOrEmpty(this.ImageName))
            {
                this.ExecuteClientActionNewSM(
                    null,
                    this.CommandRuntime.ToString(),
                    () => this.ComputeClient.VirtualMachineOSImages.List(),
                    (s, response) => response.Images.Select(
                        t => this.ContextFactory<VirtualMachineOSImageListResponse.VirtualMachineOSImage, OSImageContext>(t, s)));

                this.ExecuteClientActionNewSM(
                    null,
                    this.CommandRuntime.ToString(),
                    () => this.ComputeClient.VirtualMachineVMImages.List(),
                    (s, response) => response.VMImages.Select(
                        t => this.ContextFactory<VirtualMachineVMImageListResponse.VirtualMachineVMImage, VMImageContext>(t, s)));
            }
            else
            {
                var imageType = new VirtualMachineImageHelper(this.ComputeClient).GetImageType(this.ImageName);
                bool isOSImage = imageType.HasFlag(VirtualMachineImageType.OSImage);
                bool isVMImage = imageType.HasFlag(VirtualMachineImageType.VMImage);

                if (!isVMImage)
                {
                    this.ExecuteClientActionNewSM(
                        null,
                        this.CommandRuntime.ToString(),
                        () => this.ComputeClient.VirtualMachineOSImages.Get(this.ImageName),
                        (s, t) => this.ContextFactory<VirtualMachineOSImageGetResponse, OSImageContext>(t, s));
                }
                else
                {
                    if (isOSImage)
                    {
                        this.ExecuteClientActionNewSM(
                            null,
                            this.CommandRuntime.ToString(),
                            () => this.ComputeClient.VirtualMachineOSImages.Get(this.ImageName),
                            (s, t) => this.ContextFactory<VirtualMachineOSImageGetResponse, OSImageContext>(t, s));
                    }

                    this.ExecuteClientActionNewSM(
                            null,
                            this.CommandRuntime.ToString(),
                            () =>
                            {
                                if (string.IsNullOrEmpty(this.Location)
                                 && string.IsNullOrEmpty(this.Publisher)
                                 && string.IsNullOrEmpty(this.Category))
                                {
                                    return this.ComputeClient.VirtualMachineVMImages.List();
                                }
                                else
                                {
                                    return this.ComputeClient.VirtualMachineVMImages.ListAndFilter(
                                        this.Location,
                                        this.Publisher,
                                        this.Category);
                                }
                            },
                            (s, response) =>
                            {
                                var imgs = response.VMImages.Where(
                                    t => string.Equals(
                                        t.Name,
                                        this.ImageName,
                                        StringComparison.OrdinalIgnoreCase));

                                return imgs.Select(
                                        t => this.ContextFactory<VirtualMachineVMImageListResponse.VirtualMachineVMImage, VMImageContext>(t, s));
                            });
                }
            }
        }

        protected override void OnProcessRecord()
        {
            GetAzureVMImageProcess();
        }
    }
}
