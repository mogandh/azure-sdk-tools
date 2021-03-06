﻿// ----------------------------------------------------------------------------------
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

namespace Microsoft.WindowsAzure.Commands.GameServices.Model
{
    using Microsoft.WindowsAzure.Commands.Common.Models;
    using Microsoft.WindowsAzure.Commands.GameServices.Model.Common;
    using Microsoft.WindowsAzure.Commands.GameServices.Model.Contract;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading;
    using System.Xml;
    using Microsoft.WindowsAzure.Commands.ServiceManagement.Model;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

    /// <summary>
    ///     Implements ICloudGameClient to use HttpClient for communication
    /// </summary>
    public class CloudGameClient : ICloudGameClient
    {
        public const string CloudGameResourceProviderApiVersion = "2013-09-01";
        private readonly HttpClient _httpClient;
        private readonly HttpClient _httpXmlClient;
        private readonly GameServicesCmdletsInfo _info;

        /// <summary>
        ///     Creates new CloudGameClient.
        /// </summary>
        /// <param name="subscription">The Windows Azure subscription data object</param>
        /// <param name="logger">The logger action</param>
        /// <param name="httpClient">The HTTP Client to use to communicate with RDFE</param>
        /// <param name="httpXmlClient">The HTTP Client for processing XML data</param>
        public CloudGameClient(AzureContext context, Action<string> logger, HttpClient httpClient, HttpClient httpXmlClient)
        {
            Context = context;
            Logger = logger;
            _httpClient = httpClient;
            _httpXmlClient = httpXmlClient;
            _info = ClientHelper.Info;
        }

        /// <summary>
        ///     Creates new CloudGameClient.
        /// </summary>
        /// <param name="subscription">The Windows Azure subscription data object</param>
        /// <param name="logger">The logger action</param>
        public CloudGameClient(AzureContext context, Action<string> logger)
            : this(context, 
                   logger, 
                   ClientHelper.CreateCloudGameHttpClient(context, CloudGameUriElements.ApplicationJsonMediaType, logger), 
                   ClientHelper.CreateCloudGameHttpClient(context, CloudGameUriElements.ApplicationXmlMediaType, logger))
        {
        }

        /// <summary>
        ///     Gets or sets the subscription.
        /// </summary>
        /// <value>
        ///     The subscription.
        /// </value>
        public AzureContext Context { get; set; }

        /// <summary>
        ///     Gets or sets the logger
        /// </summary>
        /// <value>
        ///     The logger.
        /// </value>
        public Action<string> Logger { get; set; }

        /// <summary>
        ///     Gets general information about the cloud game client.
        /// </summary>
        /// <returns></returns>
        public GameServicesCmdletsInfo Info
        {
            get { return _info; }
        }

        /// <summary>
        /// Gets the VM packages.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <returns></returns>
        public async Task<VmPackageCollectionResponse> GetVmPackages(string cloudGameName, CloudGamePlatform platform)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.VmPackagesResourcePath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName);
            var message = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<VmPackageCollectionResponse>(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Upload VM package components to a cloud game.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="packageName">The name of the package.</param>
        /// <param name="maxPlayers">The max number of players allowed.</param>
        /// <param name="assetId">The Id of a previously uploaded asset file.</param>
        /// <param name="certificateIds">The ids of certificates to associate.</param>
        /// <param name="cspkgFileName">The name of the local cspkg file name.</param>
        /// <param name="cspkgStream">The cspkg file stream.</param>
        /// <param name="cscfgFileName">The name of the local cscfg file name.</param>
        /// <param name="cscfgStream">The game cscfg file stream.</param>
        /// <returns>
        /// True if successful.
        /// </returns>
        /// <exception cref="ServiceResponseException"></exception>
        /// <exception cref="ServiceManagementError"></exception>
        public async Task<VmPackagePostResponse> NewVmPackage(
            string cloudGameName,
            CloudGamePlatform platform,
            string packageName,
            int maxPlayers,
            Guid? assetId,
            Guid[] certificateIds,
            string cspkgFileName,
            Stream cspkgStream,
            string cscfgFileName,
            Stream cscfgStream)
        {
            if (cspkgStream.Length == 0 || cscfgStream.Length == 0)
            {
                throw new ArgumentException("File stream must not be empty.");
            }

            certificateIds = certificateIds ?? new Guid[0];
            var requestMetadata = new VmPackageRequest()
            {
                CspkgFilename = Path.GetFileName(cspkgFileName),
                CscfgFilename = Path.GetFileName(cscfgFileName),
                MaxAllowedPlayers = maxPlayers,
                MinRequiredPlayers = 1,
                Name = packageName,
                AssetId = assetId.HasValue ? assetId.Value.ToString() : null,
                CertificateIds = Array.ConvertAll(certificateIds, certId => certId.ToString())
            };

            var platformResourceString = ClientHelper.GetPlatformResourceTypeString(platform);

            VmPackagePostResponse responseMetadata;
            using (var multipartFormContent = new MultipartFormDataContent())
            {
                multipartFormContent.Add(new StringContent(ClientHelper.ToJson(requestMetadata)), "metadata");
                multipartFormContent.Add(new StreamContent(cscfgStream), "packageconfig");

                var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.VmPackagesResourcePath, platformResourceString, cloudGameName);
                var responseMessage = await _httpClient.PostAsync(url, multipartFormContent).ConfigureAwait(false);
                responseMetadata = await ClientHelper.ProcessJsonResponse<VmPackagePostResponse>(responseMessage).ConfigureAwait(false);
            }

            bool uploadSuccess;
            StorageException exception = null;
            try
            {
                // Use the pre-auth URL received in the response to upload the cspkg file. Wait for it to complete
                var cloudblob = new CloudBlockBlob(new Uri(responseMetadata.CspkgPreAuthUrl));
                await Task.Factory.FromAsync(
                    (callback, state) => cloudblob.BeginUploadFromStream(cspkgStream, callback, state),
                    cloudblob.EndUploadFromStream,
                    TaskCreationOptions.None).ConfigureAwait(false);
                uploadSuccess = true;
            }
            catch (StorageException ex)
            {
                // workaround because await cannot be used in a "catch" block
                uploadSuccess = false;
                exception = ex;
            }

            if (!uploadSuccess)
            {
                // Attempt to clean up first
                await this.RemoveVmPackage(cloudGameName, platform, Guid.Parse(responseMetadata.VmPackageId));

                var errorMessage = string.Format("Failed to upload cspkg for cloud game. gameId {0} platform {1} cspkgName {2}", cloudGameName, platformResourceString, cspkgFileName);
                throw ClientHelper.CreateExceptionFromJson((HttpStatusCode)exception.RequestInformation.HttpStatusCode, errorMessage + "\nException: " + exception);
            }

            using (var multipartFormContent = new MultipartFormDataContent())
            {
                multipartFormContent.Add(new StringContent(ClientHelper.ToJson(requestMetadata)), "metadata");
                var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.VmPackageResourcePath, platformResourceString, cloudGameName, responseMetadata.VmPackageId);
                var responseMessage = await _httpClient.PutAsync(url, multipartFormContent).ConfigureAwait(false);
                if (!responseMessage.IsSuccessStatusCode)
                {
                    // Error result, so throw an exception
                    if (responseMessage.StatusCode == HttpStatusCode.Conflict)
                    {
                        throw ClientHelper.CreateExceptionFromJson(responseMessage.StatusCode, "Unable to create VM package. Ensure no other VM packages for this cloud game have the same 'MaxPlayers' value");
                    }

                    throw ClientHelper.CreateExceptionFromJson(responseMessage);
                }
            }

            return responseMetadata;
        }

        /// <summary>
        /// Remove the VM package.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="vmPackageId">The VM package Id.</param>
        /// <returns></returns>
        public async Task<bool> RemoveVmPackage(string cloudGameName, CloudGamePlatform platform, Guid vmPackageId)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.VmPackageResourcePath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName, vmPackageId);
            var responseMessage = await _httpClient.DeleteAsync(url).ConfigureAwait(false);

            if (responseMessage.IsSuccessStatusCode)
            {
                return true;
            }

            // Error result, so throw an exception
            if (responseMessage.StatusCode == HttpStatusCode.Conflict)
            {
                throw ClientHelper.CreateExceptionFromJson(responseMessage.StatusCode, "Unable to remove VM package. Ensure VM package is not currently deployed");
            }

            throw ClientHelper.CreateExceptionFromJson(responseMessage);
        }

        /// <summary>
        /// Gets the game modes associated with a parent schema.
        /// </summary>
        /// <param name="gameModeSchemaId">The game mode schema Id.</param>
        /// <returns></returns>
        public async Task<GameModeCollectionResponse> GetGameModes(Guid gameModeSchemaId)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.GameModesResourcePath, gameModeSchemaId);
            var responseMessage = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<GameModeCollectionResponse>(responseMessage).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Game Mode Schema.
        /// </summary>
        /// <param name="schemaName">The game mode schema name.</param>
        /// <param name="fileName">The game mode schema original filename.</param>
        /// <param name="schemaStream">The game mode schema stream.</param>
        /// <returns></returns>
        public async Task<NewGameModeSchemaResponse> NewGameModeSchema(string schemaName, string fileName, Stream schemaStream)
        {
            if (schemaStream.Length == 0)
            {
                throw new ArgumentException("File stream must not be empty.");
            }

            // Idempotent call to do a first time registration of the subscription-level wrapping container.
            await ClientHelper.RegisterAndCreateContainerResourceIfNeeded(_httpClient, _httpXmlClient).ConfigureAwait(false);

            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.GameModeSchemasResourcePath);
            using (var multipartContent = new MultipartFormDataContent())
            {
                var newGameModeSchema = new GameModeSchema()
                {
                    Name = schemaName,
                    Filename = Path.GetFileName(fileName)
                };

                multipartContent.Add(new StringContent(ClientHelper.ToJson(newGameModeSchema)), "metadata");
                multipartContent.Add(new StreamContent(schemaStream), "variantSchema");

                var responseMessage = await _httpClient.PostAsync(url, multipartContent).ConfigureAwait(false);
                return await ClientHelper.ProcessJsonResponse<NewGameModeSchemaResponse>(responseMessage).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Remove the game mode schema.
        /// </summary>
        /// <param name="gameModeSchemaId">The game mode schema ID.</param>
        /// <returns></returns>
        /// <exception cref="ServiceResponseException"></exception>
        /// <exception cref="ServiceManagementError"></exception>
        public async Task<bool> RemoveGameModeSchema(Guid gameModeSchemaId)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.GameModeSchemaResourcePath, gameModeSchemaId);
            var responseMessage = await _httpClient.DeleteAsync(url).ConfigureAwait(false);
            if (!responseMessage.IsSuccessStatusCode)
            {
                // Error result, so throw an exception
                if (responseMessage.StatusCode == HttpStatusCode.Conflict)
                {
                    throw ClientHelper.CreateExceptionFromJson(responseMessage.StatusCode,
                        "Unable to remove game mode schema. Ensure game mode schema is not currently referenced by any cloud games");
                }

                throw ClientHelper.CreateExceptionFromJson(responseMessage);
            }

            return true;
        }

        /// <summary>
        /// Gets the game mode schemas.
        /// </summary>
        /// <param name="getDetails">If the list of child game modes should be included or not.</param>
        /// <returns></returns>
        public async Task<GameModeSchemaCollectionResponse> GetGameModeSchemas(bool getDetails = false)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.GameModeSchemasGetResourcePath, getDetails);
            var responseMessage = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<GameModeSchemaCollectionResponse>(responseMessage).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Game Mode.
        /// </summary>
        /// <param name="gameModeSchemaId">The parent game mode schema identifier.</param>
        /// <param name="gameModeName">The game mode name.</param>
        /// <param name="gameModeFileName">The game mode original filename.</param>
        /// <param name="gameModeStream">The game mode stream.</param>
        /// <returns></returns>
        public async Task<NewGameModeResponse> NewGameMode(Guid gameModeSchemaId, string gameModeName, string gameModeFileName, Stream gameModeStream)
        {
            if (gameModeStream.Length == 0)
            {
                throw new ArgumentException("File stream must not be empty.");
            }

            // Container resource should already be created if the (required) game mode schema exists,
            // so no need to check that again.
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.GameModesResourcePath, gameModeSchemaId);
            using (var multipartContent = new MultipartFormDataContent())
            {
                var newGameMode = new GameMode()
                {
                    Name = gameModeName,
                    FileName = Path.GetFileName(gameModeFileName)
                };

                multipartContent.Add(new StringContent(ClientHelper.ToJson(newGameMode)), "metadata");
                multipartContent.Add(new StreamContent(gameModeStream), "variant");

                var responseMessage = await _httpClient.PostAsync(url, multipartContent).ConfigureAwait(false);
                return await ClientHelper.ProcessJsonResponse<NewGameModeResponse>(responseMessage).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Remove the game mode.
        /// </summary>
        /// <param name="gameModeSchemaId">The game mode schema ID.</param>
        /// <param name="gameModeId">The game mode ID.</param>
        /// <returns></returns>
        public async Task<bool> RemoveGameMode(Guid gameModeSchemaId, Guid gameModeId)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.GameModeResourcePath, gameModeSchemaId, gameModeId);
            var responseMessage = await _httpClient.DeleteAsync(url).ConfigureAwait(false);
            if (!responseMessage.IsSuccessStatusCode)
            {
                // Error result, so throw an exception
                throw ClientHelper.CreateExceptionFromJson(responseMessage);
            }

            return true;
        }

        /// <summary>
        /// Gets the certificates.
        /// </summary>
        /// <param name="cloudGameId">An optional cloud game ID to filter by.</param>
        /// <returns></returns>
        public async Task<CertificateCollectionResponse> GetCertificates(Guid? cloudGameId)
        {
            var url = _httpClient.BaseAddress + (
                cloudGameId.HasValue ? string.Format(CloudGameUriElements.CertificatesForGameResourcePath, cloudGameId.Value) :
                CloudGameUriElements.CertificatesResourcePath);
            var message = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<CertificateCollectionResponse>(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a certificate.
        /// </summary>
        /// <param name="certificateName">The certificate name.</param>
        /// <param name="certificateFileName">The certificate filename.</param>
        /// <param name="certificatePassword">The certificate password.</param>
        /// <param name="certificateStream">The certificate stream.</param>
        /// <returns></returns>
        public async Task<CertificatePostResponse> NewCertificate(
            string certificateName,
            string certificateFileName,
            string certificatePassword,
            Stream certificateStream)
        {
            if (certificateStream.Length == 0)
            {
                throw new ArgumentException("File stream must not be empty.");
            }

            if (!certificateFileName.EndsWith(".cer", StringComparison.OrdinalIgnoreCase))
            {
                certificateFileName = certificateFileName.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase)
                    ? certificateFileName
                    : certificateFileName + ".pfx";
            }
            else
            {
                if (!string.IsNullOrEmpty(certificatePassword))
                {
                    throw new ArgumentException(".cer certificates cannot include a password");
                }
            }

            // Idempotent call to do a first time registration of the subscription-level wrapping container.
            await ClientHelper.RegisterAndCreateContainerResourceIfNeeded(_httpClient, _httpXmlClient).ConfigureAwait(false);

            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.CertificatesResourcePath);

            var certificate = new CertificateRequest()
            {
                Name = certificateName,
                Filename = Path.GetFileName(certificateFileName),
                Password = certificatePassword
            };

            var multipartContent = new MultipartFormDataContent();
            {
                multipartContent.Add(new StringContent(ClientHelper.ToJson(certificate)), "metadata");
                multipartContent.Add(new StreamContent(certificateStream), "certificate");
                var message = await _httpClient.PostAsync(url, multipartContent).ConfigureAwait(false);
                return await ClientHelper.ProcessJsonResponse<CertificatePostResponse>(message).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Remove the game certificate.
        /// </summary>
        /// <param name="certificateId">The ID of the certificate to be removed.</param>
        /// <returns></returns>
        public async Task<bool> RemoveCertificate(Guid certificateId)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.CertificateResourcePath, certificateId);
            var message = await _httpClient.DeleteAsync(url).ConfigureAwait(false);
            if (!message.IsSuccessStatusCode)
            {
                // Error result, so throw an exception
                if (message.StatusCode == HttpStatusCode.Conflict)
                {
                    throw ClientHelper.CreateExceptionFromJson(message.StatusCode, "Unable to remove certificate. Ensure certificate is not currently referenced by any cloud games");
                }

                throw ClientHelper.CreateExceptionFromJson(message);
            }

            return true;
        }

        /// <summary>
        /// Gets the asset packages.
        /// </summary>
        /// <param name="cloudGameId">An optional cloud game ID to filter by.</param>
        /// <returns></returns>
        public async Task<AssetCollectionResponse> GetAssets(Guid? cloudGameId)
        {
            var url = _httpClient.BaseAddress + (
                cloudGameId.HasValue ? string.Format(CloudGameUriElements.AssetsForGameResourcePath, cloudGameId.Value) :
                CloudGameUriElements.AssetsResourcePath);
            var message = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<AssetCollectionResponse>(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new asset package.
        /// </summary>
        /// <param name="assetName">The asset name.</param>
        /// <param name="assetFileName">The asset filename.</param>
        /// <param name="assetStream">The asset filestream.</param>
        /// <returns></returns>
        /// <exception cref="ServiceResponseException"></exception>
        /// <exception cref="ServiceManagementError"></exception>
        public async Task<AssetPostResponse> NewAsset(string assetName, string assetFileName, Stream assetStream)
        {
            if (assetStream.Length == 0)
            {
                throw new ArgumentException("File stream must not be empty.");
            }

            // Idempotent call to do a first time registration of the subscription-level wrapping container.
            await ClientHelper.RegisterAndCreateContainerResourceIfNeeded(_httpClient, _httpXmlClient).ConfigureAwait(false);

            // Call in to get an AssetID and preauthURL to use for upload of the asset
            var newGameAssetRequest = new AssetRequest()
            {
                Filename = Path.GetFileName(assetFileName),
                Name = assetName
            };

            var multipartFormContent = new MultipartFormDataContent
            {
                {
                    new StringContent(ClientHelper.ToJson(newGameAssetRequest)), "metadata"
                }
            };

            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.AssetsResourcePath);
            var responseMessage = await _httpClient.PostAsync(url, multipartFormContent).ConfigureAwait(false);
            var postAssetResult = await ClientHelper.ProcessJsonResponse<AssetPostResponse>(responseMessage).ConfigureAwait(false);

            bool uploadSuccess;
            StorageException exception = null;
            try
            {
                var cloudblob = new CloudBlockBlob(new Uri(postAssetResult.AssetPreAuthUrl));
                await Task.Factory.FromAsync(
                    (callback, state) => cloudblob.BeginUploadFromStream(assetStream, callback, state),
                    cloudblob.EndUploadFromStream,
                    TaskCreationOptions.None).ConfigureAwait(false);
                uploadSuccess = true;
            }
            catch (StorageException ex)
            {
                // workaround because await cannot be used in a "catch" block
                uploadSuccess = false;
                exception = ex;
            }

            if (!uploadSuccess)
            {
                // Attempt to clean up first
                await this.RemoveAsset(Guid.Parse(postAssetResult.AssetId));

                var errorMessage = string.Format("Failed to upload asset file for CloudGame instance to azure storage. assetId {0}", postAssetResult.AssetId);
                throw ClientHelper.CreateExceptionFromJson((HttpStatusCode)exception.RequestInformation.HttpStatusCode, errorMessage + "\nException: " + exception);
            }

            var multpartFormContentMetadata = new MultipartFormDataContent
            {
                {
                    new StringContent(ClientHelper.ToJson(newGameAssetRequest)),"metadata"
                }
            };

            url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.AssetResourcePath, postAssetResult.AssetId);
            responseMessage = await _httpClient.PutAsync(url, multpartFormContentMetadata).ConfigureAwait(false);

            if (!responseMessage.IsSuccessStatusCode)
            {
                // Error result, so throw an exception
                throw ClientHelper.CreateExceptionFromJson(responseMessage);
            }

            // Return the Asset info
            return postAssetResult;
        }

        /// <summary>
        /// Creates a new game package.
        /// </summary>
        /// <param name="cloudGameName">The cloud game.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="vmPackageId">The parent VM package Id.</param>
        /// <param name="name">The game package name.</param>
        /// <param name="fileName">The game package filename.</param>
        /// <param name="isActive">Whether the game package should be activated or not.</param>
        /// <param name="fileStream">The game package filestream.</param>
        /// <returns></returns>
        public async Task<GamePackagePostResponse> NewGamePackage(
            string cloudGameName,
            CloudGamePlatform platform,
            Guid vmPackageId,
            string name,
            string fileName,
            bool isActive,
            Stream fileStream)
        {
            if (fileStream.Length == 0)
            {
                throw new ArgumentException("File stream must not be empty.");
            }

            // Call in to get a game package ID and preauth URL to use for upload of the game package
            var newGamePackageRequest = new GamePackageRequest()
            {
                Filename = Path.GetFileName(fileName),
                Name = name,
                Active = isActive
            };

            var multipartFormContent = new MultipartFormDataContent
            {
                {
                    new StringContent(ClientHelper.ToJson(newGamePackageRequest)),
                    "metadata"
                }
            };

            var platformResourceString = ClientHelper.GetPlatformResourceTypeString(platform);
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.GamePackagesResourcePath, platformResourceString, cloudGameName, vmPackageId);
            var responseMessage = await _httpClient.PostAsync(url, multipartFormContent).ConfigureAwait(false);
            var postGamePackageResult = await ClientHelper.ProcessJsonResponse<GamePackagePostResponse>(responseMessage).ConfigureAwait(false);

            bool uploadSuccess;
            StorageException exception = null;
            try
            {
                var cloudblob = new CloudBlockBlob(new Uri(postGamePackageResult.GamePackagePreAuthUrl));
                await Task.Factory.FromAsync(
                    (callback, state) => cloudblob.BeginUploadFromStream(fileStream, callback, state),
                    cloudblob.EndUploadFromStream,
                    TaskCreationOptions.None).ConfigureAwait(false);
                uploadSuccess = true;
            }
            catch (StorageException ex)
            {
                // workaround because await cannot be used in a "catch" block
                uploadSuccess = false;
                exception = ex;
            }

            if (!uploadSuccess)
            {
                // Attempt to clean up first
                await this.RemoveGamePackage(cloudGameName, platform, vmPackageId, Guid.Parse(postGamePackageResult.GamePackageId));

                var errorMessage = string.Format("Failed to upload game package file for cloud game instance to azure storage. gameId {0}, platform {1}, vmPackageId, {2} GamePackageId {3}", cloudGameName, platformResourceString, vmPackageId, postGamePackageResult.GamePackageId);
                throw ClientHelper.CreateExceptionFromJson((HttpStatusCode)exception.RequestInformation.HttpStatusCode, errorMessage + "\nException: " + exception);
            }

            var multpartFormContentMetadata = new MultipartFormDataContent
            {
                {
                    new StringContent(ClientHelper.ToJson(newGamePackageRequest)), "metadata"
                }
            };

            url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.GamePackageResourcePath, platformResourceString, cloudGameName, vmPackageId, postGamePackageResult.GamePackageId);
            responseMessage = await _httpClient.PutAsync(url, multpartFormContentMetadata).ConfigureAwait(false);
            if (!responseMessage.IsSuccessStatusCode)
            {
                // Error result, so throw an exception
                throw ClientHelper.CreateExceptionFromJson(responseMessage);
            }

            // Return the CodeFile info
            return postGamePackageResult;
        }

        /// <summary>
        /// Sets values on the game package that need to change.
        /// </summary>
        /// <param name="cloudGameName">The cloud game.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="vmPackageId">The parent VM package Id.</param>
        /// <param name="gamePackageId">The Id of the game package to change.</param>
        /// <param name="name">The game package name.</param>
        /// <param name="fileName">The game package filename.</param>
        /// <param name="isActive">Whether the game package should be activated or not.</param>
        /// <returns></returns>
        public async Task<bool> SetGamePackage(
            string cloudGameName,
            CloudGamePlatform platform,
            Guid vmPackageId,
            Guid gamePackageId,
            string name,
            string fileName,
            bool isActive)
        {
            // Create the new game package metadata
            var gamePackageRequest = new GamePackageRequest()
            {
                Name = name,
                Filename = Path.GetFileName(fileName),
                Active = isActive
            };

            var multpartFormContentMetadata = new MultipartFormDataContent
            {
                {
                    new StringContent(ClientHelper.ToJson(gamePackageRequest)),
                    "metadata"
                }
            };

            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.GamePackageResourcePath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName, vmPackageId, gamePackageId);
            var message = await _httpClient.PutAsync(url, multpartFormContentMetadata).ConfigureAwait(false);
            if (!message.IsSuccessStatusCode)
            {
                // Error result, so throw an exception
                throw ClientHelper.CreateExceptionFromJson(message);
            }

            return true;
        }

        /// <summary>
        /// Gets the game packages.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="vmPackageId">The parent VM package identifier.</param>
        /// <returns>
        /// Collection of game packages
        /// </returns>
        public async Task<GamePackageCollectionResponse> GetGamePackages(string cloudGameName, CloudGamePlatform platform, Guid vmPackageId)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.GamePackagesResourcePath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName, vmPackageId);
            var message = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<GamePackageCollectionResponse>(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Remove the game package.
        /// </summary>
        /// <param name="cloudGameName">The cloud game.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="vmPackageId">The parent VM package Id.</param>
        /// <param name="gamePackageId">The ID of the game package to remove.</param>
        /// <returns>
        /// True if removed, false if not removed.
        /// </returns>
        public async Task<bool> RemoveGamePackage(string cloudGameName, CloudGamePlatform platform, Guid vmPackageId, Guid gamePackageId)
        {
            string url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.GamePackageResourcePath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName, vmPackageId, gamePackageId);
            var message = await _httpClient.DeleteAsync(url).ConfigureAwait(false);
            if (!message.IsSuccessStatusCode)
            {
                // Error result, so throw an exception
                if (message.StatusCode == HttpStatusCode.Conflict)
                {
                    throw ClientHelper.CreateExceptionFromJson(message.StatusCode, "Unable to remove game package. Ensure game package is not currently in use by any cloud games");
                }

                throw ClientHelper.CreateExceptionFromJson(message);
            }

            return true;
        }

        /// <summary>
        /// Remove the asset package.
        /// </summary>
        /// <param name="assetId">The ID of the asset to be removed.</param>
        /// <returns></returns>
        /// <exception cref="ServiceResponseException"></exception>
        /// <exception cref="ServiceManagementError"></exception>
        public async Task<bool> RemoveAsset(Guid assetId)
        {
            string url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.AssetResourcePath, assetId);
            var message = await _httpClient.DeleteAsync(url).ConfigureAwait(false);
            if (!message.IsSuccessStatusCode)
            {
                // Error result, so throw an exception
                if (message.StatusCode == HttpStatusCode.Conflict)
                {
                    throw ClientHelper.CreateExceptionFromJson(message.StatusCode, "Unable to remove asset. Ensure asset is not currently referenced by any cloud games");
                }

                throw ClientHelper.CreateExceptionFromJson(message);
            }

            return true;
        }

        /// <summary>
        /// Gets the compute summary report.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <returns></returns>
        public async Task<DashboardSummary> GetComputeSummaryReport(string cloudGameName, CloudGamePlatform platform)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.DashboardSummaryPath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName);
            var message = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<DashboardSummary>(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the deployments report.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <returns></returns>
        public async Task<DeploymentData> GetComputeDeploymentsReport(string cloudGameName, CloudGamePlatform platform)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.DeploymentsReportPath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName);
            var message = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<DeploymentData>(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the service pools report.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <returns></returns>
        public async Task<PoolData> GetComputePoolsReport(string cloudGameName, CloudGamePlatform platform)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.ServicepoolsReportPath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName);
            var message = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<PoolData>(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new cloud game resource.
        /// </summary>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="titleId">The title ID within the subscription to use (in Decimal form)</param>
        /// <param name="selectionOrder">The selection order to use</param>
        /// <param name="sandboxes">A comma seperated list of sandbox names</param>
        /// <param name="resourceSetIds">A comma seperated list of resource set IDs</param>
        /// <param name="name">The name of the Cloud Game</param>
        /// <param name="schemaId">The Id of an existing variant schema</param>
        /// <param name="schemaName">The name of the game mode schema to use if a schema Id is not specified.</param>
        /// <param name="schemaFileName">The local schema file name (only used for reference) if a schema Id is not specified.</param>
        /// <param name="schemaStream">The schema data as a file stream, used if a schema Id is not specified.</param>\
        /// <param name="tags">The tags for the cloud game.</param>
        /// <returns>
        /// The cloud task for completion
        /// </returns>
        public async Task<bool> NewCloudGame(
            CloudGamePlatform platform,
            string titleId,
            int selectionOrder,
            string[] sandboxes,
            string[] resourceSetIds,
            string name,
            Guid? schemaId,
            string schemaName,
            string schemaFileName,
            Stream schemaStream,
            Hashtable tags)
        {
            var platformResourceString = ClientHelper.GetPlatformResourceTypeString(platform);

            // Idempotent call to do a first time registration of the cloud service wrapping container.
            await ClientHelper.RegisterCloudService(_httpClient, _httpXmlClient, platformResourceString).ConfigureAwait(false);

            GameModeSchemaRequest gameModeSchemaRequestData = null;
            if (!string.IsNullOrEmpty(schemaName))
            {
                // Schema name provided, so must have schemaStream, etc.
                if (schemaStream == null || string.IsNullOrEmpty(schemaFileName) || schemaStream.Length == 0)
                {
                    throw new ServiceResponseException(HttpStatusCode.BadRequest, "Invalid Game Mode Schema values provided.");
                }

                string schemaContent;
                using (var streamReader = new StreamReader(schemaStream))
                {
                    schemaContent = streamReader.ReadToEnd();
                }

                gameModeSchemaRequestData = new GameModeSchemaRequest()
                {
                    Metadata = new GameModeSchema()
                    {
                        Name = schemaName,
                        Filename = Path.GetFileName(schemaFileName),
                        TitleId = titleId
                    },
                    Content = schemaContent
                };
            }

            var cloudGame = new CloudGame()
            {
                Name = name,
                PlatformResourceType = platformResourceString,
                ResourceSets = string.Join(",", resourceSetIds),
                Sandboxes = string.Join(",", sandboxes),
                SchemaName = schemaName,
                TitleId = titleId,
                SelectionOrder = selectionOrder
            };

            List<Tag> tagsList = null;
            if (tags != null && tags.Count > 0)
            {
                tagsList = new List<Tag>();
                foreach (string key in tags.Keys)
                {
                    tagsList.Add(new Tag() { Name = key, Value = (string)tags[key] });
                }
            }

            var putGameRequest = new CloudGameRequest()
            {
                CloudGame = cloudGame,
                Tags = tags == null ? null : tagsList.ToArray()
            };

            // If a schemaID is provided, use that in the request, otherwise, add the schema data contract to the put request
            if (schemaId.HasValue)
            {
                if (schemaFileName != null || schemaStream != null)
                {
                    throw new ServiceResponseException(HttpStatusCode.BadRequest, "Cannot specify both an existing Game Mode Schema ID to reference as well as upload a new Game Mode Schema.");
                }

                cloudGame.SchemaId = schemaId.Value.ToString();
            }
            else if (gameModeSchemaRequestData != null)
            {
                putGameRequest.GameModeSchema = gameModeSchemaRequestData;
            }

            var doc = new XmlDocument();
            var resource = new Resource()
            {
                Name = name,
                ETag = Guid.NewGuid().ToString(),       // BUGBUG What should this ETag value be?
                Plan = string.Empty,
                ResourceProviderNamespace = CloudGameUriElements.NamespaceName,
                Type = platformResourceString,
                SchemaVersion = CloudGameUriElements.SchemaVersion,
                IntrinsicSettings = new XmlNode[]
                {
                    doc.CreateCDataSection(ClientHelper.ToJson(putGameRequest))
                }
            };

            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.CloudGameResourcePath, platformResourceString, name);
            var initialResponse = await _httpClient.PutAsXmlAsync(url, resource).ConfigureAwait(false);
            var opStatus = await ClientHelper.PollOperationStatus(initialResponse, _httpXmlClient, 2, 30, Logger);
            return opStatus.Status == WindowsAzure.OperationStatus.Succeeded; // Note: timeout treated as failure
        }

        /// <summary>
        /// Removes a cloud game instance.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="checkStateFirst">if set to <c>true</c> check state first.</param>
        /// <returns></returns>
        /// <exception cref="ServiceResponseException">Invalid cloud game status. Cloud game may not be Deploying/Stopping or Deployed.</exception>
        public async Task<bool> RemoveCloudGame(string cloudGameName, CloudGamePlatform platform, bool checkStateFirst = true)
        {
            if (checkStateFirst)
            {
                CloudGame gameInfo = null;
                try
                {
                    gameInfo = this.GetCloudGame(cloudGameName, platform).Result;
                }
                catch (ServiceResponseException)
                {
                    // 404s will be caught when we talk with RDFE, so no need to do anything here
                }

                if (gameInfo != null &&
                       (string.Equals(gameInfo.Status, "Deployed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(gameInfo.Status, "Deploying", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(gameInfo.Status, "Stopping", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(gameInfo.Status, "Failed", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ServiceResponseException(HttpStatusCode.Conflict, "Invalid cloud game status. Cloud game may not be Deploying, Stopping, Deployed, or Failed. Try stopping the cloud game first.");
                }
            }

            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.CloudGameResourcePath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName);
            var initialResponse = await _httpClient.DeleteAsync(url).ConfigureAwait(false);
            if (initialResponse.StatusCode == HttpStatusCode.NotFound)
            {
                // Already removed
                return true;
            }

            var opStatus = await ClientHelper.PollOperationStatus(initialResponse, _httpXmlClient, 1, 20, Logger);
            return opStatus.Status == WindowsAzure.OperationStatus.Succeeded; // Note: timeout treated as failure
        }

        /// <summary>
        /// Gets the cloud game instances for the Azure Game Services resource in the current subscription
        /// </summary>
        /// <param name="tags">The tags if available.</param>
        /// <returns></returns>
        public async Task<CloudGameColletion> GetCloudGames(Hashtable tags)
        {
            var url = _httpClient.BaseAddress + CloudGameUriElements.GetCloudServicesResourcePath;
            var message = await _httpXmlClient.GetAsync(url).ConfigureAwait(false);
            var games = await ClientHelper.ProcessCloudServiceResponse(this, message).ConfigureAwait(false);

            if (tags == null || tags.Count == 0 || games == null || games.Count == 0)
            {
                return games;
            }

            var filteredCollection = new CloudGameColletion();
            foreach (var game in games)
            {
                var allMatch = true;
                foreach (var tagKeyObj in tags.Keys)
                {
                    var tagKey = (string)tagKeyObj;
                    var tagVal = (string)tags[tagKey];
                    if (!game.Tags.Any((tag) => tag.Name == tagKey && tag.Value == tagVal))
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch)
                {
                    filteredCollection.Add(game);
                }
            }

            return filteredCollection;
        }

        /// <summary>
        /// Gets a cloud game.
        /// </summary>
        /// <param name="cloudGameName">Name of the cloud game.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <returns></returns>
        public async Task<CloudGame> GetCloudGame(string cloudGameName, CloudGamePlatform platform)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.CloudGameResourceInfoPath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName);
            var cloudGameResponseMessage = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<CloudGame>(cloudGameResponseMessage).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the AzureGameServicesProperties for the current subscription
        /// </summary>
        /// <returns>
        /// The task for completion.
        /// </returns>
        public async Task<AzureGameServicesPropertiesResponse> GetAzureGameServicesProperties()
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.ResourcePropertiesPath);

            var message = await _httpXmlClient.GetAsync(url).ConfigureAwait(false);
            var propertyList = await ClientHelper.ProcessXmlResponse<ResourceProviderProperties>(message).ConfigureAwait(false);

            if (propertyList == null)
            {
                return null;
            }

            var property = propertyList.Find((prop) => prop.Key == "publisherInfo");
            if (property == null ||
                property.Value == null)
            {
                return null;
            }

            return ClientHelper.DeserializeJsonToObject<AzureGameServicesPropertiesResponse>(property.Value);
        }

        /// <summary>
        /// Publishes the cloud game.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="sandboxes">Optional, string delimitted list of sandboxes to deploy to</param>
        /// <param name="geoRegions">Optional, string delimitted list of geo regions to deploy to</param>
        /// <param name="publishOnly">if set to <c>true</c> publish only, and do not deploy.</param>
        /// <returns>
        /// The task for completion.
        /// </returns>
        public async Task<bool> DeployCloudGame(string cloudGameName, CloudGamePlatform platform, string[] sandboxes, string[] geoRegions, bool publishOnly)
        {
            var sandboxesStr = string.Join(",", sandboxes);
            var regionsStr = string.Join(",", geoRegions);
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.DeployCloudGamePath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName, sandboxesStr, regionsStr, !publishOnly);
            var message = await _httpClient.PutAsync(url, null).ConfigureAwait(false);
            if (!message.IsSuccessStatusCode)
            {
                // Error result, so throw an exception
                throw ClientHelper.CreateExceptionFromJson(message);
            }

            return true;
        }

        /// <summary>
        /// Stops the cloud game.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="unpublishOnly">if set to <c>true</c> unpublish only, and do not attempt to destroy pools.</param>
        /// <returns>
        /// The task for completion.
        /// </returns>
        public async Task<bool> StopCloudGame(string cloudGameName, CloudGamePlatform platform, bool unpublishOnly)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.StopCloudGamePath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName, !unpublishOnly);
            var message = await _httpClient.PutAsync(url, null).ConfigureAwait(false);
            if (!message.IsSuccessStatusCode)
            {
                // Error result, so throw an exception
                if (message.StatusCode == HttpStatusCode.Conflict)
                {
                    throw ClientHelper.CreateExceptionFromJson(message.StatusCode,
                        "Unable to stop cloud game. Cloud game must be in the Deployed, Deploying, Stopping, or Failed state to stop");
                }

                throw ClientHelper.CreateExceptionFromJson(message);
            }

            return true;
        }

        /// <summary>
        /// Gets the list of available diagnostic log files for the specific instance.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="instanceId">The Id of the instance to get log files for.</param>
        /// <param name="geoRegion">The geo region of the instance (if known).</param>
        /// <returns> A list of URIs to download individual log files.</returns>
        public async Task<EnumerateDiagnosticFilesResponse> GetLogFiles(string cloudGameName, CloudGamePlatform platform, string instanceId, string geoRegion)
        {
            var platformString = ClientHelper.GetPlatformResourceTypeString(platform);
            var url = new StringBuilder(_httpClient.BaseAddress + string.Format(CloudGameUriElements.LogFilePath, platformString, cloudGameName, instanceId));
            if (!string.IsNullOrEmpty(geoRegion))
            {
                url.AppendFormat("/?geoRegion={0}", geoRegion);
            }

            var message = await _httpClient.GetAsync(url.ToString()).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<EnumerateDiagnosticFilesResponse>(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the list of available diagnostic dump files for the specific instance.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="instanceId">The Id of the instance to get dump files for.</param>
        /// <param name="geoRegion">The geo region of the instance (if known).</param>
        /// <returns>A list of URIs to download individual dump files.</returns>
        public async Task<EnumerateDiagnosticFilesResponse> GetDumpFiles(string cloudGameName, CloudGamePlatform platform, string instanceId, string geoRegion)
        {
            var platformString = ClientHelper.GetPlatformResourceTypeString(platform);
            var url = new StringBuilder(_httpClient.BaseAddress + string.Format(CloudGameUriElements.DumpFilePath, platformString, cloudGameName, instanceId));
            if (!string.IsNullOrEmpty(geoRegion))
            {
                url.AppendFormat("/?geoRegion={0}", geoRegion);
            }

            var message = await _httpClient.GetAsync(url.ToString()).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<EnumerateDiagnosticFilesResponse>(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the list of clusters.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="geoRegion">The (optional) region(s) to enumerate clusters from (comma-separated).</param>
        /// <param name="status">The (optional) status to filter on.</param>
        /// <param name="clusterId">The (optional) cluster ID to query for.</param>
        /// <param name="agentId">The (optional) agent ID to query for.</param>
        /// <returns>
        /// A list of clusters that match the region and status filter
        /// </returns>
        public async Task<EnumerateClustersResponse> GetClusters(string cloudGameName, CloudGamePlatform platform, string geoRegion, string status, string clusterId, string agentId)
        {
            const string allQuery = "All";
            if (string.IsNullOrEmpty(geoRegion))
            {
                geoRegion = allQuery;
            }

            if (string.IsNullOrEmpty(status))
            {
                status = allQuery;
            }

            var url = new StringBuilder(_httpClient.BaseAddress +
                string.Format(CloudGameUriElements.EnumerateClustersPath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName, geoRegion, status));
            if (!string.IsNullOrEmpty(clusterId))
            {
                url.AppendFormat("&clusterId={0}", clusterId);
            }

            if (!string.IsNullOrEmpty(agentId))
            {
                url.AppendFormat("&agentId={0}", agentId);
            }

            var message = await _httpClient.GetAsync(url.ToString()).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<EnumerateClustersResponse>(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the monitoring counter names.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <returns>A list of monitoring counter names.</returns>
        public async Task<List<string>> GetComputeMonitoringCounters(string cloudGameName, CloudGamePlatform platform)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.MonitoringCountersPath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName);
            var message = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<List<string>>(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the compute monitoring counter data.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="geoRegionName">Name of the geo region.</param>
        /// <param name="startTime">The start time.</param>
        /// <param name="endTime">The end time.</param>
        /// <param name="timeZoom">The time zoom.</param>
        /// <param name="counterNames">The selected counters.</param>
        /// <returns>The counter data for the selected counters.</returns>
        public async Task<CounterChartDataResponse> GetComputeMonitoringCounterData(
            string cloudGameName,
            CloudGamePlatform platform,
            string geoRegionName,
            DateTime startTime,
            DateTime endTime,
            TimeSpan timeZoom,
            string[] counterNames)
        {
            var counterNamesString = string.Join(",", counterNames);
            var url = _httpClient.BaseAddress + string.Format(
                CloudGameUriElements.MonitoringCounterDataPath,
                ClientHelper.GetPlatformResourceTypeString(platform),
                cloudGameName,
                startTime.ToString("s"), // Converts to ISO 8601 string
                endTime.ToString("s"),
                (int)timeZoom.TotalSeconds,
                geoRegionName,
                counterNamesString);

            var message = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<CounterChartDataResponse>(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Configures the cloud game properties.
        /// </summary>
        /// <param name="cloudGameName">The cloud game name.</param>
        /// <param name="platform">The cloud game platform.</param>
        /// <param name="resourceSets">The resource set IDs.</param>
        /// <param name="sandboxes">The sandboxes.</param>
        /// <returns>
        /// The task for completion.
        /// </returns>
        public async Task<bool> ConfigureCloudGame(string cloudGameName, CloudGamePlatform platform, string[] resourceSets, string[] sandboxes)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.ConfigureGamePath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName);

            var configureContract = new CloudGameConfiguration()
            {
                ResourceSets = string.Join(",", resourceSets),
                Sandboxes = string.Join(",", sandboxes)
            };

            var message = await _httpClient.PutAsJsonAsync(url, configureContract).ConfigureAwait(false);
            if (!message.IsSuccessStatusCode)
            {
                // Error result, so throw an exception
                throw ClientHelper.CreateExceptionFromJson(message);
            }

            return true;
        }

        /// <summary>
        /// Repairs the session host.
        /// </summary>
        /// <param name="cloudGameName">Name of the cloud game.</param>
        /// <param name="platform">The platform.</param>
        /// <param name="sessionHostId">The session host identifier.</param>
        /// <returns></returns>
        public async Task<bool> RepairSessionHost(string cloudGameName, CloudGamePlatform platform, string sessionHostId)
        {
            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.RepairSessionHostPath, ClientHelper.GetPlatformResourceTypeString(platform), cloudGameName, sessionHostId);

            var message = await _httpClient.PutAsJsonAsync(url, string.Empty).ConfigureAwait(false);
            if (!message.IsSuccessStatusCode)
            {
                // Error result, so throw an exception
                throw ClientHelper.CreateExceptionFromJson(message);
            }

            return true;
        }

        /// <summary>
        /// Creates a new Insights configuration item.
        /// </summary>
        /// <param name="targetName">Name of the target.</param>
        /// <param name="targetType">Type of the target.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <returns></returns>
        public async Task<bool> NewInsightsConfigItem(string targetName, string targetType, string connectionString)
        {
            // Idempotent call to do a first time registration of the subscription-level wrapping container.
            await ClientHelper.RegisterAndCreateContainerResourceIfNeeded(_httpClient, _httpXmlClient).ConfigureAwait(false);

            var newInsightsConfigRequest = new InsightsConfigItem()
            {
                TargetName = targetName,
                TargetType = targetType,
                ConnectionString = connectionString
            };

            var multipartFormContent = new MultipartFormDataContent
            {
                {
                    new StringContent(ClientHelper.ToJson(newInsightsConfigRequest)), "metadata"
                }
            };

            var url = _httpClient.BaseAddress + CloudGameUriElements.InsightsResourcePath;
            var responseMessage = await _httpClient.PostAsync(url, multipartFormContent).ConfigureAwait(false);
            return ClientHelper.ProcessBooleanJsonResponse(responseMessage);
        }

        /// <summary>
        /// Gets the insights configuration items.
        /// </summary>
        /// <returns></returns>
        public async Task<InsightsConfigItemsResponse> GetInsightsConfigItems()
        {
            var url = _httpClient.BaseAddress + CloudGameUriElements.InsightsResourcePath;
            var message = await _httpClient.GetAsync(url).ConfigureAwait(false);
            return await ClientHelper.ProcessJsonResponse<InsightsConfigItemsResponse>(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the Insights configuration item.
        /// </summary>
        /// <param name="targetName">Name of the target.</param>
        /// <param name="targetType">Type of the target.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <returns></returns>
        public async Task<bool> SetInsightsConfigItem(string targetName, string targetType, string connectionString)
        {
            // Idempotent call to do a first time registration of the subscription-level wrapping container.
            await ClientHelper.RegisterAndCreateContainerResourceIfNeeded(_httpClient, _httpXmlClient).ConfigureAwait(false);

            var newInsightsConfigRequest = new InsightsConfigItem()
            {
                TargetName = targetName,
                TargetType = targetType,
                ConnectionString = connectionString
            };

            var multipartFormContent = new MultipartFormDataContent
            {
                {
                    new StringContent(ClientHelper.ToJson(newInsightsConfigRequest)), "metadata"
                }
            };

            var url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.InsightsItemResourcePath, targetName);
            var responseMessage = await _httpClient.PutAsync(url, multipartFormContent).ConfigureAwait(false);
            return ClientHelper.ProcessBooleanJsonResponse(responseMessage);
        }

        /// <summary>
        /// Removes the Insights configuration item.
        /// </summary>
        /// <param name="targetName">Name of the target.</param>
        /// <returns></returns>
        public async Task<bool> RemoveInsightsConfigItem(string targetName)
        {
            // Idempotent call to do a first time registration of the subscription-level wrapping container.
            await ClientHelper.RegisterAndCreateContainerResourceIfNeeded(_httpClient, _httpXmlClient).ConfigureAwait(false);

            string url = _httpClient.BaseAddress + string.Format(CloudGameUriElements.InsightsItemResourcePath, targetName);
            var responseMessage = await _httpClient.DeleteAsync(url).ConfigureAwait(false);
            return ClientHelper.ProcessBooleanJsonResponse(responseMessage);
        }
    }
}
