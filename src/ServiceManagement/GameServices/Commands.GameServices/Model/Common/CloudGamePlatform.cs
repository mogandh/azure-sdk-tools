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

namespace Microsoft.WindowsAzure.Commands.GameServices.Model.Common
{
    /// <summary>
    ///     The cloud game platforms.
    /// </summary>
    public enum CloudGamePlatform
    {
        /// <summary>
        /// The Xbox One compute platform (default)
        /// </summary>
        XboxOne = 0,
        
        /// <summary>
        /// The Xbox 360 compute platform
        /// </summary>
        Xbox360,
        
        /// <summary>
        /// The PC compute platform
        /// </summary>
        PC
    }
}
