using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NAppUpdate.Framework;
using NAppUpdate.Framework.Common;
using NAppUpdate.Framework.Tasks;
using NAppUpdate.Framework.Utils;
using NAppUpdate.Framework.Updater;

namespace NAppUpdate.Updater
{
	internal static class AppStart
	{
		private static ArgumentsParser _args;
		private static Logger _logger;
		private static IUpdaterDisplay _console;
        private static Thread _uiThread;

        private static void ExecuteWithShadowCopy()
        {
            /* Enable shadow copying */
            var currentInfo = AppDomain.CurrentDomain.SetupInformation;
            AppDomainSetup newSetup = new AppDomainSetup();
            newSetup.ConfigurationFile = currentInfo.ConfigurationFile;
            newSetup.ApplicationBase = currentInfo.ApplicationBase;
            newSetup.ApplicationName = "Updater";
            newSetup.CachePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "shadow_cache");
            newSetup.ShadowCopyFiles = "true";
            newSetup.ShadowCopyDirectories = Environment.CurrentDirectory;

            AppDomain newDomain = AppDomain.CreateDomain("UpdaterShadowed", AppDomain.CurrentDomain.Evidence, newSetup);
            newDomain.ExecuteAssembly(Assembly.GetExecutingAssembly().Location);
            AppDomain.Unload(newDomain);
        }
        
        [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
		private static void Main()
		{
#if DEBUG
            //Debugger.Launch();
#endif

            // Check if shadow copying is enabled for this instance, if it isn't then spin off a new AppDomain that is 
            // enabled for shadow copy
            if (!AppDomain.CurrentDomain.ShadowCopyFiles)
            {
                ExecuteWithShadowCopy();
                return;
            }

			string tempFolder = string.Empty;
			string logFile = string.Empty;
			_args = ArgumentsParser.Get();

			_logger = UpdateManager.Instance.Logger;
			_args.ParseCommandLineArgs();
            if (_args.ShowConsole)  // Keep the default console implementation
            {
                _console = new ConsoleForm();
                _uiThread = new Thread(_ =>
                {
                    _console.Show();
                    Application.Run();
                }) { IsBackground = true };

                _uiThread.Start();
            }
            else if (!string.IsNullOrEmpty(_args.CustomUiType))
            {
                try
                {
                    AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                    Log("Loading custom UI");
                    Type uiType = Type.GetType(_args.CustomUiType);

                    _uiThread = new Thread(_ =>
                    {
                        _console = Activator.CreateInstance(uiType) as IUpdaterDisplay;
                        _console.Show();
                        Application.Run();
                    }) { IsBackground = true };

                    _uiThread.Start();
                }
                catch (Exception)
                {
                    _console = null;
                    Log("Failed to show the custom UI");
                }
            }
            else
            {
                Log("Skipping UI");
            }

			Log("Starting to process cold updates...");

			var workingDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
			if (_args.Log) {
				// Setup a temporary location for the log file, until we can get the DTO
				logFile = Path.Combine(workingDir, @"NauUpdate.log");
			}

			try {
				// Get the update process name, to be used to create a named pipe and to wait on the application
				// to quit
				string syncProcessName = _args.ProcessName;
				if (string.IsNullOrEmpty(syncProcessName)) //Application.Exit();
					throw new ArgumentException("The command line needs to specify the mutex of the program to update.", "ar" + "gs");

				Log("Update process name: '{0}'", syncProcessName);

				// Load extra assemblies to the app domain, if present
				var availableAssemblies = FileSystem.GetFiles(workingDir, "*.exe|*.dll", SearchOption.TopDirectoryOnly);
				foreach (var assemblyPath in availableAssemblies) {
					Log("Loading {0}", assemblyPath);

					if (assemblyPath.Equals(Assembly.GetEntryAssembly().Location, StringComparison.InvariantCultureIgnoreCase) || assemblyPath.EndsWith("NAppUpdate.Framework.dll")) {
						Log("\tSkipping (part of current execution)");
						continue;
					}

					try {
// ReSharper disable UnusedVariable
						var assembly = Assembly.LoadFile(assemblyPath);
// ReSharper restore UnusedVariable
					} catch (BadImageFormatException ex) {
						Log("\tSkipping due to an error: {0}", ex.Message);
					}
				}

				// Connect to the named pipe and retrieve the updates list
				var dto = NauIpc.ReadDto(syncProcessName) as NauIpc.NauDto;

				// Make sure we start updating only once the application has completely terminated
				Thread.Sleep(1000); // Let's even wait a bit
				bool createdNew;
				using (var mutex = new Mutex(false, syncProcessName + "Mutex", out createdNew)) {
					try {
						if (!createdNew) mutex.WaitOne();
					} catch (AbandonedMutexException) {
						// An abandoned mutex is exactly what we are expecting...
					} finally {
						Log("The application has terminated (as expected)");
					}
				}

				bool updateSuccessful = true;

				if (dto == null || dto.Configs == null) throw new Exception("Invalid DTO received");

				if (dto.LogItems != null) // shouldn't really happen
					_logger.LogItems.InsertRange(0, dto.LogItems);
				dto.LogItems = _logger.LogItems;

				// Get some required environment variables
				string appPath = dto.AppPath;
				string appDir = dto.WorkingDirectory ?? Path.GetDirectoryName(appPath) ?? string.Empty;
				tempFolder = dto.Configs.TempFolder;
				string backupFolder = dto.Configs.BackupFolder;
				bool relaunchApp = dto.RelaunchApplication;

				if (!string.IsNullOrEmpty(dto.AppPath)) logFile = Path.Combine(Path.GetDirectoryName(dto.AppPath), @"NauUpdate.log"); // now we can log to a more accessible location

				if (dto.Tasks == null || dto.Tasks.Count == 0) throw new Exception("Could not find the updates list (or it was empty).");

				Log("Got {0} task objects", dto.Tasks.Count);
#if DEBUG
                Debugger.Launch();
#endif
//This can be handy if you're trying to debug the updater.exe!
//#if (DEBUG)
                {
                    if (_args.ShowConsole)
                    {
                        _console.WriteLine();
                        _console.WriteLine("Pausing to attach debugger.  Press any key to continue.");
                        _console.ReadKey();
                    }

                }
//#endif

				// Perform the actual off-line update process
				foreach (var t in dto.Tasks) {
					Log("Task \"{0}\": {1}", t.Description, t.ExecutionStatus);

					if (t.ExecutionStatus != TaskExecutionStatus.RequiresAppRestart && t.ExecutionStatus != TaskExecutionStatus.RequiresPrivilegedAppRestart) {
						Log("\tSkipping");
						continue;
					}

					Log("\tExecuting...");

					// TODO: Better handling on failure: logging, rollbacks
                    try
                    {
                        if (_console != null)
                        {
                            t.ProgressDelegate += _console.ReportProgress;
                        }
                        t.ExecutionStatus = t.Execute(true);
                    }
                    catch (Exception ex)
                    {
                        Log(ex);
                        updateSuccessful = false;
                        t.ExecutionStatus = TaskExecutionStatus.Failed;
                    }
                    finally
                    {
                        if (_console != null)
                        {
                            t.ProgressDelegate -= _console.ReportProgress;
                        }
                    }

					if (t.ExecutionStatus == TaskExecutionStatus.Successful) continue;
					Log("\tTask execution failed");
					updateSuccessful = false;
					break;
				}

				if (updateSuccessful) {
					Log("Finished successfully");
					Log("Removing backup folder");
					if (Directory.Exists(backupFolder)) FileSystem.DeleteDirectory(backupFolder);
				} else {
					MessageBox.Show("Update Failed");
					Log(Logger.SeverityLevel.Error, "Update failed");
				}

				// Start the application only if requested to do so
				if (relaunchApp) {
					Log("Re-launching process {0} with working dir {1}", appPath, appDir);
					ProcessStartInfo info;
					if (_args.ShowConsole)
					{
						info = new ProcessStartInfo
						{
							UseShellExecute = false,
							WorkingDirectory = appDir,
							FileName = appPath,
						};
					}
					else
					{
						info = new ProcessStartInfo
						{
							UseShellExecute = true,
							WorkingDirectory = appDir,
							FileName = appPath,
						};
					}

					var p = NauIpc.LaunchProcessAndSendDto(dto, info, syncProcessName);
					if (p == null) throw new UpdateProcessFailedException("Unable to relaunch application and/or send DTO");
				}

				Log("All done");
				//Application.Exit();
			} catch (Exception ex) {
				// supressing catch because if at any point we get an error the update has failed
				Log(ex);
			} finally {
				if (_args.Log) {
					// at this stage we can't make any assumptions on correctness of the path
					FileSystem.CreateDirectoryStructure(logFile, true);
					_logger.Dump(logFile);
				}

				if (_console != null) {
					if (_args.Log) {
						_console.WriteLine();
						_console.WriteLine("Log file was saved to {0}", logFile);
						_console.WriteLine();
					}
					_console.WriteLine();
					_console.WriteLine("Press any key or close this window to exit.");
					_console.WaitForClose();
				}
				if (!string.IsNullOrEmpty(tempFolder)) SelfCleanUp(tempFolder);
				Application.Exit();
			}
		}

        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string[] assemblyName = args.Name.Split(new char[]{','});
            string searchFor = Path.Combine(Environment.CurrentDirectory,assemblyName[0]);

            string[] extensions = new string[]{".exe",".dll"};
            string filename;
            foreach(string ext in extensions)
            {
                filename = searchFor + ext;
                if(File.Exists(filename))
                    return Assembly.LoadFile(filename);
            }
            
            return null;
        }

		private static void SelfCleanUp(string tempFolder)
		{
			// Delete the updater EXE and the temp folder
			Log("Removing updater and temp folder... {0}", tempFolder);
			try {
				var info = new ProcessStartInfo {
					Arguments = string.Format(@"/C ping 1.1.1.1 -n 1 -w 3000 > Nul & echo Y|del ""{0}\*.*"" & rmdir ""{0}""", tempFolder),
					WindowStyle = ProcessWindowStyle.Hidden,
					CreateNoWindow = true,
					FileName = "cmd.exe"
				};

				Process.Start(info);
			} catch {
				/* ignore exceptions thrown while trying to clean up */
			}
		}

		private static void Log(string message, params object[] args)
		{
			Log(Logger.SeverityLevel.Debug, message, args);
		}

		private static void Log(Logger.SeverityLevel severity, string message, params object[] args)
		{
			message = string.Format(message, args);

			_logger.Log(severity, message);
			if (_console!= null) _console.WriteLine(message);

			//Application.DoEvents();
		}

		private static void Log(Exception ex)
		{
			_logger.Log(ex);

			if (_console != null) {
				_console.WriteLine("*********************************");
				_console.WriteLine("   An error has occurred:");
				_console.WriteLine("   " + ex);
				_console.WriteLine("*********************************");

				_console.WriteLine();
				_console.WriteLine("The updater will close when you close this window.");
			}

			//Application.DoEvents();
		}
	}
}