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
    using System.Runtime.Serialization;

    [DataContract(Namespace = "")]
    public sealed class InsightsConfigItem
    {
        /// <summary>
        /// Gets or sets the name of the target.
        /// </summary>
        /// <value>
        /// The name of the target.
        /// </value>
        [DataMember(Name = "targetName")]
        public string TargetName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the type of the target.
        /// </summary>
        /// <value>
        /// The type of the target.
        /// </value>
        [DataMember(Name = "targetType")]
        public string TargetType
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the connection string.
        /// </summary>
        /// <value>
        /// The connection string.
        /// </value>
        [DataMember(Name = "connectionString")]
        public string ConnectionString
        {
            get;
            set;
        }
    }
}
