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

namespace Microsoft.WindowsAzure.Management.ServiceManagement.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Security.Cryptography.X509Certificates;
    using System.Xml;
    using Utilities.Common;
    using WindowsAzure.ServiceManagement;

    /// <summary>
    /// Set Windows Azure Service Diagnostics Extension.
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "AzureServiceDiagnosticsExtension"), OutputType(typeof(ManagementOperationContext))]
    public class SetAzureServiceDiagnosticsExtensionCommand : BaseAzureServiceDiagnosticsExtensionCmdlet
    {
        public SetAzureServiceDiagnosticsExtensionCommand()
            : base()
        {
        }

        public SetAzureServiceDiagnosticsExtensionCommand(IServiceManagement channel)
            : base(channel)
        {
        }

        [Parameter(Position = 0, Mandatory = false, ParameterSetName = "SetExtension", HelpMessage = "Cloud Service Name")]
        [Parameter(Position = 0, Mandatory = false, ParameterSetName = "SetExtensionUsingThumbprint", HelpMessage = "Cloud Service Name")]
        [ValidateNotNullOrEmpty]
        public override string ServiceName
        {
            get;
            set;
        }

        [Parameter(Position = 1, Mandatory = false, ParameterSetName = "SetExtension", HelpMessage = "Production (default) or Staging.")]
        [Parameter(Position = 1, Mandatory = false, ParameterSetName = "SetExtensionUsingThumbprint", HelpMessage = "Production (default) or Staging.")]
        [ValidateSet(DeploymentSlotType.Production, DeploymentSlotType.Staging, IgnoreCase = true)]
        public override string Slot
        {
            get;
            set;
        }

        [Parameter(Position = 2, Mandatory = false, ParameterSetName = "SetExtension", HelpMessage = "Default All Roles, or specify ones for Named Roles.")]
        [Parameter(Position = 2, Mandatory = false, ParameterSetName = "SetExtensionUsingThumbprint", HelpMessage = "Default All Roles, or specify ones for Named Roles.")]
        [ValidateNotNullOrEmpty]
        public override string[] Role
        {
            get;
            set;
        }

        [Parameter(Position = 3, Mandatory = false, ParameterSetName = "SetExtension", HelpMessage = "X509Certificate used to encrypt password.")]
        [ValidateNotNullOrEmpty]
        public override X509Certificate2 X509Certificate
        {
            get;
            set;
        }

        [Parameter(Position = 3, Mandatory = true, ParameterSetName = "SetExtensionUsingThumbprint", HelpMessage = "Thumbprint of a certificate used for encryption.")]
        [ValidateNotNullOrEmpty]
        public override string CertificateThumbprint
        {
            get;
            set;
        }

        [Parameter(Position = 4, Mandatory = true, ParameterSetName = "SetExtensionUsingThumbprint", HelpMessage = "Algorithm associated with the Thumbprint.")]
        [ValidateNotNullOrEmpty]
        public override string ThumbprintAlgorithm
        {
            get;
            set;
        }

        [Parameter(Position = 4, Mandatory = true, ParameterSetName = "SetExtension", HelpMessage = "Diagnostics ConnectionQualifiers")]
        [Parameter(Position = 5, Mandatory = true, ParameterSetName = "SetExtensionUsingThumbprint", HelpMessage = "Diagnostics ConnectionQualifiers")]
        [ValidateNotNullOrEmpty]
        public string ConnectionQualifiers
        {
            get;
            set;
        }

        [Parameter(Position = 5, Mandatory = true, ParameterSetName = "SetExtension", HelpMessage = "Diagnostics DefaultEndpointsProtocol")]
        [Parameter(Position = 6, Mandatory = true, ParameterSetName = "SetExtensionUsingThumbprint", HelpMessage = "Diagnostics DefaultEndpointsProtocol")]
        [ValidateNotNullOrEmpty]
        public string DefaultEndpointsProtocol
        {
            get;
            set;
        }

        [Parameter(Position = 6, Mandatory = true, ParameterSetName = "SetExtension", HelpMessage = "Diagnostics Storage Name")]
        [Parameter(Position = 7, Mandatory = true, ParameterSetName = "SetExtensionUsingThumbprint", HelpMessage = "Diagnostics Storage Name")]
        [ValidateNotNullOrEmpty]
        public string Name
        {
            get;
            set;
        }

        [Parameter(Position = 7, Mandatory = false, ParameterSetName = "SetExtension", HelpMessage = "Diagnostics Configuration")]
        [Parameter(Position = 8, Mandatory = false, ParameterSetName = "SetExtensionUsingThumbprint", HelpMessage = "Diagnostics Configuration")]
        [ValidateNotNullOrEmpty]
        public XmlDocument DiagnosticsConfiguration
        {
            get;
            set;
        }

        [Parameter(Position = 9, Mandatory = false, ParameterSetName = "SetExtension", HelpMessage = "Diagnostics StorageKey")]
        [Parameter(Position = 10, Mandatory = false, ParameterSetName = "SetExtensionUsingThumbprint", HelpMessage = "Diagnostics StorageKey")]
        [ValidateNotNullOrEmpty]
        public string StorageKey
        {
            get;
            set;
        }

        protected override void ValidateParameters()
        {
            ValidateService();
            ValidateDeployment();
            ValidateRoles();
            ValidateThumbprint(true);
        }

        public void ExecuteCommand()
        {
            ValidateParameters();
            ExtensionConfigurationContext context = new ExtensionConfigurationContext
            {
                ProviderNameSpace = ExtensionNameSpace,
                Type = ExtensionType,
                CertificateThumbprint = CertificateThumbprint,
                ThumbprintAlgorithm = ThumbprintAlgorithm,
                X509Certificate = X509Certificate,
                PublicConfiguration = string.Format(PublicConfigurationXmlTemplate.ToString(), ConnectionQualifiers, DefaultEndpointsProtocol, Name, DiagnosticsConfiguration != null ? DiagnosticsConfiguration.InnerXml : ""),
                PrivateConfiguration = string.Format(PrivateConfigurationXmlTemplate.ToString(), StorageKey),
                Roles = Role != null && Role.Any() ? Role.Select(r => new ExtensionRole(r)).ToList() : new ExtensionRole[] { new ExtensionRole() }.ToList()
            };
            var extConfig = Deployment.ExtensionConfiguration;
            ExtensionManager.InstallExtension(context, Slot, ref extConfig);
            ChangeDeployment(ExtensionManager.GetBuilder().Add(extConfig).ToConfiguration());
        }

        protected override void OnProcessRecord()
        {
            ExecuteCommand();
        }
    }
}
