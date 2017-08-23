﻿//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NationalInstruments.PackageManagement.Core;
using PackageManagement.Sdk;
using Constants = PackageManagement.Sdk.Constants;

namespace PackageManagement
{
    /// <summary>
    /// A Package provider for OneGet.
    ///
    /// Important notes:
    ///    - Required Methods: Not all methods are required; some package providers do not support some features. If the methods isn't used or implemented it should be removed (or commented out)
    ///    - Error Handling: Avoid throwing exceptions from these methods. To properly return errors to the user, use the request.Error(...) method to notify the user of an error conditionm and then return.
    ///    - Communicating with the HOST and CORE: each method takes a Request (in reality, an alias for System.Object), which can be used in one of two ways:
    ///         - use the c# 'dynamic' keyword, and call functions on the object directly.
    ///         - use the <code><![CDATA[ .As<Request>() ]]></code> extension method to strongly-type it to the Request type (which calls upon the duck-typer to generate a strongly-typed wrapper).  The strongly-typed wrapper also implements several helper functions to make using the request object easier.
    /// </summary>
    public class NipkgPackageProvider
    {
        /// <summary>
        /// The features that this package supports.
        /// </summary>
        protected static Dictionary<string, string[]> Features = new Dictionary<string, string[]> {
            // specify the extensions that your provider uses for its package files (if you have any)
            { Constants.Features.SupportedExtensions, new[]{"nipkg"}},

            // you can list the URL schemes that you support searching for packages with
            { Constants.Features.SupportedSchemes, new [] {"http", "https", "file"}},

            // you can list the magic signatures (bytes at the beginning of a file) that we can use
            // to peek and see if a given file is yours.
            { Constants.Features.MagicSignatures, Constants.Signatures.ZipVariants},
        };

        //private PackageManager _packageManager = new PackageManager();

        /// <summary>
        /// Returns the name of the Provider.
        /// </summary>
        /// <returns>The name of this provider </returns>
        public string PackageProviderName
        {
            get { return "nipkg"; }
        }

        /// <summary>
        /// Returns the version of the Provider.
        /// </summary>
        /// <returns>The version of this provider </returns>
        public string ProviderVersion
        {
            get
            {
                return "1.0.0.0";
            }
        }

        /// <summary>
        /// Gets or sets the Package Manager instance.
        /// </summary>
        public PackageManager PackageManager { get; set; }

        /// <summary>
        /// Gets a working folder for the test to use to create files and subdirectories.
        /// </summary>
        /// <returns>The path to the working folder.</returns>
        public string WorkingFolder
        {
            get
            {
                return Directory.GetCurrentDirectory();
            }
        }

        /// <summary>
        /// This is just here as to give us some possibility of knowing when an unexception happens...
        /// At the very least, we'll write it to the system debug channel, so a developer can find it if they are looking for it.
        /// </summary>
        public void OnUnhandledException(string methodName, Exception exception)
        {
            Debug.WriteLine("Unexpected Exception thrown in '{0}::{1}' -- {2}\\{3}\r\n{4}", PackageProviderName, methodName, exception.GetType().Name, exception.Message, exception.StackTrace);
        }

        /// <summary>
        /// Performs one-time initialization of the $provider.
        /// </summary>
        /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
        public void InitializeProvider(Request request)
        {
            request.Debug("Calling '{0}::InitializeProvider'", PackageProviderName);
            try
            {
                PackageManager = new PackageManager();
                PackageManager.InitializeSession(GetType().Assembly.GetName().Name, GetType().Assembly.GetName().Version.ToString());
                SetConfiguration("nipkg.disablefileagent", "false");
            }
            catch (NIPkgException e)
            {
                request.Debug(e.Message);
            }
        }

        /// <summary>
        /// Sets a Package Manager configuration attribute value.
        /// </summary>
        /// <param name="attributeName">Name of the attribute.</param>
        /// <param name="attributeValue">The attribute value.</param>
        public void SetConfiguration(string attributeName, string attributeValue)
        {
            ClientLibraryRequest request = PackageManager.SetConfiguration(attributeName, attributeValue);
            request.WaitUntilRequestCompletes();
        }

        /// <summary>
        /// Returns a collection of strings to the client advertizing features this provider supports.
        /// </summary>
        /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
        public void GetFeatures(Request request)
        {
            // Nice-to-have put a debug message in that tells what's going on.
            request.Debug("Calling '{0}::GetFeatures' ", PackageProviderName);

            foreach (var feature in Features)
            {
                request.Yield(feature);
            }
        }

        /// <summary>
        /// Returns dynamic option definitions to the HOST
        ///
        /// example response:
        ///     request.YieldDynamicOption( "MySwitch", OptionType.String.ToString(), false);
        ///
        /// </summary>
        /// <param name="category">The category of dynamic options that the HOST is interested in</param>
        /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
        public void GetDynamicOptions(string category, Request request)
        {
            // Nice-to-have put a debug message in that tells what's going on.
            request.Debug("Calling '{0}::GetDynamicOptions' {1}", PackageProviderName, category);

            switch ((category ?? string.Empty).ToLowerInvariant())
            {
                case "install":
                    // todo: put any options required for install/uninstall/getinstalledpackages

                    break;

                case "provider":
                    // todo: put any options used with this provider. Not currently used.

                    break;

                case "source":
                    // todo: put any options for package sources

                    break;

                case "package":
                    // todo: put any options used when searching for packages

                    break;

                default:
                    request.Debug("Unknown category for '{0}::GetDynamicOptions': {1}", PackageProviderName, category);
                    break;
            }
        }

        /// <summary>
        /// Resolves and returns Package Sources to the client.
        ///
        /// Specified sources are passed in via the request object (<c>request.GetSources()</c>).
        ///
        /// Sources are returned using <c>request.YieldPackageSource(...)</c>
        /// </summary>
        /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
        public void ResolvePackageSources(Request request)
        {
            request.Debug("Calling '{0}::ResolvePackageSources'", PackageProviderName);

            foreach (var source in GetSource(request.Sources.ToArray()))
            {
                request.YieldPackageSource(source.Name.ToLower(), source.Uri, false, source.Enabled, false);
            }
        }

        /// <summary>
        /// This is called when the user is adding (or updating) a package source
        ///
        /// If this PROVIDER doesn't support user-defined package sources, remove this method.
        /// </summary>
        /// <param name="name">The name of the package source. If this parameter is null or empty the PROVIDER should use the location as the name (if the PROVIDER actually stores names of package sources)</param>
        /// <param name="location">The location (ie, directory, URL, etc) of the package source. If this is null or empty, the PROVIDER should use the name as the location (if valid)</param>
        /// <param name="trusted">A boolean indicating that the user trusts this package source. Packages returned from this source should be marked as 'trusted'</param>
        /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
        public void AddPackageSource(string name, string location, bool trusted, Request request)
        {
            request.Debug("Entering {0} source add -n={1} -s'{2}' (we don't support trusted = '{3}')", PackageProviderName, name, location, trusted);

            PackageManager.AddFeedConfiguration(location, name.ToLower());
        }

        /// <summary>
        /// Removes/Unregisters a package source
        /// </summary>
        /// <param name="name">The name or location of a package source to remove.</param>
        /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
        public void RemovePackageSource(string name, Request request)
        {
            request.Debug("Entering {0} source remove -n={1})", PackageProviderName, name);

            PackageManager.RemoveFeedConfiguration(name.ToLower());
        }

        /// <summary>
        /// Searches package sources given name and version information
        ///
        /// Package information must be returned using <c>request.YieldPackage(...)</c> function.
        /// </summary>
        /// <param name="name">a name or partial name of the package(s) requested</param>
        /// <param name="requiredVersion">A specific version of the package. Null or empty if the user did not specify</param>
        /// <param name="minimumVersion">A minimum version of the package. Null or empty if the user did not specify</param>
        /// <param name="maximumVersion">A maximum version of the package. Null or empty if the user did not specify</param>
        /// <param name="id">if this is greater than zero (and the number should have been generated using <c>StartFind(...)</c>, the core is calling this multiple times to do a batch search request. The operation can be delayed until <c>CompleteFind(...)</c> is called</param>
        /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
        public void FindPackage(string name, string requiredVersion, string minimumVersion, string maximumVersion, int id, Request request)
        {
            // Nice-to-have put a debug message in that tells what's going on.
            request.Debug("Calling '{0}::FindPackage' '{1}','{2}','{3}','{4}'", PackageProviderName, requiredVersion, minimumVersion, maximumVersion, id);

            var sources = GetSource(request.Sources.ToArray()).Select(source => source.Name);

            var availablePackages = new List<PackageMetadata>();
            PackageManager.PackageMetadataAvailable += (sender, args) =>
            {
                availablePackages.Add(args.PackageMetadata);
            };
            ClientLibraryRequest clientLibraryRequest = PackageManager.GetAvailablePackages(sources);
            clientLibraryRequest.WaitUntilRequestCompletes();

            foreach (var package in availablePackages.Where(package => package.GetDisplayName(CultureInfo.CurrentCulture).ToLower() == name.ToLower() || name.Equals(string.Empty)))
            {
                request.YieldSoftwareIdentity(package);
            }
        }

        /*
                /// <summary>
                /// Finds packages given a locally-accessible filename
                ///
                /// Package information must be returned using <c>request.YieldPackage(...)</c> function.
                /// </summary>
                /// <param name="file">the full path to the file to determine if it is a package</param>
                /// <param name="id">if this is greater than zero (and the number should have been generated using <c>StartFind(...)</c>, the core is calling this multiple times to do a batch search request. The operation can be delayed until <c>CompleteFind(...)</c> is called</param>
                /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
                public void FindPackageByFile(string file, int id, Request request)
                {
                    // Nice-to-have put a debug message in that tells what's going on.
                    request.Debug("Calling '{0}::FindPackageByFile' '{1}','{2}'", PackageProviderName, file, id);

                    // todo: find a package by file
                }

                /// <summary>
                /// Finds packages given a URI.
                ///
                /// The function is responsible for downloading any content required to make this work
                ///
                /// Package information must be returned using <c>request.YieldPackage(...)</c> function.
                /// </summary>
                /// <param name="uri">the URI the client requesting a package for.</param>
                /// <param name="id">if this is greater than zero (and the number should have been generated using <c>StartFind(...)</c>, the core is calling this multiple times to do a batch search request. The operation can be delayed until <c>CompleteFind(...)</c> is called</param>
                /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
                public void FindPackageByUri(Uri uri, int id, Request request)
                {
                    // Nice-to-have put a debug message in that tells what's going on.
                    request.Debug("Calling '{0}::FindPackageByUri' '{1}','{2}'", PackageProviderName, uri, id);

                    // todo: find a package by uri
                }
                        */

        /// <summary>
        /// Downloads a remote package file to a local location.
        /// </summary>
        /// <param name="fastPackageReference"></param>
        /// <param name="location"></param>
        /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
        public void DownloadPackage(string fastPackageReference, string location, Request request)
        {
            // Nice-to-have put a debug message in that tells what's going on.
            request.Debug("Calling '{0}::DownloadPackage' '{1}','{2}'", PackageProviderName, fastPackageReference, location);

            try
            {
                ClientLibraryRequest clientLibraryRequest = PackageManager.DownloadPackage(
                    new List<string>()
                    {
                        fastPackageReference.Split(RequestHelper.NullChar)[0]
                    },
                    location);

                clientLibraryRequest.WaitUntilRequestCompletes();

                request.Debug(clientLibraryRequest.GetErrorInfo().Message + clientLibraryRequest.IsCompleted);
            }
            catch (NIPkgException e)
            {
                request.Debug(e.Message);
            }
        }

        /// <summary>
        /// Installs a given package.
        /// </summary>
        /// <param name="fastPackageReference">A provider supplied identifier that specifies an exact package</param>
        /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
        public void InstallPackage(string fastPackageReference, Request request)
        {
            try
            {
                // Nice-to-have put a debug message in that tells what's going on.
                request.Debug("Calling '{0}::InstallPackage' '{1}'", PackageProviderName, fastPackageReference);
                var parts = fastPackageReference.Split(RequestHelper.NullChar);

                ClientLibraryRequest clientLibraryRequest = PackageManager.InstallPackages(new List<string>() { parts[0] }, NIPkgTransactionFlag.GuiMode|NIPkgTransactionFlag.InteractiveMode);
                clientLibraryRequest.WaitUntilRequestCompletes();
                request.YieldSoftwareIdentity(
                    fastPackageReference,
                    parts[0],
                    parts[1],
                    "MultiPartNumeric",
                    parts[2],
                    "ni.com",
                    parts[0], "", "");
                request.Debug(clientLibraryRequest.GetErrorInfo().Message + clientLibraryRequest.IsCompleted);
            }
            catch (NIPkgException e)
            {
                request.Debug(e.Message);
            }
        }

        /// <summary>
        /// Uninstalls a package
        /// </summary>
        /// <param name="fastPackageReference"></param>
        /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
        public void UninstallPackage(string fastPackageReference, Request request)
        {
            // Nice-to-have put a debug message in that tells what's going on.
            request.Debug("Calling '{0}::UninstallPackage' '{1}'", PackageProviderName, fastPackageReference);

            var parts = fastPackageReference.Split(RequestHelper.NullChar);

            ClientLibraryRequest clientLibraryRequest = PackageManager.RemovePackages(new List<string>() { parts[0] });
            clientLibraryRequest.WaitUntilRequestCompletes();
            request.YieldSoftwareIdentity(
                fastPackageReference,
                parts[0],
                parts[1],
                "MultiPartNumeric",
                parts[2],
                "ni.com",
                parts[0], "", "");
            request.Debug(clientLibraryRequest.GetErrorInfo().Message + clientLibraryRequest.IsCompleted);
        }

        /// <summary>
        /// Returns the packages that are installed
        /// </summary>
        /// <param name="name">the package name to match. Empty or null means match everything</param>
        /// <param name="requiredVersion">the specific version asked for. If this parameter is specified (ie, not null or empty string) then the minimum and maximum values are ignored</param>
        /// <param name="minimumVersion">the minimum version of packages to return . If the <code>requiredVersion</code> parameter is specified (ie, not null or empty string) this should be ignored</param>
        /// <param name="maximumVersion">the maximum version of packages to return . If the <code>requiredVersion</code> parameter is specified (ie, not null or empty string) this should be ignored</param>
        /// <param name="request">
        ///     An object passed in from the CORE that contains functions that can be used to interact with
        ///     the CORE and HOST
        /// </param>
        public void GetInstalledPackages(string name, string requiredVersion, string minimumVersion, string maximumVersion, Request request)
        {
            // Nice-to-have put a debug message in that tells what's going on.
            request.Debug("Calling '{0}::GetInstalledPackages' '{1}','{2}','{3}','{4}'", PackageProviderName, name, requiredVersion, minimumVersion, maximumVersion);

            var installedPackages = new List<PackageMetadata>();
            PackageManager.InstalledPackageMetadataAvailable += (sender, args) =>
            {
                if (name == string.Empty || name == args.PackageMetadata.GetDisplayName(CultureInfo.CurrentCulture))
                {
                    installedPackages.Add(args.PackageMetadata);
                }
            };
            ClientLibraryRequest clientLibraryRequest = PackageManager.GetInstalledPackages();
            clientLibraryRequest.WaitUntilRequestCompletes();

            foreach (var package in installedPackages)
            {
                request.YieldSoftwareIdentity(package);
            }
        }

        private List<FeedConfiguration> GetSource(params string[] names)
        {
            var sources = new List<FeedConfiguration>();
            var all = new List<FeedConfiguration>();
            PackageManager.FeedConfigurationAvailable += (sender, args) =>
            {
                all.Add(args.FeedConfiguration);
            };
            ClientLibraryRequest clientLibraryRequest = PackageManager.GetFeedConfigurations();
            clientLibraryRequest.WaitUntilRequestCompletes();

            if (names.Any())
            {
                // the system is requesting sources that match the values passed.
                // if the value passed can be a legitimate source, but is not registered, return a package source marked unregistered.
                foreach (var source in all)
                {
                    if (names.Any(name => name.ToLower() == source.Name.ToLower() || name.ToLower() == source.Uri.ToLower()))
                    {
                        sources.Add(source);
                    }
                }
            }
            else
            {
                sources = all;
            }
            return sources;
        }

        /*
        /// <summary>
        ///
        /// </summary>
        /// <param name="fastPackageReference"></param>
        /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
        public void GetPackageDetails(string fastPackageReference, Request request)
        {
            // Nice-to-have put a debug message in that tells what's going on.
            request.Debug("Calling '{0}::GetPackageDetails' '{1}'", PackageProviderName, fastPackageReference);

            // todo: GetPackageDetails that are more expensive than FindPackage* can deliver
        }

        /// <summary>
        /// Initializes a batch search request.
        /// </summary>
        /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
        /// <returns></returns>
        public int StartFind(Request request)
        {
            // Nice-to-have put a debug message in that tells what's going on.
            request.Debug("Calling '{0}::StartFind'", PackageProviderName);

            return default(int);
        }

        /// <summary>
        /// Finalizes a batch search request.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="request">An object passed in from the CORE that contains functions that can be used to interact with the CORE and HOST</param>
        /// <returns></returns>
        public void CompleteFind(int id, Request request)
        {
            // Nice-to-have put a debug message in that tells what's going on.
            request.Debug("Calling '{0}::CompleteFind' '{1}'", PackageProviderName, id);
        }
        */
    }
}