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

namespace Microsoft.WindowsAzure.Commands.GameServices.Model.Contract
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading.Tasks;

    [DataContract(Namespace = "")]
    public class VmPackageResponse
    {
        /// <summary>
        /// Gets or sets the friendly name of the VM package.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        [DataMember(Name = "name")]
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the VM package ID.
        /// </summary>
        /// <value>
        /// The gsi Id.
        /// </value>
        [DataMember(Name = "gsiId")]
        public Guid? VmPackageId
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets MinRequiredPlayers, which defines the minimum number of players needed in a session hosted by the VM package.
        /// </summary>
        /// <value>
        /// The min required players.
        /// </value>
        [DataMember(Name = "minRequiredPlayers")]
        public int MinRequiredPlayers
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets MaxAllowedPlayers, which defines how many players fits in a session hosted by the VM package.
        /// </summary>
        /// <value>
        /// The max allowed players.
        /// </value>
        [DataMember(Name = "maxRequiredPlayers")]
        public int MaxAllowedPlayers
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the developer facing file name of the cspkg.
        /// </summary>
        /// <value>
        /// The name of the CSPKG file.
        /// </value>
        [DataMember(Name = "cspkgFileName")]
        public string CspkgFileName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the developer facing file name of the cscfg.
        /// </summary>
        /// <value>
        /// The name of the CSCFG file.
        /// </value>
        [DataMember(Name = "cscfgFileName")]
        public string CscfgFileName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the cloud game Id that the VM package belongs to.
        /// </summary>
        /// <value>
        /// The gsi set Id.
        /// </value>
        [DataMember(Name = "gsiSetId")]
        public Guid CloudGameId
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the certificate Ids that the VM package uses and needs to be deployed.
        /// </summary>
        /// <value>
        /// The certificate ids.
        /// </value>
        [DataMember(Name = "certificateIds")]
        public Guid[] CertificateIds
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the associated asset ids.
        /// </summary>
        /// <value>
        /// The asset ids.
        /// </value>
        [DataMember(Name = "gsiAssetIds")]
        public Guid[] AssetIds
        {
            get;
            set;
        }
    }
}
