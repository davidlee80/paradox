﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SiliconStudio.Core.Diagnostics;

namespace SiliconStudio.Core.Reflection
{
    public class AssemblyContainer
    {
        private readonly Dictionary<string, Assembly> loadedAssemblies = new Dictionary<string, Assembly>(StringComparer.InvariantCultureIgnoreCase);
        private static readonly string[] KnownAssemblyExtensions = { ".dll", ".exe" };
        [ThreadStatic]
        private static AssemblyContainer loadingInstance;

        [ThreadStatic]
        private static LoggerResult log;

        [ThreadStatic]
        private static List<string> searchDirectoryList;

        /// <summary>
        /// The default assembly container loader.
        /// </summary>
        public static readonly AssemblyContainer Default = new AssemblyContainer();

        static AssemblyContainer()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        public AssemblyContainer()
        {
        }

        public Assembly LoadAssemblyFromPath(string assemblyFullPath, ILogger outputLog = null, List<string> lookupDirectoryList = null)
        {
            if (assemblyFullPath == null) throw new ArgumentNullException("assemblyFullPath");

            log = new LoggerResult();

            lookupDirectoryList = lookupDirectoryList ?? new List<string>();
            assemblyFullPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, assemblyFullPath));
            var assemblyDirectory = Path.GetDirectoryName(assemblyFullPath);

            if (assemblyDirectory == null || !Directory.Exists(assemblyDirectory))
            {
                throw new ArgumentException("Invalid assembly path. Doesn't contain directory information");
            }

            if (!lookupDirectoryList.Contains(assemblyDirectory, StringComparer.InvariantCultureIgnoreCase))
            {
                lookupDirectoryList.Add(assemblyDirectory);
            }

            var previousLookupList = searchDirectoryList;
            try
            {
                loadingInstance = this;
                searchDirectoryList = lookupDirectoryList;

                return LoadAssemblyFromPathInternal(assemblyFullPath);
            }
            finally
            {
                loadingInstance = null;
                searchDirectoryList = previousLookupList;

                if (outputLog != null)
                {
                    log.CopyTo(outputLog);
                }
            }
        }

        private Assembly LoadAssemblyByName(string assemblyName)
        {
            if (assemblyName == null) throw new ArgumentNullException("assemblyName");

            var assemblyPartialPathList = new List<string>();
            assemblyPartialPathList.AddRange(KnownAssemblyExtensions.Select(knownExtension => assemblyName + knownExtension));

            foreach (var directoryPath in searchDirectoryList)
            {
                foreach (var assemblyPartialPath in assemblyPartialPathList)
                {
                    var assemblyFullPath = Path.Combine(directoryPath, assemblyPartialPath);
                    if (File.Exists(assemblyFullPath))
                    {
                        return LoadAssemblyFromPathInternal(assemblyFullPath);
                    }
                }
            }
            return null;
        }

        private string CopySafeShadow(string path)
        {
            for(int i = 1; i < 20; i++)
            {
                var shadowName = Path.ChangeExtension(path, "shadow" + i);
                try
                {
                    File.Copy(path, shadowName, true);
                    return shadowName;
                }
                catch (Exception)
                {
                }
            }

            return null;
        }

        private Assembly LoadAssemblyFromPathInternal(string assemblyFullPath)
        {
            if (assemblyFullPath == null) throw new ArgumentNullException("assemblyFullPath");

            string safeShadowPath = null;
            try
            {
                assemblyFullPath = Path.GetFullPath(assemblyFullPath);

                lock (loadedAssemblies)
                {
                    Assembly assembly;
                    if (loadedAssemblies.TryGetValue(assemblyFullPath, out assembly))
                    {
                        return assembly;
                    }

                    if (!File.Exists(assemblyFullPath))
                        return null;

                    // Create a shadow copy of the assembly to load
                    safeShadowPath = CopySafeShadow(assemblyFullPath);
                    if (safeShadowPath == null)
                    {
                        log.Error("Cannot create a shadow copy for assembly [{0}]", assemblyFullPath);
                        return null;
                    }

                    // Load the assembly into the current AppDomain
                    // TODO: Is using AppDomain would provide more opportunities for unloading?
                    assembly = Assembly.LoadFile(safeShadowPath);
                    loadedAssemblies.Add(assemblyFullPath, assembly);

                    // Force assembly resolve with proper name
                    // (doing it here, because if done later, loadingInstance will be set to null and it won't work)
                    Assembly.Load(assembly.FullName);

                    // Make sure that all referenced assemblies are loaded here
                    foreach (var assemblyRef in assembly.GetReferencedAssemblies())
                    {
                        Assembly.Load(assemblyRef);
                    }

                    // Make sure that Module initializer are called
                    if (assembly.GetTypes().Length > 0)
                    {
                        foreach (var module in assembly.Modules)
                        {
                            RuntimeHelpers.RunModuleConstructor(module.ModuleHandle);
                        }
                    }
                    return assembly;
                }
            }
            catch (Exception exception)
            {
                log.Error("Error while loading assembly reference [{0}]", exception, safeShadowPath ?? assemblyFullPath);
                var loaderException = exception as ReflectionTypeLoadException;
                if (loaderException != null)
                {
                    foreach (var exceptionForType in loaderException.LoaderExceptions)
                    {
                        log.Error("Unable to load type. See exception.", exceptionForType);
                    }
                }
            }
            return null;
        }

        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // If it is handled by current thread, then we can handle it here.
            var container = loadingInstance;
            if (container != null)
            {
                var assemblyName = new AssemblyName(args.Name);
                return container.LoadAssemblyByName(assemblyName.Name);
            }
            return null;
        }
    }
}