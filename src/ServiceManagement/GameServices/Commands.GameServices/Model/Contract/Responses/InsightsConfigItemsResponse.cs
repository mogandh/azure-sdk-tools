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
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract(Namespace = "")]
    public sealed class InsightsConfigItemsResponse
    {
        /// <summary>
        /// Gets or sets the insights configuration items.
        /// </summary>
        /// <value>
        /// The insights configuration items.
        /// </value>
        [DataMember(Name = "configItems", Order = 0)]
        public List<InsightsConfigItem> InsightsConfigItems
        {
            get;
            set;
        }
    }
}
