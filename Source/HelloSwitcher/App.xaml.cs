﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using HelloSwitcher.Models;
using HelloSwitcher.Views;

namespace HelloSwitcher
{
	public partial class App : Application
	{
		private Settings _settings;
		private DeviceSwitcher _switcher;
		private DeviceUsbWatcher _watcher;
		private NotifyIconHolder _holder;

		internal static bool IsInteractive { get; } = Environment.UserInteractive;

		protected override async void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			DispatcherUnhandledException += (_, e) => Logger.RecordException(e.Exception);
			TaskScheduler.UnobservedTaskException += (_, e) => Logger.RecordException(e.Exception);
			AppDomain.CurrentDomain.UnhandledException += (_, e) => Logger.RecordException(e.ExceptionObject);

			_settings = new Settings();
			await _settings.LoadAsync();

			_switcher = new DeviceSwitcher(_settings);
			await _switcher.CheckAsync();

			_watcher = new DeviceUsbWatcher();
			_watcher.UsbDeviceChanged += async (_, e) =>
			{
				await _switcher.CheckAsync(e.deviceName, e.exists);

				if (IsInteractive)
				{
					_holder?.UpdateIcon(_switcher.RemovableCameraExists);
					await UpdateWindow();
				}
			};

			if (IsInteractive)
			{
				_holder = new NotifyIconHolder(
					new[]
					{
						"pack://application:,,,/HelloSwitcher;component/Resources/Disconnected.ico",
						"pack://application:,,,/HelloSwitcher;component/Resources/Connected.ico",
					},
					"Hello Switcher",
					new[]
					{
						(ToolStripItemType.Label, "Hello Switcher", (Action)null), // (Action) is necessary to indicate the type.
						(ToolStripItemType.Separator, null, null),
						(ToolStripItemType.Button, "Open settings", () => ShowWindow()),
						(ToolStripItemType.Separator, null, null),
						(ToolStripItemType.Button, "Re-check USB camera", async () =>
						{
							await _switcher.CheckAsync();
							_holder.UpdateIcon(_switcher.RemovableCameraExists);
							await UpdateWindow();
						}),
						(ToolStripItemType.Button, "Enable built-in camera", async () =>
						{
							await _switcher.EnableAsync();
							await UpdateWindow();
						}),
						(ToolStripItemType.Button, "Disable built-in camera", async () =>
						{
							await _switcher.DisableAsync();
							await UpdateWindow();
						}),
						(ToolStripItemType.Separator, null, null),
						(ToolStripItemType.Button,"Close", async () =>
						{
							await _switcher.EnableAsync();
							this.Shutdown();
						})
					});
				_holder.UpdateIcon(_switcher.RemovableCameraExists);

				if (!_settings.IsLoaded)
					ShowWindow();
			}
		}

		private void ShowWindow()
		{
			var window = this.Windows.OfType<SettingsWindow>().FirstOrDefault();
			if (window is not null)
			{
				window.Activate();
				return;
			}

			this.MainWindow = new SettingsWindow(_settings);
			this.MainWindow.Closed += OnClosed;
			this.MainWindow.Show();

			async void OnClosed(object sender, EventArgs e)
			{
				this.MainWindow = null;
				await _switcher.CheckAsync();
				_holder.UpdateIcon(_switcher.RemovableCameraExists);
			}
		}

		private Task UpdateWindow()
		{
			var window = this.Windows.OfType<SettingsWindow>().FirstOrDefault();
			if (window is null)
				return Task.CompletedTask;

			return window.SearchAsync();
		}

		protected override void OnExit(ExitEventArgs e)
		{
			_watcher?.Dispose();
			_holder?.Dispose();

			base.OnExit(e);
		}
	}
}